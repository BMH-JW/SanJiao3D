using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    /// <summary>是否正在做场景淡入淡出（含新场景的开幕淡入）。</summary>
    public bool IsTransitioning => isTransitioning;

    [Header("画面淡入淡出")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("音乐淡入淡出")]
    [SerializeField] private float musicFadeDuration = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.6f;

    private CanvasGroup canvasGroup;
    private AudioSource musicSource;
    private Coroutine musicRoutine;
    private Coroutine startFadeRoutine;
    private bool isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad 只对根物体生效
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        // 读取玩家保存的音乐音量
        musicVolume = Mathf.Clamp01(GameAudioSettings.GetMusicVolume(musicVolume));

        CreateOverlay();
        CreateMusicSource();
    }

    private void Start()
    {
        startFadeRoutine = StartCoroutine(StartFadeRoutine());
    }

    private IEnumerator StartFadeRoutine()
    {
        if (canvasGroup == null) yield break;

        // 开场从黑淡入
        canvasGroup.alpha = 1f;
        yield return Fade(0f);

        startFadeRoutine = null;
    }

    private void CreateOverlay()
    {
        GameObject canvasGo = new GameObject(
            "AutoFadeCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        GameObject imageGo = new GameObject("AutoFadeImage", typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);

        Image image = imageGo.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void CreateMusicSource()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
    }

    // 进入场景时调用：音乐淡入
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        if (musicSource.clip != clip)
        {
            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.volume = 0f;
            musicSource.Play();
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }

        StartMusicFade(musicVolume * AudioLoudness.GetGain(clip), musicFadeDuration, false);
    }

    // 跳转前调用：音乐淡出
    public void StopMusic(float duration = -1f)
    {
        if (musicSource == null) return;

        StartMusicFade(0f, duration < 0f ? musicFadeDuration : duration, true);
    }

    /// <summary>当前背景音乐音量。</summary>
    public float MusicVolume
    {
        get { return musicVolume; }
    }

    /// <summary>设置背景音乐音量并保存（设置面板用），立即生效。</summary>
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        GameAudioSettings.MusicVolume = musicVolume;

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }

        if (musicSource != null && musicSource.clip != null)
            musicSource.volume = musicVolume * AudioLoudness.GetGain(musicSource.clip);
    }

    private void StartMusicFade(float target, float duration, bool stopWhenDone)
    {
        if (musicRoutine != null)
            StopCoroutine(musicRoutine);

        musicRoutine = StartCoroutine(MusicFadeRoutine(target, duration, stopWhenDone));
    }

    private IEnumerator MusicFadeRoutine(float target, float duration, bool stopWhenDone)
    {
        float start = musicSource.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        musicSource.volume = target;

        if (stopWhenDone)
            musicSource.Stop();
    }

    public void FadeToScene(string sceneName)
    {
        if (isTransitioning) return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneFader: 场景名为空，请在按钮的 On Click 参数里填写目标场景名");
            return;
        }

        // 开场淡入还没播完就点了按钮：先停掉，避免两个淡变打架
        if (startFadeRoutine != null)
        {
            StopCoroutine(startFadeRoutine);
            startFadeRoutine = null;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        StartCoroutine(FadeRoutine(sceneName));
    }

    private IEnumerator FadeRoutine(string sceneName)
    {
        isTransitioning = true;

        StopMusic(fadeDuration); // 音乐和黑幕一起淡出
        yield return Fade(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        if (op == null)
        {
            Debug.LogError($"SceneFader: 场景 \"{sceneName}\" 加载失败，请检查是否已加入 Build Settings");
            yield return Fade(0f);
            isTransitioning = false;
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        yield return null; // 等新场景渲染一帧

        yield return Fade(0f); // 新场景淡入

        isTransitioning = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.blocksRaycasts = targetAlpha > 0f;

        float start = canvasGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
    }
}