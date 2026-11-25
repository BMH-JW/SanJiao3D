using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>一股可单独调节的音频。</summary>
[Serializable]
public class AudioChannel
{
    [Tooltip("名字，方便识别（比如：铃声 / 切菜 Great / 开门）")]
    public string name = "音频";

    [Tooltip("这条音频对应的 AudioSource")]
    public AudioSource source;

    [Tooltip("也可以直接拖音频文件：运行时会自动找到正在使用这个音频的 AudioSource（适合运行时才创建的铃声等）")]
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("这条音频自己的音量")]
    public float volume = 1f;

    [Tooltip("单独静音这条音频")]
    public bool mute = false;

    [NonSerialized] public AudioSource resolvedSource;
}

/// <summary>
/// 全局音频控制器：
/// Master Volume 控制所有声音，下面的列表可以一条一条单独调节每条音频的音量。
/// 挂在任意常驻物体上即可；场景里没有列出来的 AudioSource 会自动收集进来。
/// </summary>
public class AudioVolumeController : MonoBehaviour
{
    public static AudioVolumeController Instance { get; private set; }

    [Header("音效总音量")]
    [Range(0f, 1f)]
    [Tooltip("统一控制所有音效 / 提示音的音量（背景音乐不受影响，由 SceneFader 控制）")]
    [FormerlySerializedAs("masterVolume")]
    public float sfxVolume = 1f;

    [Tooltip("全部静音，不影响各条音频自己的音量数值")]
    public bool muteAll = false;

    [Header("各个音频（分别调节）")]
    public List<AudioChannel> channels = new List<AudioChannel>();

    [Header("响度统一")]
    [Tooltip("按每个音频文件的响度自动衰减，让不同来源的音效/音乐听起来一样响")]
    public bool normalizeLoudness = true;

    [Header("自动收集")]
    [Tooltip("把场景里没列出来的 AudioSource 自动加进列表（音量 1）")]
    public bool autoCollectSources = true;

    [Tooltip("自动收集的扫描间隔（秒）")]
    public float rescanInterval = 1f;

    private float nextScanTime;

    private void Awake()
    {
        Instance = this;
        AudioLoudness.Enabled = normalizeLoudness;

        // 读取玩家保存的音效音量
        sfxVolume = Mathf.Clamp01(GameAudioSettings.GetSfxVolume(sfxVolume));
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
        AudioLoudness.Enabled = normalizeLoudness;

        // 编辑器里拖动时可直接听到效果
        AudioListener.volume = muteAll ? 0f : 1f;
    }

    // ====================================================================

    /// <summary>设置音效总音量（0~1）并保存。</summary>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        GameAudioSettings.SfxVolume = sfxVolume;
        Apply();
    }

    /// <summary>用 Slider 控制音效总音量。</summary>
    public void SetSfxVolume(Slider slider)
    {
        if (slider != null)
            SetSfxVolume(slider.value);
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
        // 静音时才动 AudioListener（它会影响背景音乐，平时保持 1）
        AudioListener.volume = muteAll ? 0f : 1f;

        AudioLoudness.Enabled = normalizeLoudness;

        for (int i = 0; i < channels.Count; i++)
        {
            AudioChannel channel = channels[i];

            if (channel == null) continue;

            AudioSource target = channel.source != null
                ? channel.source
                : channel.resolvedSource;

            if (target == null) continue;

            // 同一条音量设置下，按音频本身的响度再衰减一次，做到各音源听感一致
            float gain = AudioLoudness.GetGain(target.clip);

            target.volume = channel.mute
                ? 0f
                : Mathf.Clamp01(channel.volume * sfxVolume * gain);
        }
    }

    private void RescanIfNeeded()
    {
        if (Time.unscaledTime < nextScanTime) return;

        nextScanTime =
            Time.unscaledTime + Mathf.Max(0.25f, rescanInterval);

        // 只拖了音频文件、没有拖 AudioSource 的通道，按 Clip 自动绑定
        BindClipChannels();

        if (!autoCollectSources) return;

        ScanSources();
    }

    /// <summary>给只填了音频文件的通道找到对应的 AudioSource（运行时创建的也能找到）。</summary>
    private void BindClipChannels()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            AudioChannel channel = channels[i];
            if (channel == null) continue;

            if (channel.source != null || channel.clip == null)
            {
                channel.resolvedSource = null;
                continue;
            }

            AudioSource bound = channel.resolvedSource;
            if (bound != null && bound.clip == channel.clip)
                continue;

            channel.resolvedSource = FindSourceByClip(channel.clip);
        }
    }

    private static AudioSource FindSourceByClip(AudioClip clip)
    {
        AudioSource[] all = FindObjectsOfType<AudioSource>(true);

        for (int i = 0; i < all.Length; i++)
        {
            AudioSource source = all[i];
            if (source == null || source.clip != clip) continue;

            // 常驻物体上的背景音乐由 SceneFader 控制，不接管
            if (source.gameObject.scene.name == "DontDestroyOnLoad") continue;

            return source;
        }

        return null;
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

            // 常驻物体（如 SceneFader 的背景音乐）由它自己控制音量，
            // 不要收进来，否则淡入时捕获到的 0 会被每帧写回去，音乐永远听不到
            if (source.gameObject.scene.name == "DontDestroyOnLoad") continue;

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
            AudioChannel channel = channels[i];
            if (channel == null) continue;

            if (channel.source == source || channel.resolvedSource == source)
                return true;
        }

        return false;
    }
}