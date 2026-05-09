"""Firebase Firestore adapter for Pose2Play.

This module keeps Firebase initialization isolated from the rest of the backend.
If credentials are missing or Firebase cannot initialize, logging is disabled
and the rehab app keeps running normally.
"""

from __future__ import annotations

import os
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional

try:
    import firebase_admin
    from firebase_admin import credentials, firestore
except Exception:  # pragma: no cover - optional dependency
    firebase_admin = None
    credentials = None
    firestore = None


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def to_json_safe(value: Any) -> Any:
    """Convert common Python/numpy values into Firestore-safe JSON values."""
    if value is None:
        return None

    if isinstance(value, (str, int, float, bool)):
        return value

    if isinstance(value, datetime):
        return value

    if isinstance(value, Path):
        return str(value)

    if isinstance(value, dict):
        return {str(key): to_json_safe(item) for key, item in value.items()}

    if isinstance(value, (list, tuple, set)):
        return [to_json_safe(item) for item in value]

    numpy_item = getattr(value, "item", None)
    if callable(numpy_item):
        try:
            return to_json_safe(numpy_item())
        except Exception:
            pass

    if hasattr(value, "tolist"):
        try:
            return to_json_safe(value.tolist())
        except Exception:
            pass

    return str(value)


class FirebaseDB:
    """Minimal Firestore wrapper with graceful disablement."""

    def __init__(self) -> None:
        self.enabled = False
        self.error_message: Optional[str] = None
        self.db = None
        self.app = None
        self.service_account_path = os.getenv("FIREBASE_SERVICE_ACCOUNT_PATH", "").strip()
        self._initialize()

    def _initialize(self) -> None:
        if firebase_admin is None:
            self.error_message = "firebase-admin is not installed"
            return

        if not self.service_account_path:
            self.error_message = "FIREBASE_SERVICE_ACCOUNT_PATH is not set"
            return

        credential_path = Path(self.service_account_path)
        if not credential_path.exists():
            self.error_message = f"Service account file not found: {credential_path}"
            return

        try:
            try:
                self.app = firebase_admin.get_app()
            except Exception:
                self.app = firebase_admin.initialize_app(credentials.Certificate(str(credential_path)))

            self.db = firestore.client()
            self.enabled = True
            print(f"✅ Firebase Firestore enabled: {credential_path}")
        except Exception as exc:  # pragma: no cover - runtime safety
            self.error_message = str(exc)
            self.enabled = False
            self.db = None
            self.app = None
            print(f"⚠️ Firebase disabled: {exc}")

    def is_enabled(self) -> bool:
        return self.enabled and self.db is not None

    def _session_ref(self, session_id: str):
        return self.db.collection("sessions").document(session_id)

    def _subcollection_ref(self, session_id: str, subcollection: str):
        return self._session_ref(session_id).collection(subcollection)

    def set_session_document(self, session_id: str, data: Dict[str, Any], merge: bool = False) -> bool:
        if not self.is_enabled():
            return False

        try:
            self._session_ref(session_id).set(to_json_safe(data), merge=merge)
            return True
        except Exception as exc:
            self.error_message = str(exc)
            print(f"⚠️ Firestore session write failed for {session_id}: {exc}")
            return False

    def update_session_document(self, session_id: str, data: Dict[str, Any]) -> bool:
        if not self.is_enabled():
            return False

        try:
            self._session_ref(session_id).update(to_json_safe(data))
            return True
        except Exception as exc:
            self.error_message = str(exc)
            print(f"⚠️ Firestore session update failed for {session_id}: {exc}")
            return False

    def set_subcollection_document(
        self,
        session_id: str,
        subcollection: str,
        document_id: str,
        data: Dict[str, Any],
        merge: bool = False,
    ) -> bool:
        if not self.is_enabled():
            return False

        try:
            self._subcollection_ref(session_id, subcollection).document(document_id).set(
                to_json_safe(data),
                merge=merge,
            )
            return True
        except Exception as exc:
            self.error_message = str(exc)
            print(
                f"⚠️ Firestore write failed for sessions/{session_id}/{subcollection}/{document_id}: {exc}"
            )
            return False

    def list_session_subcollection(self, session_id: str, subcollection: str) -> List[Dict[str, Any]]:
        if not self.is_enabled():
            return []

        try:
            documents = []
            for snapshot in self._subcollection_ref(session_id, subcollection).stream():
                payload = snapshot.to_dict() or {}
                payload["_documentId"] = snapshot.id
                documents.append(payload)
            return documents
        except Exception as exc:
            self.error_message = str(exc)
            print(f"⚠️ Firestore read failed for sessions/{session_id}/{subcollection}: {exc}")
            return []

    def list_session_reps(self, session_id: str) -> List[Dict[str, Any]]:
        reps = self.list_session_subcollection(session_id, "reps")
        reps.sort(key=lambda item: (item.get("repNumber") is None, item.get("repNumber", 0), item.get("_documentId", "")))
        return reps

    def get_session_document(self, session_id: str) -> Optional[Dict[str, Any]]:
        if not self.is_enabled():
            return None

        try:
            snapshot = self._session_ref(session_id).get()
            if not snapshot.exists:
                return None
            payload = snapshot.to_dict() or {}
            payload["sessionId"] = session_id
            return payload
        except Exception as exc:
            self.error_message = str(exc)
            print(f"⚠️ Firestore read failed for session {session_id}: {exc}")
            return None

    def get_latest_resumable_session(self) -> Optional[Dict[str, Any]]:
        if not self.is_enabled():
            return None

        try:
            candidates: List[Dict[str, Any]] = []
            for snapshot in self.db.collection("sessions").where("canResume", "==", True).stream():
                payload = snapshot.to_dict() or {}
                status = payload.get("status")
                if status not in {"paused", "active"}:
                    continue
                payload["sessionId"] = snapshot.id
                candidates.append(payload)

            if not candidates:
                return None

            def sort_key(item: Dict[str, Any]):
                updated = item.get("lastUpdatedAt") or item.get("updatedAt") or item.get("startTime")
                if isinstance(updated, datetime):
                    return updated
                return datetime.min.replace(tzinfo=timezone.utc)

            return deepcopy(sorted(candidates, key=sort_key, reverse=True)[0])
        except Exception as exc:
            self.error_message = str(exc)
            print(f"⚠️ Firestore resumable-session lookup failed: {exc}")
            return None

    def close(self) -> None:
        self.enabled = False
        self.db = None
        self.app = None
