// Exercise Logic Module
export class ExerciseManager {
    constructor() {
        this.currentExercise = 'squat';
        this.repCount = 0;
        this.currentState = 'STANDING';
        this.sessionAngles = [];
        this.sessionReps = [];
        this.personalTarget = null; // Push target (challenge goal)
        this.minimumThreshold = null; // Minimum required for rep to count
        this.userBaseline = null;
        this.currentPhase = 'BASELINE';
        this.baselineReps = [];
        this.BASELINE_REP_COUNT = 3;
        this.currentRepAngleSequence = [];
        this.userId = 'user_' + Date.now();
    }

    checkSquat(kneeAngle) {
        this.sessionAngles.push(kneeAngle);
        
        // LSTM: Capture angle sequence during movement
        if (this.currentState !== 'STANDING') {
            const angleFeatures = new Array(18).fill(kneeAngle);
            this.currentRepAngleSequence.push(angleFeatures);
        }

        // BASELINE MODE
        if (this.currentPhase === 'BASELINE') {
            if (this.currentState === 'STANDING' && kneeAngle < 160) {
                this.currentState = 'DESCENDING';
                return { feedback: `Baseline ${this.baselineReps.length + 1}/${this.BASELINE_REP_COUNT}: Squat down! 💪` };
            } else if (this.currentState === 'DESCENDING' && kneeAngle > 160) {
                this.currentState = 'STANDING';
                const achievedAngle = Math.min(...this.sessionAngles.slice(-30));
                this.baselineReps.push(achievedAngle);
                return { baseline: true, angle: achievedAngle };
            }
            return null;
        }

        // TRAINING MODE
        if (this.currentState === 'STANDING' && kneeAngle < 160) {
            this.currentState = 'DESCENDING';
            return { feedback: 'Keep going down! 💪' };
        } 
        else if (this.currentState === 'DESCENDING' && kneeAngle <= this.minimumThreshold) {
            // Rep counts if minimum threshold reached
            this.currentState = 'THRESHOLD_MET';
            const achieved = Math.min(...this.sessionAngles.slice(-30));
            
            const reachedPushTarget = achieved <= this.personalTarget;
            
            const repData = {
                angle: achieved,
                timestamp: Date.now(),
                minimumThreshold: this.minimumThreshold,
                pushTarget: this.personalTarget,
                reachedPushTarget: reachedPushTarget,
                baseline: this.userBaseline
            };
            
            let feedbackMsg;
            let isPersonalBest = false;
            
            if (reachedPushTarget) {
                feedbackMsg = `🎯 PUSH TARGET! ${Math.round(achieved)}°`;
            } else {
                feedbackMsg = `✅ Rep counted! ${Math.round(achieved)}° (Push: ${Math.round(this.personalTarget)}°)`;
            }
            
            if (achieved < this.userBaseline) {
                this.userBaseline = achieved;
                feedbackMsg = `🏆 NEW PERSONAL BEST: ${Math.round(achieved)}°!`;
                isPersonalBest = true;
            }
            
            this.sessionReps.push(repData);
            this.repCount++;
            
            return { 
                repCompleted: true, 
                repData, 
                feedbackMsg, 
                isPersonalBest,
                angleSequence: this.currentRepAngleSequence.slice()
            };
        }
        else if ((this.currentState === 'THRESHOLD_MET' || this.currentState === 'DESCENDING') && kneeAngle > 160) {
            this.currentState = 'STANDING';
            this.currentRepAngleSequence = [];
        }
        
        return null;
    }

    checkHipExercise(hipAngle) {
        this.sessionAngles.push(hipAngle);
        
        // LSTM: Capture angle sequence during movement
        if (this.currentState !== 'STANDING') {
            const angleFeatures = new Array(18).fill(hipAngle);
            this.currentRepAngleSequence.push(angleFeatures);
        }

        // BASELINE MODE
        if (this.currentPhase === 'BASELINE') {
            if (this.currentState === 'STANDING' && hipAngle < 150) {
                this.currentState = 'LIFTING';
                return { feedback: `Baseline ${this.baselineReps.length + 1}/${this.BASELINE_REP_COUNT}: Lift your leg! 💪` };
            } else if (this.currentState === 'LIFTING' && hipAngle > 170) {
                this.currentState = 'STANDING';
                const achievedAngle = Math.min(...this.sessionAngles.slice(-30));
                this.baselineReps.push(achievedAngle);
                return { baseline: true, angle: achievedAngle };
            }
            return null;
        }

        // TRAINING MODE
        if (this.currentState === 'STANDING' && hipAngle < 150) {
            this.currentState = 'LIFTING';
            return { feedback: 'Keep lifting! 💪' };
        } 
        else if (this.currentState === 'LIFTING' && hipAngle <= this.minimumThreshold) {
            // Rep counts if minimum threshold reached
            this.currentState = 'THRESHOLD_MET';
            const achieved = Math.min(...this.sessionAngles.slice(-30));
            
            const reachedPushTarget = achieved <= this.personalTarget;
            
            const repData = {
                angle: achieved,
                timestamp: Date.now(),
                minimumThreshold: this.minimumThreshold,
                pushTarget: this.personalTarget,
                reachedPushTarget: reachedPushTarget,
                baseline: this.userBaseline
            };
            
            let feedbackMsg;
            let isPersonalBest = false;
            
            if (reachedPushTarget) {
                feedbackMsg = `🎯 PUSH TARGET! ${Math.round(achieved)}°`;
            } else {
                feedbackMsg = `✅ Rep counted! ${Math.round(achieved)}° (Push: ${Math.round(this.personalTarget)}°)`;
            }
            
            if (achieved < this.userBaseline) {
                this.userBaseline = achieved;
                feedbackMsg = `🏆 NEW PERSONAL BEST: ${Math.round(achieved)}°!`;
                isPersonalBest = true;
            }
            
            this.sessionReps.push(repData);
            this.repCount++;
            
            return { 
                repCompleted: true, 
                repData, 
                feedbackMsg, 
                isPersonalBest,
                angleSequence: this.currentRepAngleSequence.slice()
            };
        }
        else if ((this.currentState === 'THRESHOLD_MET' || this.currentState === 'LIFTING') && hipAngle > 170) {
            this.currentState = 'STANDING';
            this.currentRepAngleSequence = [];
        }
        
        return null;
    }

    checkShoulderExercise(shoulderAngle) {
        this.sessionAngles.push(shoulderAngle);
        
        // LSTM: Capture angle sequence during movement
        if (this.currentState !== 'STANDING') {
            const angleFeatures = new Array(18).fill(shoulderAngle);
            this.currentRepAngleSequence.push(angleFeatures);
        }

        // BASELINE MODE
        if (this.currentPhase === 'BASELINE') {
            if (this.currentState === 'STANDING' && shoulderAngle > 30) {
                this.currentState = 'RAISING';
                this.currentRepAngleSequence = [];
                return { feedback: `Baseline ${this.baselineReps.length + 1}/${this.BASELINE_REP_COUNT}: Keep raising! 💪` };
            } else if (this.currentState === 'RAISING' && shoulderAngle < 30) {
                this.currentState = 'STANDING';
                const achievedAngle = Math.max(...this.sessionAngles.slice(-30));
                this.baselineReps.push(achievedAngle);
                return { baseline: true, angle: achievedAngle };
            }
            return null;
        }

        // TRAINING MODE
        if (this.currentState === 'STANDING' && shoulderAngle > 30) {
            this.currentState = 'RAISING';
            this.currentRepAngleSequence = [];
            return { feedback: 'Keep raising! 💪' };
        } 
        else if (this.currentState === 'RAISING' && shoulderAngle >= this.minimumThreshold) {
            // Rep counts if minimum threshold reached
            this.currentState = 'THRESHOLD_MET';
            const achieved = Math.max(...this.sessionAngles.slice(-30));
            
            const reachedPushTarget = achieved >= this.personalTarget;
            
            const repData = {
                angle: achieved,
                timestamp: Date.now(),
                minimumThreshold: this.minimumThreshold,
                pushTarget: this.personalTarget,
                reachedPushTarget: reachedPushTarget,
                baseline: this.userBaseline
            };
            
            let feedbackMsg;
            let isPersonalBest = false;
            
            if (reachedPushTarget) {
                feedbackMsg = `🎯 PUSH TARGET! ${Math.round(achieved)}°`;
            } else {
                feedbackMsg = `✅ Rep counted! ${Math.round(achieved)}° (Push: ${Math.round(this.personalTarget)}°)`;
            }
            
            if (achieved > this.userBaseline) {
                this.userBaseline = achieved;
                feedbackMsg = `🏆 NEW PERSONAL BEST: ${Math.round(achieved)}°!`;
                isPersonalBest = true;
            }
            
            this.sessionReps.push(repData);
            this.repCount++;
            
            return { 
                repCompleted: true, 
                repData, 
                feedbackMsg, 
                isPersonalBest,
                angleSequence: this.currentRepAngleSequence.slice()
            };
        }
        else if ((this.currentState === 'THRESHOLD_MET' || this.currentState === 'RAISING') && shoulderAngle < 30) {
            this.currentState = 'STANDING';
            this.currentRepAngleSequence = [];
        }
        
        return null;
    }

    establishBaseline() {
        if (this.baselineReps.length < this.BASELINE_REP_COUNT) {
            const exerciseAction = this.currentExercise === 'shoulder' ? 'Raise your arm as high as you can!' : 
                                  this.currentExercise === 'squat' ? 'Squat as deep as you can!' :
                                  'Lift your leg as high as you can!';
            return { complete: false, message: `Baseline ${this.baselineReps.length}/${this.BASELINE_REP_COUNT}: ${exerciseAction} 💪` };
        }
        
        // Calculate baseline and set dual targets
        if (this.currentExercise === 'shoulder') {
            this.userBaseline = Math.max(...this.baselineReps);
            // Minimum threshold: 70% of baseline (achievable)
            this.minimumThreshold = Math.round(this.userBaseline * 0.7);
            // Push target: 10° above baseline (challenging)
            this.personalTarget = Math.round(this.userBaseline + 10);
        } else {
            // For squat/hip: lower angle = better ROM
            this.userBaseline = Math.min(...this.baselineReps);
            // Minimum threshold: 130% of baseline (easier - higher angle)
            this.minimumThreshold = Math.round(this.userBaseline * 1.3);
            // Push target: 10° below baseline (harder - lower angle)
            this.personalTarget = Math.round(this.userBaseline - 10);
        }
        
        this.currentPhase = 'TRAINING';
        
        return { 
            complete: true, 
            baseline: this.userBaseline,
            minimumThreshold: this.minimumThreshold,
            pushTarget: this.personalTarget,
            message: `✅ Baseline: ${Math.round(this.userBaseline)}° | Min: ${Math.round(this.minimumThreshold)}° | Push: ${Math.round(this.personalTarget)}°`
        };
    }

    reset() {
        this.repCount = 0;
        this.currentState = 'STANDING';
        this.sessionAngles = [];
        this.sessionReps = [];
        this.currentPhase = 'BASELINE';
        this.baselineReps = [];
        this.userBaseline = 0;
        this.currentRepAngleSequence = [];
    }

    changeExercise(exercise) {
        this.currentExercise = exercise;
        this.reset();
    }
    
    // Getter methods for external access
    getRepCount() {
        return this.repCount;
    }
    
    getSessionReps() {
        return this.sessionReps;
    }
    
    getSessionAngles() {
        return this.sessionAngles;
    }
    
    getPersonalTarget() {
        return this.personalTarget;
    }
    
    getUserBaseline() {
        return this.userBaseline;
    }
    
    getCurrentPhase() {
        return this.currentPhase;
    }

    getBaselineProgress() {
        return {
            count: this.baselineReps.length,
            required: this.BASELINE_REP_COUNT
        };
    }
    
    // Setter methods
    setPersonalTarget(target) {
        this.personalTarget = target;
    }
    
    getMinimumThreshold() {
        return this.minimumThreshold;
    }
    
    setMinimumThreshold(threshold) {
        this.minimumThreshold = threshold;
    }
    
    // Update both targets together (maintaining gap)
    updateTargets(newPushTarget) {
        if (this.currentExercise === 'shoulder') {
            this.personalTarget = newPushTarget;
            // Keep minimum at 70% of new target
            this.minimumThreshold = Math.round(newPushTarget * 0.7);
        } else {
            // For squat/hip: push target is the lower (harder) angle
            this.personalTarget = newPushTarget;
            // Minimum threshold: 130% of push target (higher/easier angle)
            this.minimumThreshold = Math.round(newPushTarget * 1.3);
        }
    }
}
