using System.Collections;
using UnityEngine;

/// <summary>
/// 两句提示依次出现：第一条出现 → 消失 → 间隔 → 第二条出现 → 消失。
/// 挂在常驻物体上（保持激活），把两条提示拖进来，调 Play() 触发。
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

    private Coroutine routine;

    private void Awake()
    {
        if (firstHint != null) firstHint.SetActive(false);
        if (secondHint != null) secondHint.SetActive(false);
    }

    /// <summary>播放两条提示。挂在 On Returned To First Person 上调用。</summary>
    public void Play()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

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