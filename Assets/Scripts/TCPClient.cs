// TCPClient.cs
// ����ȭ ����Ʈ:
// - ��Ŀ �����忡�� Unity API ���� ȣ�� ����(Stopwatch ���)
// - �ڵ� �翬�� + ��ġ��(������ Ÿ�Ӿƿ�)
// - ���� Ʃ��(����, Nagle off, KeepAlive* try/catch)
// - �ߺ� �翬�� ���� �� ���� ���� ����
//
// Unity 2020~2023 / .NET 4.x / IL2CPP ȣȯ

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
// �浹 ����: Stopwatch�� System.Diagnostics, �α״� Unity Debug
using SystemDiag = System.Diagnostics;
using Debug = UnityEngine.Debug;

public class TCPClient : MonoBehaviour
{
    [Header("Connect")]
    public bool connectOnStart = true;
    public int connectTimeoutMs = 4000;

    [Header("RHX Address")]
    public string host = "192.168.228.240";
    public int commandPort = 5000;
    public int waveformPort = 5001;

    [Header("Channels")]
    public int channels = 64;
    public bool enableSpikeOutput = false;

    [Header("Resilience / Tuning")]
    public bool autoReconnect = true;
    public int reconnectDelayMs = 2000;      // ù ��õ� ����
    public float watchdogTimeoutSec = 5f;        // �� �ð� ���� ��� �̼��� �� �翬��
    public int recvBufferBytes = 1 << 20;   // 1MB
    public int sendBufferBytes = 1 << 16;   // 64KB
    public bool tcpKeepAlive = true;      // �Ϻ� �÷���(UWP)������ ���õ� �� ����

    // ��ϴ� RMS �̺�Ʈ(���ν����� �ƴ�! ���� �����忡�� ȣ��)
    public event Action<double[], int, int> OnWaveformBlockRms;

    // ����
    private TcpClient _cmdClient, _waveClient;
    private NetworkStream _cmdStream, _waveStream;
    private RHXWaveformClient _waveWorker;
    private CancellationTokenSource _cts;

    private volatile int _blockCounter;

    // ��ġ��: ��Ŀ���� ������Ʈ(Stopwatch ���, ������ ������)
    private long _lastTick = SystemDiag.Stopwatch.GetTimestamp();
    private static readonly double TickToSec = 1.0 / SystemDiag.Stopwatch.Frequency;

    private bool _reconnecting;
    private bool _shuttingDown;
    private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1); // ConnectAll serialize

    // �� ��ü �Լ�

    private async void Start()
    {
        Application.runInBackground = true; // ��Ŀ�� �Ҿ ����
        if (connectOnStart) await ConnectAllAndRun();
        InvokeRepeating(nameof(WatchdogTick), 2f, 2f);
    }

    private async void OnDestroy()
    {
        _shuttingDown = true;
        CancelInvoke(nameof(WatchdogTick));
        await DisconnectAll();
        _connectGate.Dispose();
    }

    [ContextMenu("Connect All + Run")]
    public async void ConnectAllAndRunContext() => await ConnectAllAndRun();

    private async Task ConnectAllAndRun()
    {
        try
        {
            await ConnectAll();
            await StartRoutineAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCPClient] ConnectAllAndRun failed: {ex.Message}");
            if (autoReconnect && !_shuttingDown) _ = StartReconnectLoop("connect failure");
        }
    }

    private async Task<TcpClient> ConnectTcpAsync(string h, int port, int timeoutMs, string tag)
    {
        IPAddress[] addrs;
        if (h == "127.0.0.1") addrs = new[] { IPAddress.Loopback };
        else if (h.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            addrs = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback };
        else addrs = await Dns.GetHostAddressesAsync(h);

        Exception last = null;
        foreach (var a in addrs)
        {
            var cli = new TcpClient(a.AddressFamily);
            try
            {
                var t = cli.ConnectAsync(a, port);
                var done = await Task.WhenAny(t, Task.Delay(timeoutMs));
                if (done != t) throw new TimeoutException($"Timeout {timeoutMs}ms");

                // ���� Ʃ��
                cli.NoDelay = true;
                try
                {
                    cli.ReceiveBufferSize = recvBufferBytes;
                    cli.SendBufferSize = sendBufferBytes;
                    cli.LingerState = new LingerOption(false, 0);
                    if (tcpKeepAlive) cli.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch { /* �Ϻ� �÷������� ������ �ɼ� ���� */ }

                return cli;
            }
            catch (Exception ex)
            {
                last = ex;
                try { cli.Close(); } catch { }
                Debug.LogWarning($"[TCPClient] {tag}: connect {a}:{port} failed: {ex.Message}");
            }
        }
        throw last ?? new SocketException((int)SocketError.NotConnected);
    }

    public async Task ConnectAll()
    {
        await _connectGate.WaitAsync();
        try
        {
            await DisconnectAll(); // �׻� ������

            if (_shuttingDown) return;
            _cts = new CancellationTokenSource();

            _cmdClient = await ConnectTcpAsync(host, commandPort, connectTimeoutMs, "Command");
            _cmdStream = _cmdClient.GetStream();
            Debug.Log($"[TCPClient] Command connected {host}:{commandPort}");

            _waveClient = await ConnectTcpAsync(host, waveformPort, connectTimeoutMs, "Waveform");
            _waveStream = _waveClient.GetStream();
            Debug.Log($"[TCPClient] Waveform connected {host}:{waveformPort}");

            _waveWorker = new RHXWaveformClient(_waveStream, channels);
            _waveWorker.OnBlockParsed += HandleWaveformBlock; // ��Ŀ ������ (Unity API ȣ�� ����)
            _waveWorker.OnStreamFault += ex =>
            {
                Debug.LogWarning($"[TCPClient] Waveform stream fault: {ex.GetType().Name} {ex.Message}");
                if (autoReconnect && !_shuttingDown) _ = StartReconnectLoop("stream fault");
            };

            // ���� ������(LongRunning)�� ���� (������/GC ���� �ּ�ȭ)
            _ = Task.Factory.StartNew(async () =>
            {
                try
                {
                    try { Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal; } catch { }
                    await _waveWorker.RunAsync(_cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TCPClient] Worker exit: {ex.Message}");
                    if (autoReconnect && !_shuttingDown) _ = StartReconnectLoop("worker exit");
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

            // ��ġ�� �ʱ�ȭ(Stopwatch)
            Interlocked.Exchange(ref _lastTick, SystemDiag.Stopwatch.GetTimestamp());
            _reconnecting = false;

            Debug.Log("[TCPClient] All sockets connected; worker running.");
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async Task DisconnectAll()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;

        try { _waveWorker?.Dispose(); } catch { }
        _waveWorker = null;

        try { _waveStream?.Dispose(); } catch { }
        _waveStream = null;

        try { _waveClient?.Close(); } catch { }
        _waveClient = null;

        try { _cmdStream?.Dispose(); } catch { }
        _cmdStream = null;

        try { _cmdClient?.Close(); } catch { }
        _cmdClient = null;

        await Task.Yield();
    }

    public async Task StartRoutineAsync()
    {
        if (_cmdStream == null) return;

        await SendCommandAsync("execute clearalldataoutputs;");
        for (int ch = 0; ch < channels; ch++)
        {
            string name_a = $"a-{ch:000}";
            string cmd = enableSpikeOutput
                ? $"set {name_a}.tcpdataoutputenabled true; set {name_a}.tcpdataoutputenabledspike true;"
                : $"set {name_a}.tcpdataoutputenabled true;";
            await SendCommandAsync(cmd);
        }
        await SendCommandAsync("set runmode run;");
        Debug.Log("[TCPClient] Routine sent (clear �� enable �� run).");
    }

    public async Task StopRoutineAsync()
    {
        if (_cmdStream == null) return;
        await SendCommandAsync("set runmode stop;");
    }

    public async Task SendCommandAsync(string cmd)
    {
        if (_cmdStream == null) return;
        var bytes = Encoding.ASCII.GetBytes(cmd);
        try
        {
            await _cmdStream.WriteAsync(bytes, 0, bytes.Length);
            await _cmdStream.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCPClient] SendCommand error: {ex.Message}");
        }
    }

    // === ��Ŀ ������ �ݹ�(����: Unity API ����) ===
    private void HandleWaveformBlock(double[] rmsPerChannel, int firstFrameTimestamp)
    {
        Interlocked.Exchange(ref _lastTick, SystemDiag.Stopwatch.GetTimestamp()); // ��ġ�� ����
        int idx = Interlocked.Increment(ref _blockCounter);

        try { OnWaveformBlockRms?.Invoke(rmsPerChannel, idx, firstFrameTimestamp); }
        catch (Exception ex) { Debug.LogWarning($"[TCPClient] RMS listener error: {ex.Message}"); }
    }

    // ���� ������ �ֱ� üũ
    private void WatchdogTick()
    {
        if (!autoReconnect || _reconnecting || _waveWorker == null) return;

        long last = Interlocked.Read(ref _lastTick);
        double elapsedSec = (SystemDiag.Stopwatch.GetTimestamp() - last) * TickToSec;

        if (elapsedSec > watchdogTimeoutSec)
        {
            Debug.LogWarning($"[TCPClient] Watchdog: no blocks for {elapsedSec:F1}s -> reconnect.");
            _ = StartReconnectLoop("watchdog");
        }
    }

    private async Task StartReconnectLoop(string reason)
    {
        if (_reconnecting || _shuttingDown) return;
        _reconnecting = true;

        int delay = reconnectDelayMs;
        while (autoReconnect && !_shuttingDown)
        {
            Debug.Log($"[TCPClient] Reconnecting ({reason}) in {delay} ms...");
            await Task.Delay(delay);
            try
            {
                await ConnectAll();
                await StartRoutineAsync();
                Debug.Log("[TCPClient] Reconnected.");
                _reconnecting = false;
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TCPClient] Reconnect failed: {ex.Message}");
                delay = Mathf.Min(delay * 2, 10000); // 2s �� 4s �� ... �� 10s
            }
        }
        _reconnecting = false;
    }
    public bool IsConnected
    {
        get
        {
            bool cmdConnected = _cmdClient != null && _cmdClient.Connected;
            bool waveConnected = _waveClient != null && _waveClient.Connected;
            bool workerRunning = _waveWorker != null && _waveWorker.IsRunning;
            return cmdConnected && waveConnected && workerRunning;
        }
    }

}

