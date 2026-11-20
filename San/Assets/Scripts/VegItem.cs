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
    public int targetCutCount = 3;
    public float finishRatio = 0.2f;

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
    private Image vegImage;
    private bool useImageFill;
    private float fixedLeftEdgeLocal;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseSize = rt.sizeDelta;
        originalLocalPos = rt.anchoredPosition;
        originalParent = rt.parent;
        vegGraphic = GetComponent<Graphic>();
        manager = ResolveManager();

        // 用 Filled 模式裁掉一部分贴图，切一刀就少一截，而不是把整张图压扁
        vegImage = GetComponent<Image>();
        if (vegImage != null && vegImage.sprite != null)
        {
            vegImage.type = Image.Type.Filled;
            vegImage.fillMethod = Image.FillMethod.Horizontal;
            vegImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            vegImage.fillAmount = 1f;
            useImageFill = true;
        }
    }

    // 先找激活的，再找未激活的并自动激活
    private CutGameManager ResolveManager()
    {
        if (manager != null) return manager;

        manager = FindObjectOfType<CutGameManager>();
        if (manager != null) return manager;

#if UNITY_2020_1_OR_NEWER
        CutGameManager inactive = FindObjectOfType<CutGameManager>(true);
        if (inactive != null)
        {
            Debug.LogWarning("[VegItem] CutGameManager 所在物体未激活，已自动激活：" + GetPath(inactive.transform), inactive);
            inactive.gameObject.SetActive(true);
            manager = inactive;
            return manager;
        }
#endif

        Debug.LogError("[VegItem] 场景里找不到 CutGameManager 组件。请把 CutGameManager 挂到 2DCanvas 上，并确认它没有被 Deactivate On 2D 关掉。", this);
        return null;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("点击蔬菜：" + name + "，IsOnBoard=" + IsOnBoard);

        if (IsOnBoard)
        {
            if (manager == null) manager = ResolveManager();
            if (manager != null) manager.OnCutClick(eventData.position);
            return;
        }

        if (flyCoroutine != null) return;

        if (manager == null) manager = ResolveManager();
        if (manager == null) return;

        if (!manager.CanPickNextVeg())
        {
            Debug.Log("[VegItem] 上一个蔬菜的碎片还没全部处理完，暂不能选择 " + name);
            return;
        }

        StartFly();
    }

    public void StartFly()
    {
        if (boardCenter == null || !boardCenter.gameObject.activeInHierarchy)
        {
            Debug.LogError("[VegItem] Board Center 为空或未激活", this);
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
        flyCoroutine = null;

        if (manager == null) manager = ResolveManager();
        if (manager == null) yield break;      // 协程里不能用 return;

        manager.SetCurrentVeg(this);
    }

    public void LockLeftEdge()
    {
        fixedLeftEdgeLocal = rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f;
    }

    public bool CutOnce()
    {
        CurrentCutCount++;
        float totalShrink = 1f - finishRatio;
        float perCut = totalShrink / Mathf.Max(1, targetCutCount);
        CurrentRatio = Mathf.Clamp01(1f - CurrentCutCount * perCut);

        if (useImageFill)
        {
            // 左端固定，右边一截一截消失，贴图本身不拉伸
            vegImage.fillAmount = CurrentRatio;
        }
        else
        {
            // 没有 Image 时的兜底：只能改宽度
            float newWidth = baseSize.x * CurrentRatio;
            rt.sizeDelta = new Vector2(newWidth, baseSize.y);
            rt.anchoredPosition = new Vector2(fixedLeftEdgeLocal + newWidth * 0.5f, rt.anchoredPosition.y);
        }

        IsCutDone = CurrentCutCount >= targetCutCount;
        Debug.Log("[" + name + "] 切第 " + CurrentCutCount + "/" + targetCutCount +
                  " 刀，剩余 " + CurrentRatio.ToString("F2") +
                  "，切完=" + IsCutDone);
        return !IsCutDone;
    }

    public void ResetVeg()
    {
        if (flyCoroutine != null) StopCoroutine(flyCoroutine);
        flyCoroutine = null;
        CurrentCutCount = 0;
        CurrentRatio = 1f;
        IsCutDone = false;
        IsOnBoard = false;
        rt.sizeDelta = baseSize;
        if (useImageFill) vegImage.fillAmount = 1f;

        if (originalParent != null)
            rt.SetParent(originalParent, false);

        rt.anchoredPosition = originalLocalPos;
        gameObject.SetActive(true);

        if (vegGraphic != null) vegGraphic.raycastTarget = true;
    }
}