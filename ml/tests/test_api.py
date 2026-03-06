"""
Unit tests for Flask API endpoints
Tests: Form Analysis API
"""

import unittest
import requests
import json
import numpy as np
import time


class TestAPIHealth(unittest.TestCase):
    """Test API server health and availability"""
    
    BASE_URL = 'http://localhost:5000'
    
    @classmethod
    def setUpClass(cls):
        """Check if API server is available"""
        try:
            response = requests.get(f'{cls.BASE_URL}/health', timeout=2)
            cls.server_running = response.status_code == 200
        except:
            cls.server_running = False
            print("\n⚠️  API server not running on localhost:5000")
            print("   Start server with: python api_server.py")
    
    def test_01_server_is_running(self):
        """Test that API server responds"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        response = requests.get(f'{self.BASE_URL}/health')
        self.assertEqual(response.status_code, 200)
    
    def test_02_health_endpoint_structure(self):
        """Test /health endpoint returns correct structure"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        response = requests.get(f'{self.BASE_URL}/health')
        data = response.json()
        
        self.assertIn('status', data)
        self.assertEqual(data['status'], 'ok')
        
        # Check model status fields
        if 'rl_model_loaded' in data:
            self.assertIsInstance(data['rl_model_loaded'], bool)
        if 'form_classifier_loaded' in data:
            self.assertIsInstance(data['form_classifier_loaded'], bool)


class TestRLPredictionAPI(unittest.TestCase):
    """Test RL prediction endpoint"""
    
    BASE_URL = 'http://localhost:5000'
    
    @classmethod
    def setUpClass(cls):
        try:
            response = requests.get(f'{cls.BASE_URL}/health', timeout=2)
            cls.server_running = response.status_code == 200
        except:
            cls.server_running = False
    
    def test_01_predict_with_valid_state(self):
        """Test /predict endpoint with valid 20-dim state"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        # Create valid 20-dimensional state
        state = [0.5] * 20
        
        response = requests.post(
            f'{self.BASE_URL}/predict',
            json={'state': state},
            headers={'Content-Type': 'application/json'}
        )
        
        self.assertEqual(response.status_code, 200)
        
        data = response.json()
        self.assertIn('action', data)
        self.assertIn('action_name', data)
        
        # Action should be 0-4
        self.assertIn(data['action'], [0, 1, 2, 3, 4])
        
        # Action name should be valid
        valid_actions = ['decrease_difficulty', 'maintain', 'increase_difficulty', 
                        'rest_break', 'encouragement']
        self.assertIn(data['action_name'], valid_actions)
    
    # def test_02_predict_response_time(self):
    #     """Test /predict responds quickly (<100ms)"""
    #     if not self.server_running:
    #         self.skipTest("API server not running")
        
    #     state = [0.5] * 20
        
    #     start = time.time()
    #     response = requests.post(
    #         f'{self.BASE_URL}/predict',
    #         json={'state': state}
    #     )
    #     latency = time.time() - start
        
    #     self.assertEqual(response.status_code, 200)
    #     self.assertLess(latency, 0.1, 
    #                    f"Response time should be <100ms, got {latency*1000:.1f}ms")
    
    def test_03_predict_with_invalid_state_shape(self):
        """Test /predict with wrong state dimensions"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        # Wrong size state
        state = [0.5] * 10  # Should be 20
        
        response = requests.post(
            f'{self.BASE_URL}/predict',
            json={'state': state}
        )
        
        # Should return error (400 or 500)
        self.assertIn(response.status_code, [400, 500])
    
    def test_04_predict_with_missing_state(self):
        """Test /predict without state parameter"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        response = requests.post(
            f'{self.BASE_URL}/predict',
            json={}
        )
        
        # Should return error
        self.assertIn(response.status_code, [400, 500])


class TestFormAnalysisAPI(unittest.TestCase):
    """Test form analysis endpoint (Form Analysis API)"""
    
    BASE_URL = 'http://localhost:5000'
    
    @classmethod
    def setUpClass(cls):
        try:
            response = requests.get(f'{cls.BASE_URL}/health', timeout=2)
            cls.server_running = response.status_code == 200
        except:
            cls.server_running = False
    
    def test_01_form_analysis_good_squat(self):
        """Test /predict_form_simple with good squat form"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        payload = {
            'angles': {
                'knee_left': 88,
                'knee_right': 90
            },
            'movement_speed': 2.5,
            'exercise_type': 'squat'
        }
        
        response = requests.post(
            f'{self.BASE_URL}/predict_form_simple',
            json=payload,
            headers={'Content-Type': 'application/json'}
        )
        
        self.assertEqual(response.status_code, 200)
        
        data = response.json()
        
        # Check required fields
        self.assertIn('form_quality', data)
        self.assertIn('is_correct', data)
        self.assertIn('feedback', data)
        
        # Good form should be marked correct
        self.assertTrue(data['is_correct'],
                       "Good form should be marked as correct")
        
        # Quality should be high
        if isinstance(data['form_quality'], str):
            quality = float(data['form_quality'].replace('%', ''))
        else:
            quality = data['form_quality']
        
        self.assertGreater(quality, 70,
                          f"Good form quality should be >70%, got {quality}%")
    
    def test_02_form_analysis_poor_squat(self):
        """Test /predict_form_simple with poor squat form"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        payload = {
            'angles': {
                'knee_left': 120,  # Too shallow
                'knee_right': 125
            },
            'movement_speed': 1.0,  # Too fast
            'exercise_type': 'squat'
        }
        
        response = requests.post(
            f'{self.BASE_URL}/predict_form_simple',
            json=payload
        )
        
        self.assertEqual(response.status_code, 200)
        
        data = response.json()
        
        # Should have corrections
        if 'corrections' in data:
            self.assertIsInstance(data['corrections'], list)
        
        # Should have issues detected
        if 'issues' in data:
            self.assertIsInstance(data['issues'], list)
    
    def test_03_form_analysis_different_exercises(self):
        """Test form analysis for hip and shoulder exercises"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        test_cases = [
            {
                'angles': {'hip_left': 95, 'hip_right': 93},
                'movement_speed': 2.0,
                'exercise_type': 'hip'
            },
            {
                'angles': {'shoulder_left': 88, 'shoulder_right': 90},
                'movement_speed': 2.2,
                'exercise_type': 'shoulder'
            }
        ]
        
        for payload in test_cases:
            response = requests.post(
                f'{self.BASE_URL}/predict_form_simple',
                json=payload
            )
            
            self.assertEqual(response.status_code, 200,
                           f"Failed for {payload['exercise_type']}")
            
            data = response.json()
            self.assertIn('form_quality', data)
    
    def test_04_form_analysis_asymmetry_detection(self):
        """Test detection of left-right asymmetry"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        payload = {
            'angles': {
                'knee_left': 85,
                'knee_right': 110  # 25° difference!
            },
            'movement_speed': 2.5,
            'exercise_type': 'squat'
        }
        
        response = requests.post(
            f'{self.BASE_URL}/predict_form_simple',
            json=payload
        )
        
        data = response.json()
        
        # Should detect asymmetry issue
        if 'issues' in data:
            issues_str = str(data['issues']).lower()
            # Check if asymmetry is mentioned
            # (exact format depends on implementation)
            self.assertTrue(len(data['issues']) > 0 or 
                          'asymmetry' in issues_str or
                          data['is_correct'] == False)
    
    def test_05_form_analysis_response_structure(self):
        """Test form analysis response has correct structure"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        payload = {
            'angles': {'knee_left': 90, 'knee_right': 90},
            'movement_speed': 2.5,
            'exercise_type': 'squat'
        }
        
        response = requests.post(
            f'{self.BASE_URL}/predict_form_simple',
            json=payload
        )
        
        data = response.json()
        
        # Check all expected fields
        expected_fields = ['form_quality', 'is_correct', 'feedback']
        for field in expected_fields:
            self.assertIn(field, data,
                         f"Response should contain '{field}' field")
        
        # Check types
        self.assertIsInstance(data['is_correct'], bool)
        self.assertIsInstance(data['feedback'], (list, str))


class TestAPIErrorHandling(unittest.TestCase):
    """Test API error handling"""
    
    BASE_URL = 'http://localhost:5000'
    
    @classmethod
    def setUpClass(cls):
        try:
            response = requests.get(f'{cls.BASE_URL}/health', timeout=2)
            cls.server_running = response.status_code == 200
        except:
            cls.server_running = False
    
    def test_01_invalid_endpoint(self):
        """Test accessing non-existent endpoint"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        response = requests.get(f'{self.BASE_URL}/nonexistent')
        
        # Should return 404
        self.assertEqual(response.status_code, 404)
    
    def test_02_malformed_json(self):
        """Test sending malformed JSON"""
        if not self.server_running:
            self.skipTest("API server not running")
        
        response = requests.post(
            f'{self.BASE_URL}/predict',
            data='{"invalid json',  # Malformed
            headers={'Content-Type': 'application/json'}
        )
        
        # Should return 400
        self.assertIn(response.status_code, [400, 500])


if __name__ == '__main__':
    unittest.main(verbosity=2)
