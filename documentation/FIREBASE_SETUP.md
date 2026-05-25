# Firebase Firestore Setup

This project can optionally log session summaries and completed reps to Firebase Firestore from the Python backend.

## 1. Create a Firebase project

- Open the Firebase console.
- Create a new project or use an existing one.
- Enable Firestore Database in the project.

## 2. Create a service account key

- Go to Project settings.
- Open the Service accounts tab.
- Generate a new private key.
- Save the JSON file somewhere local and secure.

## 3. Set the environment variable

Set `FIREBASE_SERVICE_ACCOUNT_PATH` to the full path of the downloaded JSON file.

Example on Windows PowerShell:

```powershell
$env:FIREBASE_SERVICE_ACCOUNT_PATH = "C:\path\to\firebase-service-account.json"
```

Optional:

```powershell
$env:POSE2PLAY_RESUME_LATEST = "1"
$env:POSE2PLAY_APP_VERSION = "0.1.0"
```

## 4. Install backend dependencies

Install the Python requirements from `ml/requirements.txt`.

## 5. Run the backend normally

Start the Python backend the same way you already do.

If you want the backend to resume the latest paused or active session on launch, start it with:

```powershell
python api_server.py --resume-latest
```

Without that flag, the backend starts a fresh session by default.

## 6. Confirm data is being written

- Open the Firestore console.
- Look for documents under `sessions/{sessionId}`.
- Completed reps appear under `sessions/{sessionId}/reps`.
- Optional feedback events appear under `sessions/{sessionId}/feedback_events`.
