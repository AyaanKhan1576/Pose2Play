// UI Manager Module
export class UIManager {
    constructor() {
        this.elements = {
            status: document.getElementById('status'),
            feedback: document.getElementById('feedback'),
            repCount: document.getElementById('repCount'),
            currentAngle: document.getElementById('currentAngle'),
            targetAngle: document.getElementById('targetAngle'),
            minimumThreshold: document.getElementById('minimumThreshold'),
            formQuality: document.getElementById('formQuality'),
            formROM: document.getElementById('formROM'),
            formConsistency: document.getElementById('formConsistency'),
            formStatus: document.getElementById('formStatus'),
            gamificationPanel: document.getElementById('gamificationPanel'),
            totalRP: document.getElementById('totalRP'),
            sessionRP: document.getElementById('sessionRP'),
            currentLevel: document.getElementById('currentLevel'),
            streakDays: document.getElementById('streakDays'),
            levelName: document.getElementById('levelName'),
            levelProgress: document.getElementById('levelProgress'),
            achievementList: document.getElementById('achievementList')
        };
    }

    updateStatus(type, message) {
        this.elements.status.className = 'status ' + type;
        this.elements.status.textContent = message;
    }

    updateFeedback(message) {
        this.elements.feedback.textContent = message;
    }

    updateRepCount(count) {
        this.elements.repCount.textContent = count;
    }

    updateCurrentAngle(angle) {
        this.elements.currentAngle.textContent = Math.round(angle) + '°';
    }

    updateTargetAngle(angle, adjustment = null) {
        if (angle === null) {
            this.elements.targetAngle.textContent = '--';
        } else {
            this.elements.targetAngle.textContent = Math.round(angle) + '°';
        }
        
        // Update target status if adjustment info provided
        const targetStatus = document.getElementById('targetStatus');
        if (targetStatus && adjustment !== null) {
            if (adjustment > 0) {
                targetStatus.textContent = '↓ Easier';
                targetStatus.style.color = '#48bb78'; // Green
            } else if (adjustment < 0) {
                targetStatus.textContent = '↑ Harder';
                targetStatus.style.color = '#f5576c'; // Red
            } else {
                targetStatus.textContent = '= Stable';
                targetStatus.style.color = 'rgba(255,255,255,0.9)';
            }
        } else if (targetStatus) {
            targetStatus.textContent = 'Challenge';
            targetStatus.style.color = 'rgba(255,255,255,0.9)';
        }
    }
    
    updateMinimumThreshold(threshold) {
        if (!this.elements.minimumThreshold) return;
        
        if (threshold === null) {
            this.elements.minimumThreshold.textContent = '--';
        } else {
            this.elements.minimumThreshold.textContent = Math.round(threshold) + '°';
        }
    }

    updateFormAnalysis(sessionAngles, sessionReps, currentExercise, formResult = null) {
        // Update from form API result
        if (formResult) {
            const qualityPercent = Math.round(parseFloat(formResult.form_quality.replace('%', '')));
            this.elements.formQuality.textContent = formResult.form_quality;
            this.elements.formQuality.className = 'form-metric-value ' + 
                (qualityPercent >= 85 ? 'good' : qualityPercent >= 70 ? 'warning' : 'error');
            
            this.elements.formStatus.textContent = formResult.is_correct ? 'Correct' : 'Needs Work';
            this.elements.formStatus.className = 'form-metric-value ' + (formResult.is_correct ? 'good' : 'warning');
        }

        // Update ROM
        if (sessionAngles.length > 5) {
            const recentAngles = sessionAngles.slice(-30);
            const maxROM = currentExercise === 'shoulder' ? Math.max(...recentAngles) : Math.min(...recentAngles);
            this.elements.formROM.textContent = Math.round(maxROM) + '°';
            this.elements.formROM.className = 'form-metric-value good';
        }

        // Update Consistency
        if (sessionReps.length >= 3) {
            const last3 = sessionReps.slice(-3).map(r => r.angle);
            const avg = last3.reduce((a, b) => a + b, 0) / last3.length;
            const variance = last3.reduce((sum, angle) => sum + Math.pow(angle - avg, 2), 0) / last3.length;
            const consistency = Math.max(0, 100 - Math.sqrt(variance) * 3);
            this.elements.formConsistency.textContent = Math.round(consistency) + '%';
            this.elements.formConsistency.className = 'form-metric-value ' + 
                (consistency >= 80 ? 'good' : consistency >= 60 ? 'warning' : 'error');
        }
    }

    resetExerciseDisplay() {
        this.elements.repCount.textContent = '0';
        this.elements.currentAngle.textContent = '--';
        this.elements.targetAngle.textContent = '--';
        this.updateFeedback('Exercise reset');
    }

    toggleGamificationPanel() {
        return this.elements.gamificationPanel.classList.toggle('active');
    }

    updateGamificationPanel(userRewards, repCount, sessionReps, personalTarget, userBaseline) {
        // Calculate session rewards
        const baseRP = repCount * 10;
        const qualityBonus = sessionReps.length > 0 ? Math.round(sessionReps.length * 5 * 0.8) : 0;
        const streakBonus = 50;
        const sessionEarned = baseRP + qualityBonus + streakBonus;
        
        userRewards.sessionRP = sessionEarned;
        userRewards.totalRP += sessionEarned;
        
        const newLevel = Math.floor(userRewards.totalRP / 100) + 1;
        userRewards.currentLevel = newLevel;
        
        const rpInCurrentLevel = userRewards.totalRP % 100;
        const progressPercent = rpInCurrentLevel;
        
        const levelNames = ['Beginner', 'Novice', 'Intermediate', 'Advanced', 'Expert', 'Master', 'Legend'];
        const levelIndex = Math.min(Math.floor(newLevel / 2), levelNames.length - 1);
        const levelName = levelNames[levelIndex];
        
        userRewards.streakDays = Math.min(userRewards.streakDays + 1, 30);
        
        // Update UI
        this.elements.totalRP.textContent = userRewards.totalRP;
        this.elements.sessionRP.textContent = sessionEarned;
        this.elements.currentLevel.textContent = newLevel;
        this.elements.streakDays.textContent = String(userRewards.streakDays);
        this.elements.levelName.textContent = levelName;
        this.elements.levelProgress.style.width = progressPercent + '%';
        
        // Generate achievements
        this.elements.achievementList.innerHTML = '';
        const achievements = [];
        
        if (repCount >= 10) achievements.push('10 Reps Champion');
        if (repCount >= 20) achievements.push('20 Rep Warrior');
        if (userRewards.streakDays >= 3) achievements.push('3-Day Streak');
        if (userRewards.streakDays >= 7) achievements.push('Week Warrior');
        if (newLevel >= 3) achievements.push('Level 3 Reached');
        if (newLevel >= 5) achievements.push('Level 5 Master');
        if (sessionEarned >= 200) achievements.push('200+ RP Session');
        if (personalTarget && userBaseline && Math.abs(personalTarget - userBaseline) >= 10) {
            achievements.push('10 Degree Improvement');
        }
        
        if (achievements.length === 0) {
            achievements.push('Keep exercising to unlock achievements.');
        }
        
        achievements.forEach(achievement => {
            const item = document.createElement('div');
            item.className = 'achievement-item';
            item.textContent = achievement;
            this.elements.achievementList.appendChild(item);
        });
        
        this.updateFeedback(`Earned ${sessionEarned} RP. Level ${newLevel} - ${levelName}`);
        console.log(`Gamification: ${sessionEarned} RP earned, Level ${newLevel}, ${achievements.length} achievements`);
    }
}
