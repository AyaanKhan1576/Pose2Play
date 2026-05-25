# Setup Guide for Team Members

Quick setup guide to get the Pose2Play system running on your machine.

## 📋 Prerequisites Checklist

Before starting, make sure you have:
- [ ] Windows 10/11
- [ ] Python 3.8 or higher installed ([Download](https://www.python.org/downloads/))
- [ ] Git installed ([Download](https://git-scm.com/downloads))
- [ ] Webcam connected
- [ ] Modern web browser (Chrome, Edge, or Firefox)

## 🚀 Setup Steps

### Step 1: Clone Repository

```powershell
# Open PowerShell and navigate to your desired location
cd C:\Users\YourName\Projects

# Clone the repository
git clone <repository-url>

# Navigate to project directory
cd "Pose2Play base model iteration 1"
```

### Step 2: Install Python Dependencies

```powershell
# Navigate to ml directory
cd ml

# Create virtual environment
python -m venv rl_env

# Activate virtual environment
.\rl_env\Scripts\activate

# You should see (rl_env) in your terminal

# Install all dependencies
pip install -r requirements.txt

# This will take 5-10 minutes
```

**Expected output:**
```
Successfully installed stable-baselines3-2.0.0 gymnasium-0.29.1 ...
```

### Step 3: Download Pre-trained Models

**Option A: Get from Team Drive**
1. Download `models.zip` from team shared folder
2. Extract to `ml/models/`
3. Verify you have:
   - `ml/models/dqn/DQN_rehab_final.zip`
   - `ml/models/form_classifier/form_classifier_rf.pkl`

**Option B: Train Models Yourself** (requires Dataset - see Step 4)
```powershell
# Process dataset
python data_processor.py --input ..\Dataset --output .\data\processed

# Train form classifier (~30 seconds)
python train_form_classifier.py --model rf --output .\models\form_classifier

# Train RL agent (~30 minutes)
python train_rl.py --algorithm DQN --timesteps 100000 --output .\models\dqn
```

### Step 4: (Optional) Download Dataset

Only needed if you want to retrain models.

1. Download `Dataset.zip` from team shared folder
2. Extract to parent directory:
```
├── Dataset/              ← Extract here
│   ├── inertial/
│   └── optical/
└── Pose2Play base model iteration 1/
    └── ml/
```

### Step 5: Test Installation

```powershell
# Make sure you're in ml/ directory with rl_env activated

# Test API
python api_server.py
```

You should see:
```
✅ Loaded RL model: ./models/dqn/DQN_rehab_final.zip
✅ Loaded form classifier: ./models/form_classifier/form_classifier_rf.pkl
🚀 RL API Server Starting...
```

Press `Ctrl+C` to stop.

### Step 6: Run the Demo

```powershell
# Navigate back to project root
cd ..

# Run quick start script
.\START_PHASE7.ps1
```

This will:
1. Start Flask API server in new window
2. Open demo in your browser
3. Ready to use!

**In the browser:**
1. Allow camera access when prompted
2. Click "Start Detection"
3. Try doing squats!

## ✅ Verification

Check that everything works:

```powershell
cd ml
.\rl_env\Scripts\activate
python test_form_api.py
```

You should see:
```
🎉 All tests passed! Form classification is working perfectly!
```

## 🐛 Common Issues

### Issue 1: "python is not recognized"
**Solution:** Python not installed or not in PATH
```powershell
# Check Python installation
python --version

# If not found, reinstall Python and check "Add to PATH" during installation
```

### Issue 2: "pip install" fails
**Solution:** Try upgrading pip first
```powershell
python -m pip install --upgrade pip
pip install -r requirements.txt
```

### Issue 3: Virtual environment activation fails
**Solution:** Enable script execution
```powershell
# Run PowerShell as Administrator
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

# Try activating again
.\rl_env\Scripts\activate
```

### Issue 4: Models not found
**Solution:** Check file paths
```powershell
# Verify models exist
ls ml\models\dqn\DQN_rehab_final.zip
ls ml\models\form_classifier\form_classifier_rf.pkl

# If missing, get from team drive or train models
```

### Issue 5: API server won't start
**Solution:** Port 5000 might be in use
```powershell
# Check what's using port 5000
netstat -ano | findstr :5000

# Kill the process (replace PID with actual process ID)
taskkill /PID <PID> /F

# Try starting API again
```

### Issue 6: Camera not detected
**Solution:**
- Check if camera works in other apps (Camera app)
- Close other apps using camera (Teams, Zoom, etc.)
- Try different browser
- Check browser permissions (Settings → Privacy → Camera)

### Issue 7: Demo loads but no pose detection
**Solution:**
- Refresh browser
- Clear browser cache
- Check browser console (F12) for errors
- Ensure good lighting and camera angle

## 📁 Project Structure (What Goes Where)

```
Pose2Play base model iteration 1/
├── demo/
│   └── index.html           # Web app - you can modify UI here
├── ml/
│   ├── rl_env/              # Virtual environment (created by you)
│   ├── models/
│   │   ├── dqn/            # RL model (download or train)
│   │   └── form_classifier/ # Form model (download or train)
│   ├── data/
│   │   └── processed/       # Processed data (if you train)
│   ├── train_rl.py         # Train RL agent
│   ├── train_form_classifier.py  # Train form classifier
│   ├── api_server.py       # Flask API
│   └── requirements.txt     # Python dependencies
├── shared/
│   └── models/
│       └── pose_landmarker_lite.task  # MediaPipe (included)
├── START_PHASE7.ps1        # Quick start (run this!)
├── README.md               # Project overview
├── MASTER_GUIDE.md         # Full documentation
└── SETUP.md                # This file
```

## 🔄 Daily Workflow

Every time you work on the project:

```powershell
# 1. Navigate to project
cd "path\to\Pose2Play base model iteration 1"

# 2. Activate virtual environment
cd ml
.\rl_env\Scripts\activate

# 3. Pull latest changes
git pull

# 4. Start working!
# - To run demo: cd .. then .\START_PHASE7.ps1
# - To train models: python train_xxx.py
# - To test API: python test_form_api.py
```

## 🤝 Making Changes

### Modifying the UI
- Edit `demo/index.html`
- Refresh browser to see changes
- No need to restart API server

### Modifying API Logic
- Edit `ml/api_server.py`
- Restart API server (Ctrl+C then restart)

### Adding New Features
1. Create new branch: `git checkout -b feature-name`
2. Make changes
3. Test thoroughly
4. Commit: `git commit -m "Description"`
5. Push: `git push origin feature-name`
6. Create pull request

### Training New Models
```powershell
cd ml
.\rl_env\Scripts\activate

# Train form classifier
python train_form_classifier.py --model rf --output .\models\form_classifier

# Train RL agent
python train_rl.py --algorithm DQN --timesteps 100000 --output .\models\dqn
```

## 📞 Getting Help

1. **Check MASTER_GUIDE.md** - Comprehensive documentation (3500+ lines)
2. **Check console errors** - Press F12 in browser
3. **Ask team members** - [Team contact method]
4. **Open GitHub issue** - Describe the problem + screenshots

## 🎯 Quick Start Summary

```powershell
# One-time setup
git clone <repo>
cd "Pose2Play base model iteration 1\ml"
python -m venv rl_env
.\rl_env\Scripts\activate
pip install -r requirements.txt

# Download models from team drive (or train yourself)

# Every time you work
cd "Pose2Play base model iteration 1"
.\START_PHASE7.ps1

# Browser opens → Allow camera → Start Detection → Exercise!
```

## ✅ You're Ready!

If you can:
- [x] Run `.\START_PHASE7.ps1` without errors
- [x] See camera feed in browser
- [x] Do squats and see rep counter increase
- [x] See Form Quality Panel update
- [x] See Gamification panel show RP

**Then you're all set! 🎉**

---

**Need help?** Contact [Team Lead Name] or check MASTER_GUIDE.md
