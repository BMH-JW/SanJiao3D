using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StorySequence : MonoBehaviour
{
    [System.Serializable]
    public class Line
    {
        [Tooltip("这一句已经排好的文字对象，直接从场景拖进来")]
        public Text subtitleObject;

        [Tooltip("这一句停留多少秒后播下一句；0 表示等玩家点击")]
        public float duration = 2f;
    }

    [System.Serializable]
    public class Slide
    {
        [Tooltip("这一张剧情图对应的场景物体（场景1、场景2…）")]
        public GameObject slideObject;

        public Line[] lines;

        [Tooltip("每句播完后的停顿秒数，1~2 秒比较自然")]
        public float lineGap = 1.5f;

        [Tooltip("没配置任何句子时这张图停留的秒数；0 表示等点击")]
        public float duration = 3f;
    }

    [Header("UI 引用")]
    [SerializeField] private Button skipButton;

    [Header("剧情音乐")]
    [Tooltip("整段剧情播放的背景音乐，循环播放")]
    [SerializeField] private AudioClip storyMusic;
    [SerializeField] private AudioSource bgmSource;

    [Header("剧情数据")]
    [SerializeField] private Slide[] slides;

    [Tooltip("第一句字幕开始前的等待时间；0 则立即开始")]
    [SerializeField] private float initialDelay = 2.5f;

    [Header("关卡场景")]
#if UNITY_EDITOR
    [Tooltip("从 Project 窗口把关卡场景文件拖到这里")]
    [SerializeField] private SceneAsset levelScene;
#endif
    [HideInInspector, SerializeField] private string levelSceneName;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelScene != null)
            levelSceneName = levelScene.name;
    }
#endif

    private bool skipRequested;

    private void Start()
    {
        if (slides == null || slides.Length == 0)
        {
            Debug.LogWarning("StorySequence 没有配置剧情，直接进入关卡");
            LoadLevel();
            return;
        }

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        HideAllSubtitles();

        // 先关掉所有剧情图，只显示第一张
        ShowSlide(0);

        PlayStoryMusic();

        StartCoroutine(PlayRoutine());
    }

    private void PlayStoryMusic()
    {
        if (bgmSource == null || storyMusic == null) return;

        bgmSource.clip = storyMusic;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    private IEnumerator PlayRoutine()
    {
        // 第一句开始前的等待，期间点跳过可提前开始
        skipRequested = false;
        float t = 0f;
        while (t < initialDelay && !skipRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < slides.Length; i++)
        {
            ShowSlide(i);
            skipRequested = false;

            Slide slide = slides[i];

            if (slide.lines != null && slide.lines.Length > 0)
            {
                for (int j = 0; j < slide.lines.Length; j++)
                {
                    Line line = slide.lines[j];
                    ShowLine(line);

                    float timer = 0f;
                    while (!skipRequested && (line.duration <= 0f || timer < line.duration))
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }

                    if (skipRequested) break;

                    float gapEnd = Time.time + slide.lineGap;
                    while (!skipRequested && Time.time < gapEnd)
                        yield return null;

                    if (skipRequested) break;
                }
            }
            else
            {
                float timer = 0f;
                while (!skipRequested && (slide.duration <= 0f || timer < slide.duration))
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
            }
        }

        LoadLevel();
    }

    private void ShowSlide(int index)
    {
        // 只激活当前这张，其余全部隐藏
        foreach (Slide slide in slides)
        {
            if (slide.slideObject != null)
                slide.slideObject.SetActive(false);
        }

        if (slides[index].slideObject != null)
            slides[index].slideObject.SetActive(true);

        // 换图时不清空字幕
    }

    private void ShowLine(Line line)
    {
        if (line.subtitleObject != null)
            line.subtitleObject.gameObject.SetActive(true);
    }

    private void HideAllSubtitles()
    {
        if (slides == null) return;

        foreach (Slide slide in slides)
        {
            if (slide.lines == null) continue;

            foreach (Line line in slide.lines)
            {
                if (line.subtitleObject != null)
                    line.subtitleObject.gameObject.SetActive(false);
            }
        }
    }

    // 绑定到「跳过剧情」按钮的 On Click ()
    public void OnSkipClicked()
    {
        skipRequested = true;
    }

    private void LoadLevel()
    {
        if (bgmSource != null)
            bgmSource.Stop();

        if (string.IsNullOrEmpty(levelSceneName))
        {
            Debug.LogWarning("没有拖入关卡场景，请检查 Inspector 的 Level Scene");
            return;
        }

        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(levelSceneName);
        else
            SceneManager.LoadScene(levelSceneName);
    }
}