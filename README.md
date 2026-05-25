# Pose2Play

## Contributors

- Ayaan Khan
- Minahil Ali
- Ahmed Hannan

## What Is Pose2Play?

Pose2Play is an AI-powered rehabilitation and exercise guidance system that uses real-time pose detection, exercise analysis, gamification, and adaptive feedback to support home-based physiotherapy.

The system tracks user movement through a webcam, analyzes exercise form, provides live corrective feedback, and visualizes progress through a web interface and VR training environment.

## Key Features

- Real-time pose detection using MediaPipe body landmarks
- Exercise form analysis for rehabilitation movements
- Live corrective feedback
- Gamified rehabilitation with score, levels, streaks, and progress
- Adaptive difficulty and encouragement strategy using reinforcement learning
- User performance tracking and personalized targets
- Flask API backend for ML and RL inference
- Optional Firebase Firestore logging for session and rep summaries
- Unity VR visualization for immersive Meta Quest training
- Pre-trained models included for immediate use

## Supported Exercises

- Shoulder raises for shoulder rehabilitation
- Squats for knee rehabilitation
- Hip abduction and adduction for hip strengthening

## System Overview

```text
Webcam
-> MediaPipe Pose Detection
-> Python Exercise Analysis
-> ML Form Classifier
-> RL Difficulty / Feedback Logic
-> Web Demo and/or Unity VR Visualization
-> Live Stats, Score, Feedback, and Progress
```

For the VR version:

```text
Python Backend
-> UDP Live Exercise Data
-> Unity VR App
-> Camera HUD, Avatar, Arena, Score, Feedback, and Gamification
```

## Prerequisites

Minimum:

- Python 3.8+
- Modern browser such as Chrome, Edge, or Firefox
- Webcam
- Windows PowerShell for provided startup scripts

Recommended:

- Python 3.13+
- 720p webcam at 30 FPS
- 16 GB RAM
- Meta Quest 3 for VR deployment
- Unity with OpenXR support for the VR application

## Setup

### 1. Clone the Repository

```bash
git clone <your-repo-url>
cd "Pose2Play"
```

### 2. Create Python Environment

```powershell
cd ml
python -m venv rl_env
.\rl_env\Scripts\activate
pip install -r requirements.txt
```

### 3. Confirm Pre-Trained Models

Pre-trained models are included:

```text
ml/models/dqn/DQN_rehab_final.zip
ml/models/form_classifier/form_classifier_rf.pkl
```

You can run the system without retraining.

### 4. Optional Dataset Setup for Retraining

Download the PHYTMO dataset:

```text
https://zenodo.org/records/6319979/files/PHYTMO.zip
```

Extract it.

## Running the Project

### Option 1: Quick Start

From the project root:

```powershell
.\START_PHASE7.ps1
```

This starts the Flask API server and opens the browser demo.

### Option 2: Manual Start

Terminal 1:

```powershell
cd ml
.\rl_env\Scripts\activate
python api_server.py
```

Terminal 2:

Open:

```text
demo/index.html
```

in your browser.

## Using the Demo

1. Allow camera access.
2. Click `Start Detection`.
3. Select an exercise.
4. Perform the exercise in view of the webcam.
5. Watch the live panels for:
   - Form quality
   - Feedback and corrections
   - Reps
   - Score
   - Level
   - Streaks
   - Adaptive targets

## Unity VR Setup

The Unity VR application receives live exercise statistics from the Python backend through UDP.

The VR scene focuses on:

- Immersive rehabilitation training arena
- Humanoid avatar visualization
- Camera-attached VR HUD
- Live score, reps, level, phase, form, streak, and feedback
- Avatar spotlight and training platform
- Gamified environment changes when levels increase

Do not change the UDP packet format unless required. The Unity app expects the backend dashboard data stream to remain compatible.

## Optional Firebase Logging

Pose2Play can log compact session and rep summaries to Firebase Firestore from the Python backend.

Firebase is optional and disabled by default. The system runs normally without it.

See:

```text
FIREBASE_SETUP.md
```

## Training Models

Pre-trained models are already included. Retraining is only needed if you want to test new data, change hyperparameters, or experiment with new algorithms.

### Process Dataset

```powershell
cd ml
python data_processor.py --input ..\Dataset --output .\data\processed
```

Expected output:

```text
data/processed/train.csv
data/processed/val.csv
data/processed/test.csv
```

### Train Form Classifier

```powershell
python train_form_classifier.py --model rf --output .\models\form_classifier
```

Output:

```text
models/form_classifier/form_classifier_rf.pkl
```

### Train RL Agent

```powershell
python train_rl.py --algorithm DQN --timesteps 100000 --output .\models\dqn
```

Output:

```text
models/dqn/DQN_rehab_final.zip
logs/DQN_<timestamp>/
```

### Evaluate Models

```powershell
python test_form_api.py
python train_rl.py --mode eval --algorithm DQN --model .\models\dqn\DQN_rehab_final.zip
```

## API Endpoints

The Flask server runs at:

```text
http://localhost:5000
```

### GET /health

Checks server and model status.

Example response:

```json
{
  "status": "ok",
  "rl_model_loaded": true,
  "form_classifier_loaded": true
}
```

### POST /predict

Returns RL difficulty or encouragement action.

Example request:

```json
{
  "state": [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
}
```

Example response:

```json
{
  "action": 0,
  "action_name": "encourage",
  "confidence": 0.95
}
```

### POST /predict_form_simple

Returns form quality and corrective feedback.

Example request:

```json
{
  "angles": {
    "knee_left": 85,
    "knee_right": 87
  },
  "movement_speed": 2.5,
  "exercise_type": "squat"
}
```

Example response:

```json
{
  "form_quality": "95.0%",
  "is_correct": true,
  "feedback": ["Excellent form"],
  "corrections": [],
  "issues_detected": []
}
```

## Model Performance

### Form Classifier

- Model: Random Forest
- Test accuracy: 70.9%
- Correct-form precision: 68%
- Correct-form recall: 81%
- Training samples: 1,268
- Exercise categories: hip, knee, shoulder

### RL Agent

- Algorithm: DQN
- Mean reward: 902
- Completion rate: 100%
- Training length: 100,000 timesteps
- Learned behavior: encouragement-first strategy with minimal unnecessary difficulty increases


## Documentation

Additional documentation:

```text
MASTER_GUIDE.md
FIREBASE_SETUP.md
```

`MASTER_GUIDE.md` contains the full project history, phase documentation, architecture notes, testing guides, and extended troubleshooting.


## Acknowledgments

Dataset source:

```text
https://zenodo.org/records/6319979
```

## License

This project is proprietary and all rights are reserved.

The repository is publicly visible for portfolio and academic demonstration purposes only. No reuse, redistribution, modification, or commercial usage is permitted without explicit written permission from the authors.

See the `LICENSE` file for details.
