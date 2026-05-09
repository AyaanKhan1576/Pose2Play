from flask import Flask, request, jsonify, send_from_directory, send_file
from flask_cors import CORS
from flask_socketio import SocketIO
from stable_baselines3 import DQN
import argparse
import atexit
import numpy as np
import os
from pathlib import Path
import signal
import sys
import torch
import socket as udp_socket
import json
from scipy.interpolate import interp1d

# Add ml directory to path for imports
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from form_feedback import FormFeedbackGenerator
from models.lstm_quality import ShoulderLSTM
from personalization import RehabPersonalizer
from session_logger import SessionLogger

# Get paths
ml_dir = Path(__file__).parent
demo_dir = ml_dir.parent / 'demo'


def _load_local_env_file() -> None:
    """Load a repo-root .env file without overriding existing environment values."""
    env_path = ml_dir.parent / '.env'
    if not env_path.exists():
        return

    try:
        for raw_line in env_path.read_text(encoding='utf-8').splitlines():
            line = raw_line.strip()
            if not line or line.startswith('#') or '=' not in line:
                continue

            key, value = line.split('=', 1)
            key = key.strip()
            value = value.strip().strip('"').strip("'")
            if key and key not in os.environ:
                os.environ[key] = value
    except Exception:
        pass


_load_local_env_file()

# Create Flask app with static files from demo directory
app = Flask(__name__, 
            static_folder=str(demo_dir),
            static_url_path='')
CORS(app, resources={r"/*": {"origins": "*"}})
socketio = SocketIO(app, cors_allowed_origins="*")

# UDP sockets for forwarding realtime data to Unity
UNITY_UDP_IP = os.getenv("UNITY_UDP_IP", "127.0.0.1")
UNITY_POSE_UDP_PORT = int(os.getenv("UNITY_POSE_UDP_PORT", "5055"))
UNITY_DASHBOARD_UDP_PORT = int(os.getenv("UNITY_DASHBOARD_UDP_PORT", "5056"))

# Parse comma-separated UDP targets (e.g., "127.0.0.1,192.168.18.30")
UNITY_UDP_TARGETS = [ip.strip() for ip in UNITY_UDP_IP.split(",") if ip.strip()]
if not UNITY_UDP_TARGETS:
    UNITY_UDP_TARGETS = ["127.0.0.1"]

_pose_udp_sock = udp_socket.socket(udp_socket.AF_INET, udp_socket.SOCK_DGRAM)
_dashboard_udp_sock = udp_socket.socket(udp_socket.AF_INET, udp_socket.SOCK_DGRAM)

# Enable broadcast for Quest mode
_pose_udp_sock.setsockopt(udp_socket.SOL_SOCKET, udp_socket.SO_REUSEADDR, 1)
_dashboard_udp_sock.setsockopt(udp_socket.SOL_SOCKET, udp_socket.SO_REUSEADDR, 1)
_pose_udp_sock.setsockopt(udp_socket.SOL_SOCKET, udp_socket.SO_BROADCAST, 1)
_dashboard_udp_sock.setsockopt(udp_socket.SOL_SOCKET, udp_socket.SO_BROADCAST, 1)

# UDP send attempt counters to reduce error spam
_pose_udp_failures = 0
_dashboard_udp_failures = 0
_pose_udp_success = 0
_dashboard_udp_success = 0
_udp_error_reported = False

LANDMARK_MAP = {
    "left_shoulder": 11, "right_shoulder": 12,
    "left_elbow": 13,    "right_elbow": 14,
    "left_wrist": 15,    "right_wrist": 16,
    "left_hip": 23,      "right_hip": 24,
    "left_knee": 25,     "right_knee": 26,
    "left_ankle": 27,    "right_ankle": 28,
    "left_heel": 29,     "right_heel": 30,
    "left_foot": 31,     "right_foot": 32,
}

session_logger = None

@socketio.on('pose_data')
def handle_pose_data(landmarks):
    """Receive landmarks from browser and forward to Unity via UDP"""
    try:
        if landmarks is None:
            return

        # Socket.IO payload can arrive as a list or dict-like object depending on serializer.
        def get_landmark_at(index):
            if isinstance(landmarks, list):
                if 0 <= index < len(landmarks):
                    return landmarks[index]
                return None

            if isinstance(landmarks, dict):
                if index in landmarks:
                    return landmarks[index]
                key = str(index)
                return landmarks.get(key)

            return None

        def parse_landmark(index):
            lm = get_landmark_at(index)
            if not isinstance(lm, dict):
                return None

            if 'x' not in lm or 'y' not in lm or 'z' not in lm:
                return None

            try:
                return [round(float(lm['x']), 4), round(float(lm['y']), 4), round(float(lm['z']), 4)]
            except (TypeError, ValueError):
                return None

        pose = {}
        for name, idx in LANDMARK_MAP.items():
            parsed = parse_landmark(idx)
            if parsed is not None:
                pose[name] = parsed

        # Require core joints before forwarding to Unity.
        required = ['left_hip', 'right_hip', 'left_wrist', 'right_wrist', 'left_ankle', 'right_ankle']
        if any(k not in pose for k in required):
            return

        msg = json.dumps(pose).encode('utf-8')
        sent_any = False
        for target_ip in UNITY_UDP_TARGETS:
            try:
                _pose_udp_sock.sendto(msg, (target_ip, UNITY_POSE_UDP_PORT))
                sent_any = True
            except (OSError, BlockingIOError, ConnectionError) as udp_err:
                global _pose_udp_failures, _udp_error_reported
                _pose_udp_failures += 1
                # Print first send error only once to avoid log spam.
                if not _udp_error_reported:
                    print(f"⚠️  UDP forwarding warning for target {target_ip}:{UNITY_POSE_UDP_PORT}: {udp_err}")
                    print(f"   💡 Continuing with other UDP targets: {UNITY_UDP_TARGETS}")
                    _udp_error_reported = True

        if sent_any:
            global _pose_udp_success
            _pose_udp_success += 1
    except Exception as e:
        print(f"Pose data handling error: {e}")


@socketio.on('dashboard_data')
def handle_dashboard_data(payload):
    """Receive compact dashboard state from browser and forward to Unity via UDP"""
    try:
        if not isinstance(payload, dict):
            return

        dashboard = {
            'type': 'dashboard_update',
            'exercise': payload.get('exercise', 'squat'),
            'phase': payload.get('phase', 'BASELINE'),
            'repCount': int(payload.get('repCount', 0)),
            'currentAngle': payload.get('currentAngle'),
            'pushTarget': payload.get('pushTarget'),
            'minimumThreshold': payload.get('minimumThreshold'),
            'formQuality': payload.get('formQuality'),
            'status': payload.get('status', ''),
            'feedback': payload.get('feedback', ''),
            'isCorrect': bool(payload.get('isCorrect', False)),
            'calibration': payload.get('calibration', {'count': 0, 'required': 3}),
            'timestamp': int(payload.get('timestamp', 0))
        }

        msg = json.dumps(dashboard).encode('utf-8')
        dashboard_sent_any = False
        for target_ip in UNITY_UDP_TARGETS:
            try:
                _dashboard_udp_sock.sendto(msg, (target_ip, UNITY_DASHBOARD_UDP_PORT))
                dashboard_sent_any = True
            except (OSError, BlockingIOError, ConnectionError):
                global _dashboard_udp_failures
                _dashboard_udp_failures += 1
                # Silent to keep dashboard logs clean.

        if dashboard_sent_any:
            global _dashboard_udp_success
            _dashboard_udp_success += 1
    except Exception as e:
        print(f"Dashboard data handling error: {e}")

# Load RL model for difficulty adjustment
model_path = './models/dqn/DQN_rehab_final.zip'
if os.path.exists(model_path):
    model = DQN.load(model_path)
    print(f"✅ Loaded RL model: {model_path}")
else:
    print(f"⚠️ RL model not found: {model_path}")
    model = None

# Load form classification model
form_model_path = './models/form_classifier/form_classifier_rf.pkl'
if os.path.exists(form_model_path):
    form_classifier = FormFeedbackGenerator(form_model_path)
    print(f"✅ Loaded form classifier: {form_model_path}")
else:
    print(f"⚠️ Form classifier not found: {form_model_path}")
    form_classifier = None

# ============================================================
# LSTM MOVEMENT QUALITY MODEL (NEW)
# ============================================================

# Load LSTM model for shoulder movement quality
lstm_model_path = './models/shoulder_lstm_model.pt'
lstm_model = None
lstm_metadata = None
personalizer = None

if os.path.exists(lstm_model_path):
    try:
        # Load checkpoint
        checkpoint = torch.load(lstm_model_path, map_location='cpu')
        
        # Create model with saved architecture
        lstm_model = ShoulderLSTM(
            input_size=checkpoint['input_size'],
            hidden_size=checkpoint.get('hidden_size', 64),
            num_layers=checkpoint.get('num_layers', 2),
            dropout=0.0  # No dropout for inference
        )
        
        # Load trained weights
        lstm_model.load_state_dict(checkpoint['model_state_dict'])
        lstm_model.eval()  # Set to evaluation mode
        
        # Store metadata for preprocessing
        lstm_metadata = {
            'seq_len': checkpoint['seq_len'],
            'angle_mean': np.array(checkpoint['angle_mean']),
            'angle_std': np.array(checkpoint['angle_std']),
            'global_max_rom': checkpoint['global_max_rom'],
            'input_size': checkpoint['input_size']
        }
        
        # Initialize personalizer
        personalizer = RehabPersonalizer(
            global_max_rom=lstm_metadata['global_max_rom'],
            base_increment_deg=5.0,
            max_extra_deg=30.0,
            ema_alpha=0.3
        )
        
        print(f"✅ Loaded LSTM model: {lstm_model_path}")
        print(f"   - Input size: {lstm_metadata['input_size']}")
        print(f"   - Sequence length: {lstm_metadata['seq_len']}")
        print(f"   - Global max ROM: {lstm_metadata['global_max_rom']:.1f}°")
        
    except Exception as e:
        print(f"⚠️ Error loading LSTM model: {e}")
        lstm_model = None
else:
    print(f"⚠️ LSTM model not found: {lstm_model_path}")
    print("   Train the model first with: python train_lstm.py")

session_logger = SessionLogger(
    default_resume_latest=os.getenv('POSE2PLAY_RESUME_LATEST', '0') == '1',
    app_version=os.getenv('POSE2PLAY_APP_VERSION', '0.1.0'),
    model_version=os.path.basename(lstm_model_path) if lstm_model else None,
)


def _request_payload():
    payload = request.get_json(silent=True)
    return payload if isinstance(payload, dict) else {}


def _response_payload(value):
    return json.loads(json.dumps(value, default=str))


@app.route('/')
def serve_demo():
    """Serve the main demo page"""
    return send_from_directory(str(demo_dir), 'index.html')


@app.route('/<path:path>')
def serve_static(path):
    """Serve static files from demo directory"""
    return send_from_directory(str(demo_dir), path)


@app.route('/health', methods=['GET'])
def health():
    return jsonify({
        'status': 'ok', 
        'rl_model_loaded': model is not None,
        'form_classifier_loaded': form_classifier is not None,
        'lstm_model_loaded': lstm_model is not None,
        'personalizer_loaded': personalizer is not None,
        'firebase_enabled': session_logger.is_enabled() if session_logger else False,
        'session_logging_enabled': session_logger.is_enabled() if session_logger else False,
        'active_session_id': session_logger.current_session_id if session_logger else None,
        'models': {
            'rl': 'DQN_rehab_final.zip' if model else None,
            'lstm': 'shoulder_lstm_model.pt' if lstm_model else None,
            'form': 'form_classifier_rf.pkl' if form_classifier else None
        }
    })


@app.route('/session/start', methods=['POST'])
def start_session():
    payload = _request_payload()
    exercise_type = payload.get('exerciseType') or payload.get('exercise') or 'unknown'
    metadata = payload.get('metadata') if isinstance(payload.get('metadata'), dict) else {}
    if payload.get('appVersion'):
        metadata['appVersion'] = payload.get('appVersion')
    if payload.get('modelVersion'):
        metadata['modelVersion'] = payload.get('modelVersion')

    session_id = payload.get('sessionId')
    resume_latest = payload.get('resumeLatest')

    try:
        if session_logger.current_session_id and session_logger.status == 'active':
            session = session_logger.get_current_session()
        elif session_id:
            session = session_logger.resume_session(session_id, exercise_type=exercise_type, metadata=metadata)
        elif resume_latest is True or (resume_latest is None and session_logger.default_resume_latest):
            latest = session_logger.get_latest_resumable_session()
            if latest:
                session = session_logger.resume_session(latest['sessionId'], exercise_type=exercise_type, metadata=metadata)
            else:
                session = session_logger.start_new_session(exercise_type, metadata)
        else:
            session = session_logger.start_new_session(exercise_type, metadata)

        return jsonify(_response_payload({
            'ok': True,
            'session': session,
            'restoredReps': session_logger.get_current_reps(),
            'firebaseEnabled': session_logger.is_enabled(),
            'resumed': bool(session_id or resume_latest or session_logger.default_resume_latest),
        }))
    except Exception as exc:
        session_logger.fail_session(str(exc))
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/rep', methods=['POST'])
def log_session_rep():
    payload = _request_payload()
    rep_data = payload.get('repData') if isinstance(payload.get('repData'), dict) else {}
    if not rep_data:
        rep_data = payload.get('rep') if isinstance(payload.get('rep'), dict) else {}
    if not rep_data:
        rep_data = {key: value for key, value in payload.items() if key not in {'metadata', 'feedbackEvent'}}

    exercise_type = payload.get('exerciseType') or rep_data.get('exerciseType') or session_logger.exercise_type or 'unknown'
    rep_data.setdefault('exerciseType', exercise_type)
    if payload.get('phase') and not rep_data.get('exercisePhase'):
        rep_data['exercisePhase'] = payload.get('phase')
    if payload.get('qualityScore') is not None and rep_data.get('qualityScore') is None:
        rep_data['qualityScore'] = payload.get('qualityScore')
    if payload.get('feedbackLabels') is not None and not rep_data.get('feedbackLabels'):
        rep_data['feedbackLabels'] = payload.get('feedbackLabels')
    if payload.get('mistakeFlags') is not None and not rep_data.get('mistakeFlags'):
        rep_data['mistakeFlags'] = payload.get('mistakeFlags')
    if payload.get('rlStateFeatures') is not None and rep_data.get('rlStateFeatures') is None:
        rep_data['rlStateFeatures'] = payload.get('rlStateFeatures')
    if payload.get('rlActionOrRecommendation') is not None and rep_data.get('rlActionOrRecommendation') is None:
        rep_data['rlActionOrRecommendation'] = payload.get('rlActionOrRecommendation')
    if payload.get('rewardSignal') is not None and rep_data.get('rewardSignal') is None:
        rep_data['rewardSignal'] = payload.get('rewardSignal')
    if payload.get('notes') is not None and rep_data.get('notes') is None:
        rep_data['notes'] = payload.get('notes')

    try:
        rep = session_logger.log_rep(rep_data)
        feedback_event = payload.get('feedbackEvent')
        if isinstance(feedback_event, dict):
            session_logger.log_feedback_event(feedback_event)
        return jsonify(_response_payload({
            'ok': True,
            'rep': rep,
            'session': session_logger.get_current_session(),
            'firebaseEnabled': session_logger.is_enabled(),
        }))
    except Exception as exc:
        session_logger.fail_session(str(exc))
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/feedback', methods=['POST'])
def log_session_feedback():
    payload = _request_payload()
    try:
        event = payload.get('eventData') if isinstance(payload.get('eventData'), dict) else payload
        logged = session_logger.log_feedback_event(event)
        return jsonify(_response_payload({'ok': True, 'event': logged, 'firebaseEnabled': session_logger.is_enabled()}))
    except Exception as exc:
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/pause', methods=['POST'])
def pause_session():
    payload = _request_payload()
    try:
        snapshot = payload.get('snapshotData') if isinstance(payload.get('snapshotData'), dict) else None
        paused = session_logger.pause_session(snapshot)
        return jsonify(_response_payload({'ok': True, 'session': paused, 'firebaseEnabled': session_logger.is_enabled()}))
    except Exception as exc:
        session_logger.fail_session(str(exc))
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/end', methods=['POST'])
def end_session():
    payload = _request_payload()
    try:
        summary = payload.get('summaryData') if isinstance(payload.get('summaryData'), dict) else None
        ended = session_logger.end_session(summary)
        return jsonify(_response_payload({'ok': True, 'session': ended, 'firebaseEnabled': session_logger.is_enabled()}))
    except Exception as exc:
        session_logger.fail_session(str(exc))
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/fail', methods=['POST'])
def fail_session():
    payload = _request_payload()
    error_message = payload.get('errorMessage') or payload.get('message') or 'Session failed'
    try:
        failed = session_logger.fail_session(error_message)
        return jsonify(_response_payload({'ok': True, 'session': failed, 'firebaseEnabled': session_logger.is_enabled()}))
    except Exception as exc:
        return jsonify({'ok': False, 'error': str(exc)}), 500


@app.route('/session/latest-resumable', methods=['GET'])
def latest_resumable_session():
    latest = session_logger.get_latest_resumable_session() if session_logger else None
    return jsonify({
        'ok': True,
        'session': latest,
        'firebaseEnabled': session_logger.is_enabled() if session_logger else False,
    })
 
@app.route('/predict', methods=['POST'])
def predict():
    try:
        data = request.json
        state = np.array(data['state'], dtype=np.float32)
        
        if model is None:
            return jsonify({'error': 'Model not loaded'}), 500
        
        # Get action from trained model
        action, _ = model.predict(state, deterministic=True)
        
        # Map action to description
        action_names = ['decrease_difficulty', 'maintain', 'increase_difficulty', 'rest_break', 'encouragement']
        action_name = action_names[int(action)]
        
        return jsonify({
            'action': int(action),
            'action_name': action_name,
            'confidence': float(0.95)  # Placeholder
        })
    
    except Exception as e:
        return jsonify({'error': str(e)}), 400


@app.route('/predict_form', methods=['POST'])
def predict_form():
    """
    Predict exercise form quality and provide corrections
    
    Expected input:
    {
        "features": [59-element array of sensor features],
        "exercise_type": "squat" | "hip_abduction_left" | etc.
    }
    
    Returns:
    {
        "prediction": 0 or 1,
        "form_quality": "87.5%",
        "is_correct": true/false,
        "feedback": ["Great depth!", ...],
        "corrections": ["Keep knees aligned", ...],
        "issues_detected": ["knee_valgus", ...]
    }
    """
    try:
        if form_classifier is None:
            return jsonify({'error': 'Form classifier not loaded'}), 500
        
        data = request.json
        features = np.array(data.get('features', []), dtype=np.float32)
        exercise_type = data.get('exercise_type', 'squat')
        
        if len(features) == 0:
            return jsonify({'error': 'No features provided'}), 400
        
        # Analyze form
        result = form_classifier.analyze_form(features, exercise_type)
        
        return jsonify(result)
    
    except Exception as e:
        return jsonify({'error': str(e)}), 400


@app.route('/predict_form_simple', methods=['POST'])
def predict_form_simple():
    """
    Simplified form prediction for webcam-based exercises
    Uses simplified feature extraction from pose landmarks
    
    Expected input:
    {
        "angles": {
            "knee_left": 85,
            "knee_right": 87,
            "hip_left": 90,
            "hip_right": 88
        },
        "movement_speed": 2.5,  # seconds per rep
        "exercise_type": "squat"
    }
    
    Returns: Same as /predict_form but with rule-based analysis
    """
    try:
        data = request.json
        angles = data.get('angles', {})
        movement_speed = data.get('movement_speed', 3.0)
        exercise_type = data.get('exercise_type', 'squat')
        
        # Simple rule-based form analysis for webcam exercises
        feedback = []
        corrections = []
        issues = []
        is_correct = True
        form_quality = 100.0
        
        if exercise_type == 'squat':
            knee_left = angles.get('knee_left', 90)
            knee_right = angles.get('knee_right', 90)
            
            # Check depth
            avg_knee = (knee_left + knee_right) / 2
            if avg_knee > 100:
                issues.append('shallow_depth')
                corrections.append("⚠️ Squat deeper - aim for 90° or below")
                form_quality -= 20
                is_correct = False
            
            # Check asymmetry
            asymmetry = abs(knee_left - knee_right)
            if asymmetry > 10:
                issues.append('asymmetry')
                corrections.append(f"⚠️ Uneven depth - left: {knee_left:.0f}°, right: {knee_right:.0f}°")
                form_quality -= 15
                is_correct = False
            
            # Check speed
            if movement_speed < 1.5:
                issues.append('too_fast')
                corrections.append("⚠️ Slow down - take 2-3 seconds per rep")
                form_quality -= 10
                is_correct = False
            
            # Positive feedback if form is good
            if is_correct:
                feedback.append("✅ Excellent form! Perfect depth and balance.")
        
        elif 'hip' in exercise_type:
            hip_angle = angles.get('hip_left' if 'left' in exercise_type else 'hip_right', 90)
            
            # Check range
            if hip_angle > 120:
                issues.append('insufficient_lift')
                corrections.append("⚠️ Lift leg higher - aim for 45°")
                form_quality -= 25
                is_correct = False
            
            if is_correct:
                feedback.append("✅ Perfect! Good range of motion.")
        
        elif 'shoulder' in exercise_type:
            shoulder_angle = angles.get('shoulder_left', 90)
            
            # Check range
            if shoulder_angle < 80:
                issues.append('insufficient_raise')
                corrections.append("⚠️ Raise arm higher - aim for 90°")
                form_quality -= 20
                is_correct = False
            
            if is_correct:
                feedback.append("✅ Great! Arm at perfect height.")
        
        # If no specific feedback, add generic
        if not feedback and not corrections:
            if is_correct:
                feedback.append("✅ Good form! Keep it up.")
            else:
                corrections.append("⚠️ Form needs improvement")
        
        form_quality = max(0, form_quality)
        
        return jsonify({
            'prediction': 1 if is_correct else 0,
            'form_quality': f"{form_quality:.1f}%",
            'is_correct': is_correct,
            'feedback': feedback,
            'corrections': corrections,
            'issues': issues,  # Keep both for compatibility
            'issues_detected': issues,
            'confidence': form_quality / 100.0
        })
    
    except Exception as e:
        return jsonify({'error': str(e)}), 400


# ============================================================
# LSTM QUALITY PREDICTION ENDPOINT (NEW)
# ============================================================

def resample_sequence(sequence: np.ndarray, target_length: int) -> np.ndarray:
    """
    Resample a time-series sequence to fixed length using linear interpolation.
    
    Args:
      sequence: [T_raw, F] array
      target_length: Desired sequence length
    
    Returns:
      [target_length, F] array
    """
    T_raw, F = sequence.shape
    
    if T_raw == target_length:
        return sequence
    
    # Create interpolation function for each feature
    original_indices = np.linspace(0, T_raw - 1, T_raw)
    target_indices = np.linspace(0, T_raw - 1, target_length)
    
    resampled = np.zeros((target_length, F))
    
    for f in range(F):
        interpolator = interp1d(original_indices, sequence[:, f], kind='linear')
        resampled[:, f] = interpolator(target_indices)
    
    return resampled


@app.route('/predict_quality', methods=['POST'])
def predict_quality():
    """
    LSTM-based movement quality prediction for shoulder exercises.
    
    Expected input:
    {
        "user_id": "user_123",
        "angles": [
            [a11, a12, ..., a1F],
            [a21, a22, ..., a2F],
            ...
        ]
    }
    
    Where angles is a 2D list [T_raw, F] of raw joint angles for ONE shoulder rep.
    
    Returns:
    {
        "quality_score": 0.87,           # Movement quality [0, 1]
        "rep_rom": 78.3,                 # Range of motion (degrees)
        "personalized_target_angle": 105.0  # Adaptive target for next rep
    }
    """
    try:
        if lstm_model is None:
            return jsonify({'error': 'LSTM model not loaded'}), 500
        
        if personalizer is None:
            return jsonify({'error': 'Personalizer not initialized'}), 500
        
        data = request.json
        user_id = data.get('user_id', 'default_user')
        angles_raw = np.array(data.get('angles', []), dtype=np.float32)
        
        if len(angles_raw) == 0:
            return jsonify({'error': 'No angles provided'}), 400
        
        # Ensure 2D array [T, F]
        if angles_raw.ndim == 1:
            angles_raw = angles_raw.reshape(-1, 1)
        
        T_raw, F = angles_raw.shape
        
        # Validate feature count
        if F != lstm_metadata['input_size']:
            return jsonify({
                'error': f'Feature mismatch: expected {lstm_metadata["input_size"]}, got {F}'
            }), 400
        
        # Validate minimum sequence length
        if T_raw < 3:
            return jsonify({'error': 'Sequence too short (need at least 3 frames)'}), 400
        
        # 1. Compute rep ROM (before normalization)
        rep_rom = float(np.max(np.abs(angles_raw)))
        
        # 2. Resample to fixed sequence length
        seq_len = lstm_metadata['seq_len']
        angles_resampled = resample_sequence(angles_raw, seq_len)
        
        # 3. Normalize using training statistics
        angles_normalized = (angles_resampled - lstm_metadata['angle_mean']) / lstm_metadata['angle_std']
        
        # 4. Convert to tensor [1, seq_len, F] (batch size 1)
        angles_tensor = torch.FloatTensor(angles_normalized).unsqueeze(0)
        
        # 5. Run LSTM inference
        with torch.no_grad():
            logits = lstm_model(angles_tensor)
            quality_score = float(torch.sigmoid(logits).item())
        
        # 6. Update personalization and get target
        personalized_target = personalizer.update_and_get_target(
            user_id=user_id,
            rep_rom=rep_rom,
            quality_score=quality_score
        )
        
        # 7. Return results
        return jsonify({
            'quality_score': round(quality_score, 3),
            'rep_rom': round(rep_rom, 2),
            'personalized_target_angle': round(personalized_target, 1),
            'user_id': user_id
        })
    
    except Exception as e:
        import traceback
        traceback.print_exc()
        return jsonify({'error': str(e)}), 400


# ============================================================
# TEXT-TO-SPEECH ENDPOINT (for VR voice cues)
# ============================================================
@app.route('/generate_tts', methods=['POST'])
def generate_tts():
    """Generate speech audio from text and return as WAV file"""
    try:
        import pyttsx3
        import tempfile
        
        data = request.json or {}
        text = data.get('text', 'Voice cue')
        
        if not text or len(text) > 1000:
            return jsonify({'error': 'Invalid text'}), 400
        
        # Create speech synthesizer
        engine = pyttsx3.init()
        engine.setProperty('rate', 150)  # Speed: 150 wpm
        engine.setProperty('volume', 1.0)  # Volume: 1.0 = max
        
        # Save to temporary file and return it directly.
        with tempfile.NamedTemporaryFile(suffix='.wav', delete=False) as tmp:
            temp_file = tmp.name

        engine.save_to_file(text, temp_file)
        engine.runAndWait()

        if os.path.exists(temp_file):
            response = send_file(
                temp_file,
                mimetype='audio/wav',
                as_attachment=False,
                conditional=False,
            )

            @response.call_on_close
            def cleanup_temp_file():
                try:
                    if os.path.exists(temp_file):
                        os.remove(temp_file)
                except Exception:
                    pass

            return response
        
        return jsonify({'error': 'TTS generation failed'}), 500
        
    except Exception as e:
        print(f"TTS error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({'error': str(e)}), 500


def _pause_session_on_shutdown(reason: str = 'shutdown'):
    if session_logger and session_logger.current_session_id:
        try:
            session_logger.pause_session({'shutdownReason': reason})
        except Exception:
            pass


def _handle_shutdown_signal(reason: str):
    _pause_session_on_shutdown(reason)
    raise SystemExit(0)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument('--resume-latest', action='store_true', help='Resume the latest paused or active session on launch')
    parser.add_argument('--new-session', action='store_true', help='Force a fresh session even if a resumable session exists')
    args = parser.parse_args()

    if args.new_session:
        session_logger.default_resume_latest = False
    else:
        session_logger.default_resume_latest = args.resume_latest or session_logger.default_resume_latest

    atexit.register(_pause_session_on_shutdown, 'process_exit')
    try:
        signal.signal(signal.SIGINT, lambda signum, frame: _handle_shutdown_signal('sigint'))
        signal.signal(signal.SIGTERM, lambda signum, frame: _handle_shutdown_signal('sigterm'))
    except Exception:
        pass

    print("="*60)
    print("🚀 Pose2Play - All-in-One Server")
    print("="*60)
    print("\nLoaded Models:")
    print(f"  ✅ RL Model (DQN):          {model is not None}")
    print(f"  ✅ Form Classifier:         {form_classifier is not None}")
    print(f"  ✅ LSTM Quality Model:      {lstm_model is not None}")
    print(f"  ✅ Personalizer:            {personalizer is not None}")
    print(f"  ✅ Firebase Logging:        {session_logger.is_enabled() if session_logger else False}")
    print("\nNetwork Configuration:")
    targets_str = ", ".join([f"{ip}:{UNITY_POSE_UDP_PORT}" for ip in UNITY_UDP_TARGETS])
    print(f"  📡 UDP Pose Target(s):      {targets_str}")
    targets_dash_str = ", ".join([f"{ip}:{UNITY_DASHBOARD_UDP_PORT}" for ip in UNITY_UDP_TARGETS])
    print(f"  📡 UDP Dashboard Target(s): {targets_dash_str}")
    print(f"  ℹ️  Unity Play mode setup: .\START_PHASE7.ps1 -Mode Local (or -Mode Quest for VR)")
    server_host = os.getenv('POSE2PLAY_SERVER_HOST', 'localhost')
    server_port = int(os.getenv('POSE2PLAY_SERVER_PORT', '5000'))
    browser_host = 'localhost' if server_host == '0.0.0.0' else server_host

    latest_session = session_logger.get_latest_resumable_session() if session_logger else None
    print("\n" + "="*60)
    print(f"🌐 Open in browser: http://{browser_host}:{server_port}")
    print("📷 Make sure to ALLOW camera access!")
    if session_logger and session_logger.default_resume_latest:
        print(f"🔁 Resume mode: enabled{' | latest resumable session found' if latest_session else ''}")
    else:
        print("🆕 Resume mode: start new session by default")
    print("="*60 + "\n")
    socketio.run(app, host=server_host, port=server_port, debug=False)
