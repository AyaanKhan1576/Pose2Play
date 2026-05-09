"""Session-level Firestore logging for Pose2Play.

The logger keeps the current session in memory, writes compact per-session and
per-rep documents, and degrades safely when Firestore is not configured.
"""

from __future__ import annotations

from collections import Counter
from copy import deepcopy
from datetime import datetime, timezone
from statistics import mean
from typing import Any, Dict, List, Optional
from uuid import uuid4

from firebase_db import FirebaseDB, to_json_safe, utc_now


class SessionLogger:
    def __init__(
        self,
        firebase_db: Optional[FirebaseDB] = None,
        default_resume_latest: bool = False,
        app_version: Optional[str] = None,
        model_version: Optional[str] = None,
    ) -> None:
        self.firebase_db = firebase_db or FirebaseDB()
        self.default_resume_latest = default_resume_latest
        self.app_version = app_version or None
        self.model_version = model_version or None
        self.current_session: Optional[Dict[str, Any]] = None
        self.current_session_id: Optional[str] = None
        self.exercise_type: Optional[str] = None
        self.reps: List[Dict[str, Any]] = []
        self.feedback_events: List[Dict[str, Any]] = []
        self.metadata: Dict[str, Any] = {}
        self.status: Optional[str] = None
        self.started_at: Optional[datetime] = None
        self.last_error: Optional[str] = None

    def is_enabled(self) -> bool:
        return self.firebase_db.is_enabled()

    def _new_session_id(self) -> str:
        return f"session_{utc_now().strftime('%Y%m%d_%H%M%S')}_{uuid4().hex[:8]}"

    def _normalize_list(self, value: Any) -> List[Any]:
        if value is None:
            return []
        if isinstance(value, list):
            return value
        if isinstance(value, tuple):
            return list(value)
        return [value]

    def _coerce_number(self, value: Any) -> Optional[float]:
        try:
            if value is None:
                return None
            return float(value)
        except (TypeError, ValueError):
            return None

    def _extract_score(self, rep: Dict[str, Any]) -> Optional[float]:
        for key in ("qualityScore", "quality_score", "score", "formQuality"):
            value = rep.get(key)
            if isinstance(value, str) and value.endswith("%"):
                value = value[:-1]
            number = self._coerce_number(value)
            if number is not None:
                return number
        return None

    def _extract_joint_summary(self, rep: Dict[str, Any]) -> Dict[str, Any]:
        summary: Dict[str, Any] = {}
        if rep.get("minJointAngles"):
            summary["minJointAngles"] = deepcopy(rep["minJointAngles"])
        if rep.get("maxJointAngles"):
            summary["maxJointAngles"] = deepcopy(rep["maxJointAngles"])
        if rep.get("rangeOfMotion"):
            summary["rangeOfMotion"] = deepcopy(rep["rangeOfMotion"])
        if rep.get("angle") is not None:
            summary["primaryAngle"] = rep.get("angle")
        return summary

    def _extract_feedback_labels(self, rep: Dict[str, Any]) -> List[Any]:
        return self._normalize_list(rep.get("feedbackLabels") or rep.get("feedback_labels") or rep.get("feedback"))

    def _extract_mistake_flags(self, rep: Dict[str, Any]) -> List[Any]:
        return self._normalize_list(rep.get("mistakeFlags") or rep.get("mistake_flags") or rep.get("issues") or rep.get("issues_detected"))

    def _extract_phase(self, rep: Dict[str, Any]) -> Optional[str]:
        phase_timing = rep.get("phaseTiming") or rep.get("phase_timing") or {}
        if isinstance(phase_timing, dict):
            phase = phase_timing.get("exercisePhase") or phase_timing.get("phase")
            if phase:
                return str(phase)
        return rep.get("exercisePhase") or rep.get("phase")

    def _extract_session_state(self) -> Dict[str, Any]:
        return dict(self.metadata.get("rlAdaptiveState") or self.metadata.get("adaptiveState") or {})

    def _compute_recent_average_score(self, reps: List[Dict[str, Any]], sample_size: int = 5) -> Optional[float]:
        scores = [score for score in (self._extract_score(rep) for rep in reps[-sample_size:]) if score is not None]
        if not scores:
            return None
        return float(mean(scores))

    def _compute_trend(self, reps: List[Dict[str, Any]]) -> Optional[str]:
        scores = [score for score in (self._extract_score(rep) for rep in reps) if score is not None]
        if len(scores) < 4:
            return None

        first_window = scores[: max(2, len(scores) // 3)]
        last_window = scores[-max(2, len(scores) // 3) :]
        first_avg = mean(first_window)
        last_avg = mean(last_window)
        delta = last_avg - first_avg

        if delta > 0.05:
            return "improving"
        if delta < -0.05:
            return "declining"
        return "stable"

    def _aggregate_main_issues(self, reps: List[Dict[str, Any]]) -> List[str]:
        counter: Counter[str] = Counter()
        for rep in reps:
            for issue in self._extract_mistake_flags(rep):
                if issue:
                    counter[str(issue)] += 1
        return [issue for issue, _ in counter.most_common(3)]

    def _compute_summary(self, summary_data: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        summary_data = deepcopy(summary_data or {})
        rep_scores = [score for score in (self._extract_score(rep) for rep in self.reps) if score is not None]

        rep_min_angles: List[float] = []
        rep_max_angles: List[float] = []
        for rep in self.reps:
            min_angles = rep.get("minJointAngles") or {}
            max_angles = rep.get("maxJointAngles") or {}
            if isinstance(min_angles, dict):
                rep_min_angles.extend(
                    number for number in (self._coerce_number(value) for value in min_angles.values()) if number is not None
                )
            if isinstance(max_angles, dict):
                rep_max_angles.extend(
                    number for number in (self._coerce_number(value) for value in max_angles.values()) if number is not None
                )

        summary = {
            "totalReps": len(self.reps),
            "averageScore": float(mean(rep_scores)) if rep_scores else summary_data.get("averageScore"),
            "bestRepScore": max(rep_scores) if rep_scores else summary_data.get("bestRepScore"),
            "worstRepScore": min(rep_scores) if rep_scores else summary_data.get("worstRepScore"),
            "averageMinAngle": float(mean(rep_min_angles)) if rep_min_angles else summary_data.get("averageMinAngle"),
            "averageMaxAngle": float(mean(rep_max_angles)) if rep_max_angles else summary_data.get("averageMaxAngle"),
            "mainIssues": summary_data.get("mainIssues") or self._aggregate_main_issues(self.reps),
            "improvementTrend": summary_data.get("improvementTrend") or self._compute_trend(self.reps),
            "latestQualityScore": self._extract_score(self.reps[-1]) if self.reps else summary_data.get("latestQualityScore"),
            "lastRepNumber": self.reps[-1].get("repNumber") if self.reps else summary_data.get("lastRepNumber"),
            "lastKnownExercisePhase": self._extract_phase(self.reps[-1]) if self.reps else summary_data.get("lastKnownExercisePhase"),
        }
        summary.update({key: value for key, value in summary_data.items() if value is not None})
        return summary

    def _build_progress_snapshot(self) -> Dict[str, Any]:
        latest_rep = self.reps[-1] if self.reps else {}
        recent_scores = [score for score in (self._extract_score(rep) for rep in self.reps[-5:]) if score is not None]
        return {
            "exerciseType": self.exercise_type,
            "totalRepsCompleted": len(self.reps),
            "currentSetNumber": self.metadata.get("currentSetNumber"),
            "targetReps": self.metadata.get("targetReps"),
            "pushTarget": latest_rep.get("pushTarget") if latest_rep else self.metadata.get("pushTarget"),
            "minimumThreshold": latest_rep.get("minimumThreshold") if latest_rep else self.metadata.get("minimumThreshold"),
            "userBaseline": latest_rep.get("baseline") if latest_rep else self.metadata.get("userBaseline"),
            "currentPhase": self._extract_phase(latest_rep) if latest_rep else self.metadata.get("currentPhase") or self.status,
            "latestJointAngleSummary": self._extract_joint_summary(latest_rep),
            "latestFeedbackLabels": self._extract_feedback_labels(latest_rep),
            "latestMistakeFlags": self._extract_mistake_flags(latest_rep),
            "recentAverageScore": float(mean(recent_scores)) if recent_scores else self.metadata.get("recentAverageScore"),
            "rlAdaptiveState": self.metadata.get("rlAdaptiveState") or self.metadata.get("adaptiveState"),
            "difficultyLevel": self.metadata.get("difficultyLevel") or self.metadata.get("currentExerciseParameters"),
            "lastRepNumber": latest_rep.get("repNumber") if latest_rep else self.current_session.get("lastRepNumber") if self.current_session else None,
            "lastKnownExercisePhase": self._extract_phase(latest_rep) if latest_rep else self.current_session.get("lastKnownExercisePhase") if self.current_session else None,
            "latestQualityScore": self._extract_score(latest_rep),
        }

    def _build_session_payload(
        self,
        session_id: str,
        exercise_type: str,
        status: str,
        metadata: Optional[Dict[str, Any]] = None,
        summary_data: Optional[Dict[str, Any]] = None,
        paused: bool = False,
    ) -> Dict[str, Any]:
        metadata = deepcopy(metadata or {})
        summary = self._compute_summary(summary_data if summary_data is not None else None)
        progress_snapshot = self._build_progress_snapshot()
        now = utc_now()
        start_time = self.started_at or now
        duration_seconds = max((now - start_time).total_seconds(), 0.0)
        end_time = summary_data.get("endTime") if summary_data else None

        payload = {
            "sessionId": session_id,
            "exerciseType": exercise_type,
            "startTime": start_time,
            "endTime": end_time,
            "durationSeconds": summary_data.get("durationSeconds") if summary_data else duration_seconds,
            "status": status,
            "canResume": paused or status in {"active", "paused"},
            "totalReps": len(self.reps),
            "averageScore": summary.get("averageScore"),
            "bestRepScore": summary.get("bestRepScore"),
            "worstRepScore": summary.get("worstRepScore"),
            "averageMinAngle": summary.get("averageMinAngle"),
            "averageMaxAngle": summary.get("averageMaxAngle"),
            "mainIssues": summary.get("mainIssues", []),
            "improvementTrend": summary.get("improvementTrend"),
            "modelVersion": summary_data.get("modelVersion") if summary_data else self.model_version,
            "appVersion": summary_data.get("appVersion") if summary_data else self.app_version,
            "createdAt": self.current_session.get("createdAt") if self.current_session else start_time,
            "updatedAt": now,
            "lastUpdatedAt": now,
            "lastRepNumber": summary.get("lastRepNumber") or len(self.reps),
            "lastKnownExercisePhase": summary.get("lastKnownExercisePhase"),
            "latestQualityScore": summary.get("latestQualityScore"),
            "progressSnapshot": summary_data.get("progressSnapshot") if summary_data and summary_data.get("progressSnapshot") is not None else progress_snapshot,
        }

        latest_rep = self.reps[-1] if self.reps else {}
        if latest_rep:
            payload["pushTarget"] = latest_rep.get("pushTarget")
            payload["minimumThreshold"] = latest_rep.get("minimumThreshold")
            payload["userBaseline"] = latest_rep.get("baseline")
            payload["currentPhase"] = latest_rep.get("phaseTiming", {}).get("exercisePhase") or latest_rep.get("exercisePhase") or summary.get("lastKnownExercisePhase") or self.status
        else:
            payload["pushTarget"] = metadata.get("pushTarget")
            payload["minimumThreshold"] = metadata.get("minimumThreshold")
            payload["userBaseline"] = metadata.get("userBaseline")
            payload["currentPhase"] = metadata.get("currentPhase") or summary.get("lastKnownExercisePhase") or self.status

        if metadata:
            payload.setdefault("metadata", {})
            payload["metadata"] = to_json_safe(metadata)

        if summary_data:
            payload.update({key: value for key, value in summary_data.items() if value is not None})

        return payload

    def _push_session_update(self, session_id: str, payload: Dict[str, Any], create: bool = False) -> None:
        if create:
            self.firebase_db.set_session_document(session_id, payload, merge=False)
        else:
            self.firebase_db.update_session_document(session_id, payload)

    def _sync_current_session(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        self.current_session = deepcopy(payload)
        self.current_session_id = payload.get("sessionId")
        self.exercise_type = payload.get("exerciseType")
        self.status = payload.get("status")
        self.metadata = deepcopy(payload.get("metadata") or self.metadata or {})
        self.started_at = payload.get("startTime") if isinstance(payload.get("startTime"), datetime) else self.started_at
        return payload

    def start_session(self, exercise_type: str, metadata: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        if self.default_resume_latest:
            latest = self.get_latest_resumable_session()
            if latest:
                return self.resume_session(latest["sessionId"], exercise_type=exercise_type, metadata=metadata)
        return self.start_new_session(exercise_type, metadata)

    def start_new_session(self, exercise_type: str, metadata: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        if self.current_session_id and self.status == "active":
            self.pause_session()

        now = utc_now()
        self.current_session_id = self._new_session_id()
        self.exercise_type = exercise_type
        self.reps = []
        self.feedback_events = []
        self.metadata = deepcopy(metadata or {})
        self.status = "active"
        self.started_at = now

        payload = self._build_session_payload(
            session_id=self.current_session_id,
            exercise_type=exercise_type,
            status="active",
            metadata=self.metadata,
        )
        self._sync_current_session(payload)
        self._push_session_update(self.current_session_id, payload, create=True)
        return deepcopy(payload)

    def resume_session(
        self,
        session_id: str,
        exercise_type: Optional[str] = None,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> Dict[str, Any]:
        if not session_id:
            return self.start_new_session(exercise_type or "unknown", metadata)

        if self.current_session_id and self.status == "active" and self.current_session_id != session_id:
            self.pause_session()

        existing = self.firebase_db.get_session_document(session_id) or {}
        loaded_reps = self.firebase_db.list_session_reps(session_id)
        now = utc_now()

        self.current_session_id = session_id
        self.exercise_type = exercise_type or existing.get("exerciseType") or "unknown"
        self.reps = deepcopy(loaded_reps)
        self.feedback_events = []
        merged_metadata = deepcopy(existing.get("metadata") or {})
        if metadata:
            merged_metadata.update(deepcopy(metadata))
        self.metadata = merged_metadata
        self.status = "active"
        self.started_at = existing.get("startTime") if isinstance(existing.get("startTime"), datetime) else now

        payload = self._build_session_payload(
            session_id=session_id,
            exercise_type=self.exercise_type,
            status="active",
            metadata=self.metadata,
        )
        payload["canResume"] = True
        payload["createdAt"] = existing.get("createdAt") or payload["createdAt"]
        if existing.get("progressSnapshot"):
            payload["progressSnapshot"] = existing.get("progressSnapshot")
        self._sync_current_session(payload)
        self._push_session_update(session_id, payload, create=not bool(existing))
        return deepcopy(payload)

    def log_rep(self, rep_data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        if not self.current_session_id:
            return None

        now = utc_now()
        rep = deepcopy(rep_data or {})
        rep_number = int(rep.get("repNumber") or (len(self.reps) + 1))
        rep["repNumber"] = rep_number
        rep.setdefault("timestamp", now)
        rep.setdefault("durationSeconds", rep.get("durationSeconds"))
        rep.setdefault("feedbackLabels", self._extract_feedback_labels(rep))
        rep.setdefault("mistakeFlags", self._extract_mistake_flags(rep))
        rep.setdefault("phaseTiming", rep.get("phaseTiming") or {})
        rep.setdefault("notes", rep.get("notes"))
        if "minJointAngles" not in rep and "maxJointAngles" not in rep:
            angle = rep.get("angle")
            exercise_type = (rep.get("exerciseType") or self.exercise_type or "").lower()
            if angle is not None:
                if "shoulder" in exercise_type:
                    rep["maxJointAngles"] = {"primary": angle}
                    rep["rangeOfMotion"] = {"primary": angle}
                else:
                    rep["minJointAngles"] = {"primary": angle}
                    rep["rangeOfMotion"] = {"primary": angle}

        self.reps.append(rep)
        self.status = self.status or "active"

        rep_document_id = rep.get("repId") or f"rep_{rep_number:04d}"
        self.firebase_db.set_subcollection_document(self.current_session_id, "reps", rep_document_id, rep, merge=False)

        session_update = self._build_session_payload(
            session_id=self.current_session_id,
            exercise_type=self.exercise_type or rep.get("exerciseType") or "unknown",
            status="active",
            metadata=self.metadata,
        )
        session_update["lastRepNumber"] = rep_number
        session_update["lastKnownExercisePhase"] = self._extract_phase(rep)
        session_update["latestQualityScore"] = self._extract_score(rep)
        session_update["pushTarget"] = rep.get("pushTarget")
        session_update["minimumThreshold"] = rep.get("minimumThreshold")
        session_update["userBaseline"] = rep.get("baseline")
        session_update["currentPhase"] = self._extract_phase(rep) or session_update.get("currentPhase")
        session_update["progressSnapshot"] = self._build_progress_snapshot()
        session_update["canResume"] = True
        session_update["status"] = "active"
        session_update["updatedAt"] = now
        session_update["lastUpdatedAt"] = now
        self._sync_current_session(session_update)
        self._push_session_update(self.current_session_id, session_update)
        return deepcopy(rep)

    def log_feedback_event(self, event_data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        if not self.current_session_id:
            return None

        now = utc_now()
        event = deepcopy(event_data or {})
        event.setdefault("timestamp", now)
        event.setdefault("severity", event.get("severity", "info"))
        event_id = event.get("eventId") or f"event_{len(self.feedback_events) + 1:04d}"
        event["eventId"] = event_id
        self.feedback_events.append(event)
        self.firebase_db.set_subcollection_document(self.current_session_id, "feedback_events", event_id, event, merge=False)

        snapshot = self._build_progress_snapshot()
        snapshot["latestFeedbackEvent"] = {
            "eventType": event.get("eventType"),
            "message": event.get("message"),
            "severity": event.get("severity"),
            "repNumber": event.get("repNumber"),
            "exercisePhase": event.get("exercisePhase"),
            "relatedMetric": event.get("relatedMetric"),
        }
        self.firebase_db.update_session_document(
            self.current_session_id,
            {
                "updatedAt": now,
                "lastUpdatedAt": now,
                "progressSnapshot": snapshot,
                "canResume": True,
            },
        )
        if self.current_session is not None:
            self.current_session["progressSnapshot"] = snapshot
            self.current_session["lastUpdatedAt"] = now
        return deepcopy(event)

    def pause_session(self, snapshot_data: Optional[Dict[str, Any]] = None) -> Optional[Dict[str, Any]]:
        if not self.current_session_id:
            return None

        now = utc_now()
        snapshot = deepcopy(snapshot_data) if snapshot_data is not None else self._build_progress_snapshot()
        payload = {
            "status": "paused",
            "canResume": True,
            "lastUpdatedAt": now,
            "updatedAt": now,
            "lastRepNumber": len(self.reps),
            "lastKnownExercisePhase": snapshot.get("lastKnownExercisePhase"),
            "latestQualityScore": snapshot.get("latestQualityScore"),
            "pushTarget": snapshot.get("pushTarget"),
            "minimumThreshold": snapshot.get("minimumThreshold"),
            "userBaseline": snapshot.get("userBaseline"),
            "currentPhase": snapshot.get("currentPhase") or self.status,
            "progressSnapshot": snapshot,
        }
        payload.update(self._compute_summary())
        self.status = "paused"
        if self.current_session is not None:
            self.current_session.update(payload)
        self.firebase_db.update_session_document(self.current_session_id, payload)
        return deepcopy(payload)

    def end_session(self, summary_data: Optional[Dict[str, Any]] = None) -> Optional[Dict[str, Any]]:
        if not self.current_session_id:
            return None

        now = utc_now()
        summary_data = deepcopy(summary_data or {})
        summary = self._compute_summary(summary_data)
        payload = {
            "status": "completed",
            "canResume": False,
            "endTime": summary_data.get("endTime") or now,
            "durationSeconds": summary_data.get("durationSeconds") or max((now - (self.started_at or now)).total_seconds(), 0.0),
            "lastUpdatedAt": now,
            "updatedAt": now,
            "lastRepNumber": len(self.reps),
            "lastKnownExercisePhase": summary.get("lastKnownExercisePhase"),
            "latestQualityScore": summary.get("latestQualityScore"),
            "pushTarget": self.reps[-1].get("pushTarget") if self.reps else None,
            "minimumThreshold": self.reps[-1].get("minimumThreshold") if self.reps else None,
            "userBaseline": self.reps[-1].get("baseline") if self.reps else None,
            "currentPhase": self.status,
            "progressSnapshot": summary_data.get("progressSnapshot") or self._build_progress_snapshot(),
        }
        payload.update(summary)
        payload.update({key: value for key, value in summary_data.items() if value is not None})
        self.status = "completed"
        if self.current_session is not None:
            self.current_session.update(payload)
        self.firebase_db.update_session_document(self.current_session_id, payload)
        return deepcopy(payload)

    def fail_session(self, error_message: str) -> Optional[Dict[str, Any]]:
        if not self.current_session_id:
            return None

        now = utc_now()
        self.last_error = error_message
        payload = {
            "status": "failed",
            "canResume": False,
            "errorMessage": error_message,
            "endTime": now,
            "lastUpdatedAt": now,
            "updatedAt": now,
            "lastRepNumber": len(self.reps),
            "progressSnapshot": self._build_progress_snapshot(),
        }
        payload.update(self._compute_summary())
        self.status = "failed"
        if self.current_session is not None:
            self.current_session.update(payload)
        self.firebase_db.update_session_document(self.current_session_id, payload)
        return deepcopy(payload)

    def get_latest_resumable_session(self) -> Optional[Dict[str, Any]]:
        return self.firebase_db.get_latest_resumable_session()

    def get_current_session(self) -> Optional[Dict[str, Any]]:
        return deepcopy(self.current_session) if self.current_session else None

    def get_current_reps(self) -> List[Dict[str, Any]]:
        return deepcopy(self.reps)

    def reset_local_state(self) -> None:
        self.current_session = None
        self.current_session_id = None
        self.exercise_type = None
        self.reps = []
        self.feedback_events = []
        self.metadata = {}
        self.status = None
        self.started_at = None
        self.last_error = None
