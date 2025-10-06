using UnityEngine;
using System;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

public class TCPClientUnity : MonoBehaviour
{
    private TcpClient client;
    private StreamReader reader;
    private Thread receiveThread;

    // Unity ���� �����忡�� �����ϰ� ����ϱ� ���� ť
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    [Header("TCP Settings")]
    public string serverIP = "127.0.0.1";
    public int port = 5000;

    void Start()
    {
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient(serverIP, port);
            reader = new StreamReader(client.GetStream());

            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("������ ����Ǿ����ϴ�.");
        }
        catch (Exception e)
        {
            Debug.LogError("���� ���� ����: " + e.Message);
        }
    }

    void ReceiveData()
    {
        try
        {
            while (client != null && client.Connected)
            {
                string data = reader.ReadLine();
                if (data != null)
                {
                    messageQueue.Enqueue(data);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("������ ���� ����: " + e.Message);
        }
    }

    void Update()
    {
        // ť�� ���� �޽����� Unity ���� �����忡�� ���
        while (messageQueue.TryDequeue(out string msg))
        {
            Debug.Log("�����κ��� ����: " + msg);
        }
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void Disconnect()
    {
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();

        if (reader != null)
            reader.Close();

        if (client != null)
            client.Close();
    }
}
