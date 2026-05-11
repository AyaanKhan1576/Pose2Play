using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using System.Net;

#if !UNITY_WEBGL
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
#endif

public class VRVoiceCuePlayer : MonoBehaviour
{
    public enum DeploymentMode
    {
        Development,    // Local PC testing
        VR              // VR headset deployment (Quest, Vive, etc.)
    }

    [Header("Deployment")]
    [SerializeField] private DeploymentMode deploymentMode = DeploymentMode.Development;
    [SerializeField] private bool useHTTPS = false;

    [Header("Data")]
    [SerializeField] private VRDashboardReceiver dashboardReceiver;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("UI for Voice Feedback")]
    [SerializeField] private TMP_Text voiceCueText;

    [Header("Backend - Development")]
    [SerializeField] private string ttsServerUrl_Dev = "http://localhost:5000";

    [Header("Backend - VR")]
    [SerializeField] private string ttsServerUrl_VR = "http://192.168.1.100:5000";  // Change to your PC's LAN IP
    [SerializeField] private bool autoDetectVRBackend = true;

    [Header("Timing")]
    [SerializeField] private float minSecondsBetweenCues = 1.0f;
    [SerializeField] private float textDisplayDuration = 3.0f;
    [SerializeField] private float requestTimeout = 10.0f;  // Longer timeout for VR networks

    [Header("Retry")]
    [SerializeField] private int maxRetries = 2;

    private float _lastCueTime;
    private int _lastRepCount;
    private bool _isFetchingAudio;
    private float _nextRelinkTime;
    private string _activeServerUrl;
    private static bool _certificateValidationSetup = false;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Setup certificate validation once globally
        SetupCertificateValidation();

        // Determine active server URL based on deployment mode
        DetermineActiveServerUrl();

        TrySubscribeDashboardReceiver();
    }

    private void SetupCertificateValidation()
    {
#if !UNITY_WEBGL
        if (_certificateValidationSetup) return;

        try
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                // Allow localhost/127.0.0.1 and private network IPs for development
                string hostName = sender?.ToString() ?? "";
                
                if (hostName.Contains("localhost") || 
                    hostName.Contains("127.0.0.1") ||
                    hostName.Contains("192.168") ||
                    hostName.Contains("10.0") ||
                    hostName.Contains("172.16"))
                {
                    return true;  // Allow local development
                }

                // For other URLs, enforce strict validation (production)
                return sslPolicyErrors == SslPolicyErrors.None;
            };

            _certificateValidationSetup = true;
            Debug.Log("[VRVoiceCuePlayer] Certificate validation configured for local development");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[VRVoiceCuePlayer] Could not setup certificate validation: {ex.Message}");
        }
#else
        _certificateValidationSetup = true;
#endif
    }

    private void DetermineActiveServerUrl()
    {
        if (deploymentMode == DeploymentMode.Development)
        {
            _activeServerUrl = useHTTPS ? ttsServerUrl_Dev.Replace("http://", "https://") : ttsServerUrl_Dev;
            Debug.Log($"[VRVoiceCuePlayer] Development mode: {_activeServerUrl}");
        }
        else
        {
            if (autoDetectVRBackend)
            {
                // Try to auto-detect backend on local network
                _activeServerUrl = DetectVRBackendUrl();
            }
            else
            {
                _activeServerUrl = useHTTPS ? ttsServerUrl_VR.Replace("http://", "https://") : ttsServerUrl_VR;
            }
            Debug.Log($"[VRVoiceCuePlayer] VR mode: {_activeServerUrl}");
        }
    }

    private string DetectVRBackendUrl()
    {
        // For Quest/VR, try common LAN IP patterns
        string[] commonIPs = new string[]
        {
            "http://192.168.1.100:5000",
            "http://192.168.0.100:5000",
            "http://10.0.0.100:5000",
            ttsServerUrl_VR
        };

        foreach (string url in commonIPs)
        {
            if (!string.IsNullOrEmpty(url) && url.Contains(":5000"))
            {
                return useHTTPS ? url.Replace("http://", "https://") : url;
            }
        }

        return useHTTPS ? ttsServerUrl_VR.Replace("http://", "https://") : ttsServerUrl_VR;
    }

    /// <summary>
    /// Reconfigure server URL at runtime (useful for VR deployment)
    /// </summary>
    public void SetServerUrl(string newUrl)
    {
        _activeServerUrl = newUrl;
        Debug.Log($"[VRVoiceCuePlayer] Server URL updated to: {_activeServerUrl}");
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
        StartCoroutine(FetchAndPlayTTS(text, attempt: 1));
    }

    private IEnumerator FetchAndPlayTTS(string text, int attempt = 1)
    {
        _isFetchingAudio = true;

        // Show text caption immediately
        if (voiceCueText != null)
        {
            voiceCueText.text = $"🔊 {text}";
        }

        if (string.IsNullOrEmpty(_activeServerUrl))
        {
            Debug.LogError("[VRVoiceCuePlayer] Server URL not configured");
            yield return new WaitForSeconds(textDisplayDuration);
            _isFetchingAudio = false;
            yield break;
        }

        string requestUrl = $"{_activeServerUrl}/generate_tts";
        bool requestSucceeded = false;
        byte[] audioData = null;

        // Prepare request to backend
        using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
        {
            string json = JsonUtility.ToJson(new TTSRequest { text = text });
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                audioData = request.downloadHandler.data;
                requestSucceeded = true;
            }
            else
            {
                Debug.LogWarning($"[VRVoiceCuePlayer] TTS request failed (attempt {attempt}/{maxRetries + 1}): {request.error}");
                requestSucceeded = false;
            }
        }

        // Handle retry or playback
        if (requestSucceeded && audioData != null)
        {
            StartCoroutine(PlayAudioFromBytes(audioData, text));
        }
        else if (attempt <= maxRetries && deploymentMode == DeploymentMode.VR)
        {
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(FetchAndPlayTTS(text, attempt + 1));
        }
        else
        {
            // Show text if audio fails
            yield return new WaitForSeconds(textDisplayDuration);
        }

        _isFetchingAudio = false;
    }

    private IEnumerator PlayAudioFromBytes(byte[] audioData, string originalText)
    {
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogWarning("[VRVoiceCuePlayer] Received empty audio data");
            yield return new WaitForSeconds(textDisplayDuration);
            yield break;
        }

        // Save WAV to temporary file
        string tempPath = Path.Combine(Application.persistentDataPath, "tts_audio.wav");
        bool fileSaved = false;

        try
        {
            File.WriteAllBytes(tempPath, audioData);
            fileSaved = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRVoiceCuePlayer] Failed to write audio file: {ex.Message}");
            fileSaved = false;
        }

        if (!fileSaved)
        {
            yield return new WaitForSeconds(textDisplayDuration);
            yield break;
        }

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
                Debug.LogWarning($"[VRVoiceCuePlayer] Audio load failed: {www.error}");
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
