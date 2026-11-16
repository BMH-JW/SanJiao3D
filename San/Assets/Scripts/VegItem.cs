using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class VegItem : MonoBehaviour, IPointerClickHandler
{
    [Header("菜板参考（拖 VegSlot）")]
    public RectTransform boardCenter;

    [Header("飞行")]
    public float flyDuration = 0.35f;
    public bool moveToBoardParent = true;

    [Header("切割")]
    public float remainRatioPerCut = 0.7f;
    public float finishThreshold = 0.25f;

    public bool IsOnBoard { get; private set; }
    public float CurrentRatio { get; private set; } = 1f;

    private RectTransform rt;
    private Vector2 baseSize;
    private Vector2 originalLocalPos;
    private Transform originalParent;
    private CutGameManager manager;
    private Coroutine flyCoroutine;
    private Graphic vegGraphic;

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

        if (IsOnBoard)
        {
            // 已在菜板：蔬菜可能盖住动态线挡掉点击，这里直接转发给切菜逻辑
            if (manager != null) manager.OnCutClick(eventData.position);
            return;
        }

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
            // 放到菜板层级内，再拖到线后面（更靠下），避免被线视觉盖住
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

        // 到菜板后：可以保留 Raycast 给切菜层处理，这里不再自己抢事件；
        // 如果还是点到蔬菜不切，就临时关掉自身射线测试：
        // if (vegGraphic != null) vegGraphic.raycastTarget = false;

        if (manager == null) manager = FindObjectOfType<CutGameManager>();
        if (manager != null) manager.SetCurrentVeg(this);
    }

    public bool CutOnce()
    {
        CurrentRatio *= remainRatioPerCut;
        CurrentRatio = Mathf.Max(CurrentRatio, 0f);

        // 单边缩（纯本地坐标，不受父节点缩放影响）：固定左边缘 anchoredPosition，只从右边缩短
        float leftEdgeLocal = rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f;
        float newWidth = baseSize.x * CurrentRatio;
        rt.sizeDelta = new Vector2(newWidth, baseSize.y);
        rt.anchoredPosition = new Vector2(leftEdgeLocal + newWidth * 0.5f, rt.anchoredPosition.y);

        Debug.Log($"切一刀，剩余比例：{CurrentRatio:F2}，宽：{newWidth:F1}");
        return CurrentRatio > finishThreshold;
    }

    public void CenterOnBoard()
    {
        if (boardCenter != null)
            rt.position = boardCenter.position;
    }

    public void ResetVeg()
    {
        CurrentRatio = 1f;
        IsOnBoard = false;
        rt.sizeDelta = baseSize;

        if (originalParent != null)
            rt.SetParent(originalParent, false);

        rt.anchoredPosition = originalLocalPos;
        gameObject.SetActive(true);

        if (vegGraphic != null) vegGraphic.raycastTarget = true;
    }
}