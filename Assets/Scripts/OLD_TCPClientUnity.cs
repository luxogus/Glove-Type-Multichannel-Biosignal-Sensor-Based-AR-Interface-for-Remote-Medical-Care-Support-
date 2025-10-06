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

    // Unity 메인 스레드에서 안전하게 출력하기 위한 큐
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

            Debug.Log("서버에 연결되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError("서버 연결 실패: " + e.Message);
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
            Debug.LogError("데이터 수신 오류: " + e.Message);
        }
    }

    void Update()
    {
        // 큐에 쌓인 메시지를 Unity 메인 스레드에서 출력
        while (messageQueue.TryDequeue(out string msg))
        {
            Debug.Log("서버로부터 수신: " + msg);
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
