using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>一股可单独调节的音频。</summary>
[Serializable]
public class AudioChannel
{
    [Tooltip("名字，方便识别（比如：铃声 / 切菜 Great / 开门）")]
    public string name = "音频";

    [Tooltip("这条音频对应的 AudioSource")]
    public AudioSource source;

    [Range(0f, 1f)]
    [Tooltip("这条音频自己的音量")]
    public float volume = 1f;

    [Tooltip("单独静音这条音频")]
    public bool mute = false;
}

/// <summary>
/// 全局音频控制器：
/// Master Volume 控制所有声音，下面的列表可以一条一条单独调节每条音频的音量。
/// 挂在任意常驻物体上即可；场景里没有列出来的 AudioSource 会自动收集进来。
/// </summary>
public class AudioVolumeController : MonoBehaviour
{
    public static AudioVolumeController Instance { get; private set; }

    [Header("总音量")]
    [Range(0f, 1f)]
    [Tooltip("统一控制所有声音的总音量")]
    public float masterVolume = 1f;

    [Tooltip("全部静音，不影响各条音频自己的音量数值")]
    public bool muteAll = false;

    [Header("各个音频（分别调节）")]
    public List<AudioChannel> channels = new List<AudioChannel>();

    [Header("自动收集")]
    [Tooltip("把场景里没列出来的 AudioSource 自动加进列表（音量 1）")]
    public bool autoCollectSources = true;

    [Tooltip("自动收集的扫描间隔（秒）")]
    public float rescanInterval = 1f;

    private float nextScanTime;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
        RescanIfNeeded();
    }

    private void OnValidate()
    {
        // 编辑器里拖动时可直接听到效果
        AudioListener.volume =
            muteAll ? 0f : Mathf.Clamp01(masterVolume);
    }

    // ====================================================================

    /// <summary>设置总音量（0~1）。</summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        Apply();
    }

    /// <summary>用 Slider 控制总音量。</summary>
    public void SetMasterVolume(Slider slider)
    {
        if (slider != null)
            SetMasterVolume(slider.value);
    }

    public void SetMuteAll(bool value)
    {
        muteAll = value;
        Apply();
    }

    public void ToggleMuteAll()
    {
        SetMuteAll(!muteAll);
    }

    /// <summary>单独设置某一条音频的音量（0~1）。</summary>
    public void SetChannelVolume(int index, float volume)
    {
        if (index < 0 || index >= channels.Count) return;

        if (channels[index] == null) return;

        channels[index].volume = Mathf.Clamp01(volume);
        Apply();
    }

    // ====================================================================

    private void Apply()
    {
        AudioListener.volume =
            muteAll ? 0f : Mathf.Clamp01(masterVolume);

        for (int i = 0; i < channels.Count; i++)
        {
            AudioChannel channel = channels[i];

            if (channel == null || channel.source == null) continue;

            channel.source.volume = channel.mute
                ? 0f
                : Mathf.Clamp01(channel.volume);
        }
    }

    private void RescanIfNeeded()
    {
        if (!autoCollectSources) return;

        if (Time.unscaledTime < nextScanTime) return;

        nextScanTime =
            Time.unscaledTime + Mathf.Max(0.25f, rescanInterval);

        ScanSources();
    }

    /// <summary>把场景里还没列出来的 AudioSource 全部加进列表。</summary>
    [ContextMenu("扫描场景里的 AudioSource")]
    public void ScanSources()
    {
        AudioSource[] all = FindObjectsOfType<AudioSource>(true);

        for (int i = 0; i < all.Length; i++)
        {
            AudioSource source = all[i];

            if (source == null) continue;
            if (HasChannel(source)) continue;

            channels.Add(new AudioChannel
            {
                name = source.gameObject.name,
                source = source,
                // 保留这条音频原本的音量，避免统一被拉满
                volume = Mathf.Clamp01(source.volume)
            });
        }
    }

    private bool HasChannel(AudioSource source)
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i] != null && channels[i].source == source)
                return true;
        }

        return false;
    }
}
