using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 项目内音频响度统一：
/// 按每个 AudioClip 的实际 RMS 响度算出一个衰减系数，
/// 让不同来源（下载的音效、自己录的、音乐）听起来大小一致。
///
/// 只做衰减不做放大（AudioSource.volume 上限是 1），
/// 所以目标定得比大多数素材低一点，再由 Master Volume 统一抬高。
/// </summary>
public static class AudioLoudness
{
    /// <summary>是否启用响度统一。</summary>
    public static bool Enabled = true;

    /// <summary>统一目标 RMS（约 -23 dBFS）。素材比它响就衰减，比它轻就保持原样。</summary>
    public static float TargetRms = 0.07f;

    /// <summary>峰值上限，给足余量避免削波。</summary>
    public static float PeakCeiling = 0.95f;

    /// <summary>最多衰减到原音量的这个比例，防止把声音压没。</summary>
    public static float MinGain = 0.08f;

    /// <summary>分析时长上限（秒），长音乐只取一段计算，避免占内存。</summary>
    public const float MaxAnalyzeSeconds = 20f;

    public static bool Verbose = true;

    private static readonly Dictionary<AudioClip, float> gainCache =
        new Dictionary<AudioClip, float>();

    /// <summary>取这个 AudioClip 的响度修正系数（0.08~1）。</summary>
    public static float GetGain(AudioClip clip)
    {
        if (!Enabled || clip == null) return 1f;
        if (gainCache.TryGetValue(clip, out float cached)) return cached;

        float gain = ComputeGain(clip);
        gainCache[clip] = gain;

        if (Verbose)
        {
            Debug.Log($"[AudioLoudness] {clip.name} → 衰减系数 {gain:0.00}");
        }

        return gain;
    }

    /// <summary>统一响度地播放一次性音效（volumeScale 仍然是设计时音量）。</summary>
    public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1f)
    {
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale * GetGain(clip)));
    }

    public static void ClearCache()
    {
        gainCache.Clear();
    }

    private static float ComputeGain(AudioClip clip)
    {
        int channels = Mathf.Max(1, clip.channels);
        int frequency = Mathf.Max(1, clip.frequency);
        int totalFrames = clip.samples;

        if (totalFrames <= 0) return 1f;

        int framesToRead = Mathf.Min(totalFrames, Mathf.RoundToInt(frequency * MaxAnalyzeSeconds));
        if (framesToRead <= 0) return 1f;

        // 从 1/4 处开始取，避开前奏/静音段
        int maxOffset = Mathf.Max(0, totalFrames - framesToRead);
        int offsetFrames = Mathf.Clamp(maxOffset / 4, 0, maxOffset);

        float[] buffer = new float[framesToRead * channels];

        // Streaming / 压缩在内存里的音频读不到数据，保持原样
        if (!clip.GetData(buffer, offsetFrames)) return 1f;

        double sum = 0d;
        float peak = 0f;

        for (int i = 0; i < buffer.Length; i++)
        {
            float v = buffer[i];
            sum += (double)v * v;
            float a = Mathf.Abs(v);
            if (a > peak) peak = a;
        }

        float rms = Mathf.Sqrt((float)(sum / buffer.Length));
        if (rms < 0.0001f) return 1f;

        float gain = TargetRms / rms;

        if (peak > 0.0001f)
            gain = Mathf.Min(gain, PeakCeiling / peak);

        return Mathf.Clamp(gain, MinGain, 1f);
    }
}
