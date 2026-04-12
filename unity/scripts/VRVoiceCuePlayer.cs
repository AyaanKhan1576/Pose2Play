using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class VRVoiceCuePlayer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private VRDashboardReceiver dashboardReceiver;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("UI for Voice Feedback")]
    [SerializeField] private TMP_Text voiceCueText;

    [Header("Backend")]
    [SerializeField] private string ttsServerUrl = "http://localhost:5000";

    [Header("Timing")]
    [SerializeField] private float minSecondsBetweenCues = 1.0f;
    [SerializeField] private float textDisplayDuration = 3.0f;

    private float _lastCueTime;
    private int _lastRepCount;
    private bool _isFetchingAudio;
    private float _nextRelinkTime;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        TrySubscribeDashboardReceiver();
    }

    private void OnEnable()
    {
        TrySubscribeDashboardReceiver();
    }

    private void OnDisable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
        }
    }

    private void Update()
    {
        if (dashboardReceiver == null && Time.time >= _nextRelinkTime)
        {
            TrySubscribeDashboardReceiver();
            _nextRelinkTime = Time.time + 2f;
        }
    }

    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null) return;
        if (Time.time - _lastCueTime < minSecondsBetweenCues) return;
        if (_isFetchingAudio) return;

        // Track rep changes
        if (packet.repCount > _lastRepCount)
        {
            _lastRepCount = packet.repCount;
            PlayVoiceCue($"Rep {packet.repCount}");
            return;
        }

        // Handle form feedback
        string feedback = packet.feedback != null ? packet.feedback.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(feedback))
        {
            // Form correction (contains warning symbol or negative keywords)
            if (feedback.Contains("⚠️") || feedback.ToLower().Contains("avoid") || 
                feedback.ToLower().Contains("too") || feedback.ToLower().Contains("incorrect"))
            {
                string cleanFeedback = feedback.Replace("⚠️", "").Trim();
                PlayVoiceCue(cleanFeedback);
                return;
            }

            // Good form encouragement
            if (packet.isCorrect)
            {
                PlayVoiceCue(feedback);
            }
        }
    }

    /// <summary>
    /// Request TTS audio from backend and play it
    /// </summary>
    private void PlayVoiceCue(string text)
    {
        _lastCueTime = Time.time;
        StopAllCoroutines();
        StartCoroutine(FetchAndPlayTTS(text));
    }

    private IEnumerator FetchAndPlayTTS(string text)
    {
        _isFetchingAudio = true;

        // Show text caption immediately
        if (voiceCueText != null)
        {
            voiceCueText.text = $"🔊 {text}";
        }

        // Prepare request to backend
        using (UnityWebRequest request = new UnityWebRequest($"{ttsServerUrl}/generate_tts", "POST"))
        {
            string json = JsonUtility.ToJson(new TTSRequest { text = text });
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Convert downloaded audio to AudioClip
                byte[] audioData = request.downloadHandler.data;
                StartCoroutine(PlayAudioFromBytes(audioData, text));
            }
            else
            {
                Debug.LogWarning($"TTS request failed: {request.error}");
                // Still show text even if audio fails
                yield return new WaitForSeconds(textDisplayDuration);
            }
        }

        _isFetchingAudio = false;
    }

    private IEnumerator PlayAudioFromBytes(byte[] audioData, string originalText)
    {
        // Save WAV to temporary file
        string tempPath = Path.Combine(Application.persistentDataPath, "tts_audio.wav");
        
        File.WriteAllBytes(tempPath, audioData);

        AudioClip clip = null;

        // Load as AudioClip
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                clip = DownloadHandlerAudioClip.GetContent(www);
            }
            else
            {
                Debug.LogWarning($"Audio load failed: {www.error}");
            }
        }

        // Play audio if loaded
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }

        // Wait a bit then clear text
        yield return new WaitForSeconds(0.5f);
        if (voiceCueText != null)
        {
            voiceCueText.text = string.Empty;
        }

        // Clean up temp file (non-blocking, can fail silently)
        if (File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);
            }
            catch { }
        }
    }

    [System.Serializable]
    private class TTSRequest
    {
        public string text;
    }

    private void TrySubscribeDashboardReceiver()
    {
        if (dashboardReceiver == null)
        {
            dashboardReceiver = FindFirstObjectByType<VRDashboardReceiver>();
        }

        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
            dashboardReceiver.OnDashboardPacket += HandleDashboardPacket;
        }
    }
}
