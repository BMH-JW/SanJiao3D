using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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
    public RectTransform plateSlot;

    [Header("反馈图片（Great / Miss）")]
    [Tooltip("切得准的时候显示的图片（直接拖图片资源）")]
    public Sprite greatFeedbackSprite;
    [Tooltip("切偏时显示的图片（直接拖图片资源）")]
    public Sprite missFeedbackSprite;
    [Tooltip("用来显示反馈图的 UI Image；留空则在 Feedback Panel 里自动找一个")]
    public Image feedbackImage;
    [Tooltip("反馈显示多少秒后自动消失")]
    public float feedbackShowSeconds = 0.7f;

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
    public float plateHoldSeconds = 2f;
    public bool forceShowRedLineNow = true;

    [Header("音效")]
    public AudioClip greatClip;
    public AudioClip missClip;

    [Header("起火与校准")]
    public GameObject fireIcon;            // 起火标志（可点击）
    public GameObject fireAnimation;       // 火焰帧动画（挂 Animator）
    public GameObject calibrationCanvas;   // 校准条所在 Canvas（默认 inactive）
    public float fireIconDelay = 0.5f;     // 所有蔬菜切完后多久出现起火标志

    [Header("调试")]
    public bool debugLog = true;

    // 运行时状态
    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    private VegItem currentVeg;
    private int vegIndex;                   // 当前是第几个蔬菜
    private int expectedPieceCount;         // 本蔬菜预期产生的碎片数
    private int placedPieceCount;           // 已归位盘子的碎片数

    private int moveDir = 1;
    private bool canClick;
    private AudioSource sfxSource;
    private Coroutine platingFlow;          // 装盘→清盘→下一个 的协程

    void Awake()
    {
        canClick = false;
        CurrentPhase = Phase.Idle;

        // 进入 2D 时确保时间在走（防止上一个阶段把 timeScale 停成 0）
        Time.timeScale = 1f;

        // 强制覆盖 Inspector 里可能残留的旧值
        plateHoldSeconds = 2f;

        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (feedbackImage != null) feedbackImage.gameObject.SetActive(false);
        if (redLine != null) redLine.gameObject.SetActive(forceShowRedLineNow);
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);

        // 起火相关默认隐藏
        if (fireIcon != null) fireIcon.SetActive(false);
        if (fireAnimation != null) fireAnimation.SetActive(false);
        if (calibrationCanvas != null) calibrationCanvas.SetActive(false);

        SetupFireIconClick();
        EnsureBoardClickCatcher();

        sfxSource = gameObject.GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

        if (debugLog)
            Debug.Log("[Cut] Awake 完成 | timeScale=" + Time.timeScale +
                      " | dynamicLine=" + (dynamicLine != null ? "已赋值" : "空") +
                      " | redLine=" + (redLine != null ? "已赋值" : "空") +
                      " | boardArea=" + (boardArea != null ? boardArea.name : "空") +
                      " | allVegs=" + (allVegs != null ? allVegs.Length.ToString() : "空"), this);
    }

    void Start()
    {
        // 玩家点第一个 Veg 触发 Flying → SetCurrentVeg → StartDynamicLine
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
        if (dynamicLine == null)
        {
            Debug.LogError("[Cut] dynamicLine 没有赋值！请把 DynamicLine 拖到 CutGameManager 上", this);
            return;
        }

        // 确保线自己和所有父级都激活
        Transform t = dynamicLine;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                Debug.LogWarning("[Cut] 父级处于关闭状态，已重新打开：" + t.name, t);
                t.gameObject.SetActive(true);
            }
            t = t.parent;
        }

        dynamicLine.anchoredPosition = new Vector2(-moveRange, dynamicLine.anchoredPosition.y);
        moveDir = 1;
        dynamicLine.gameObject.SetActive(true);
        canClick = true;

        if (debugLog)
            Debug.Log("[Cut] DynamicLine 已开启 | 层级激活=" + dynamicLine.gameObject.activeInHierarchy +
                      " | 父级=" + (dynamicLine.parent != null ? dynamicLine.parent.name : "null") +
                      " | pos=" + dynamicLine.anchoredPosition +
                      " | size=" + dynamicLine.sizeDelta +
                      " | timeScale=" + Time.timeScale, this);
    }

    void StopDynamicLine()
    {
        if (dynamicLine != null) dynamicLine.gameObject.SetActive(false);
        canClick = false;
    }

    // ======== 切菜入口 ========
    public void OnCutClick(Vector2 screenPos)
    {
        if (debugLog)
            Debug.Log("[Cut] 收到点击 phase=" + CurrentPhase + ", canClick=" + canClick + ", veg=" + currentVeg);

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

    // 当前蔬菜未切完、碎片未全部入盘清空前都不可选
    public bool CanPickNextVeg()
    {
        return CurrentPhase == Phase.Idle && platingFlow == null;
    }

    // ======== VegItem 调用：飞到菜板后 ========
    public void SetCurrentVeg(VegItem veg)
    {
        if (veg == null)
        {
            Debug.LogError("[Cut] SetCurrentVeg 收到 null", this);
            return;
        }

        if (currentVeg != null && currentVeg != veg)
            currentVeg.gameObject.SetActive(false);

        currentVeg = veg;
        CurrentPhase = Phase.Cutting;
        expectedPieceCount = veg.targetCutCount;
        placedPieceCount = 0;

        veg.LockLeftEdge();

        // 蔬菜落到菜板后关闭自身射线拦截
        Graphic g = veg.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        if (redLine != null) redLine.gameObject.SetActive(true);
        AlignVegUnderRedLine(veg);
        StartDynamicLine();

        if (debugLog)
            Debug.Log("[" + veg.name + "] 进入 Phase.Cutting，目标 " + expectedPieceCount + " 刀", this);
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
            Debug.Log("[" + currentVeg.name + "] 切完，进入 Phase.Plating，等玩家把 " + expectedPieceCount + " 块碎片入盘子");
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

        // 同一个碎片预制体，只按当前蔬菜换 Sprite（没配置就保留预制体自带的图）
        Sprite sliceSprite = currentVeg != null ? currentVeg.GetSliceSprite() : null;
        if (sliceSprite != null)
        {
            Image img = go.GetComponentInChildren<Image>(true);
            if (img != null) img.sprite = sliceSprite;
        }

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
        Debug.Log("[Plating] 碎片入盘 " + placedPieceCount + "/" + expectedPieceCount);

        if (placedPieceCount >= expectedPieceCount && platingFlow == null)
        {
            platingFlow = StartCoroutine(PlateHoldThenAdvance());
        }
    }

    IEnumerator PlateHoldThenAdvance()
    {
        platingFlow = null;
        vegIndex++;

        int total = allVegs != null ? allVegs.Length : 0;
        Debug.Log("[Game] 已完成第 " + vegIndex + "/" + total + " 个蔬菜");

        if (total == 0 || vegIndex >= total)
        {
            // 所有蔬菜都切完并入盘 → 等待后显示起火标志
            Debug.Log("[Game] 所有蔬菜处理完，等待 " + fireIconDelay + " 秒后显示起火标志");
            CurrentPhase = Phase.Idle;
            StartCoroutine(ShowFireIconAfterDelay());
        }
        else
        {
            CurrentPhase = Phase.Idle;
            Debug.Log("[Game] 请点击下一个蔬菜（" + vegIndex + "/" + total + "）");
        }
        yield break;
    }

    IEnumerator ShowFireIconAfterDelay()
    {
        Debug.Log("[Fire] 倒计时 " + fireIconDelay + " 秒...");
        yield return new WaitForSeconds(fireIconDelay);

        if (fireIcon != null)
        {
            fireIcon.SetActive(true);
            Debug.Log("[Fire] 起火标志已显示：" + fireIcon.name);
        }
        else
        {
            Debug.LogWarning("[Fire] fireIcon 未赋值，请把 Fire 拖到 CutGameManager 上");
        }
    }

    // ======== 起火标志点击处理 ========
    void SetupFireIconClick()
    {
        if (fireIcon == null) return;

        Button btn = fireIcon.GetComponent<Button>();
        if (btn == null) btn = fireIcon.AddComponent<Button>();

        // 确保有 Graphic 才能被射线检测
        Graphic g = fireIcon.GetComponent<Graphic>();
        if (g == null) g = fireIcon.AddComponent<Image>();
        g.raycastTarget = true;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnFireIconClick);
    }

    public void OnFireIconClick()
    {
        Debug.Log("[Fire] 起火标志被点击 → 播放火焰动画 + 显示校准条");

        if (fireIcon != null) fireIcon.SetActive(false);

        // 播放火焰帧动画（SetActive(true) 后 Animator 自动从状态机播放）
        if (fireAnimation != null) fireAnimation.SetActive(true);

        if (calibrationCanvas != null)
        {
            calibrationCanvas.SetActive(true);
            Debug.Log("[Fire] 校准条 Canvas 已激活，PotCalibrationGame.BeginCalibration() 将自动执行");
        }
        else
        {
            Debug.LogWarning("[Fire] calibrationCanvas 未配置，请在 Inspector 拖入校准条所在的 Canvas");
        }
    }

    void ClearPlate()
    {
        if (plateSlot == null) return;

        for (int i = plateSlot.childCount - 1; i >= 0; i--)
            Destroy(plateSlot.GetChild(i).gameObject);

        Debug.Log("[Plating] 盘子已清空");
    }

    // ======== 盘子里随机落点（限制在 plateSlot 框内） ========
    public Vector2 GetNextPlateLocalPos(RectTransform pieceRt)
    {
        if (plateSlot == null) return pieceRt.anchoredPosition;

        Vector2 plateSize = plateSlot.sizeDelta;
        if (plateSize.x <= 0f || plateSize.y <= 0f)
            return pieceRt.anchoredPosition;

        float pieceW = pieceRt.sizeDelta.x > 0 ? pieceRt.sizeDelta.x : 60f;
        float pieceH = pieceRt.sizeDelta.y > 0 ? pieceRt.sizeDelta.y : 60f;

        // 按盘子自身的 pivot 换算框的四条边（碎片锚点、轴心都是中心）
        Vector2 pivot = plateSlot.pivot;
        float left = -pivot.x * plateSize.x;
        float right = (1f - pivot.x) * plateSize.x;
        float bottom = -pivot.y * plateSize.y;
        float top = (1f - pivot.y) * plateSize.y;

        // 内缩半个碎片，保证整块碎片都在框内
        float minX = left + pieceW * 0.5f;
        float maxX = right - pieceW * 0.5f;
        float minY = bottom + pieceH * 0.5f;
        float maxY = top - pieceH * 0.5f;

        // 碎片比盘子还大时放正中间
        float x = minX <= maxX ? Random.Range(minX, maxX) : (left + right) * 0.5f;
        float y = minY <= maxY ? Random.Range(minY, maxY) : (bottom + top) * 0.5f;

        return new Vector2(x, y) + plateSlotOffset;
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
        if (boardArea == null)
        {
            Debug.LogWarning("[Cut] boardArea 为空，切菜点击可能收不到", this);
            return;
        }

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

    public void ShowFeedback(string msg)
    {
        bool isGreat = msg.Contains("Great");
        bool isMiss = msg.Contains("Miss");
        Sprite sprite = isGreat ? greatFeedbackSprite : (isMiss ? missFeedbackSprite : null);

        if (sprite != null)
        {
            if (feedbackImage == null) feedbackImage = FindFeedbackImage();

            if (feedbackImage != null)
            {
                // 文字换成图片：Great / Miss 只换 Sprite
                ActivateChain(feedbackImage.transform);
                feedbackImage.sprite = sprite;
                feedbackImage.gameObject.SetActive(true);

                if (feedbackText != null && feedbackText.gameObject != feedbackImage.gameObject)
                    feedbackText.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[Cut] 配了反馈图片，但找不到用来显示的 Image，请把 Feedback Image 拖到 CutGameManager 上", this);
            }
        }
        else
        {
            // 没配图片时退回原来的文字反馈
            if (feedbackPanel != null) feedbackPanel.SetActive(true);
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(true);
                feedbackText.text = msg;
            }
        }

        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), Mathf.Max(0.01f, feedbackShowSeconds));

        if (sfxSource != null)
        {
            if (isGreat && greatClip != null)
                sfxSource.PlayOneShot(greatClip);
            else if (isMiss && missClip != null)
                sfxSource.PlayOneShot(missClip);
        }
    }

    void HideFeedback()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (feedbackImage != null) feedbackImage.gameObject.SetActive(false);
    }

    // Feedback Image 没拖时，在 Feedback Panel 下面找一个带图片的 Image
    private Image FindFeedbackImage()
    {
        if (feedbackPanel == null) return null;

        Image[] images = feedbackPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].transform != feedbackPanel.transform)
                return images[i];
        }
        return feedbackPanel.GetComponent<Image>();
    }

    private static void ActivateChain(Transform t)
    {
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    // ======== 工具 ========
    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}