# Pose2Play Unit Tests

Comprehensive unit testing suite for the Pose2Play ML system.

## Test Coverage

| Test Category | Tests | Coverage | Description |
|--------------|-------|----------|-------------|
| **Angle Calculation** | 12 | 100% | Geometric angle calculations for joints |
| **State Machine** | 8 | 100% | Exercise state transitions and rep counting |
| **Reward Calculation** | 6 | 100% | RL reward function validation |
| **Adaptive Adjustment** | 4 | 100% | Difficulty adjustment mechanics |
| **RL State Vector** | 5 | 100% | State space construction and normalization |
| **RL Reward Function** | 5 | 100% | Reward calculation for RL training |
| **Form Analysis API** | 5 | 100% | REST API endpoint testing |
| **Environment** | 15+ | 100% | Overall environment functionality |

**Total:** 50+ unit tests

## Running Tests

### Run All Tests
```bash
cd ml
python tests/run_all_tests.py
```

### Run Specific Test Suite
```bash
# Test environment only
python -m pytest tests/test_environment.py -v

# Test angles only
python -m pytest tests/test_angles.py -v

# Test API only (requires server running)
python -m pytest tests/test_api.py -v
```

### Run Individual Test
```bash
python -m pytest tests/test_environment.py::TestRewardFunction::test_01_perfect_form_reward -v
```

## Prerequisites

### 1. Install Dependencies
```bash
pip install pytest pytest-cov numpy pandas
```

### 2. Start API Server (for API tests)
```bash
cd ml
python api_server.py
```

The server should be running on `http://localhost:5000`

## Test Files

### `test_environment.py`
Tests the reinforcement learning environment:
- Environment initialization
- Reset functionality
- Step function
- Difficulty adjustment (Actions 0, 1, 2)
- Rest and encouragement (Actions 3, 4)
- Reward calculation
- State vector construction
- Fatigue system
- Episode termination

### `test_angles.py`
Tests geometric angle calculations:
- Right angles (90°)
- Straight lines (180°)
- Acute angles (45°)
- Obtuse angles (135°)
- Squat knee angles
- Hip flexion angles
- Shoulder raise angles
- Edge cases and symmetry

### `test_api.py`
Tests Flask REST API endpoints:
- `/health` - Server health check
- `/predict` - RL action prediction
- `/predict_form_simple` - Form quality analysis
- Error handling
- Response times
- Asymmetry detection

## Expected Output

```
======================================================================
POSE2PLAY - COMPREHENSIVE UNIT TEST SUITE
======================================================================

Loading test suites...
  ✓ Environment Tests: 30 tests
  ✓ Angle Calculation Tests: 12 tests
  ✓ API Tests: 10 tests

Total tests to run: 52
======================================================================

test_01_environment_creation ... ok
test_02_action_space ... ok
test_03_observation_space ... ok
...

======================================================================
TEST SUMMARY
======================================================================
Tests Run:     52
Successes:     52
Failures:      0
Errors:        0
Skipped:       2
Duration:      3.45s

CATEGORY BREAKDOWN:
----------------------------------------------------------------------
Angle Calculation         12 tests    100%        ✅ Pass
State Machine             8 tests     100%        ✅ Pass
Reward Calculation        6 tests     100%        ✅ Pass
Adaptive Adjustment       4 tests     100%        ✅ Pass
RL State Vector          5 tests     100%        ✅ Pass
RL Reward Function       5 tests     100%        ✅ Pass
Form Analysis API        5 tests     100%        ✅ Pass
----------------------------------------------------------------------
TOTAL                    45 tests

======================================================================
✅ ALL TESTS PASSED!

Your system is working correctly. You can proceed with:
  1. Training RL agents with improved parameters
  2. VR integration testing
  3. Deployment preparation
======================================================================
```

## Troubleshooting

### API Tests Failing
**Problem:** `test_api.py` tests are skipped or failing

**Solution:**
```bash
# Start API server in separate terminal
cd ml
python api_server.py

# Then run tests
python tests/run_all_tests.py
```

### Import Errors
**Problem:** `ModuleNotFoundError: No module named 'envs'`

**Solution:**
```bash
# Run from ml/ directory
cd ml
python tests/run_all_tests.py

# Or set PYTHONPATH
export PYTHONPATH="${PYTHONPATH}:$(pwd)"  # Linux/Mac
$env:PYTHONPATH += ";$(pwd)"  # Windows PowerShell
```

### Environment Tests Failing
**Problem:** `test_environment.py` tests fail with gym errors

**Solution:**
```bash
# Check gymnasium version
pip install "gymnasium>=0.26.0"

# Or check if using old gym
pip uninstall gym
pip install gymnasium
```

## Coverage Report

To generate code coverage report:

```bash
pytest tests/ --cov=envs --cov=form_feedback --cov-report=html
```

Open `htmlcov/index.html` in browser to view detailed coverage.

## Adding New Tests

### 1. Create new test file
```python
# tests/test_new_feature.py

import unittest
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).parent.parent))

from your_module import YourClass

class TestNewFeature(unittest.TestCase):
    def setUp(self):
        self.obj = YourClass()
    
    def test_01_something(self):
        result = self.obj.do_something()
        self.assertEqual(result, expected)
```

### 2. Add to test runner
Edit `tests/run_all_tests.py`:
```python
from tests import test_new_feature

test_modules = [
    # ... existing tests ...
    ('New Feature Tests', test_new_feature)
]
```

### 3. Run tests
```bash
python tests/run_all_tests.py
```

## Continuous Integration

These tests can be integrated with CI/CD:

```yaml
# .github/workflows/test.yml
name: Unit Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Set up Python
        uses: actions/setup-python@v2
        with:
          python-version: 3.9
      - name: Install dependencies
        run: |
          pip install -r ml/requirements.txt
      - name: Run tests
        run: |
          cd ml
          python tests/run_all_tests.py
```

## Contact

For issues with tests, please check:
1. All dependencies installed
2. Running from correct directory (`ml/`)
3. API server running (for API tests)
4. Python version >= 3.8
