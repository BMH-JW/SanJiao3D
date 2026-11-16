﻿using UnityEngine;
using UnityEngine.UI;

public class CutGameManager : MonoBehaviour
{
    [Header("场景引用")]
    public VegItem currentVeg;
    public RectTransform redLine;
    public RectTransform boardArea;   // 菜板（拖菜板物体进来，点击它任意位置都可切）
    public GameObject feedbackPanel;
    public Text feedbackText;
    public GameObject completePanel;
    public RectTransform plateSlot;

    [Header("动态虚线")]
    public RectTransform dynamicLine;
    public float moveRange = 260f;
    public float moveSpeed = 180f;
    public float greatRange = 40f;

    [Header("碎片")]
    public GameObject slicePrefab;
    public Vector2 sliceOffsetBase = Vector2.zero;
    public Vector2 sliceOffsetRandom = new Vector2(40f, 30f);
    public float sliceRotRandom = 25f;   // 碎片生成时的随机旋转角度（±度）
    public Vector2 plateSlotOffset = Vector2.zero;  // 盘子锚点与视觉中心的修正偏移

    [Header("结束面板")]
    public float completePanelCloseDelay = 3f;
    public bool forceShowRedLineNow = true;

    [Header("音效")]
    public AudioClip greatClip;
    public AudioClip missClip;

    private AudioSource sfxSource;

    private int moveDir = 1;
    private bool canClick;
    public bool IsBusy { get; set; }
    private int sliceIndex;
    private int platePieceCount = 0;

    void Awake()
    {
        canClick = false;
        sliceIndex = 0;

        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);
        if (redLine != null) redLine.gameObject.SetActive(forceShowRedLineNow);
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);

        EnsureBoardClickCatcher();

        // 自动挂 AudioSource，不打断其他正在播的音效
        sfxSource = gameObject.GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
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

        if (x >= moveRange) { x = moveRange; moveDir = -1; }
        else if (x <= -moveRange) { x = -moveRange; moveDir = 1; }

        dynamicLine.anchoredPosition = new Vector2(x, dynamicLine.anchoredPosition.y);
    }

    public void StartDynamicLine()
    {
        if (dynamicLine == null) return;
        dynamicLine.anchoredPosition = new Vector2(-moveRange, dynamicLine.anchoredPosition.y);
        moveDir = 1;
        dynamicLine.gameObject.SetActive(true);
        canClick = true;
        Debug.Log("动态线启动，进入切菜阶段");
    }

    public void StopDynamicLine()
    {
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);
        canClick = false;
    }

    // 给切菜点击脚本调用
    public void OnCutClick(Vector2 screenPos)
    {
        Debug.Log($"切菜点击，canClick={canClick}, currentVeg={currentVeg}");

        if (!canClick || currentVeg == null || !currentVeg.IsOnBoard)
        {
            Debug.Log("暂不允许切");
            return;
        }

        // 只有 Great 才算切一刀；Miss 只提示，不缩蔬菜、不出碎片
        if (!IsGreatNow())
        {
            ShowFeedback("Miss");
            return;
        }

        DoCut();
    }

    // 动态线是否贴近红线中心（Great 判定）
    bool IsGreatNow()
    {
        if (redLine == null || dynamicLine == null) return false;

        // 统一用世界 X 比较，再除以缩放归一化回 UI 单位
        float scale = Mathf.Max(dynamicLine.lossyScale.x, 0.0001f);
        float delta = Mathf.Abs(dynamicLine.position.x - redLine.position.x) / scale;
        return delta <= greatRange;
    }

    // 保证菜板能接收点击：没有 Graphic 就补一个透明 Image，并挂上点击转发
    void EnsureBoardClickCatcher()
    {
        // 没手动拖引用时自动找：从 VegItem.boardCenter 的父节点取
        if (boardArea == null)
        {
            VegItem veg = FindObjectOfType<VegItem>();
            if (veg != null && veg.boardCenter != null)
                boardArea = veg.boardCenter.parent as RectTransform;
        }

        if (boardArea == null) return;

        Image img = boardArea.GetComponent<Image>();
        if (img == null)
        {
            img = boardArea.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }
        img.raycastTarget = true;

        DynamicLineClick click = boardArea.GetComponent<DynamicLineClick>();
        if (click == null) click = boardArea.gameObject.AddComponent<DynamicLineClick>();
        click.manager = this;
    }

    public void SetCurrentVeg(VegItem veg)
    {
        currentVeg = veg;
        sliceIndex = 0;
        IsBusy = false;

        // 蔬菜落到菜板后关闭自身射线拦截，让点击能透到菜板层
        Graphic g = veg.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        if (redLine != null) redLine.gameObject.SetActive(true);

        AlignVegUnderRedLine(veg);
        StartDynamicLine();
        Debug.Log($"新蔬菜就位，剩余比例 {currentVeg.CurrentRatio}");
    }

    // 把蔬菜摆到红线正下方：X 对齐红线，且渲染层级在红线之下（红线盖在蔬菜上）
    void AlignVegUnderRedLine(VegItem veg)
    {
        if (veg == null || redLine == null) return;

        RectTransform rt = veg.GetComponent<RectTransform>();

        Vector3 pos = rt.position;
        pos.x = redLine.position.x;
        rt.position = pos;

        // 与红线同父节点时，插到红线前一个层级，保证红线画在蔬菜上面
        if (rt.parent == redLine.parent)
            rt.SetSiblingIndex(redLine.GetSiblingIndex());
    }

    public void DoCut()
    {
        if (currentVeg == null) return;

        ShowFeedback("Great！");

        bool stillHave = currentVeg.CutOnce();
        SpawnOneSlice();
        sliceIndex++;

        if (!stillHave)
            FinishCutting();
    }

    void SpawnOneSlice()
    {
        if (slicePrefab == null)
        {
            Debug.LogError("Slice Prefab 为空");
            return;
        }

        // 碎片先在预制体默认 localPosition 附近散开（小范围随机），让多个碎片堆一起也看得出
        Transform parent = plateSlot != null ? plateSlot.parent : transform;
        GameObject go = Instantiate(slicePrefab, parent);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 random = new Vector2(
                Random.Range(-sliceOffsetRandom.x, sliceOffsetRandom.x),
                Random.Range(-sliceOffsetRandom.y, sliceOffsetRandom.y)
            );
            rt.anchoredPosition += sliceOffsetBase + random;

            // 随机旋转，让重叠碎片一眼能区分
            float rotZ = Random.Range(-sliceRotRandom, sliceRotRandom);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        }

        SlicePiece piece = go.GetComponent<SlicePiece>();
        if (piece != null)
            piece.Init(plateSlot, this);
    }

    void FinishCutting()
    {
        StopDynamicLine();

        if (currentVeg != null)
            currentVeg.gameObject.SetActive(false);

        currentVeg = null;
        if (redLine != null) redLine.gameObject.SetActive(false);

        if (completePanel != null)
        {
            completePanel.SetActive(true);
            CancelInvoke(nameof(AutoCloseCompletePanel));
            Invoke(nameof(AutoCloseCompletePanel), completePanelCloseDelay);
        }
    }

    // 给 SlicePiece 调用：在 plateSlot 的矩形范围内排碎片，不重叠不超界
    public Vector2 GetNextPlateLocalPos(RectTransform pieceRt)
    {
        if (plateSlot == null) return pieceRt.anchoredPosition;

        Vector2 plateSize = plateSlot.sizeDelta;
        if (plateSize.x <= 0f || plateSize.y <= 0f)
            return pieceRt.anchoredPosition;

        float pieceW = pieceRt.sizeDelta.x > 0 ? pieceRt.sizeDelta.x : 60f;
        float pieceH = pieceRt.sizeDelta.y > 0 ? pieceRt.sizeDelta.y : 60f;
        float gap = 6f;

        // 在盘子矩形里算每行能放几个
        int perRow = Mathf.Max(1, Mathf.FloorToInt((plateSize.x + gap) / (pieceW + gap)));

        int col = platePieceCount % perRow;
        int row = platePieceCount / perRow;
        platePieceCount++;

        // 盘子矩形中心为 (0,0)，碎片也以中心锚点摆放
        float totalW = perRow * pieceW + (perRow - 1) * gap;
        float rowWidth = totalW;
        float rowHeight = pieceH;

        // 把整行水平居中；行多了按行堆叠，整体垂直居中
        Vector2 pos;
        pos.x = -rowWidth * 0.5f + pieceW * 0.5f + col * (pieceW + gap);
        pos.y = plateSize.y * 0.5f - pieceH * 0.5f - row * (pieceH + gap);

        // 若碎片超出盘子下缘，就改成自下往上堆或换行后往中间收
        float bottomLimit = -plateSize.y * 0.5f + pieceH * 0.5f;
        if (pos.y < bottomLimit) pos.y = bottomLimit;

        return pos + plateSlotOffset;
    }

    void AutoCloseCompletePanel()
    {
        if (completePanel != null) completePanel.SetActive(false);
    }

    public void ShowFeedback(string msg)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);
        if (feedbackText != null) feedbackText.text = msg;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 0.7f);

        // 按反馈类型播放对应音效（PlayOneShot 不打断同 AudioSource 上其他音效）
        if (sfxSource != null)
        {
            if (msg.Contains("Great") && greatClip != null)
                sfxSource.PlayOneShot(greatClip);
            else if (msg.Contains("Miss") && missClip != null)
                sfxSource.PlayOneShot(missClip);
        }
    }

    void HideFeedback()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }
}