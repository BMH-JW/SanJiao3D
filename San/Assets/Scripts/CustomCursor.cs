using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在游戏画面内显示自定义鼠标光标（图片可更换）。
/// 会自动解除鼠标锁定并隐藏系统光标，用一张 UI 图片跟随鼠标位置。
/// 想换图片：把本脚本挂到场景里任意物体上，在 Inspector 指定 Cursor Sprite 即可；
/// 不挂也可以，会自动用内置圆环光标。
/// </summary>
public class CustomCursor : MonoBehaviour
{
    [Header("鼠标图片（留空则使用内置圆环）")]
    public Sprite cursorSprite;
    [Header("显示大小（像素）")]
    public Vector2 cursorSize = new Vector2(40f, 40f);
    [Header("热点偏移（图片中心相对鼠标点的像素偏移）")]
    public Vector2 hotspot = Vector2.zero;

    private static CustomCursor instance;
    private Image cursorImage;
    private Sprite defaultSprite;

    // 缓存场景里是否存在第一人称控制器（PlayerMover），避免每帧 FindObjectOfType
    private PlayerMover cachedPlayerMover;
    private bool playerMoverResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<CustomCursor>() != null) return;

        GameObject go = new GameObject("CustomCursor(Auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<CustomCursor>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUI();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 换场景后重新判定当前场景有没有第一人称控制器
        cachedPlayerMover = null;
        playerMoverResolved = false;
    }

    private PlayerMover GetPlayerMover()
    {
        if (!playerMoverResolved)
        {
            cachedPlayerMover = FindObjectOfType<PlayerMover>();
            playerMoverResolved = true;
        }
        return cachedPlayerMover;
    }

    void BuildUI()
    {
        GameObject canvasGo = new GameObject("CursorCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;   // 盖在所有 UI 之上

        // 故意不加 GraphicRaycaster：鼠标图片不会挡住任何点击

        GameObject imgGo = new GameObject("CursorImage");
        imgGo.transform.SetParent(canvasGo.transform, false);

        cursorImage = imgGo.AddComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
        cursorImage.sprite = cursorSprite != null ? cursorSprite : GetDefaultSprite();

        RectTransform rt = cursorImage.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cursorSize;

        UpdatePosition();
    }

    void Update()
    {
        bool inGameplayScene = GetPlayerMover() != null;

        if (inGameplayScene)
        {
            // 3D 场景：PlayerMover 游玩时会锁定鼠标转视角。锁定时 Input.mousePosition 被钉在
            // 屏幕中心，显示自定义光标没意义，隐藏；只有解锁（打开 UI / 菜单）时才跟随鼠标显示。
            // 这里不要强行解锁，否则 WebGL 下鼠标撞到浏览器边界就没有移动增量，视角转不动。
            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.visible = false;
                if (cursorImage != null) cursorImage.enabled = true;
                UpdatePosition();
            }
            else
            {
                if (cursorImage != null) cursorImage.enabled = false;
            }
        }
        else
        {
            // 封面 / 剧情等纯 UI 场景：没有第一人称控制器，始终保持解锁并显示自定义光标。
            // Cursor.lockState 是全局状态、跨场景不会自动重置——如果从 3D 场景带着“锁定”
            // 状态回到这里，不主动解锁的话自定义光标和系统光标都会一起消失。
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            if (cursorImage != null) cursorImage.enabled = true;
            UpdatePosition();
        }
    }

    void UpdatePosition()
    {
        if (cursorImage == null) return;

        Vector3 p = Input.mousePosition;
        cursorImage.rectTransform.position =
            new Vector3(p.x + hotspot.x, p.y + hotspot.y, 0f);
    }

    // 没有指定图片时生成一个圆环光标
    private Sprite GetDefaultSprite()
    {
        if (defaultSprite != null) return defaultSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                Color c = new Color(1f, 1f, 1f, 0f);
                if (d <= 26f && d >= 18f) c.a = 1f;        // 外圈
                else if (d < 4f) c.a = 1f;                 // 中心点
                else if (d < 18f) c.a = 0.25f;             // 内圈淡色
                if (d <= 27f && d >= 25f) c = new Color(0f, 0f, 0f, 0.9f); // 描边

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        defaultSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        defaultSprite.name = "DefaultCursorSprite";
        return defaultSprite;
    }
}
