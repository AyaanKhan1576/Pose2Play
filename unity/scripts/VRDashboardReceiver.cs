using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class CalibrationState
{
    public int count;
    public int required;
}

[Serializable]
public class DashboardPacket
{
    public string type;
    public string exercise;
    public string phase;
    public int repCount;
    public float currentAngle;
    public float pushTarget;
    public float minimumThreshold;
    public string formQuality;
    public string status;
    public string feedback;
    public bool isCorrect;
    public CalibrationState calibration;
    public long timestamp;
}

public class VRDashboardReceiver : MonoBehaviour
{
    [Header("UDP")]
    [SerializeField] private int listenPort = 5056;

    [Tooltip("Desktop backend IPv4 address (example: 192.168.18.24). Do not use localhost for Quest builds.")]
    [SerializeField] private string desktopBackendIp = "192.168.18.30";

    [Tooltip("If enabled, only packets from Desktop Backend IP are accepted.")]
    [SerializeField] private bool onlyAcceptDesktopIp = false;

    [Tooltip("If filtering is enabled and Desktop Backend IP is empty or stale, auto-learn sender IP from first valid packet.")]
    [SerializeField] private bool autoLearnDesktopIp = true;

    private UdpClient _udpClient;
    private Thread _receiveThread;
    private volatile bool _running;

    private readonly object _dataLock = new object();
    private DashboardPacket _latestPacket;
    private bool _hasNewPacket;
    private long _lastPacketReceivedUtcTicks;
    private bool _loggedLearnedIp;

    public string LastSenderIp { get; private set; } = "None";
    public float SecondsSinceLastPacket
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastPacketReceivedUtcTicks);
            if (ticks <= 0)
            {
                return -1f;
            }

            TimeSpan delta = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            return (float)delta.TotalSeconds;
        }
    }

    public event Action<DashboardPacket> OnDashboardPacket;

    private void Start()
    {
        StartReceiver();
    }

    private void Update()
    {
        DashboardPacket packetToPublish = null;

        lock (_dataLock)
        {
            if (_hasNewPacket)
            {
                packetToPublish = _latestPacket;
                _hasNewPacket = false;
            }
        }

        if (packetToPublish != null)
        {
            OnDashboardPacket?.Invoke(packetToPublish);
        }
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    public void StartReceiver()
    {
        if (_running) return;

        ValidateDesktopIp();

        try
        {
            _udpClient = new UdpClient(listenPort);
            _udpClient.Client.ReceiveTimeout = 500;
            _running = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();
            Debug.Log($"[VRDashboardReceiver] Listening on UDP {listenPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VRDashboardReceiver] Failed to start: {ex.Message}");
        }
    }

    public void StopReceiver()
    {
        _running = false;

        try
        {
            _udpClient?.Close();
            _udpClient = null;
        }
        catch
        {
            // Ignore socket close exceptions during teardown.
        }

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(200);
        }
    }

    private void ReceiveLoop()
    {
        var endPoint = new IPEndPoint(IPAddress.Any, listenPort);

        while (_running)
        {
            try
            {
                byte[] bytes = _udpClient.Receive(ref endPoint);
                string senderIp = endPoint.Address?.ToString() ?? "Unknown";

                if (onlyAcceptDesktopIp && autoLearnDesktopIp && ShouldAutoLearnSenderIp())
                {
                    desktopBackendIp = senderIp;
                    if (!_loggedLearnedIp)
                    {
                        _loggedLearnedIp = true;
                        Debug.Log($"[VRDashboardReceiver] Auto-learned Desktop Backend IP: {desktopBackendIp}");
                    }
                }

                if (onlyAcceptDesktopIp && !IsAllowedSenderIp(senderIp))
                {
                    continue;
                }

                LastSenderIp = senderIp;
                Interlocked.Exchange(ref _lastPacketReceivedUtcTicks, DateTime.UtcNow.Ticks);
                string json = Encoding.UTF8.GetString(bytes);
                var packet = JsonUtility.FromJson<DashboardPacket>(json);

                if (packet == null) continue;

                lock (_dataLock)
                {
                    _latestPacket = packet;
                    _hasNewPacket = true;
                }
            }
            catch (SocketException)
            {
                if (!_running) break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRDashboardReceiver] Parse/receive issue: {ex.Message}");
            }
        }
    }

    private void ValidateDesktopIp()
    {
        if (string.IsNullOrWhiteSpace(desktopBackendIp))
        {
            Debug.LogWarning("[VRDashboardReceiver] Desktop Backend IP is empty. Set your desktop IPv4 for Quest deployments.");
            return;
        }

        string normalized = desktopBackendIp.Trim().ToLowerInvariant();
        if (normalized == "localhost" || normalized == "127.0.0.1")
        {
            Debug.LogWarning("[VRDashboardReceiver] Desktop Backend IP is localhost/127.0.0.1. This will fail on Quest; use desktop LAN IPv4.");
        }

        if (!IPAddress.TryParse(desktopBackendIp, out _))
        {
            Debug.LogWarning("[VRDashboardReceiver] Desktop Backend IP is not a valid IPv4/IPv6 string.");
        }
    }

    private bool IsAllowedSenderIp(string senderIp)
    {
        if (string.IsNullOrWhiteSpace(desktopBackendIp))
        {
            return true;
        }

        if (senderIp == desktopBackendIp)
        {
            return true;
        }

#if UNITY_EDITOR
        if (senderIp == "127.0.0.1" || senderIp == "::1")
        {
            return true;
        }
#endif

        return false;
    }

    private bool ShouldAutoLearnSenderIp()
    {
        if (string.IsNullOrWhiteSpace(desktopBackendIp))
        {
            return true;
        }

        string normalized = desktopBackendIp.Trim();
        return normalized == "192.168.18.30";
    }
}
