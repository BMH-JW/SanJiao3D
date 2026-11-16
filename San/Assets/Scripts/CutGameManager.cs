using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CutGameManager : MonoBehaviour
{
    public enum Phase { Idle, Cutting, Plating }

    [Header("蔬菜列表（拖 Veg_01/02/03 进来，顺序即出场顺序）")]
    public VegItem[] allVegs;

    [Header("场景引用")]
    public RectTransform redLine;
    public RectTransform boardArea;
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
    public float sliceRotRandom = 25f;
    public Vector2 plateSlotOffset = Vector2.zero;

    [Header("流程控制")]
    public float plateHoldSeconds = 2f;   // 盘子停留多久后清空
    public float completePanelCloseDelay = 3f;
    public bool forceShowRedLineNow = true;

    [Header("音效")]
    public AudioClip greatClip;
    public AudioClip missClip;

    // 运行时状态
    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    private VegItem currentVeg;
    private int vegIndex;                   // 当前是第几个蔬菜
    private int expectedPieceCount;        // 本蔬菜预期产生的碎片数
    private int placedPieceCount;           // 已归位盘子的碎片数
    private int platePieceCount;            // 盘子总碎片数（跨蔬菜累加，用于排队）

    private int moveDir = 1;
    private bool canClick;
    private AudioSource sfxSource;
    private Coroutine platingFlow;         // 装盘→清盘→下一个 的协程

    void Awake()
    {
        canClick = false;
        CurrentPhase = Phase.Idle;

        // 强制覆盖 Inspector 里可能残留的旧值（避免序列化字段覆盖代码默认）
        plateHoldSeconds = 2f;
        completePanelCloseDelay = 3f;

        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);
        if (redLine != null) redLine.gameObject.SetActive(forceShowRedLineNow);
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);

        EnsureBoardClickCatcher();

        sfxSource = gameObject.GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        // 自动开始：所有 Veg 重置后，玩家点第一个 Veg 触发 Flying → SetCurrentVeg → StartDynamicLine
        // 这里不自动触发，让玩家去点 VegItem；或者如果你想全自动，解开下面这行：
        // StartCuttingNextVeg();
    }

    void Update()
    {
        if (dynamicLine != null && dynamicLine.gameObject.activeSelf)
            MoveDynamicLine();
    }

    // ======== 动态线 ========
    void MoveDynamicLine()
    {
        if (redLine == null) return;
        float x = dynamicLine.anchoredPosition.x;
        x += moveDir * moveSpeed * Time.deltaTime;
        if (x >= moveRange) { x = moveRange; moveDir = -1; }
        else if (x <= -moveRange) { x = -moveRange; moveDir = 1; }
        dynamicLine.anchoredPosition = new Vector2(x, dynamicLine.anchoredPosition.y);
    }

    void StartDynamicLine()
    {
        if (dynamicLine == null) return;
        dynamicLine.anchoredPosition = new Vector2(-moveRange, dynamicLine.anchoredPosition.y);
        moveDir = 1;
        dynamicLine.gameObject.SetActive(true);
        canClick = true;
    }

    void StopDynamicLine()
    {
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);
        canClick = false;
    }

    // ======== 切菜入口 ========
    public void OnCutClick(Vector2 screenPos)
    {
        Debug.Log($"[Cut] phase={CurrentPhase}, canClick={canClick}, veg={currentVeg}");

        if (CurrentPhase != Phase.Cutting) return;
        if (!canClick || currentVeg == null || !currentVeg.IsOnBoard) return;

        if (!IsGreatNow())
        {
            ShowFeedback("Miss");
            return;
        }

        DoCut();
    }

    bool IsGreatNow()
    {
        if (redLine == null || dynamicLine == null) return false;
        float scale = Mathf.Max(dynamicLine.lossyScale.x, 0.0001f);
        float delta = Mathf.Abs(dynamicLine.position.x - redLine.position.x) / scale;
        return delta <= greatRange;
    }

    // ======== VegItem 调用：飞到菜板后 ========
    public void SetCurrentVeg(VegItem veg)
    {
        // 如果还在切上一个（比如协程刚结束前），先清干净
        if (currentVeg != null && currentVeg != veg)
            currentVeg.gameObject.SetActive(false);

        currentVeg = veg;
        CurrentPhase = Phase.Cutting;
        expectedPieceCount = veg.targetCutCount;
        placedPieceCount = 0;

        veg.LockLeftEdge();  // 记录落板左边缘，后续单边缩靠它

        // 蔬菜落到菜板后关闭自身射线拦截
        Graphic g = veg.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        if (redLine != null) redLine.gameObject.SetActive(true);
        AlignVegUnderRedLine(veg);
        StartDynamicLine();
        Debug.Log($"[{veg.name}] 进入 Phase.Cutting，目标 {expectedPieceCount} 刀");
    }

    void AlignVegUnderRedLine(VegItem veg)
    {
        if (veg == null || redLine == null) return;
        RectTransform rt = veg.GetComponent<RectTransform>();
        Vector3 pos = rt.position;
        pos.x = redLine.position.x;
        rt.position = pos;
        if (rt.parent == redLine.parent)
            rt.SetSiblingIndex(redLine.GetSiblingIndex());
    }

    // ======== 切一刀 ========
    public void DoCut()
    {
        if (currentVeg == null) return;

        ShowFeedback("Great！");

        bool stillHave = currentVeg.CutOnce();
        SpawnOneSlice();

        if (!stillHave)
        {
            // 本蔬菜切完 → 进入 Plating
            CurrentPhase = Phase.Plating;
            StopDynamicLine();

            // 蔬菜切完后隐藏，碎片留在菜板上让玩家一个一个点
            currentVeg.gameObject.SetActive(false);
            Debug.Log($"[{currentVeg.name}] 切完，进入 Phase.Plating，等玩家把 {expectedPieceCount} 块碎片入盘子");
        }
    }

    void SpawnOneSlice()
    {
        if (slicePrefab == null)
        {
            Debug.LogError("Slice Prefab 为空");
            return;
        }

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
            float rotZ = Random.Range(-sliceRotRandom, sliceRotRandom);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        }

        SlicePiece piece = go.GetComponent<SlicePiece>();
        if (piece != null)
            piece.Init(plateSlot, this);
    }

    // ======== SlicePiece 回调：归位盘子 ========
    public void NotifyPiecePlaced()
    {
        placedPieceCount++;
        platePieceCount++;
        Debug.Log($"[Plating] 碎片入盘 {placedPieceCount}/{expectedPieceCount}");

        // 本蔬菜所有碎片都入盘了 → 等 plateHoldSeconds 再清空并推进
        if (placedPieceCount >= expectedPieceCount && platingFlow == null)
        {
            platingFlow = StartCoroutine(PlateHoldThenAdvance());
        }
    }

    IEnumerator PlateHoldThenAdvance()
    {
        Debug.Log($"[Plating] 盘子停留 {plateHoldSeconds} 秒后清空...");
        yield return new WaitForSeconds(plateHoldSeconds);

        ClearPlate();

        vegIndex++;
        platingFlow = null;

        if (vegIndex >= allVegs.Length)
        {
            // 三个都切完了 → 弹 CompletePanel（3 秒后自动关）
            Debug.Log("[Game] 三个蔬菜全部切完，弹 CompletePanel");
            CurrentPhase = Phase.Idle;
            ShowCompletePanel();
        }
        else
        {
            // 等玩家手动点下一个蔬菜
            CurrentPhase = Phase.Idle;
            Debug.Log($"[Game] 请手动点击下一个蔬菜（vegIndex={vegIndex}）");
        }
    }

    void ClearPlate()
    {
        if (plateSlot == null) return;

        for (int i = plateSlot.childCount - 1; i >= 0; i--)
            Destroy(plateSlot.GetChild(i).gameObject);

        platePieceCount = 0;
        Debug.Log("[Plating] 盘子已清空");
    }

    // ======== 盘子排位置（跨蔬菜累加） ========
    public Vector2 GetNextPlateLocalPos(RectTransform pieceRt)
    {
        if (plateSlot == null) return pieceRt.anchoredPosition;

        Vector2 plateSize = plateSlot.sizeDelta;
        if (plateSize.x <= 0f || plateSize.y <= 0f)
            return pieceRt.anchoredPosition;

        float pieceW = pieceRt.sizeDelta.x > 0 ? pieceRt.sizeDelta.x : 60f;
        float pieceH = pieceRt.sizeDelta.y > 0 ? pieceRt.sizeDelta.y : 60f;
        float gap = 6f;

        int perRow = Mathf.Max(1, Mathf.FloorToInt((plateSize.x + gap) / (pieceW + gap)));
        int col = platePieceCount % perRow;
        int row = platePieceCount / perRow;

        float totalW = perRow * pieceW + (perRow - 1) * gap;
        Vector2 pos;
        pos.x = -totalW * 0.5f + pieceW * 0.5f + col * (pieceW + gap);
        pos.y = plateSize.y * 0.5f - pieceH * 0.5f - row * (pieceH + gap);

        float bottomLimit = -plateSize.y * 0.5f + pieceH * 0.5f;
        if (pos.y < bottomLimit) pos.y = bottomLimit;

        return pos + plateSlotOffset;
    }

    // ======== 其他 ========
    void EnsureBoardClickCatcher()
    {
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

    void ShowCompletePanel()
    {
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
        {
            // 关 panel 自身
            completePanel.SetActive(false);

            // 如果 panel 是一个 Canvas 或 Canvas 的子物体，也把 Canvas 关掉
            Canvas canvas = completePanel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.gameObject != completePanel.gameObject)
                canvas.gameObject.SetActive(false);
        }
    }

    public void ShowFeedback(string msg)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);
        if (feedbackText != null) feedbackText.text = msg;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 0.7f);

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
