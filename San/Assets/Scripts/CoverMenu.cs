using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 「设置 / 退出游戏」逻辑，封面场景和 3D 游戏场景共用。
/// 按钮由你自己在场景里创建，然后：
///   1. 把本组件挂到任意物体上（建议挂在 Canvas 上）
///   2. 在按钮的 Button → OnClick 里拖入本组件，选 CoverMenu.OpenSettings / CoverMenu.QuitGame
/// 设置面板会在第一次点「设置」时自动生成（独立的 Overlay Canvas 上，保证鼠标可交互）。
/// 3D 场景里会自动暂停第一人称操作和场景点击，关闭设置后恢复。
/// </summary>
public class CoverMenu : MonoBehaviour
{
    [Header("按钮（可选，填了会自动绑定点击）")]
    public Button settingsButton;
    public Button quitButton;

    [Header("设置面板字体")]
    [Tooltip("留空则使用系统中文字体（微软雅黑）")]
    public Font font;

    [Header("3D 场景")]
    [Tooltip("打开设置时暂停第一人称操作和场景点击，关闭后恢复")]
    public bool blockGameplayWhileOpen = true;

    [Tooltip("3D 场景设置里的「返回封面」按钮要跳转的场景名")]
    public string coverSceneName = "封面";

    private GameObject settingsPanel;
    private VolumeSlider musicSlider;
    private VolumeSlider sfxSlider;
    private Text musicValueText;
    private Text sfxValueText;

    private HandleClick handleClick;
    private bool handleClickWasEnabled;
    private PlayerMover playerMover;
    private bool playerMoverWasEnabled;
    private bool gameplayBlocked;

    // ====================================================================

    private void Awake()
    {
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    private void OnDestroy()
    {
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        RestoreGameplay();
    }

    // ====================================================================

    /// <summary>打开设置面板（按钮 OnClick 调用）。</summary>
    public void OpenSettings()
    {
        if (settingsPanel == null)
            BuildSettingsPanel();

        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();

        BlockGameplay();

        float music = SceneFader.Instance != null
            ? SceneFader.Instance.MusicVolume
            : GameAudioSettings.MusicVolume;

        musicSlider.SetValueWithoutNotify(music);
        sfxSlider.SetValueWithoutNotify(GameAudioSettings.SfxVolume);

        RefreshValueLabels();
    }

    /// <summary>关闭设置面板。</summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        RestoreGameplay();
    }

    /// <summary>退出游戏（按钮 OnClick 调用）。</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>3D 场景设置面板里的「返回封面」按钮：渐隐后回到封面场景。</summary>
    public void ReturnToCover()
    {
        if (string.IsNullOrEmpty(coverSceneName))
            coverSceneName = "封面";

        // 先收起面板，避免渐隐过程中残留设置窗口
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(coverSceneName);
        else
            SceneManager.LoadScene(coverSceneName);
    }

    // ====================================================================

    private void OnMusicChanged(float value)
    {
        GameAudioSettings.MusicVolume = value;

        if (SceneFader.Instance != null)
            SceneFader.Instance.SetMusicVolume(value);
        else
            SceneMusic.SetLocalVolume(value);

        RefreshValueLabels();
    }

    private void OnSfxChanged(float value)
    {
        GameAudioSettings.SfxVolume = value;

        if (AudioVolumeController.Instance != null)
            AudioVolumeController.Instance.SetSfxVolume(value);

        RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        if (musicValueText != null && musicSlider != null)
            musicValueText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";

        if (sfxValueText != null && sfxSlider != null)
            sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
    }

    /// <summary>3D 场景里打开设置时，暂停走动 / 转视角和场景点击。</summary>
    private void BlockGameplay()
    {
        if (!blockGameplayWhileOpen || gameplayBlocked) return;

        gameplayBlocked = true;

        handleClick = FindObjectOfType<HandleClick>();
        if (handleClick != null)
        {
            handleClickWasEnabled = handleClick.enabled;
            handleClick.enabled = false;
        }

        playerMover = FindObjectOfType<PlayerMover>();
        if (playerMover != null)
        {
            playerMoverWasEnabled = playerMover.enabled;
            playerMover.enabled = false;
            playerMover.LockCursor(false);   // 打开设置面板，释放鼠标才能拖动滑条
        }
    }

    private void RestoreGameplay()
    {
        if (!gameplayBlocked) return;
        gameplayBlocked = false;

        if (handleClick != null && handleClickWasEnabled)
            handleClick.enabled = true;

        if (playerMover != null && playerMoverWasEnabled)
        {
            playerMover.enabled = true;
            playerMover.LockCursor(true);   // 关闭设置，重新锁定鼠标恢复第一人称转视角
        }

        handleClick = null;
        playerMover = null;
    }

    // ====================================================================

    private void EnsureEventSystem()
    {
        EventSystem system = FindObjectOfType<EventSystem>();

        if (system == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return;
        }

        // 有 EventSystem 但没有输入模块时，鼠标事件不会派发
        if (system.GetComponent<BaseInputModule>() == null)
            system.gameObject.AddComponent<StandaloneInputModule>();
    }

    private Canvas CreatePanelCanvas()
    {
        GameObject go = new GameObject(
            "CoverMenuCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 在普通 UI 之上、自定义光标(32760)/渐隐层(32767)之下

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void BuildSettingsPanel()
    {
        EnsureEventSystem();

        Canvas canvas = CreatePanelCanvas();

        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
        RectTransform root = settingsPanel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root);

        // 半透明遮罩（挡住下面 UI 的点击，避免调音量时误触场景按钮）
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        RectTransform dimRt = dim.GetComponent<RectTransform>();
        dimRt.SetParent(root, false);
        Stretch(dimRt);

        Image dimImage = dim.GetComponent<Image>();
        dimImage.sprite = null;
        dimImage.color = new Color(0f, 0f, 0f, 0.7f);
        dimImage.raycastTarget = true;

        // 窗口
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.SetParent(root, false);
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(660f, 440f);
        windowRt.anchoredPosition = Vector2.zero;

        Image windowImage = window.GetComponent<Image>();
        windowImage.sprite = null;
        windowImage.color = new Color(0.09f, 0.09f, 0.11f, 0.97f);
        windowImage.raycastTarget = true;

        CreateTextAt(windowRt, "Title", "设置", new Vector2(0f, 165f), new Vector2(400f, 54f), 42, TextAnchor.MiddleCenter, Color.white);

        CreateTextAt(windowRt, "MusicLabel", "背景音乐", new Vector2(-225f, 70f), new Vector2(180f, 40f), 28, TextAnchor.MiddleLeft, Color.white);
        musicSlider = CreateVolumeSlider(windowRt, new Vector2(30f, 70f), OnMusicChanged);
        musicValueText = CreateTextAt(windowRt, "MusicValue", "", new Vector2(255f, 70f), new Vector2(90f, 40f), 26, TextAnchor.MiddleRight, Color.white);

        CreateTextAt(windowRt, "SfxLabel", "音效 / 提示音", new Vector2(-225f, -10f), new Vector2(180f, 40f), 28, TextAnchor.MiddleLeft, Color.white);
        sfxSlider = CreateVolumeSlider(windowRt, new Vector2(30f, -10f), OnSfxChanged);
        sfxValueText = CreateTextAt(windowRt, "SfxValue", "", new Vector2(255f, -10f), new Vector2(90f, 40f), 26, TextAnchor.MiddleRight, Color.white);

        CreateSimpleButton(
            "关闭",
            windowRt,
            new Vector2(-120f, -155f),
            new Vector2(200f, 62f),
            CloseSettings,
            30);

        // 只在 3D 游戏场景（存在 PlayerMover）里显示「返回封面」；
        // 封面场景自身打开设置时不显示这个按钮
        if (FindObjectOfType<PlayerMover>() != null)
        {
            CreateSimpleButton(
                "返回封面",
                windowRt,
                new Vector2(130f, -155f),
                new Vector2(200f, 62f),
                ReturnToCover,
                30);
        }

        settingsPanel.SetActive(false);
    }

    private VolumeSlider CreateVolumeSlider(Transform parent, Vector2 anchoredPos, UnityAction<float> onChanged)
    {
        GameObject root = new GameObject("VolumeSlider", typeof(RectTransform), typeof(VolumeSlider));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(320f, 30f);
        rt.anchoredPosition = anchoredPos;

        // 轨道
        RectTransform trackRt = CreateImage("Track", rt, new Color(0.22f, 0.22f, 0.24f, 1f));
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.offsetMin = new Vector2(0f, -5f);
        trackRt.offsetMax = new Vector2(0f, 5f);

        // 已填充部分
        RectTransform fillRt = CreateImage("Fill", rt, new Color(0.9f, 0.9f, 0.9f, 1f));
        fillRt.anchorMin = new Vector2(0f, 0.5f);
        fillRt.anchorMax = new Vector2(0f, 0.5f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.sizeDelta = new Vector2(320f, 10f);
        fillRt.anchoredPosition = Vector2.zero;

        // 手柄
        RectTransform handleRt = CreateImage("Handle", rt, Color.white);
        handleRt.anchorMin = new Vector2(0f, 0.5f);
        handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(16f, 26f);
        handleRt.anchoredPosition = new Vector2(320f, 0f);

        // 最上层接收区：放在滑条 GameObject 本身上，点击/拖动整条都能生效
        GameObject hit = new GameObject("DragArea", typeof(RectTransform), typeof(Image), typeof(VolumeSlider));
        RectTransform hitRt = hit.GetComponent<RectTransform>();
        hitRt.SetParent(rt, false);
        Stretch(hitRt);

        Image hitImage = hit.GetComponent<Image>();
        hitImage.sprite = null;
        hitImage.color = new Color(1f, 1f, 1f, 0.02f);   // 留一点点透明度，避免被 Canvas 透明裁剪
        hitImage.raycastTarget = true;
        hitImage.canvasRenderer.cullTransparentMesh = false;

        VolumeSlider slider = hit.GetComponent<VolumeSlider>();
        slider.track = rt;
        slider.fill = fillRt;
        slider.handle = handleRt;
        slider.onChanged = onChanged;
        slider.SetValueWithoutNotify(1f);

        return slider;
    }

    private RectTransform CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = null;
        image.color = color;
        image.raycastTarget = true;

        return rt;
    }

    private void CreateSimpleButton(
        string label,
        Transform parent,
        Vector2 anchoredPos,
        Vector2 size,
        UnityAction onClick,
        int fontSize)
    {
        GameObject go = new GameObject(
            "Button_" + label,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image image = go.GetComponent<Image>();
        image.sprite = null;
        image.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        if (onClick != null)
            button.onClick.AddListener(onClick);

        CreateTextAt(rt, "Text", label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.12f, 1f));
    }

    private Text CreateTextAt(
        Transform parent,
        string name,
        string content,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Text text = go.GetComponent<Text>();
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.text = content;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private Font ResolveFont()
    {
        if (font != null)
            return font;

        Font osFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun" }, 32);

        if (osFont != null)
            return osFont;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}

/// <summary>不依赖 UGUI 事件系统的简易音量条：直接读鼠标，整条区域都可点击、可拖动。</summary>
public class VolumeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public RectTransform track;
    public RectTransform fill;
    public RectTransform handle;
    public UnityAction<float> onChanged;

    public float value = 1f;

    private bool mouseDragging;

    public void SetValueWithoutNotify(float newValue)
    {
        value = Mathf.Clamp01(newValue);
        Refresh();
    }

    // ---- UGUI 事件（如果射线能命中就生效） ----

    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 local;
        if (TryScreenToLocal(eventData.position, eventData.pressEventCamera, out local))
            ApplyLocal(local);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 local;
        if (TryScreenToLocal(eventData.position, eventData.pressEventCamera, out local))
            ApplyLocal(local);
    }

    // ---- 兜底：直接读鼠标，绕开 UGUI 射线 ----

    private void Update()
    {
        if (track == null) return;

        Vector2 mouse = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 local;
            if (TryScreenToLocal(mouse, null, out local) && InsideArea(local))
            {
                mouseDragging = true;
                ApplyLocal(local);
                Debug.Log("[VolumeSlider] 开始拖动，值 = " + value.ToString("0.00"));
            }
        }
        else if (mouseDragging)
        {
            if (Input.GetMouseButton(0))
            {
                Vector2 local;
                if (TryScreenToLocal(mouse, null, out local))
                    ApplyLocal(local);
            }
            else
            {
                mouseDragging = false;
            }
        }
    }

    private bool TryScreenToLocal(Vector2 screenPoint, Camera cam, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screenPoint, cam, out local);
    }

    private bool InsideArea(Vector2 local)
    {
        Rect r = track.rect;
        return local.x >= r.xMin - 8f && local.x <= r.xMax + 8f
            && local.y >= r.yMin - 12f && local.y <= r.yMax + 12f;
    }

    private void ApplyLocal(Vector2 local)
    {
        float width = track.rect.width;
        if (width <= 0.01f) return;

        value = Mathf.Clamp01((local.x - track.rect.xMin) / width);
        Refresh();

        if (onChanged != null)
            onChanged(value);
    }

    private void Refresh()
    {
        if (track == null) return;

        float width = track.rect.width;

        if (fill != null)
            fill.sizeDelta = new Vector2(width * value, fill.sizeDelta.y);

        if (handle != null)
            handle.anchoredPosition = new Vector2(width * value, handle.anchoredPosition.y);
    }
}
