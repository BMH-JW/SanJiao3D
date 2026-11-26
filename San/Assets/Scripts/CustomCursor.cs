using UnityEngine;
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

        BuildUI();
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
        // 隐藏系统光标，只显示自定义图片
        Cursor.visible = false;
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        UpdatePosition();
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
