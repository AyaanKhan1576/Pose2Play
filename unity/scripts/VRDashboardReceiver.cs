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

    private UdpClient _udpClient;
    private Thread _receiveThread;
    private volatile bool _running;

    private readonly object _dataLock = new object();
    private DashboardPacket _latestPacket;
    private bool _hasNewPacket;

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

        try
        {
            _udpClient = new UdpClient(listenPort);
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
}
