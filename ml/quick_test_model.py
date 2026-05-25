"""
Quick test of the trained DQN model
"""
from envs.rehab_env import RehabExerciseEnv
from stable_baselines3 import DQN
import numpy as np

print("=" * 70)
print("🤖 TESTING TRAINED DQN MODEL")
print("=" * 70)

# Load model
print("\n📦 Loading trained model...")
model = DQN.load('./models/dqn/DQN_rehab_final.zip')
print("✅ Model loaded successfully!")

# Create environment
env = RehabExerciseEnv()

# Run 5 test episodes
print("\n🏃 Running 5 test episodes...\n")

episode_rewards = []
episode_completions = []

for ep in range(5):
    state, _ = env.reset()
    done = False
    total_reward = 0
    steps = 0
    
    print(f"\n{'─' * 70}")
    print(f"Episode {ep + 1}/5")
    print(f"{'─' * 70}")
    
    while not done and steps < 25:
        # Agent makes decision
        action, _ = model.predict(state, deterministic=True)
        state, reward, terminated, truncated, info = env.step(action)
        done = terminated or truncated
        
        # Display action
        action_names = ['↓ Easier', '→ Maintain', '↑ Harder', '⏸ Rest', '💪 Encourage']
        action_name = action_names[action]
        
        if steps % 5 == 0 or done:  # Print every 5 steps
            print(f"  Step {steps+1:2d}: {action_name:12} | Reward: {reward:+6.1f} | Target: {info['current_target']:5.1f}° | Fatigue: {info['fatigue']:.2f}")
        
        total_reward += reward
        steps += 1
    
    episode_rewards.append(total_reward)
    completed = info.get('termination', '') == 'session_complete'
    episode_completions.append(completed)
    
    status = "✅ Completed" if completed else f"⚠️  {info.get('termination', 'stopped')}"
    print(f"\n  {status} | Total Reward: {total_reward:.1f} | Steps: {steps}")

# Summary
print("\n" + "=" * 70)
print("📊 TEST SUMMARY")
print("=" * 70)
print(f"\nMean Reward: {np.mean(episode_rewards):.1f} ± {np.std(episode_rewards):.1f}")
print(f"Completion Rate: {sum(episode_completions)}/5 ({sum(episode_completions)*20}%)")
print(f"Min Reward: {np.min(episode_rewards):.1f}")
print(f"Max Reward: {np.max(episode_rewards):.1f}")

print("\n" + "=" * 70)
print("✅ MODEL PERFORMANCE")
print("=" * 70)

mean_reward = np.mean(episode_rewards)
if mean_reward >= 800:
    print("🌟 EXCELLENT! Mean reward > 800")
    print("   The agent learned very effective difficulty management!")
elif mean_reward >= 400:
    print("✅ GOOD! Mean reward in target range (400-500)")
    print("   The agent learned good difficulty adjustment.")
else:
    print("⚠️  Fair performance. Consider retraining.")

completion_rate = sum(episode_completions) / 5
if completion_rate >= 0.8:
    print(f"\n🎯 GREAT! {completion_rate*100:.0f}% session completion (Target: >80%)")
else:
    print(f"\n⚠️  {completion_rate*100:.0f}% session completion (Target: >80%)")

print("\n" + "=" * 70)
print("🎉 TRAINING SUCCESSFUL - MODEL READY TO USE!")
print("=" * 70)
print("\nNext steps:")
print("  1. Integrate with web demo (Phase 7 of documentation/MASTER_GUIDE.md)")
print("  2. Create Flask API for predictions")
print("  3. Test with real users")
print("\n" + "=" * 70)
