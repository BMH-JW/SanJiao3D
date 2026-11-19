using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 校准结果 → 碎碎念 → 3D 情绪球飞行 → 落袋销毁 → 背包旁边出现一个小的 3D 情绪球
/// 绿色（成功）：我真是太棒了 / 开心球
/// 红色（失败）：今天真是不幸 / 失望球
/// </summary>
public class EmotionRewardFly : MonoBehaviour
{
    [Header("情绪球预制体（3D 特效）")]
    public GameObject happyBallPrefab;
    public GameObject sadBallPrefab;

    [Header("文字")]
    public string happyMumble = "我真是太棒了";
    public string sadMumble = "今天真是不幸";

    [Header("碎碎念")]
    [Tooltip("碎碎念气泡 / 文字所在的物体，情绪球从这里飞出")]
    public Transform mumbleAnchor;

    [Tooltip("气泡里的 Text，可留空自动找；TextMeshPro 不支持这个字段")]
    public Text mumbleLabel;

    [Tooltip("气泡根物体，留空则用 Mumble Anchor 所在的物体")]
    public GameObject mumbleRoot;

    [Tooltip("是否由本脚本控制气泡的显示 / 隐藏")]
    public bool controlMumbleVisibility = true;

    [Tooltip("碎碎念显示多久（秒）后消失，消失后情绪球才出场")]
    public float mumbleShowSeconds = 5f;

    [Tooltip("碎碎念消失后，等多久情绪球出场")]
    public float ballDelayAfterMumble = 0.4f;

    [Header("情绪背包")]
    [Tooltip("情绪背包图标（屏幕 HUD 上的那个图标）")]
    public RectTransform backpackIcon;

    [Tooltip("返回 3D 时自动打开背包图标（连同父级）")]
    public bool activateBackpackOnReturn = true;

    [Header("落袋后的 3D 情绪球（放在背包旁边）")]
    [Tooltip("落袋球的预制体，留空则复用飞行球预制体")]
    public GameObject landedBallPrefab;

    [Tooltip("相对背包图标的屏幕像素偏移：负 X 往左，正 Y 往上")]
    public Vector2 landedScreenOffset = new Vector2(-90f, 0f);

    [Tooltip("落袋球距离相机的深度（米），越小越靠前")]
    public float landedDistance = 0.9f;

    [Tooltip("落袋球在屏幕上的直径（像素）")]
    public float landedScreenSize = 70f;

    public bool landedAutoSize = true;

    [Tooltip("关闭 Landed Auto Size 时用这个缩放")]
    public float landedScale = 0.15f;

    [Tooltip("落袋球是否一直自转")]
    public bool landedSpin = true;

    [Tooltip("落袋球自转速度（度/秒）")]
    public float landedSpinSpeed = 60f;

    [Tooltip("落袋球自转轴倾斜角度")]
    public float landedSpinAxisTilt = 20f;

    [Header("飞行（3D 球）")]
    [Tooltip("球距离相机的固定深度（米）")]
    public float flightDistance = 1.2f;

    public float appearSeconds = 0.25f;
    public float flyToCenterSeconds = 0.5f;

    [Tooltip("在屏幕中央自转的时间")]
    public float centerHoldSeconds = 1.8f;

    [Tooltip("中央自转速度（度/秒）")]
    public float spinDegreesPerSecond = 160f;

    [Tooltip("中央自转轴倾斜角度")]
    public float spinAxisTilt = 20f;

    public float flyToBackpackSeconds = 0.6f;

    [Tooltip("飞到背包后多久销毁飞行球")]
    public float destroyDelay = 0.05f;

    [Header("3D 球尺寸")]
    public bool autoFitScreenSize = true;

    [Tooltip("飞行时球在屏幕上的直径（像素）")]
    public float landedScreenSizeFlight = 140f;

    [Tooltip("关闭 Auto Fit 时用这个缩放")]
    public float endScale = 0.4f;

    [Header("返回 3D")]
    public TaskHUD taskHUD;
    public bool callReturnTo3D = true;
    public Camera worldCamera;
    public bool debugLog = true;

    private bool playing;
    private Camera cam;

    // ====================================================================

    /// <summary>happy = true 绿色（开心），false 红色（失望）</summary>
    public void Play(bool happy)
    {
        if (playing) return;
        StartCoroutine(PlayRoutine(happy));
    }

    private IEnumerator PlayRoutine(bool happy)
    {
        playing = true;

        // 1. 回到 3D
        if (callReturnTo3D)
        {
            if (taskHUD == null) taskHUD = FindObjectOfType<TaskHUD>(true);
            if (taskHUD != null) taskHUD.ReturnTo3D();
            else Debug.LogWarning("[Emotion] 找不到 TaskHUD，3D 场景可能不会恢复", this);
        }

        yield return null;
        yield return null;

        // 2. 相机
        cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogError("[Emotion] 找不到可用相机，情绪球无法飞行", this);
            playing = false;
            yield break;
        }

        // 3. 背包图标
        if (backpackIcon != null && activateBackpackOnReturn)
            ActivateChain(backpackIcon);

        // 4. 碎碎念
        GameObject mumbleGO = mumbleRoot != null
            ? mumbleRoot
            : (mumbleAnchor != null ? mumbleAnchor.gameObject : null);

        bool hasMumble = mumbleGO != null;

        if (!hasMumble)
        {
            Debug.LogError("[Emotion] 没指定碎碎念物体（Mumble Root / Mumble Anchor 都是空），碎碎念不会显示", this);
        }
        else
        {
            if (controlMumbleVisibility)
            {
                ActivateChain(mumbleGO.transform);
                if (!mumbleGO.activeInHierarchy)
                    Debug.LogError("[Emotion] 碎碎念打开了但层级里仍不可见，检查它的父级或 Canvas", mumbleGO);
            }

            if (mumbleLabel == null)
                mumbleLabel = mumbleGO.GetComponentInChildren<Text>(true);

            if (mumbleLabel != null)
            {
                mumbleLabel.text = happy ? happyMumble : sadMumble;

                if (debugLog)
                    Debug.Log("[Emotion] 碎碎念：" + mumbleLabel.text +
                              " | 物体=" + mumbleGO.name +
                              " | 层级激活=" + mumbleGO.activeInHierarchy +
                              " | 世界位置=" + mumbleGO.transform.position, mumbleGO);
            }
            else
            {
                Debug.LogWarning("[Emotion] 碎碎念里没找到 Text（如果用 TextMeshPro，请把字段换成 TMP_Text）", mumbleGO);
            }
        }

        yield return null;

        // 5. 碎碎念显示 → 消失 → 情绪球才出场
        if (hasMumble && mumbleShowSeconds > 0f)
            yield return new WaitForSecondsRealtime(mumbleShowSeconds);

        if (hasMumble && controlMumbleVisibility)
        {
            mumbleGO.SetActive(false);
            if (debugLog) Debug.Log("[Emotion] 碎碎念已消失");
        }

        if (hasMumble && ballDelayAfterMumble > 0f)
            yield return new WaitForSecondsRealtime(ballDelayAfterMumble);

        // 6. 生成飞行球
        GameObject prefab = happy ? happyBallPrefab : sadBallPrefab;
        if (prefab == null)
        {
            Debug.LogError("[Emotion] " + (happy ? "开心" : "失望") + "情绪球预制体没拖", this);
            playing = false;
            yield break;
        }

        GameObject ball = Instantiate(prefab);
        ball.name = happy ? "EmotionBall_Happy" : "EmotionBall_Sad";
        ball.SetActive(true);

        ball.transform.SetParent(cam.transform, true);
        ball.transform.localScale = Vector3.one;

        float ballScale = ResolveFlightScale(ball);

        // 7. 起点
        Vector3 startWorld = mumbleAnchor != null ? AnchorWorld(mumbleAnchor) : FallbackStartWorld();
        Vector3 startLocal = cam.transform.InverseTransformPoint(startWorld);
        Vector3 centerLocal = new Vector3(0f, 0f, flightDistance);

        ball.transform.localPosition = startLocal;
        ball.transform.localRotation = Quaternion.identity;
        ball.transform.localScale = Vector3.zero;

        // 8. 出现
        float appear = Mathf.Max(0.0001f, appearSeconds);
        float t = 0f;
        while (t < appear)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / appear);
            ball.transform.localScale = Vector3.one * Mathf.Lerp(0f, ballScale, k);
            yield return null;
        }
        ball.transform.localScale = Vector3.one * ballScale;

        // 9. 飞到屏幕中央
        float toCenter = Mathf.Max(0.0001f, flyToCenterSeconds);
        t = 0f;
        while (t < toCenter)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / toCenter);
            float e = 1f - (1f - k) * (1f - k);
            ball.transform.localPosition = Vector3.Lerp(startLocal, centerLocal, e);
            yield return null;
        }
        ball.transform.localPosition = centerLocal;

        // 10. 中央自转（像地球）
        Vector3 spinAxis = Quaternion.AngleAxis(spinAxisTilt, Vector3.forward) * Vector3.up;
        t = 0f;
        while (t < centerHoldSeconds)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            ball.transform.Rotate(spinAxis, spinDegreesPerSecond * dt, Space.Self);
            yield return null;
        }

        // 11. 飞入背包
        float toBag = Mathf.Max(0.0001f, flyToBackpackSeconds);
        Vector3 from = ball.transform.localPosition;
        t = 0f;
        while (t < toBag)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / toBag);
            float e = k * k;
            ball.transform.localPosition = Vector3.Lerp(from, BackpackLocal(), e);
            ball.transform.localScale = Vector3.one * Mathf.Lerp(ballScale, ballScale * 0.6f, e);
            yield return null;
        }
        ball.transform.localPosition = BackpackLocal();

        // 12. 销毁飞行球
        if (destroyDelay > 0f) yield return new WaitForSecondsRealtime(destroyDelay);
        Destroy(ball);
        if (debugLog) Debug.Log("[Emotion] 飞行球已销毁");

        // 13. 在背包旁边生成一个小的 3D 情绪球，并保持自转
        SpawnLandedBall(happy);

        playing = false;
    }

    // ====================================================================
    // 工具
    // ====================================================================

    private Camera ResolveCamera()
    {
        if (worldCamera != null) return worldCamera;

        if (taskHUD == null) taskHUD = FindObjectOfType<TaskHUD>(true);
        if (taskHUD != null && taskHUD.cameraOnReturn != null)
            return taskHUD.cameraOnReturn;

        if (Camera.main != null) return Camera.main;

        Camera[] all = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled) return all[i];

        return all.Length > 0 ? all[0] : null;
    }

    private static void ActivateChain(Transform t)
    {
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private Vector3 AnchorWorld(Transform anchor)
    {
        Canvas c = anchor.GetComponentInParent<Canvas>();
        if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 sp = anchor.position;
            return cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, flightDistance * 0.8f));
        }
        return anchor.position;
    }

    private Vector3 FallbackStartWorld()
    {
        return cam.transform.position
             + cam.transform.forward * (flightDistance * 0.8f)
             - cam.transform.up * 0.15f;
    }

    private Vector2 ScreenOf(Transform tr)
    {
        Canvas c = tr.GetComponentInParent<Canvas>();
        if (c != null)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return tr.position;

            Camera cc = c.worldCamera != null ? c.worldCamera : cam;
            if (cc != null) return RectTransformUtility.WorldToScreenPoint(cc, tr.position);
        }
        return cam != null ? (Vector2)cam.WorldToScreenPoint(tr.position) : (Vector2)tr.position;
    }

    private Vector3 BackpackLocal()
    {
        if (backpackIcon == null)
            return new Vector3(0f, 0f, flightDistance);

        Vector2 screen = ScreenOf(backpackIcon);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, flightDistance));
        return cam.transform.InverseTransformPoint(world);
    }

    // 落袋球在相机本地空间里的位置：背包图标的屏幕位置 + 像素偏移
    private Vector3 LandedLocal()
    {
        Vector2 screen;

        if (backpackIcon != null)
            screen = ScreenOf(backpackIcon) + landedScreenOffset;
        else
            screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + landedScreenOffset;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, landedDistance));
        return cam.transform.InverseTransformPoint(world);
    }

    // 生成落袋后的小 3D 球（不是 UI 图片）
    private void SpawnLandedBall(bool happy)
    {
        GameObject prefab = landedBallPrefab;

        if (prefab == null)
            prefab = happy ? happyBallPrefab : sadBallPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("[Emotion] 没有可用的落袋球预制体", this);
            return;
        }

        GameObject ball = Instantiate(prefab);
        ball.name = happy ? "LandedEmotionBall_Happy" : "LandedEmotionBall_Sad";
        ball.SetActive(true);

        // 挂到相机下：玩家走动 / 转视角时，它一直固定在背包图标旁边
        ball.transform.SetParent(cam.transform, true);
        ball.transform.localScale = Vector3.one;

        ball.transform.localPosition = LandedLocal();
        ball.transform.localRotation = Quaternion.identity;

        float scale = landedAutoSize
            ? ScaleForPixels(ball, landedScreenSize, landedDistance)
            : Mathf.Max(0.0001f, landedScale);

        ball.transform.localScale = Vector3.one * scale;

        if (landedSpin)
            StartCoroutine(SpinLanded(ball.transform));

        if (debugLog)
            Debug.Log("[Emotion] 背包旁边已生成 3D 情绪球：" + ball.name +
                      " | 屏幕偏移=" + landedScreenOffset +
                      " | 缩放=" + scale, ball);
    }

    private IEnumerator SpinLanded(Transform ball)
    {
        Vector3 axis = Quaternion.AngleAxis(landedSpinAxisTilt, Vector3.forward) * Vector3.up;

        while (ball != null)
        {
            ball.Rotate(axis, landedSpinSpeed * Time.unscaledDeltaTime, Space.Self);
            yield return null;
        }
    }

    private float ResolveFlightScale(GameObject ball)
    {
        if (!autoFitScreenSize) return Mathf.Max(0.0001f, endScale);
        return ScaleForPixels(ball, landedScreenSizeFlight, flightDistance);
    }

    // 按屏幕像素反推世界缩放
    private float ScaleForPixels(GameObject obj, float pixels, float distance)
    {
        float diameter = MeasureDiameter(obj);
        float worldPerPixel = 2f * distance
                            * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                            / Mathf.Max(1, Screen.height);

        return Mathf.Max(0.0001f, pixels * worldPerPixel / diameter);
    }

    private static float MeasureDiameter(GameObject obj)
    {
        float diameter = 1f;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds b = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;

            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (has) diameter = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (diameter <= 0.0001f) diameter = 1f;

        return diameter;
    }
}