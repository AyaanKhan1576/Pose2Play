/**
 * LSTM Quality Prediction Client
 * 
 * Communicates with Flask API to get LSTM-based movement quality scores
 * for shoulder exercises.
 * 
 * Usage:
 *   import { predictQuality } from './utils/lstmClient.js';
 *   
 *   const result = await predictQuality(userId, angleSequence);
 *   console.log(`Quality: ${result.quality_score}`);
 *   console.log(`Target ROM: ${result.personalized_target_angle}°`);
 */

const API_BASE_URL = 'http://localhost:5000';

/**
 * Send angle sequence to LSTM model for quality prediction
 * 
 * @param {string} userId - Unique identifier for user
 * @param {Array<Array<number>>} angleSequence - 2D array of joint angles [T, F]
 *   Example: [
 *     [10.5, 15.2, ...],  // Frame 1
 *     [12.3, 16.8, ...],  // Frame 2
 *     ...
 *   ]
 * @returns {Promise<Object>} Result object with:
 *   - quality_score: Movement quality [0, 1]
 *   - rep_rom: Range of motion (degrees)
 *   - personalized_target_angle: Adaptive target for next rep
 */
export async function predictQuality(userId, angleSequence) {
    try {
        const response = await fetch(`${API_BASE_URL}/predict_quality`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                user_id: userId,
                angles: angleSequence
            })
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Failed to predict quality');
        }

        const result = await response.json();
        return result;

    } catch (error) {
        console.error('Error predicting quality:', error);
        
        // Return fallback values on error
        return {
            quality_score: 0.5,
            rep_rom: 0,
            personalized_target_angle: 90,
            error: error.message
        };
    }
}

/**
 * Check if LSTM API is available
 * 
 * @returns {Promise<boolean>} True if LSTM model is loaded
 */
export async function checkLSTMAvailability() {
    try {
        const response = await fetch(`${API_BASE_URL}/health`);
        const data = await response.json();
        return data.lstm_model_loaded === true;
    } catch (error) {
        console.error('Error checking LSTM availability:', error);
        return false;
    }
}

/**
 * Format quality score as percentage with color coding
 * 
 * @param {number} qualityScore - Quality score [0, 1]
 * @returns {string} HTML string with colored percentage
 */
export function formatQualityScore(qualityScore) {
    const percentage = (qualityScore * 100).toFixed(1);
    
    let color;
    if (qualityScore >= 0.8) {
        color = '#28a745'; // Green - Excellent
    } else if (qualityScore >= 0.6) {
        color = '#ffc107'; // Yellow - Good
    } else if (qualityScore >= 0.4) {
        color = '#fd7e14'; // Orange - Fair
    } else {
        color = '#dc3545'; // Red - Poor
    }
    
    return `<span style="color: ${color}; font-weight: bold;">${percentage}%</span>`;
}

/**
 * Generate feedback message based on quality score
 * 
 * @param {number} qualityScore - Quality score [0, 1]
 * @returns {string} Feedback message
 */
export function getQualityFeedback(qualityScore) {
    if (qualityScore >= 0.9) {
        return "Perfect form. Excellent execution.";
    } else if (qualityScore >= 0.8) {
        return "Great job. Form is looking good.";
    } else if (qualityScore >= 0.7) {
        return "Good effort. Minor improvements needed.";
    } else if (qualityScore >= 0.5) {
        return "Fair form. Focus on controlled movement.";
    } else {
        return "Form needs work. Slow down and focus on technique.";
    }
}

/**
 * Log prediction result to console with formatting
 * 
 * @param {Object} result - Result from predictQuality()
 */
export function logPrediction(result) {
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('LSTM Quality Prediction Results');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(`Quality Score: ${(result.quality_score * 100).toFixed(1)}%`);
    console.log(`Rep ROM: ${result.rep_rom.toFixed(1)}°`);
    console.log(`Target ROM: ${result.personalized_target_angle.toFixed(1)}°`);
    console.log(`Feedback: ${getQualityFeedback(result.quality_score)}`);
    
    if (result.error) {
        console.warn(`Error occurred: ${result.error}`);
    }
    
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
}

// Example usage (for testing)
if (typeof window !== 'undefined' && window.location.hostname === 'localhost') {
    window.lstmClient = {
        predictQuality,
        checkLSTMAvailability,
        formatQualityScore,
        getQualityFeedback,
        logPrediction
    };
}
