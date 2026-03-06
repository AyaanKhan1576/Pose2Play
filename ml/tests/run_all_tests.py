"""
Master test runner - executes all unit tests and generates summary
Run with: python tests/run_all_tests.py
"""

import unittest
import sys
import os
from pathlib import Path
import time
from io import StringIO

# Add parent directory and tests directory to path
parent_dir = Path(__file__).parent.parent
tests_dir = Path(__file__).parent
sys.path.insert(0, str(parent_dir))
sys.path.insert(0, str(tests_dir))

# Import test modules directly (avoid conflict with builtin 'tests' package)
import test_environment
import test_angles
import test_api


class DetailedTestResult(unittest.TextTestResult):
    """Custom test result class to capture detailed test information"""
    
    def __init__(self, stream, descriptions, verbosity):
        super().__init__(stream, descriptions, verbosity)
        self.test_details = []
        
    def startTest(self, test):
        super().startTest(test)
        self.current_test_start = time.time()
        
    def addSuccess(self, test):
        super().addSuccess(test)
        duration = time.time() - self.current_test_start
        self.test_details.append({
            'test': test,
            'status': 'PASS',
            'duration': duration,
            'message': ''
        })
        
    def addError(self, test, err):
        super().addError(test, err)
        duration = time.time() - self.current_test_start
        error_msg = str(err[1])
        self.test_details.append({
            'test': test,
            'status': 'ERROR',
            'duration': duration,
            'message': error_msg[:100]
        })
        
    def addFailure(self, test, err):
        super().addFailure(test, err)
        duration = time.time() - self.current_test_start
        failure_msg = str(err[1])
        self.test_details.append({
            'test': test,
            'status': 'FAIL',
            'duration': duration,
            'message': failure_msg[:100]
        })
        
    def addSkip(self, test, reason):
        super().addSkip(test, reason)
        duration = time.time() - self.current_test_start
        self.test_details.append({
            'test': test,
            'status': 'SKIP',
            'duration': duration,
            'message': reason
        })


def run_all_tests():
    """Run all test suites and generate summary"""
    
    print("="*70)
    print("POSE2PLAY - COMPREHENSIVE UNIT TEST SUITE")
    print("="*70)
    print()
    
    # Create test loader
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()
    
    # Add all test modules
    test_modules = [
        ('Environment Tests', test_environment),
        ('Angle Calculation Tests', test_angles),
        ('API Tests', test_api)
    ]
    
    # Load tests
    print("Loading test suites...")
    for name, module in test_modules:
        tests = loader.loadTestsFromModule(module)
        suite.addTests(tests)
        print(f"  ✓ {name}: {tests.countTestCases()} tests")
    
    print(f"\nTotal tests to run: {suite.countTestCases()}")
    print("="*70)
    print()
    
    # Run tests with custom result class
    start_time = time.time()
    stream = StringIO()
    runner = unittest.TextTestRunner(stream=stream, verbosity=0, resultclass=DetailedTestResult)
    result = runner.run(suite)
    duration = time.time() - start_time
    
    # Organize tests by category
    env_tests = [t for t in result.test_details if 'test_environment' in str(t['test'])]
    angle_tests = [t for t in result.test_details if 'test_angles' in str(t['test'])]
    api_tests = [t for t in result.test_details if 'test_api' in str(t['test'])]
    
    # Generate detailed output
    print("\n" + "="*100)
    print(" "*35 + "TEST RESULTS SUMMARY")
    print("="*100)
    print()
    
    # Overall statistics
    tests_run = result.testsRun
    successes = tests_run - len(result.failures) - len(result.errors) - len(result.skipped)
    failures = len(result.failures)
    errors = len(result.errors)
    skipped = len(result.skipped)
    success_rate = (successes / tests_run * 100) if tests_run > 0 else 0
    
    print(f"{'Total Tests:':<20} {tests_run:>3}")
    print(f"{'✅ Passed:':<20} {successes:>3}  ({success_rate:.1f}%)")
    print(f"{'❌ Failed:':<20} {failures:>3}")
    print(f"{'⚠️  Errors:':<20} {errors:>3}")
    print(f"{'⊘ Skipped:':<20} {skipped:>3}")
    print(f"{'⏱️  Duration:':<20} {duration:.2f}s")
    print()
    
    # Category breakdown function
    def print_category(title, tests, category_num):
        if not tests:
            return
            
        passed = sum(1 for t in tests if t['status'] == 'PASS')
        failed = sum(1 for t in tests if t['status'] == 'FAIL')
        errors = sum(1 for t in tests if t['status'] == 'ERROR')
        skipped = sum(1 for t in tests if t['status'] == 'SKIP')
        
        print("="*100)
        print(f" {category_num}. {title}")
        print("="*100)
        print(f"Status: {passed}/{len(tests)} passed | {failed} failed | {errors} errors | {skipped} skipped")
        print()
        
        # Table header
        print(f"{'Test Name':<60} {'Status':<10} {'Time':<10} {'Result'}")
        print("-"*100)
        
        # Print each test
        for test_detail in tests:
            test = test_detail['test']
            status = test_detail['status']
            duration_ms = test_detail['duration'] * 1000
            message = test_detail['message']
            
            # Extract test name and description
            test_str = str(test)
            test_name = test_str.split(' ')[0].split('.')[-1]
            
            # Get docstring description
            test_method = getattr(test.__class__, test._testMethodName, None)
            description = ''
            if test_method and test_method.__doc__:
                description = test_method.__doc__.strip().split('\n')[0][:50]
            
            # Status symbol
            status_symbol = {
                'PASS': '✅ PASS',
                'FAIL': '❌ FAIL',
                'ERROR': '⚠️  ERROR',
                'SKIP': '⊘ SKIP'
            }.get(status, status)
            
            # Format duration
            duration_str = f"{duration_ms:>6.1f}ms" if duration_ms < 1000 else f"{duration_ms/1000:>6.2f}s"
            
            # Print row
            display_name = test_name if len(test_name) <= 55 else test_name[:52] + "..."
            print(f"{display_name:<60} {status_symbol:<10} {duration_str:<10} {description}")
            
            # Print message for failures/errors/skips
            if message and status != 'PASS':
                print(f"{'':>60} └─ {message[:80]}")
        
        print()
    
    # Print each category
    print_category("✓ Environment Tests (RL Agent, State, Rewards, Fatigue)", env_tests, 1)
    print_category("✓ Angle Calculation Tests (Joint Angles, Edge Cases)", angle_tests, 2)
    print_category("✓ API Tests (Form Analysis, Predictions, Error Handling)", api_tests, 3)
    print()
    
    # Detailed failures
    if failures > 0:
        print("FAILURES:")
        print("-" * 70)
        for test, traceback in result.failures:
            print(f"❌ {test}")
            print(f"   {traceback.split('AssertionError:')[-1].strip()[:100]}")
        print()
    
    if errors > 0:
        print("ERRORS:")
        print("-" * 70)
        for test, traceback in result.errors:
            print(f"❌ {test}")
            error_msg = traceback.strip().split('\n')[-1]
            print(f"   {error_msg[:100]}")
        print()
    
    if skipped > 0:
        print("SKIPPED:")
        print("-" * 70)
        for test, reason in result.skipped:
            print(f"⊘ {test}")
            print(f"   {reason}")
        print()
    
    # Final verdict
    print("="*70)
    if result.wasSuccessful():
        print("✅ ALL TESTS PASSED!")
        print()
        print("Your system is working correctly. You can proceed with:")
        print("  1. Training RL agents with improved parameters")
        print("  2. VR integration testing")
        print("  3. Deployment preparation")
    else:
        print("❌ SOME TESTS FAILED")
        print()
        print("Please review the failures above and fix the issues.")
        print("Common issues:")
        print("  - API server not running (start with: python api_server.py)")
        print("  - Missing dependencies (run: pip install -r requirements.txt)")
        print("  - Environment configuration issues")
    
    print("="*70)
    
    return result.wasSuccessful()


if __name__ == '__main__':
    success = run_all_tests()
    sys.exit(0 if success else 1)
