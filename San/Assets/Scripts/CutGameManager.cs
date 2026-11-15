using UnityEngine;
using UnityEngine.UI;

public class CutGameManager : MonoBehaviour
{
    [Header("场景引用")]
    public VegItem currentVeg;
    public RectTransform redLine;
    public RectTransform vegArea; // 直接拖 VegSlot（蔬菜所在区域）
    public GameObject feedbackPanel;
    public Text feedbackText;
    public GameObject completePanel;
    public RectTransform plateSlot;

    [Header("动态虚线")]
    public RectTransform dynamicLine;
    public float moveRange = 260f;
    public float moveSpeed = 180f;
    public float greatRange = 40f;

    [Header("切割参数")]
    public int sliceCountPerVeg = 3;
    public GameObject slicePrefab;
    public float completePanelCloseDelay = 3f;

    [Header("调试")]
    public bool forceShowRedLineNow = true;

    private int currentCutCount;
    private int moveDir = 1;
    private bool canClick;
    public bool IsBusy { get; set; }

    void Awake()
    {
        currentCutCount = 0;
        canClick = false;

        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);

        if (redLine != null)
            redLine.gameObject.SetActive(forceShowRedLineNow);

        if (dynamicLine != null)
            dynamicLine.gameObject.SetActive(false);
    }

    void Update()
    {
        if (dynamicLine != null && dynamicLine.gameObject.activeSelf)
            MoveDynamicLine();
    }

    void MoveDynamicLine()
    {
        if (redLine == null) return;

        float x = dynamicLine.anchoredPosition.x;
        x += moveDir * moveSpeed * Time.deltaTime;

        if (x >= moveRange)
        {
            x = moveRange;
            moveDir = -1;
        }
        else if (x <= -moveRange)
        {
            x = -moveRange;
            moveDir = 1;
        }

        dynamicLine.anchoredPosition = new Vector2(x, dynamicLine.anchoredPosition.y);
    }

    public void StartDynamicLine()
    {
        if (dynamicLine == null) return;
        dynamicLine.anchoredPosition = new Vector2(-moveRange, dynamicLine.anchoredPosition.y);
        moveDir = 1;
        dynamicLine.gameObject.SetActive(true);
        canClick = true;
    }

    public void StopDynamicLine()
    {
        if (dynamicLine != null)
            dynamicLine.gameObject.SetActive(false);
        canClick = false;
    }

    public void OnPlayerClick(Vector2 screenPos)
    {
        if (!canClick) return;
        if (currentVeg == null || !currentVeg.IsOnBoard)
        {
            Debug.LogWarning("当前没有可切蔬菜");
            return;
        }

        // 核心：拿到蔬菜区域的左右边界
        Vector2 vegMin = vegArea.anchoredPosition - vegArea.sizeDelta / 2;
        Vector2 vegMax = vegArea.anchoredPosition + vegArea.sizeDelta / 2;
        float lineX = dynamicLine.anchoredPosition.x;

        // ✅ 判断：动态线是否在蔬菜横向范围里面
        if (lineX < vegMin.x || lineX > vegMax.x)
        {
            ShowFeedback("超出蔬菜范围！");
            return; // 直接终止，绝对不会执行DoCut、生成碎片
        }

        bool isGreat = CheckGreat();
        DoCut(isGreat);
    }

    bool CheckGreat()
    {
        if (redLine == null || dynamicLine == null) return false;
        float distance = Mathf.Abs(dynamicLine.anchoredPosition.x - redLine.anchoredPosition.x);
        return distance <= greatRange;
    }

    public void SetCurrentVeg(VegItem veg)
    {
        currentVeg = veg;
        currentCutCount = 0;
        IsBusy = false;

        if (redLine != null)
            redLine.gameObject.SetActive(true);

        StartDynamicLine();
        Debug.Log($"新蔬菜就位，需要切 {sliceCountPerVeg} 刀");
    }

    public void DoCut(bool isGreat)
    {
        currentCutCount++;
        Debug.Log($"第 {currentCutCount}/{sliceCountPerVeg} 刀，Great={isGreat}");

        ShowFeedback(isGreat ? "Great！" : "普通");
        SpawnOneSlice(currentCutCount);

        if (currentCutCount < sliceCountPerVeg)
        {
            return;
        }
        FinishCutting();
    }

    void SpawnOneSlice(int index)
    {
        if (slicePrefab == null)
        {
            Debug.LogError("Slice Prefab 为空");
            return;
        }

        Transform parent = plateSlot != null ? plateSlot.parent : transform;
        GameObject go = Instantiate(slicePrefab, parent);

        Vector3 offset = new Vector3((index - 2) * 25f, index * 8f, 0f);
        go.transform.localPosition += offset;

        SlicePiece piece = go.GetComponent<SlicePiece>();
        if (piece != null)
            piece.Init(plateSlot, this);
        else
            Debug.LogError("碎片预制体没有 SlicePiece 脚本");
    }

    void FinishCutting()
    {
        StopDynamicLine();

        if (currentVeg != null)
        {
            currentVeg.gameObject.SetActive(false);
        }

        currentVeg = null;
        currentCutCount = 0;

        if (redLine != null)
            redLine.gameObject.SetActive(false);

        if (completePanel != null)
        {
            completePanel.SetActive(true);
            CancelInvoke(nameof(AutoCloseCompletePanel));
            Invoke(nameof(AutoCloseCompletePanel), completePanelCloseDelay);
        }
    }

    void AutoCloseCompletePanel()
    {
        if (completePanel != null)
            completePanel.SetActive(false);
    }

    public void ShowFeedback(string msg)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);
        if (feedbackText != null) feedbackText.text = msg;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 0.7f);
    }

    void HideFeedback()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }
}