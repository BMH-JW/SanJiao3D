using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    [Tooltip("这个场景要播放的背景音乐")]
    [SerializeField] private AudioClip music;

    private void Start()
    {
        if (music == null) return;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.PlayMusic(music);
            return;
        }

        // 单独运行某个场景调试时，没有 SceneFader，本地放一份
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.volume = 1f;
        source.Play();
    }
}