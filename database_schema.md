# Firestore Schema

Pose2Play stores compact rehabilitation progress data in Firestore. The Python backend is the only writer.

## Collections

### `sessions/{sessionId}`

Session document fields:

- `sessionId`: string
- `exerciseType`: string
- `startTime`: timestamp
- `endTime`: timestamp or null
- `durationSeconds`: number
- `status`: `active` | `paused` | `completed` | `failed`
- `canResume`: boolean
- `totalReps`: number
- `averageScore`: number or null
- `bestRepScore`: number or null
- `worstRepScore`: number or null
- `averageMinAngle`: number or null
- `averageMaxAngle`: number or null
- `mainIssues`: list of strings
- `improvementTrend`: optional string such as `improving`, `stable`, or `declining`
- `modelVersion`: string or null
- `appVersion`: string or null
- `createdAt`: timestamp
- `updatedAt`: timestamp
- `lastUpdatedAt`: timestamp
- `lastRepNumber`: number or null
- `lastKnownExercisePhase`: string or null
- `latestQualityScore`: number or null
- `progressSnapshot`: object
- `metadata`: object or null
- `errorMessage`: string or null

### `sessions/{sessionId}/reps/{repId}`

Rep document fields:

- `repNumber`: number
- `timestamp`: timestamp
- `durationSeconds`: number or null
- `minJointAngles`: object or null
- `maxJointAngles`: object or null
- `rangeOfMotion`: object or null
- `qualityScore`: number or null
- `phaseTiming`: object or null
- `feedbackLabels`: list
- `mistakeFlags`: list
- `trackingConfidenceAverage`: number or null
- `rlStateFeatures`: object or array or null
- `rlActionOrRecommendation`: string or object or null
- `rewardSignal`: number or null
- `notes`: string or null
- `exerciseType`: string or null
- `exercisePhase`: string or null

### `sessions/{sessionId}/feedback_events/{eventId}`

Optional feedback event document fields:

- `timestamp`: timestamp
- `repNumber`: number or null
- `eventType`: string
- `message`: string
- `severity`: string
- `exercisePhase`: string or null
- `relatedMetric`: string or object or null

## Resume Flow

- The backend looks for the latest session where `canResume == true` and `status` is `paused` or `active`.
- Resuming continues writing to the same `sessionId`.
- New sessions always get a fresh `sessionId`.
- Paused sessions keep `progressSnapshot` so the backend can continue from the last known rep.
