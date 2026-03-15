using System.Collections;
using TMPro;
using UnityEngine;

public class VRVoiceCuePlayer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private VRDashboardReceiver dashboardReceiver;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip calibrationClip;
    [SerializeField] private AudioClip squatDeeperClip;
    [SerializeField] private AudioClip slowDownClip;
    [SerializeField] private AudioClip goodFormClip;
    [SerializeField] private AudioClip repCompleteClip;

    [Header("UI Optional")]
    [SerializeField] private TMP_Text nowPlayingText;

    [Header("Behavior")]
    [SerializeField] private float minSecondsBetweenCues = 2.5f;

    private float _lastCueTime;
    private int _lastRepCount;

    private void OnEnable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket += HandleDashboardPacket;
        }
    }

    private void OnDisable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
        }
    }

    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null || audioSource == null) return;
        if (Time.time - _lastCueTime < minSecondsBetweenCues) return;

        if (packet.phase == "BASELINE")
        {
            TryPlay(calibrationClip, "Calibration: follow the motion");
            return;
        }

        if (packet.repCount > _lastRepCount)
        {
            _lastRepCount = packet.repCount;
            TryPlay(repCompleteClip, "Rep counted");
            return;
        }

        string feedback = packet.feedback != null ? packet.feedback.ToLowerInvariant() : string.Empty;

        if (feedback.Contains("deeper"))
        {
            TryPlay(squatDeeperClip, "Squat deeper");
            return;
        }

        if (feedback.Contains("slow down"))
        {
            TryPlay(slowDownClip, "Slow down your movement");
            return;
        }

        if (packet.isCorrect)
        {
            TryPlay(goodFormClip, "Good form");
        }
    }

    private void TryPlay(AudioClip clip, string caption)
    {
        if (clip == null || audioSource == null) return;
        if (audioSource.isPlaying) return;

        audioSource.PlayOneShot(clip);
        _lastCueTime = Time.time;

        if (nowPlayingText != null)
        {
            StopAllCoroutines();
            StartCoroutine(ShowCueCaption(caption));
        }
    }

    private IEnumerator ShowCueCaption(string text)
    {
        nowPlayingText.text = text;
        yield return new WaitForSeconds(1.5f);
        nowPlayingText.text = string.Empty;
    }
}
