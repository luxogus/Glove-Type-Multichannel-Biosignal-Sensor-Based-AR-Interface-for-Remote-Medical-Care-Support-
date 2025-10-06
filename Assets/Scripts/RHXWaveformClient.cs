// RHXWaveformClient.cs
// Zero-GC block parser: 미리 할당한 버퍼 재사용, 안전 이벤트 디스패치, 스트림 오류 통지.

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class RHXWaveformClient : IDisposable
{
    private const uint MAGIC_WAVEFORM = 0x2ef07a08;
    private const int FRAMES_PER_BLOCK = 128;

    private readonly NetworkStream _stream;
    private readonly int _channels;

    private readonly int _bytesPerFrame;   // 4(timestamp) + 2*channels
    private readonly int _bytesPerBlock;   // 4(magic) + frames*bytesPerFrame

    // 재사용 버퍼(할당 1회)
    private readonly byte[] _blockBuffer;
    private readonly double[] _sumSq;
    private readonly double[] _rms;

    public event Action<double[], int> OnBlockParsed; // (rms[], firstTimestamp)
    public event Action<Exception> OnStreamFault;

    public bool IsRunning { get; private set; }

    public RHXWaveformClient(NetworkStream stream, int channels)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (channels <= 0 || channels > 256) throw new ArgumentOutOfRangeException(nameof(channels));
        _channels = channels;
        _bytesPerFrame = 4 + 2 * _channels;
        _bytesPerBlock = 4 + FRAMES_PER_BLOCK * _bytesPerFrame;

        _blockBuffer = new byte[_bytesPerBlock];
        _sumSq = new double[_channels]; // buffer overflow 방지용
        _rms = new double[_channels];
    }

    public async Task RunAsync(CancellationToken ct)
    {
        IsRunning = true;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 1) 매직 정렬
                uint magic = await ReadMagicAlignedAsync(ct);
                if (magic != MAGIC_WAVEFORM) continue;

                // 2) 나머지 블록 읽기
                int payload = FRAMES_PER_BLOCK * _bytesPerFrame;
                await ReadExactAsync(_blockBuffer, 4, payload, ct);

                // 3) RMS 계산(누적-제곱합 → 루트), 1블록 끝나면 즉시 폐기(Zero-GC)
                Array.Clear(_sumSq, 0, _sumSq.Length);
                int idx = 4;
                int firstTs = 0;

                for (int f = 0; f < FRAMES_PER_BLOCK; f++)
                {
                    int ts = ReadInt32LE(_blockBuffer, idx); idx += 4;
                    if (f == 0) firstTs = ts;

                    for (int ch = 0; ch < _channels; ch++)
                    {
                        short s = ReadInt16LE(_blockBuffer, idx); idx += 2;
                        double v = s; // counts
                        v = 0.195 * (v - 32768);
                        _sumSq[ch] += v * v;
                    }
                }
                for (int ch = 0; ch < _channels; ch++)
                    _rms[ch] = Math.Sqrt(_sumSq[ch] / FRAMES_PER_BLOCK);

                try { OnBlockParsed?.Invoke(_rms, firstTs); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning($"[RHXWaveformClient] Listener error: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) { /* 정상 종료 */ }
        catch (IOException io) { OnStreamFault?.Invoke(io); }
        catch (ObjectDisposedException od) { OnStreamFault?.Invoke(od); }
        catch (Exception ex) { OnStreamFault?.Invoke(ex); }
        finally
        {
            IsRunning = false;
        }
    }

    // 4바이트 매직 재동기화(슬라이딩 윈도)
    private async Task<uint> ReadMagicAlignedAsync(CancellationToken ct)
    {
        await ReadExactAsync(_blockBuffer, 0, 4, ct);
        uint val = ToUInt32LE(_blockBuffer, 0);
        if (val == MAGIC_WAVEFORM) return val;

        byte b0 = _blockBuffer[0], b1 = _blockBuffer[1], b2 = _blockBuffer[2], b3 = _blockBuffer[3];
        while (true)
        {
            b0 = b1; b1 = b2; b2 = b3;
            int rb = await _stream.ReadAsync(_blockBuffer, 0, 1, ct);
            if (rb != 1) throw new IOException("Stream ended while seeking magic.");
            b3 = _blockBuffer[0];

            uint m = (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
            if (m == MAGIC_WAVEFORM)
            {
                _blockBuffer[0] = b0; _blockBuffer[1] = b1; _blockBuffer[2] = b2; _blockBuffer[3] = b3;
                return m;
            }
        }
    }

    private async Task ReadExactAsync(byte[] buf, int off, int count, CancellationToken ct)
    {
        int read = 0;
        while (read < count)
        {
            int n = await _stream.ReadAsync(buf, off + read, count - read, ct);
            if (n <= 0) throw new IOException("Stream closed.");
            read += n;
        }
    }

    private static short ReadInt16LE(byte[] b, int i) => unchecked((short)(b[i] | (b[i + 1] << 8)));
    private static int ReadInt32LE(byte[] b, int i) => unchecked(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
    private static uint ToUInt32LE(byte[] b, int i) => unchecked((uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24)));

    public void Dispose()
    {
        try { _stream?.Dispose(); } catch { }
    }

}
