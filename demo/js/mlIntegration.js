// ML Integration Module - LSTM and RL
export class MLIntegration {
    constructor() {
        this.lstmEnabled = true;
        this.lastFormCheck = 0;
        this.userId = 'user_' + Date.now();
        this.sessionStartTime = null;
        this.lstmAdjustmentCount = 0;
        this.rlAdjustmentCount = 0;
    }
    
    setSessionStartTime(timestamp) {
        this.sessionStartTime = timestamp;
    }

    // LSTM Quality Prediction (Micro-adjustments ±2°)
    async predictLSTMQuality(angleSequence, repData) {
        try {
            const response = await fetch('/predict_quality', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    user_id: this.userId,
                    angles: angleSequence
                })
            });
            
            if (!response.ok) {
                console.error('LSTM API error:', response.status);
                return null;
            }
            
            const result = await response.json();
            
            // Log results
            console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
            console.log('LSTM Quality Analysis');
            console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
            console.log(`Quality Score: ${(result.quality_score * 100).toFixed(1)}%`);
            console.log(`Rep ROM: ${result.rep_rom.toFixed(1)}°`);
            console.log(`Target ROM: ${result.personalized_target_angle.toFixed(1)}°`);
            console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
            
            // COOPERATIVE ADJUSTMENT: LSTM makes micro-adjustments (±2°)
            const currentTarget = repData.targetAngle;
            let newTarget = currentTarget;
            
            if (result.personalized_target_angle) {
                const lstmSuggestion = Math.round(result.personalized_target_angle);
                const adjustment = lstmSuggestion - currentTarget;
                
                if (Math.abs(adjustment) <= 2) {
                    console.log(`LSTM micro-adjustment: ${currentTarget}° → ${lstmSuggestion}° (quality: ${(result.quality_score * 100).toFixed(0)}%)`);
                    newTarget = lstmSuggestion;
                    this.lstmAdjustmentCount++;
                } else {
                    console.log(`LSTM suggests ${lstmSuggestion}° (deferred to RL for large changes)`);
                }
            }
            
            return { 
                newTarget, 
                quality_score: result.quality_score,
                rep_rom: result.rep_rom,
                personalized_target_angle: result.personalized_target_angle
            };
        } catch (error) {
            console.error('LSTM prediction failed:', error);
            return null;
        }
    }

    // RL Adjustment (Macro-adjustments ±5° every 5 reps)
    async applyRLAdjustment(repCount, userHistory, currentTarget) {
        if (repCount % 5 !== 0) return { newTarget: currentTarget };
        
        // Pad userHistory to have at least 10 entries
        const paddedHistory = [...userHistory];
        while (paddedHistory.length < 10) {
            paddedHistory.unshift({ angle: currentTarget, quality: 'neutral' });
        }
        
        // Build state from user history (20 dimensions)
        const last10Angles = paddedHistory.slice(-10).map(r => r.angle || currentTarget);
        const consistency = this.getConsistency(paddedHistory);
        const fatigue = this.getFatigue(paddedHistory);
        const sessionTimeNorm = this.sessionStartTime ? Math.min((Date.now() - this.sessionStartTime) / 60000, 1) : 0;
        const baseline = paddedHistory[0]?.angle || currentTarget;
        const repsNorm = Math.min(repCount, 99) / 100;
        const successRate = this.getSuccessRate(paddedHistory);
        
        const state = [
            ...last10Angles,
            consistency,
            fatigue,
            sessionTimeNorm,
            currentTarget,
            baseline,
            repsNorm,
            successRate,
            0, 0, 0
        ];
        
        try {
            const response = await fetch('/predict', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ state })
            });
            
            if (!response.ok) {
                console.error('RL API error:', response.status);
                return { 
                    newTarget: currentTarget,
                    consistency,
                    fatigue,
                    successRate,
                    actionName: 'API Error',
                    action: -1
                };
            }
            
            const result = await response.json();
            const action = result.action;
            
            let newTarget = currentTarget;
            let actionName = '';
            
            switch(action) {
                case 0:
                    newTarget = currentTarget + 5;
                    actionName = 'Decrease difficulty';
                    break;
                case 1:
                    actionName = 'Maintain level';
                    break;
                case 2:
                    newTarget = currentTarget - 5;
                    actionName = 'Increase difficulty';
                    break;
                case 3:
                    actionName = 'Rest recommended';
                    break;
                case 4:
                    actionName = 'Encouragement';
                    break;
            }
            
            if (newTarget !== currentTarget) {
                this.rlAdjustmentCount++;
            }
            
            console.log(`RL Action: ${actionName} | Target: ${currentTarget}° → ${newTarget}°`);
            
            return {
                newTarget,
                consistency,
                fatigue,
                successRate,
                actionName,
                action
            };
        } catch (error) {
            console.error('RL adjustment failed:', error);
            return { 
                newTarget: currentTarget,
                consistency,
                fatigue,
                successRate,
                actionName: 'Error',
                action: -1
            };
        }
    }

    // Form Analysis with gradual quality calculation
    async analyzeForm(currentAngle, currentExercise, sessionReps) {
        const now = Date.now();
        if (now - this.lastFormCheck < 2000) return;
        this.lastFormCheck = now;

        try {
            const angles = {};
            if (currentExercise === 'squat') {
                angles.knee_left = currentAngle;
                angles.knee_right = currentAngle;
            } else if (currentExercise === 'hip') {
                angles.hip_left = currentAngle;
            } else if (currentExercise === 'shoulder') {
                angles.shoulder_left = currentAngle;
            }

            const movementSpeed = sessionReps.length > 1 
                ? (sessionReps[sessionReps.length - 1].timestamp - sessionReps[sessionReps.length - 2].timestamp) / 1000
                : 3.0;

            const response = await fetch('/predict_form_simple', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    angles: angles,
                    movement_speed: movementSpeed,
                    exercise_type: currentExercise === 'hip' ? 'hip_abduction_left' : currentExercise
                })
            });

            if (!response.ok) {
                console.log('[Form API] Not available (using offline mode)');
                // Calculate offline quality
                return this.calculateOfflineFormQuality(currentAngle, currentExercise, sessionReps);
            }

            const result = await response.json();
            
            // Enhance form quality with gradual calculation
            if (sessionReps.length > 0) {
                result.form_quality = this.calculateGradualFormQuality(sessionReps, currentExercise, result);
            }
            
            console.log('[Form Analysis]', result);
            return result;
        } catch (error) {
            console.log('[Form API] Error:', error.message);
            return this.calculateOfflineFormQuality(currentAngle, currentExercise, sessionReps);
        }
    }
    
    // Calculate form quality gradually (not just binary)
    calculateGradualFormQuality(sessionReps, currentExercise, apiResult = null) {
        if (sessionReps.length === 0) return '50%';
        
        const recent5 = sessionReps.slice(-5);
        
        // Component 1: ROM Achievement (40% weight)
        let romScore = 0;
        recent5.forEach(rep => {
            if (rep.reachedPushTarget) {
                romScore += 10; // Reached push target
            } else if (rep.angle) {
                // Partial credit based on how close to push target
                const target = rep.pushTarget || rep.targetAngle;
                const threshold = rep.minimumThreshold || (target * 0.7);
                const range = Math.abs(target - threshold);
                const achieved = Math.abs(rep.angle - threshold);
                const percent = Math.min(1, achieved / range);
                romScore += 5 + (percent * 5); // 5-10 points
            }
        });
        romScore = (romScore / recent5.length) * 4; // Scale to 40 points
        
        // Component 2: Consistency (30% weight)
        const angles = recent5.map(r => r.angle || 0);
        const avgAngle = angles.reduce((a, b) => a + b, 0) / angles.length;
        const variance = angles.reduce((sum, angle) => sum + Math.pow(angle - avgAngle, 2), 0) / angles.length;
        const stdDev = Math.sqrt(variance);
        const consistencyScore = Math.max(0, 30 - (stdDev * 2)); // Max 30 points
        
        // Component 3: Movement Speed (30% weight)
        let speedScore = 30;
        if (recent5.length > 1) {
            const durations = [];
            for (let i = 1; i < recent5.length; i++) {
                durations.push((recent5[i].timestamp - recent5[i-1].timestamp) / 1000);
            }
            const avgDuration = durations.reduce((a, b) => a + b, 0) / durations.length;
            // Ideal: 2-4 seconds per rep
            if (avgDuration < 1.5) speedScore = 15; // Too fast
            else if (avgDuration > 6) speedScore = 20; // Too slow
            else if (avgDuration >= 2 && avgDuration <= 4) speedScore = 30; // Perfect
            else speedScore = 25; // Acceptable
        }
        
        // Total quality (0-100)
        let totalQuality = Math.round(romScore + consistencyScore + speedScore);
        
        // If API provided result, blend with our calculation (50/50)
        if (apiResult && apiResult.form_quality) {
            const apiQuality = parseFloat(apiResult.form_quality.replace('%', ''));
            totalQuality = Math.round((totalQuality + apiQuality) / 2);
        }
        
        return `${totalQuality}%`;
    }
    
    // Offline form quality calculation when API unavailable
    calculateOfflineFormQuality(currentAngle, currentExercise, sessionReps) {
        const quality = this.calculateGradualFormQuality(sessionReps, currentExercise);
        return {
            form_quality: quality,
            is_correct: parseFloat(quality.replace('%', '')) >= 70,
            feedback: ['Offline mode - form calculated locally'],
            corrections: []
        };
    }

    // Helper functions
    getConsistency(history) {
        if (history.length < 3) return 0.5;
        const angles = history.slice(-5).map(r => r.angle || 0);
        const avg = angles.reduce((a, b) => a + b, 0) / angles.length;
        const variance = angles.reduce((sum, angle) => sum + Math.pow(angle - avg, 2), 0) / angles.length;
        const stdDev = Math.sqrt(variance);
        return Math.max(0, Math.min(1, 1 - (stdDev / 30)));
    }

    getFatigue(history) {
        if (history.length < 5) return 0;
        const halfPoint = Math.floor(history.length / 2);
        const recentHalf = history.slice(halfPoint).map(r => r.angle || 0);
        const earlierHalf = history.slice(0, halfPoint).map(r => r.angle || 0);
        const recentAvg = recentHalf.reduce((a, b) => a + b, 0) / recentHalf.length;
        const earlierAvg = earlierHalf.reduce((a, b) => a + b, 0) / earlierHalf.length;
        const deterioration = Math.abs(earlierAvg - recentAvg) / Math.max(earlierAvg, recentAvg, 1);
        return Math.max(0, Math.min(1, deterioration));
    }

    getSuccessRate(history) {
        if (history.length === 0) return 0.5;
        const successful = history.filter(r => r.quality === 'good' || r.quality === 'perfect').length;
        return successful / history.length;
    }
    
    // Comprehensive Target Adjustment Algorithm
    // Considers all 7 factors: ROM, Fatigue, Consistency, Trend, Duration, Pain, Session Gaps
    calculateComprehensiveTarget(currentTarget, baseline, userHistory, currentExercise) {
        if (!userHistory || userHistory.length < 3) {
            return { 
                newTarget: currentTarget, 
                factors: { rom: 0, fatigue: 0, consistency: 0, trend: 0, duration: 0 },
                adjustment: 0,
                reason: 'Insufficient data'
            };
        }
        
        const isShoulderExercise = currentExercise === 'shoulder';
        const recentReps = userHistory.slice(-10);
        const last3Reps = userHistory.slice(-3);
        const first3Reps = userHistory.slice(0, 3);
        
        // Factor 1: ROM Assessment (current capability vs baseline)
        const recentAvgAngle = recentReps.reduce((sum, r) => sum + (r.angle || 0), 0) / recentReps.length;
        let romFactor = 0;
        if (isShoulderExercise) {
            // For shoulder: higher angle = better ROM
            // If baseline=80° and recent=70°, ROM declined → ease target (LOWER the angle requirement)
            const romDecline = baseline - recentAvgAngle;
            if (romDecline > 10) romFactor = 3; // Significant decline, ease up (LOWER target angle)
            else if (romDecline > 5) romFactor = 2;
            else if (romDecline < -10) romFactor = -2; // Improvement, can push (RAISE target angle)
            else if (romDecline < -5) romFactor = -1;
        } else {
            // For squat/hip: lower angle = better ROM
            const romDecline = recentAvgAngle - baseline;
            if (romDecline > 10) romFactor = 3; // Significant decline, ease up
            else if (romDecline > 5) romFactor = 2;
            else if (romDecline < -10) romFactor = -2; // Improvement, can push
            else if (romDecline < -5) romFactor = -1;
        }
        
        // Factor 2: Fatigue (within-session degradation)
        let fatigueFactor = 0;
        if (userHistory.length >= 6) {
            const first3Avg = first3Reps.reduce((sum, r) => sum + (r.angle || 0), 0) / 3;
            const last3Avg = last3Reps.reduce((sum, r) => sum + (r.angle || 0), 0) / 3;
            
            let performanceDrop;
            if (isShoulderExercise) {
                performanceDrop = (first3Avg - last3Avg) / first3Avg; // Positive = fatigue
            } else {
                performanceDrop = (last3Avg - first3Avg) / first3Avg; // Positive = fatigue
            }
            
            if (performanceDrop > 0.15) fatigueFactor = 4; // 15%+ drop, significant fatigue
            else if (performanceDrop > 0.10) fatigueFactor = 3; // 10%+ drop
            else if (performanceDrop > 0.05) fatigueFactor = 1; // 5%+ drop
        }
        
        // Factor 3: Performance Consistency
        const consistency = this.getConsistency(userHistory);
        let consistencyFactor = 0;
        if (consistency < 0.5) consistencyFactor = 2; // Very inconsistent, ease up
        else if (consistency < 0.7) consistencyFactor = 1; // Somewhat inconsistent
        else if (consistency > 0.85) consistencyFactor = -1; // Very consistent, can push
        
        // Factor 4: Recent Trend (last 5 vs previous 5)
        let trendFactor = 0;
        if (userHistory.length >= 10) {
            const recent5 = userHistory.slice(-5);
            const previous5 = userHistory.slice(-10, -5);
            const recent5Avg = recent5.reduce((sum, r) => sum + (r.angle || 0), 0) / 5;
            const previous5Avg = previous5.reduce((sum, r) => sum + (r.angle || 0), 0) / 5;
            
            let trendImprovement;
            if (isShoulderExercise) {
                trendImprovement = recent5Avg - previous5Avg; // Positive = better
            } else {
                trendImprovement = previous5Avg - recent5Avg; // Positive = better
            }
            
            if (trendImprovement < -5) trendFactor = 2; // Declining trend
            else if (trendImprovement > 5) trendFactor = -1; // Improving trend
        }
        
        // Factor 5: Exercise Duration (time in session)
        let durationFactor = 0;
        if (this.sessionStartTime) {
            const sessionMinutes = (Date.now() - this.sessionStartTime) / 60000;
            if (sessionMinutes > 15) durationFactor = 2; // Long session, likely tired
            else if (sessionMinutes > 10) durationFactor = 1;
        }
        
        // Calculate total adjustment
        const totalAdjustment = romFactor + fatigueFactor + consistencyFactor + trendFactor + durationFactor;
        
        // Apply adjustment with bounds
        let adjustment = 0;
        if (totalAdjustment >= 5) adjustment = 5; // Ease up significantly
        else if (totalAdjustment >= 3) adjustment = 3; // Ease up moderately
        else if (totalAdjustment >= 1) adjustment = 1; // Ease up slightly
        else if (totalAdjustment <= -4) adjustment = -3; // Push harder
        else if (totalAdjustment <= -2) adjustment = -2; // Push moderately
        else if (totalAdjustment <= -1) adjustment = -1; // Push slightly
        
        // Apply adjustment direction based on exercise type
        // For shoulder: positive adjustment = ease up = LOWER angle
        // For squat/hip: positive adjustment = ease up = RAISE angle
        let newTarget;
        if (isShoulderExercise) {
            newTarget = currentTarget - adjustment; // Subtract for shoulder (easier = lower angle)
        } else {
            newTarget = currentTarget + adjustment; // Add for squat/hip (easier = higher angle)
        }
        
        // Safety bounds
        const minTarget = isShoulderExercise ? baseline - 10 : baseline + 10;
        const maxTarget = isShoulderExercise ? baseline + 30 : baseline - 30;
        const boundedTarget = Math.round(Math.max(minTarget, Math.min(maxTarget, newTarget)));
        
        // Also adjust minimum threshold based on performance
        // If struggling (adjustment > 0), lower the minimum too
        let newMinimum = null;
        if (adjustment !== 0) {
            // Minimum threshold moves proportionally (50% of push target adjustment)
            const minimumAdjustment = Math.round(adjustment * 0.5);
            if (isShoulderExercise) {
                // For shoulder: lower angle = easier
                newMinimum = Math.round(currentTarget * 0.7) - minimumAdjustment;
            } else {
                // For squat/hip: higher angle = easier
                newMinimum = Math.round(currentTarget * 1.3) + minimumAdjustment;
            }
        }
        
        // Determine reason
        let reason = '';
        if (adjustment > 0) {
            const reasons = [];
            if (romFactor > 0) reasons.push('ROM decline');
            if (fatigueFactor > 0) reasons.push('fatigue detected');
            if (consistencyFactor > 0) reasons.push('inconsistent performance');
            if (trendFactor > 0) reasons.push('declining trend');
            if (durationFactor > 0) reasons.push('long session');
            reason = `Easing difficulty: ${reasons.join(', ')}`;
        } else if (adjustment < 0) {
            const reasons = [];
            if (romFactor < 0) reasons.push('ROM improved');
            if (consistencyFactor < 0) reasons.push('consistent performance');
            if (trendFactor < 0) reasons.push('improving trend');
            reason = `Increasing challenge: ${reasons.join(', ')}`;
        } else {
            reason = 'Maintaining current level';
        }
        
        return {
            newTarget: boundedTarget,
            newMinimum: newMinimum,
            factors: {
                rom: romFactor,
                fatigue: fatigueFactor,
                consistency: consistencyFactor,
                trend: trendFactor,
                duration: durationFactor,
                total: totalAdjustment
            },
            adjustment,
            reason
        };
    }

    getSuccessRate(history) {
        if (history.length === 0) return 0.5;
        const successful = history.filter(r => r.quality === 'good' || r.quality === 'perfect').length;
        return successful / history.length;
    }
    
    // Reset ML counters
    reset() {
        this.lstmAdjustmentCount = 0;
        this.rlAdjustmentCount = 0;
    }
}
