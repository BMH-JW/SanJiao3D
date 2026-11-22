using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    private bool isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 关卡场景里如果重复放了淡入淡出 Canvas，销毁新的这份
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // DontDestroyOnLoad 只对根物体生效，挂在子物体上会被场景一起销毁
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void FadeToScene(string sceneName)
    {
        if (isTransitioning) return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneFader: 场景名为空");
            return;
        }

        StartCoroutine(FadeRoutine(sceneName));
    }

    private IEnumerator FadeRoutine(string sceneName)
    {
        isTransitioning = true;

        yield return Fade(1f); // 淡出到黑

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        if (op == null)
        {
            Debug.LogError($"SceneFader: 场景 \"{sceneName}\" 加载失败，请检查是否已加入 Build Settings");
            yield return Fade(0f); // 恢复画面，避免永久黑屏
            isTransitioning = false;
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        // 等新场景渲染出第一帧，再开始淡入
        yield return null;

        yield return Fade(0f); // 退掉黑幕

        isTransitioning = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (canvasGroup == null)
            yield break;

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