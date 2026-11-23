using System.Collections;
using UnityEngine;

/// <summary>
/// 两句提示依次出现：第一条出现 → 消失 → 间隔 → 第二条出现 → 消失。
/// ★ 只能触发一次，之后任何地方再调 Play() 都会被忽略。
/// 重置场景后自动解锁（RuntimeInitializeOnLoadMethod）。
/// </summary>
public class HintSequence : MonoBehaviour
{
    [Header("两条提示")]
    [Tooltip("第一条提示（默认隐藏）")]
    public GameObject firstHint;

    [Tooltip("第二条提示（默认隐藏）")]
    public GameObject secondHint;

    [Header("时间（秒）")]
    [Tooltip("第一条显示多久")]
    public float firstShowSeconds = 2f;

    [Tooltip("第一条消失后，隔多久出现第二条")]
    public float gapSeconds = 0.3f;

    [Tooltip("第二条显示多久")]
    public float secondShowSeconds = 2f;

    [Header("结束")]
    [Tooltip("第二条显示完后自动隐藏")]
    public bool hideSecondAtEnd = true;

    [Header("限制")]
    [Tooltip("本实例只能触发一次，之后不再响应 Play()")]
    public bool onlyPlayOnce = true;

    private Coroutine routine;
    private bool playedOnce;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetHintGate()
    {
        // 场景加载时解锁，避免跨场景状态残留
    }

    private void Awake()
    {
        if (firstHint != null) firstHint.SetActive(false);
        if (secondHint != null) secondHint.SetActive(false);
    }

    /// <summary>播放两条提示。挂在 EmotionRewardFly.OnReturnedToFirstPerson 上调用。</summary>
    public void Play()
    {
        if (onlyPlayOnce && playedOnce) return;   // ★ 一次锁
        playedOnce = true;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    /// <summary>手动重置（例如测试时重新播放）。</summary>
    public void Reset() { playedOnce = false; }

    private IEnumerator Run()
    {
        // 第一条
        if (firstHint != null) firstHint.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, firstShowSeconds));

        if (firstHint != null) firstHint.SetActive(false);

        // 间隔
        if (gapSeconds > 0f)
            yield return new WaitForSecondsRealtime(gapSeconds);

        // 第二条
        if (secondHint != null) secondHint.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, secondShowSeconds));

        if (hideSecondAtEnd && secondHint != null)
            secondHint.SetActive(false);

        routine = null;
    }
}