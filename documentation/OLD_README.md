# Pose2Play - AI-Powered Rehabilitation System

Real-time exercise form analysis and adaptive difficulty adjustment using computer vision and reinforcement learning.

## 🎯 Features

- **Real-time Pose Detection** - MediaPipe tracks 33 body landmarks at 30 FPS
- **Exercise Form Analysis** - ML classifier detects correct vs incorrect form (70.9% accuracy)
- **RL-Powered Difficulty Adjustment** - Agent learned optimal encouragement strategy (100% completion rate)
- **Gamification** - Recovery Points, levels, achievements, streaks
- **Adaptive Learning** - Per-user personalized targets based on performance history
- **Real-time Feedback** - Specific form corrections ("Squat deeper", "Balance evenly")

## 🏥 Target Exercises

- Squats (knee rehabilitation)
- Hip Abduction/Adduction (hip strengthening)
- Knee Flexion (knee mobility)
- Shoulder Raises (shoulder rehabilitation)

## 📦 Project Structure

```
Pose2Play base model iteration 1/
├── demo/
│   └── index.html           # Main web application
├── ml/
│   ├── data/
│   │   └── processed/       # Processed dataset (train/val/test)
│   ├── models/
│   │   ├── dqn/            # RL difficulty adjustment model
│   │   └── form_classifier/ # Form classification model
│   ├── train_rl.py         # Train RL agent
│   ├── train_form_classifier.py  # Train form classifier
│   ├── api_server.py       # Flask API server
│   ├── data_processor.py   # Dataset preprocessing
│   └── test_form_api.py    # API testing
├── shared/
│   └── models/
│       └── pose_landmarker_lite.task  # MediaPipe model
├── START_PHASE7.ps1        # Quick start script (Windows)
├── MASTER_GUIDE.md         # Complete system documentation
└── README.md               # This file
```

## 🚀 Quick Start

### Prerequisites

- **Python 3.8+** (Python 3.13.5 recommended)
- **Modern web browser** (Chrome, Edge, Firefox)
- **Webcam**
- **Windows** (for PowerShell scripts)

### Installation

**1. Clone the repository**
```bash
git clone <your-repo-url>
cd "Pose2Play base model iteration 1"
```

**2. Set up Python environment**
```powershell
# Navigate to ml directory
cd ml

# Create virtual environment
python -m venv rl_env

# Activate environment
.\rl_env\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

**3. Pre-trained Models** ✅

Good news! **Pre-trained models are already included** in this repository:
- `ml/models/dqn/DQN_rehab_final.zip` - RL agent (trained)
- `ml/models/form_classifier/form_classifier_rf.pkl` - Form classifier (trained)

**You can skip training and use the system immediately!**

**4. Download Dataset** (Optional - only needed for retraining)

If you want to retrain the models, download the PHYTMO dataset:

**Dataset Source:** [PHYTMO Dataset on Zenodo](https://zenodo.org/records/6319979/files/PHYTMO.zip)

After downloading, extract and place the `Dataset/` folder in the parent directory:
```
├── Dataset/
│   ├── inertial/
│   │   ├── lower/
│   │   └── upper/
│   └── optical/
└── Pose2Play base model iteration 1/
    └── ml/
```

### Firebase Firestore Logging (Optional)

Pose2Play can log compact session and rep summaries to Firebase Firestore from the Python backend. This is disabled by default and the app still runs normally without Firebase.

Setup details are in [FIREBASE_SETUP.md](FIREBASE_SETUP.md).

### Running the System

#### Option 1: Quick Start (Recommended)

```powershell
# From project root directory
.\START_PHASE7.ps1
```

This will:
1. Start Flask API server (RL + Form Classification)
2. Open demo in browser
3. Ready to use!

#### Option 2: Manual Start

```powershell
# Terminal 1: Start API server
cd ml
.\rl_env\Scripts\activate
python api_server.py

# Terminal 2: Open demo
# Open demo/index.html in your browser
```

### Using the Demo

1. **Allow camera access** when prompted
2. **Click "Start Detection"**
3. **Select exercise** from dropdown
4. **Start exercising!**
5. **Watch the panels:**
   - **Form Quality Panel** - Real-time form corrections
   - **Gamification Panel** - RP, levels, achievements
   - **Adaptive Learning** - Personalized targets

## 🧪 Training Models (Optional)

**Note:** Pre-trained models are already included! This section is only needed if you want to:
- Retrain with different hyperparameters
- Experiment with new algorithms
- Use your own dataset

### Prerequisites

Make sure you've:
1. Downloaded the [PHYTMO Dataset](https://zenodo.org/records/6319979/files/PHYTMO.zip)
2. Activated virtual environment (`.\rl_env\Scripts\activate`)

### Step 1: Process Dataset

```powershell
cd ml
python data_processor.py --input ..\Dataset --output .\data\processed
```

**Output:**
- `data/processed/train.csv` (1526 samples)
- `data/processed/val.csv` (270 samples)
- `data/processed/test.csv` (908 samples)

### Step 2: Train Form Classifier

```powershell
python train_form_classifier.py --model rf --output .\models\form_classifier
```

**Output:**
- `models/form_classifier/form_classifier_rf.pkl` (70.9% test accuracy)
- Training time: ~30 seconds

### Step 3: Train RL Agent

```powershell
python train_rl.py --algorithm DQN --timesteps 100000 --output .\models\dqn
```

**Output:**
- `models/dqn/DQN_rehab_final.zip` (902 mean reward, 100% completion)
- Training time: ~30 minutes
- Logs: `logs/DQN_<timestamp>/`

### Step 4: Evaluate Models

```powershell
# Test form classifier
python test_form_api.py

# Evaluate RL agent
python train_rl.py --mode eval --algorithm DQN --model .\models\dqn\DQN_rehab_final.zip
```

## 🔍 API Endpoints

The Flask server (`http://localhost:5000`) provides:

### GET /health
Check server status
```json
{
  "status": "ok",
  "rl_model_loaded": true,
  "form_classifier_loaded": true
}
```

### POST /predict
RL difficulty adjustment
```json
Request: {
  "state": [20-element array]
}

Response: {
  "action": 0,
  "action_name": "encourage",
  "confidence": 0.95
}
```

### POST /predict_form_simple
Webcam-based form analysis
```json
Request: {
  "angles": { "knee_left": 85, "knee_right": 87 },
  "movement_speed": 2.5,
  "exercise_type": "squat"
}

Response: {
  "form_quality": "95.0%",
  "is_correct": true,
  "feedback": ["✅ Excellent form!"],
  "corrections": [],
  "issues_detected": []
}
```

## 📊 Model Performance

### Form Classifier (Random Forest)
- **Test Accuracy:** 70.9%
- **Precision (Correct Form):** 68%
- **Recall (Correct Form):** 81%
- **Training Samples:** 1,268 (hip/knee/shoulder exercises)

### RL Agent (DQN)
- **Mean Reward:** 902 (180% above target)
- **Completion Rate:** 100% (vs 70% baseline)
- **Training Episodes:** 100,000 timesteps
- **Learned Strategy:** "Encouragement-First" (90% encouragement, minimal difficulty increases)

## 🛠️ Troubleshooting

### Issue: API not connecting
**Solution:**
```powershell
# Check if API is running
curl http://localhost:5000/health

# Restart API
cd ml
.\rl_env\Scripts\activate
python api_server.py
```

### Issue: Form panel shows "Ready" but no updates
**Solution:**
- Check browser console (F12) for errors
- Verify API is running (`/health` returns 200)
- Try refreshing browser

### Issue: Models not found
**Solution:**
```powershell
# Check if models exist
ls ml\models\dqn
ls ml\models\form_classifier

# If missing, train models (see Training Models section)
```

### Issue: Camera not working
**Solution:**
- Allow camera permissions in browser
- Check if another app is using camera
- Try different browser (Chrome recommended)

## 📚 Documentation

- **MASTER_GUIDE.md** - Complete system documentation (3500+ lines)
  - All 8 phases: Demo → Training → Evaluation → Integration
  - Step-by-step testing guides
  - Troubleshooting reference
  - Architecture diagrams

## 🔧 Development

### Adding New Exercises

1. Create exercise checker in `demo/index.html`:
```javascript
function checkNewExercise(angle) {
    // State machine logic
}
```

2. Update exercise dropdown:
```html
<option value="new_exercise">New Exercise</option>
```

3. Add to form analysis in `ml/form_feedback.py`:
```python
EXERCISE_FEEDBACK = {
    'new_exercise': {
        'correct': ["Great!"],
        'incorrect_patterns': {...}
    }
}
```

### Customizing RL Behavior

Edit `ml/train_rl.py`:
- Adjust reward function
- Modify state space (20 dimensions)
- Change action space (5 actions)
- Tune hyperparameters

### Improving Form Classifier

```powershell
# Try neural network instead of Random Forest
python train_form_classifier.py --model mlp

# Adjust thresholds in ml/form_feedback.py
THRESHOLDS = {
    'asymmetry_max': 0.15,  # Change to 0.10 for stricter
    ...
}
```

## 🤝 Contributing

When making changes:

1. **Test thoroughly** - Run test suite
```powershell
python test_form_api.py
```

2. **Update documentation** - Modify MASTER_GUIDE.md if adding features

3. **Follow code style** - Keep consistent with existing code

4. **Commit messages** - Use clear, descriptive messages

## 📝 System Requirements

### Minimum:
- CPU: Intel i5 or equivalent
- RAM: 8 GB
- Python: 3.8+
- Browser: Chrome 90+, Firefox 88+, Edge 90+

### Recommended:
- CPU: Intel i7 or equivalent
- RAM: 16 GB
- Python: 3.13+
- Webcam: 720p @ 30fps

## 🎓 Citation

If you use this project in your research:

```bibtex
@software{pose2play2025,
  title = {Pose2Play: AI-Powered Rehabilitation System},
  author = {Your Team Name},
  year = {2025},
  description = {Real-time exercise form analysis using ML and RL}
}
```

## 📄 License

See LICENSE file for details.

## 🙏 Acknowledgments

- **MediaPipe** - Pose detection library
- **Stable-Baselines3** - RL training framework
- **scikit-learn** - ML classification models
- **PHYTMO Dataset** - Rehabilitation exercise recordings (29 subjects)
  - Source: [Zenodo - PHYTMO Dataset](https://zenodo.org/records/6319979)
  - Citation: Physical Therapy Motion Dataset for rehabilitation research

## 📧 Contact

For questions or issues:
- Open a GitHub issue
- Contact: [Your email/team contact]

---

**Built:** October 2025  
**Status:** ✅ Production Ready  
**Version:** 2.0 - Form Classification Integrated  
**Models:** Trained and tested (70.9% form accuracy, 100% RL completion rate)
