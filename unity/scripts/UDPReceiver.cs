using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPReceiver : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("UDP port Unity listens on for pose packets (must match Python send port).")]
    public int port = 5055;

    [Tooltip("Desktop backend IPv4 address (example: 192.168.18.24). Do not use localhost for Quest builds.")]
    public string desktopBackendIp = "192.168.18.30";

    [Tooltip("If enabled, only packets from Desktop Backend IP are accepted.")]
    public bool onlyAcceptDesktopIp = true;

    private UdpClient client;
    private Thread receiveThread;
    private volatile bool running = false;
    private string latestMessage = "";
    private volatile bool newData = false;
    private long _lastPacketReceivedUtcTicks;

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

    [Serializable]
    public class PoseData
    {
        public float[] left_shoulder, right_shoulder;
        public float[] left_elbow,    right_elbow;
        public float[] left_wrist,    right_wrist;
        public float[] left_hip,      right_hip;
        public float[] left_knee,     right_knee;
        public float[] left_ankle,    right_ankle;
    }

    public PoseData pose;

    void Start()
    {
        ValidateDesktopIp();

        client = new UdpClient(port);
        client.Client.ReceiveTimeout = 500;   // unblocks thread every 500ms so it can check 'running'
        running = true;
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();
        Debug.Log("UDP Receiver listening on port " + port);
    }

    void ReceiveLoop()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref ep);
                string senderIp = ep.Address?.ToString() ?? "Unknown";

                if (onlyAcceptDesktopIp && !IsAllowedSenderIp(senderIp))
                {
                    continue;
                }

                LastSenderIp = senderIp;
                Interlocked.Exchange(ref _lastPacketReceivedUtcTicks, DateTime.UtcNow.Ticks);
                latestMessage = Encoding.UTF8.GetString(data);
                newData = true;
            }
            catch (SocketException) { /* timeout — loop back and check 'running' */ }
            catch (Exception) { break; }
        }
    }

    void Update()
    {
        if (!newData) return;
        newData = false;

        try
        {
            pose = JsonUtility.FromJson<PoseData>(latestMessage);
        }
        catch (Exception e)
        {
            Debug.LogWarning("JSON parse error: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        client?.Close();
        receiveThread?.Join(1000);  // wait max 1 second for thread to stop cleanly
    }

    private void ValidateDesktopIp()
    {
        if (string.IsNullOrWhiteSpace(desktopBackendIp))
        {
            Debug.LogWarning("[UDPReceiver] Desktop Backend IP is empty. Set your desktop IPv4 for Quest deployments.");
            return;
        }

        string normalized = desktopBackendIp.Trim().ToLowerInvariant();
        if (normalized == "localhost" || normalized == "127.0.0.1")
        {
            Debug.LogWarning("[UDPReceiver] Desktop Backend IP is localhost/127.0.0.1. This will fail on Quest; use desktop LAN IPv4.");
        }

        if (!IPAddress.TryParse(desktopBackendIp, out _))
        {
            Debug.LogWarning("[UDPReceiver] Desktop Backend IP is not a valid IPv4/IPv6 string.");
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
}