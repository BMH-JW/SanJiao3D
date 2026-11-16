using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class VegItem : MonoBehaviour, IPointerClickHandler
{
    [Header("菜板参考")]
    public RectTransform boardCenter;

    [Header("飞行")]
    public float flyDuration = 0.35f;
    public bool moveToBoardParent = true;

    [Header("切割")]
    public int targetCutCount = 3;   // 本蔬菜总共要切几次（每蔬菜独立，次数可不同）
    public float finishRatio = 0.2f; // 切完后剩余长度比例（0.2 = 还剩 20%）

    public bool IsOnBoard { get; private set; }
    public bool IsCutDone { get; private set; }
    public int CurrentCutCount { get; private set; }
    public float CurrentRatio { get; private set; } = 1f;

    private RectTransform rt;
    private Vector2 baseSize;
    private Vector2 originalLocalPos;
    private Transform originalParent;
    private CutGameManager manager;
    private Coroutine flyCoroutine;
    private Graphic vegGraphic;
    private float fixedLeftEdgeLocal;   // 落板后锁的左边缘（本地坐标）

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseSize = rt.sizeDelta;
        originalLocalPos = rt.anchoredPosition;
        originalParent = rt.parent;
        manager = FindObjectOfType<CutGameManager>();
        vegGraphic = GetComponent<Graphic>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"点击蔬菜：{name}，IsOnBoard={IsOnBoard}");

        if (manager != null && manager.CurrentPhase == CutGameManager.Phase.Plating)
        {
            Debug.Log("盘子还没清空，不能切下一个蔬菜");
            return;
        }

        if (IsOnBoard)
        {
            if (manager != null) manager.OnCutClick(eventData.position);
            return;
        }

        StartFly();
    }

    // 给 CutGameManager 调用：从原位飞到菜板（自动下一个蔬菜时用）
    public void StartFly()
    {
        if (boardCenter == null || !boardCenter.gameObject.activeInHierarchy)
        {
            Debug.LogError("Board Center 为空或未激活");
            return;
        }

        if (flyCoroutine != null) StopCoroutine(flyCoroutine);
        flyCoroutine = StartCoroutine(FlyToBoard());
    }

    IEnumerator FlyToBoard()
    {
        Vector3 startWorld = rt.position;
        Vector3 targetWorld = boardCenter.position;

        if (moveToBoardParent)
        {
            rt.SetParent(boardCenter.parent, true);
            rt.SetAsLastSibling();
        }

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.unscaledDeltaTime;
            rt.position = Vector3.Lerp(startWorld, targetWorld, Mathf.Clamp01(t / flyDuration));
            yield return null;
        }

        rt.position = targetWorld;
        IsOnBoard = true;

        if (manager == null) manager = FindObjectOfType<CutGameManager>();
        if (manager != null) manager.SetCurrentVeg(this);
    }

    // 蔬菜落板后，由 manager 调一次：记录落板位置作为单边缩的固定左边缘
    public void LockLeftEdge()
    {
        fixedLeftEdgeLocal = rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f;
    }

    // 切一刀：按切次数线性缩短（targetCutCount 次后缩到 finishRatio）
    // 返回 true = 还没切完；false = 切完
    public bool CutOnce()
    {
        CurrentCutCount++;
        float totalShrink = 1f - finishRatio;
        float perCut = totalShrink / Mathf.Max(1, targetCutCount);
        CurrentRatio = Mathf.Clamp01(1f - CurrentCutCount * perCut);

        float newWidth = baseSize.x * CurrentRatio;
        rt.sizeDelta = new Vector2(newWidth, baseSize.y);
        rt.anchoredPosition = new Vector2(fixedLeftEdgeLocal + newWidth * 0.5f, rt.anchoredPosition.y);

        IsCutDone = CurrentCutCount >= targetCutCount;
        Debug.Log($"[{name}] 切第 {CurrentCutCount}/{targetCutCount} 刀，剩余 {CurrentRatio:F2}，宽 {newWidth:F1}，切完={IsCutDone}");
        return !IsCutDone;
    }

    public void ResetVeg()
    {
        CurrentCutCount = 0;
        CurrentRatio = 1f;
        IsCutDone = false;
        IsOnBoard = false;
        rt.sizeDelta = baseSize;

        if (originalParent != null)
            rt.SetParent(originalParent, false);

        rt.anchoredPosition = originalLocalPos;
        gameObject.SetActive(true);

        if (vegGraphic != null) vegGraphic.raycastTarget = true;
    }
}
