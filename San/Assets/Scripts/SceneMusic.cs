using System.Collections;
using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    [Tooltip("这个场景要播放的背景音乐")]
    [SerializeField] private AudioClip music;

    private AudioSource localSource;

    private void Start()
    {
        if (music == null) return;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.PlayMusic(music);
            return;
        }

        // 单独运行某个场景调试时，没有 SceneFader，本地放一份
        localSource = gameObject.AddComponent<AudioSource>();
        localSource.clip = music;
        localSource.loop = true;
        localSource.volume = GameAudioSettings.MusicVolume * AudioLoudness.GetGain(music);
        localSource.Play();
    }

    /// <summary>没有 SceneFader 时，直接调整本地背景音乐音量。</summary>
    public static void SetLocalVolume(float volume)
    {
        SceneMusic found = FindObjectOfType<SceneMusic>();
        if (found == null || found.localSource == null) return;

        found.localSource.volume =
            Mathf.Clamp01(volume) * AudioLoudness.GetGain(found.localSource.clip);
    }

    /// <summary>没有 SceneFader 时，淡出本地播放的背景音乐。</summary>
    public static void FadeOutLocal(float duration)
    {
        SceneMusic found = FindObjectOfType<SceneMusic>();
        if (found == null || found.localSource == null) return;
        found.StartCoroutine(found.FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        AudioSource source = localSource;
        if (source == null) yield break;

        float from = source.volume;
        float d = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            if (source == null) yield break;

            source.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / d));
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
    }
}