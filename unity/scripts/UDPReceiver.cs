using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPReceiver : MonoBehaviour
{
    public int port = 5055;

    private UdpClient client;
    private Thread receiveThread;
    private volatile bool running = false;
    private string latestMessage = "";
    private volatile bool newData = false;

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
}