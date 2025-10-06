// RmsBallVisualizer.cs
// TCPClient.OnWaveformBlockRms → rms[channelIndex]로 색/라벨 갱신(메인 스레드).
using UnityEngine;
using TMPro;
using System.Threading;
using System;

public class RmsBallVisualizer : MonoBehaviour
{
    [Header("References")]
    public TCPClient rhx;
    public Renderer targetRenderer;
    public TextMeshPro label;

    [Header("Channel")]
    public int channelIndex = 0;
    public int channelCount = 64;

    [Header("Color Mapping")]
    public float minRms = 6800f;
    public float maxRms = 12000f;
    [Range(0f, 1f)] public float smooth = 0.2f;
    public Gradient colorGradient;

    [Header("Units")]
    public double _lastRms = double.NaN;
    public double _graphRms = double.NaN;
    private float _displayed;
    private Material _matInstance;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogWarning("RmsBallVisualizer: targetRenderer is null!");
        }
        else
        {
            _matInstance = targetRenderer.material;
            Debug.Log("Awake 통과됨");
        }
   

        if (colorGradient == null || colorGradient.colorKeys == null || colorGradient.colorKeys.Length == 0)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.blue,   0f),
                    new GradientColorKey(Color.green,  0.33f),
                    new GradientColorKey(Color.yellow, 0.66f),
                    new GradientColorKey(Color.red,    1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            colorGradient = g;
        }
    }

    public void OnEnable()
    {
        if (rhx != null)
        {
            rhx.OnWaveformBlockRms += OnRmsBlock;
        }
    }
    public void OnDisable()
    {
        if (rhx != null) rhx.OnWaveformBlockRms -= OnRmsBlock;
        Debug.Log("OnDisable 실행됨.");
    }

    public void Start()
    {
        //OnEnable();
    }

    // 백그라운드 스레드에서 호출 → 값만 저장
    public void OnRmsBlock(double[] rms, int blockIndex, int firstTimestamp)
    {
        if (rms == null || rms.Length == 0) return;
        int idx = Mathf.Clamp(channelIndex, 0, rms.Length - 1);
        Volatile.Write(ref _lastRms, rms[idx]);
        Debug.Log($"[RMS] Channel {channelIndex}: {rms[idx]:F2}");
    }

    public void Update()
    {
        if (_matInstance == null) return;

        double last = Volatile.Read(ref _lastRms);

        if (double.IsNaN(last))
        {
            _matInstance.color = Color.gray;
            return;
        }

        float target = (float)last;

        // 색 변화를 더 민감하게 하려면 smoothing을 낮춤
        float localSmooth = Mathf.Clamp01(smooth); // 기존 0.2f
        _displayed = Mathf.Lerp(_displayed, target, localSmooth);

        // min/max 범위를 더 좁혀서 색 변화가 극적으로 보이도록
        float t = Mathf.InverseLerp(minRms, maxRms, _displayed);

        // optional: 곡선 적용 → 낮은 값에서 색 변화 더 크게
        t =t*t;  // 제곱근: 낮은 RMS에서 색 변화 증가
                                 // t = t*t; // 반대로 낮은 값에서 색 변화 줄이고 높은 값에서 강조

        _matInstance.color = colorGradient.Evaluate(t);

        // 라벨 갱신 (microvolt 단위 가능)
    }

    //public double GetCurrentRms()
    //{
    //    double val = Volatile.Read(ref _graphRms); // 읽기만
    //    Debug.Log($"GetCurrentRms 호출됨, _graphRms: {val}");
    //    return val;
    //}

}
