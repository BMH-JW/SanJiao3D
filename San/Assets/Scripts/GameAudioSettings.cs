using UnityEngine;

/// <summary>玩家音量设置（PlayerPrefs 持久化）。</summary>
public static class GameAudioSettings
{
    private const string MusicKey = "audio.musicVolume";
    private const string SfxKey = "audio.sfxVolume";

    public const float DefaultMusicVolume = 0.6f;
    public const float DefaultSfxVolume = 1f;

    /// <summary>背景音乐音量（0~1）。</summary>
    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat(MusicKey, DefaultMusicVolume); }
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>音效 / 提示音音量（0~1），不含背景音乐。</summary>
    public static float SfxVolume
    {
        get { return PlayerPrefs.GetFloat(SfxKey, DefaultSfxVolume); }
        set
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    /// <summary>取音乐音量，没有存档时用给定默认值。</summary>
    public static float GetMusicVolume(float fallback)
    {
        return PlayerPrefs.GetFloat(MusicKey, fallback);
    }

    /// <summary>取音效音量，没有存档时用给定默认值。</summary>
    public static float GetSfxVolume(float fallback)
    {
        return PlayerPrefs.GetFloat(SfxKey, fallback);
    }
}
