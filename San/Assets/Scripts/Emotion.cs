using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 校准结果 → 碎碎念 → 3D 情绪球飞行 → 落袋销毁 →
/// 切回第一视角 → 背包旁边出现小 3D 情绪球 → 触发手机事件。
/// 落袋球不挂在任何相机下，每帧按当前启用相机重新定位到背包图标旁边。
/// </summary>
public class EmotionRewardFly : MonoBehaviour
{
    [Header("情绪球预制体（3D）")]
    public GameObject happyBallPrefab;
    public GameObject sadBallPrefab;

    [Header("结果文字")]
    public string happyMumble = "我真是太棒了";
    public string sadMumble = "今天真是不幸";

    [Header("碎碎念")]
    [Tooltip("碎碎念气泡的位置来源，情绪球从这里飞出")]
    public Transform mumbleAnchor;

    [Tooltip("气泡里的旧版 UI Text，可留空自动找")]
    public Text mumbleLabel;

    [Tooltip("气泡根物体，留空则使用 Mumble Anchor 所在物体")]
    public GameObject mumbleRoot;

    [Tooltip("是否由本脚本控制碎碎念的显示和隐藏")]
    public bool controlMumbleVisibility = true;

    [Tooltip("碎碎念显示多久后消失")]
    public float mumbleShowSeconds = 5f;

    [Tooltip("碎碎念消失后，等待多久情绪球出场")]
    public float ballDelayAfterMumble = 0.4f;

    [Header("情绪背包")]
    [Tooltip("HUD 上的情绪背包图标")]
    public RectTransform backpackIcon;

    [Tooltip("返回 3D 时自动打开背包图标及其父级")]
    public bool activateBackpackOnReturn = true;

    [Header("落袋后的 3D 情绪球")]
    [Tooltip("落袋球预制体，留空则复用飞行球预制体")]
    public GameObject landedBallPrefab;

    [Tooltip("相对背包图标的屏幕像素偏移")]
    public Vector2 landedScreenOffset = new Vector2(-90f, 0f);

    [Tooltip("落袋球距离相机的深度")]
    public float landedDistance = 0.9f;

    [Tooltip("落袋球在屏幕上的直径，单位像素")]
    public float landedScreenSize = 70f;

    public bool landedAutoSize = true;

    [Tooltip("关闭自动尺寸时使用这个缩放")]
    public float landedScale = 0.15f;

    public bool landedSpin = true;
    public float landedSpinSpeed = 60f;
    public float landedSpinAxisTilt = 20f;

    [Header("飞行球")]
    [Tooltip("飞行球距离相机的深度")]
    public float flightDistance = 1.2f;

    public float appearSeconds = 0.25f;
    public float flyToCenterSeconds = 0.5f;

    [Tooltip("在屏幕中央自转的时间")]
    public float centerHoldSeconds = 1.8f;

    [Tooltip("中央自转速度，单位度/秒")]
    public float spinDegreesPerSecond = 160f;

    [Tooltip("自转轴倾斜角度，模拟地球")]
    public float spinAxisTilt = 20f;

    public float flyToBackpackSeconds = 0.6f;

    [Tooltip("飞到背包后多久销毁飞行球")]
    public float destroyDelay = 0.05f;

    [Header("飞行球尺寸")]
    public bool autoFitScreenSize = true;

    [Tooltip("飞行球在屏幕上的直径，单位像素")]
    public float flightScreenSize = 140f;

    [Tooltip("关闭自动尺寸时使用这个缩放")]
    public float endScale = 0.4f;

    [Header("返回 3D")]
    public TaskHUD taskHUD;

    [Tooltip("由本脚本调用 TaskHUD.ReturnTo3D")]
    public bool callReturnTo3D = true;

    [Tooltip("留空则自动使用 TaskHUD 的返回相机或 Camera.main")]
    public Camera worldCamera;

    [Header("落袋后的视角")]
    [Tooltip("情绪球落袋后切换回的第一视角相机；留空自动用玩家身上的 Camera Transform")]
    public Camera firstPersonCamera;

    [Tooltip("情绪球飞进背包后，视角切换回第一视角")]
    public bool switchToFirstPersonAfterLanding = true;

    [Header("收集完成后的手机事件")]
    [Tooltip("情绪球落袋后要震动的手机，留空自动找")]
    public PhoneQuest phoneQuest;

    public bool debugLog = true;

    private bool playing;
    private Camera cam;

    // 已经落袋的小情绪球，每帧跟随当前相机重新定位
    private class LandedBall
    {
        public Transform transform;
        public float diameter;
    }

    private readonly List<LandedBall> landedBalls =
        new List<LandedBall>();

    // ====================================================================

    /// <summary>
    /// happy = true：绿色区域，开心球。
    /// happy = false：红色区域，失望球。
    /// </summary>
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
            if (taskHUD == null)
                taskHUD = FindObjectOfType<TaskHUD>(true);

            if (taskHUD != null)
                taskHUD.ReturnTo3D();
            else
                Debug.LogWarning(
                    "[Emotion] 找不到 TaskHUD，3D 场景可能不会恢复",
                    this);
        }

        yield return null;
        yield return null;

        // 2. 确定当前使用的相机
        cam = ResolveCamera();

        if (cam == null)
        {
            Debug.LogError(
                "[Emotion] 找不到可用相机，情绪球无法飞行",
                this);

            playing = false;
            yield break;
        }

        // 3. 确保背包图标可见
        if (backpackIcon != null && activateBackpackOnReturn)
            ActivateChain(backpackIcon);

        // 4. 显示碎碎念
        GameObject mumbleGO = mumbleRoot != null
            ? mumbleRoot
            : (mumbleAnchor != null
                ? mumbleAnchor.gameObject
                : null);

        bool hasMumble = mumbleGO != null;

        if (!hasMumble)
        {
            Debug.LogError(
                "[Emotion] 没指定碎碎念物体，碎碎念不会显示",
                this);
        }
        else
        {
            if (controlMumbleVisibility)
                ActivateChain(mumbleGO.transform);

            if (mumbleLabel == null)
                mumbleLabel =
                    mumbleGO.GetComponentInChildren<Text>(true);

            if (mumbleLabel != null)
            {
                mumbleLabel.text =
                    happy ? happyMumble : sadMumble;

                if (debugLog)
                {
                    Debug.Log(
                        "[Emotion] 碎碎念：" + mumbleLabel.text +
                        " | 物体=" + mumbleGO.name +
                        " | 层级激活=" +
                        mumbleGO.activeInHierarchy +
                        " | 世界位置=" +
                        mumbleGO.transform.position,
                        mumbleGO);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[Emotion] 碎碎念里没找到旧版 UI Text",
                    mumbleGO);
            }
        }

        yield return null;

        // 5. 碎碎念显示一段时间后消失
        if (hasMumble && mumbleShowSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(
                mumbleShowSeconds);
        }

        if (hasMumble && controlMumbleVisibility)
        {
            mumbleGO.SetActive(false);

            if (debugLog)
                Debug.Log("[Emotion] 碎碎念已消失");
        }

        // 6. 碎碎念消失后，等待情绪球出场
        if (hasMumble && ballDelayAfterMumble > 0f)
        {
            yield return new WaitForSecondsRealtime(
                ballDelayAfterMumble);
        }

        // 7. 生成飞行情绪球
        GameObject prefab =
            happy ? happyBallPrefab : sadBallPrefab;

        if (prefab == null)
        {
            Debug.LogError(
                "[Emotion] " +
                (happy ? "开心" : "失望") +
                "情绪球预制体没拖",
                this);

            playing = false;
            yield break;
        }

        GameObject ball = Instantiate(prefab);
        ball.name = happy
            ? "EmotionBall_Happy"
            : "EmotionBall_Sad";

        ball.SetActive(true);

        // 飞行球挂到相机下，飞行路径固定在屏幕上
        ball.transform.SetParent(cam.transform, true);
        ball.transform.localScale = Vector3.one;

        float ballScale = ResolveFlightScale(ball);

        // 8. 起点：碎碎念的位置
        Vector3 startWorld = mumbleAnchor != null
            ? AnchorWorld(mumbleAnchor)
            : FallbackStartWorld();

        Vector3 startLocal =
            cam.transform.InverseTransformPoint(startWorld);

        Vector3 centerLocal =
            new Vector3(0f, 0f, flightDistance);

        ball.transform.localPosition = startLocal;
        ball.transform.localRotation = Quaternion.identity;
        ball.transform.localScale = Vector3.zero;

        // 9. 情绪球出现
        float appear = Mathf.Max(0.0001f, appearSeconds);
        float t = 0f;

        while (t < appear)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / appear);

            ball.transform.localScale =
                Vector3.one *
                Mathf.Lerp(0f, ballScale, k);

            yield return null;
        }

        ball.transform.localScale =
            Vector3.one * ballScale;

        // 10. 飞到屏幕中央
        float toCenter =
            Mathf.Max(0.0001f, flyToCenterSeconds);

        t = 0f;

        while (t < toCenter)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / toCenter);
            float eased = 1f - (1f - k) * (1f - k);

            ball.transform.localPosition =
                Vector3.Lerp(
                    startLocal,
                    centerLocal,
                    eased);

            yield return null;
        }

        ball.transform.localPosition = centerLocal;

        // 11. 在中央像地球一样自转
        Vector3 spinAxis =
            Quaternion.AngleAxis(
                spinAxisTilt,
                Vector3.forward) * Vector3.up;

        t = 0f;

        while (t < centerHoldSeconds)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            ball.transform.Rotate(
                spinAxis,
                spinDegreesPerSecond * dt,
                Space.Self);

            yield return null;
        }

        // 12. 飞入情绪背包
        float toBag =
            Mathf.Max(0.0001f, flyToBackpackSeconds);

        Vector3 from = ball.transform.localPosition;
        t = 0f;

        while (t < toBag)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / toBag);
            float eased = k * k;

            ball.transform.localPosition =
                Vector3.Lerp(
                    from,
                    BackpackLocal(),
                    eased);

            ball.transform.localScale =
                Vector3.one *
                Mathf.Lerp(
                    ballScale,
                    ballScale * 0.6f,
                    eased);

            yield return null;
        }

        ball.transform.localPosition = BackpackLocal();

        // 13. 销毁飞行球
        if (destroyDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                destroyDelay);
        }

        Destroy(ball);

        if (debugLog)
            Debug.Log("[Emotion] 飞行球已销毁");

        // 14. 先切回第一视角
        SwitchToFirstPerson();

        // 15. 在背包旁边生成小的 3D 情绪球
        SpawnLandedBall(happy);

        // 16. 情绪球收集完成，启动手机事件
        if (phoneQuest == null)
            phoneQuest = FindObjectOfType<PhoneQuest>(true);

        if (phoneQuest != null)
        {
            phoneQuest.StartPhoneEvent();
        }
        else
        {
            Debug.LogWarning(
                "[Emotion] 场景里找不到 PhoneQuest，" +
                "手机事件不会触发",
                this);
        }

        playing = false;
    }

    // ====================================================================
    // 每帧让落袋球贴住背包图标（跟随当前启用的相机）
    // ====================================================================

    private void LateUpdate()
    {
        if (landedBalls.Count == 0) return;

        Camera activeCam = ActiveCamera();

        if (activeCam == null) return;

        for (int i = landedBalls.Count - 1; i >= 0; i--)
        {
            LandedBall entry = landedBalls[i];

            if (entry == null || entry.transform == null)
            {
                landedBalls.RemoveAt(i);
                continue;
            }

            // 背包图标被隐藏时（例如进入 2D），球也一起隐藏
            bool visible =
                backpackIcon != null &&
                backpackIcon.gameObject.activeInHierarchy;

            if (entry.transform.gameObject.activeSelf != visible)
                entry.transform.gameObject.SetActive(visible);

            if (!visible) continue;

            PlaceLandedBall(entry, activeCam);
        }
    }

    private void PlaceLandedBall(
        LandedBall entry,
        Camera activeCam)
    {
        if (entry == null ||
            entry.transform == null ||
            activeCam == null)
        {
            return;
        }

        Vector2 screen;

        if (backpackIcon != null)
        {
            screen =
                ScreenOf(backpackIcon, activeCam) +
                landedScreenOffset;
        }
        else
        {
            screen =
                new Vector2(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f) +
                landedScreenOffset;
        }

        Vector3 world = activeCam.ScreenToWorldPoint(
            new Vector3(
                screen.x,
                screen.y,
                landedDistance));

        entry.transform.position = world;

        if (landedAutoSize && entry.diameter > 0.0001f)
        {
            float worldPerPixel =
                2f * landedDistance *
                Mathf.Tan(
                    activeCam.fieldOfView *
                    0.5f * Mathf.Deg2Rad) /
                Mathf.Max(1, Screen.height);

            float scale = Mathf.Max(
                0.0001f,
                landedScreenSize * worldPerPixel /
                entry.diameter);

            entry.transform.localScale =
                Vector3.one * scale;
        }
        else
        {
            entry.transform.localScale =
                Vector3.one * Mathf.Max(0.0001f, landedScale);
        }
    }

    // 当前真正在渲染的相机
    private Camera ActiveCamera()
    {
        if (cam != null && cam.enabled)
            return cam;

        if (Camera.main != null && Camera.main.enabled)
            return Camera.main;

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
                return all[i];
        }

        return cam;
    }

    // ====================================================================
    // 视角切换
    // ====================================================================

    private void SwitchToFirstPerson()
    {
        if (!switchToFirstPersonAfterLanding) return;

        Camera target = firstPersonCamera;

        // 没手动指定，就用玩家身上的第一视角相机
        if (target == null)
        {
            PlayerMover pm = FindObjectOfType<PlayerMover>();

            if (pm != null && pm.cameraTransform != null)
                target = pm.cameraTransform.GetComponent<Camera>();
        }

        if (target == null)
            target = Camera.main;

        if (target == null)
        {
            Debug.LogWarning(
                "[Emotion] 找不到第一视角相机，视角没有切换",
                this);
            return;
        }

        // 相机或它的父级可能是关着的，先打开
        ActivateChain(target.transform);

        // 关掉其它所有相机，只留第一视角
        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Camera current = all[i];

            if (current == null || current == target) continue;

            if (current.enabled)
                current.enabled = false;
        }

        target.enabled = true;

        // 同步内部相机
        cam = target;

        if (debugLog)
            Debug.Log("[Emotion] 已切回第一视角：" + target.name, this);
    }

    // ====================================================================
    // 落袋球
    // ====================================================================

    private void SpawnLandedBall(bool happy)
    {
        GameObject prefab = landedBallPrefab;

        if (prefab == null)
        {
            prefab = happy
                ? happyBallPrefab
                : sadBallPrefab;
        }

        if (prefab == null)
        {
            Debug.LogWarning(
                "[Emotion] 没有可用的落袋球预制体",
                this);
            return;
        }

        GameObject ball = Instantiate(prefab);

        ball.name = happy
            ? "LandedEmotionBall_Happy"
            : "LandedEmotionBall_Sad";

        ball.SetActive(true);

        // 关键：不再挂在相机下，否则换视角后就固定死了
        ball.transform.SetParent(null, true);
        ball.transform.localScale = Vector3.one;

        LandedBall entry = new LandedBall
        {
            transform = ball.transform,
            diameter = MeasureDiameter(ball)
        };

        landedBalls.Add(entry);

        // 先摆一次，避免第一帧停留在原点
        PlaceLandedBall(entry, ActiveCamera());

        if (landedSpin)
            StartCoroutine(SpinLanded(ball.transform));

        if (debugLog)
        {
            Debug.Log(
                "[Emotion] 背包旁边已生成 3D 情绪球：" +
                ball.name,
                ball);
        }
    }

    private IEnumerator SpinLanded(Transform ball)
    {
        Vector3 axis =
            Quaternion.AngleAxis(
                landedSpinAxisTilt,
                Vector3.forward) * Vector3.up;

        while (ball != null)
        {
            ball.Rotate(
                axis,
                landedSpinSpeed *
                Time.unscaledDeltaTime,
                Space.Self);

            yield return null;
        }
    }

    // ====================================================================
    // 相机与坐标
    // ====================================================================

    private Camera ResolveCamera()
    {
        if (worldCamera != null)
            return worldCamera;

        if (taskHUD == null)
            taskHUD = FindObjectOfType<TaskHUD>(true);

        if (taskHUD != null &&
            taskHUD.cameraOnReturn != null)
        {
            return taskHUD.cameraOnReturn;
        }

        if (Camera.main != null)
            return Camera.main;

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
                return all[i];
        }

        return all.Length > 0 ? all[0] : null;
    }

    private static void ActivateChain(Transform target)
    {
        while (target != null)
        {
            if (!target.gameObject.activeSelf)
                target.gameObject.SetActive(true);

            target = target.parent;
        }
    }

    private Vector3 AnchorWorld(Transform anchor)
    {
        Canvas canvas =
            anchor.GetComponentInParent<Canvas>();

        if (canvas != null &&
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            Vector2 screenPosition = anchor.position;

            return cam.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    flightDistance * 0.8f));
        }

        return anchor.position;
    }

    private Vector3 FallbackStartWorld()
    {
        return cam.transform.position
             + cam.transform.forward *
               (flightDistance * 0.8f)
             - cam.transform.up * 0.15f;
    }

    private Vector2 ScreenOf(
        Transform target,
        Camera source)
    {
        Canvas canvas =
            target.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            if (canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay)
            {
                return target.position;
            }

            Camera canvasCamera =
                canvas.worldCamera != null
                    ? canvas.worldCamera
                    : source;

            if (canvasCamera != null)
            {
                return
                    RectTransformUtility.WorldToScreenPoint(
                        canvasCamera,
                        target.position);
            }
        }

        return source != null
            ? (Vector2)source.WorldToScreenPoint(
                target.position)
            : (Vector2)target.position;
    }

    private Vector3 BackpackLocal()
    {
        if (backpackIcon == null)
        {
            return new Vector3(
                0f,
                0f,
                flightDistance);
        }

        Vector2 screen = ScreenOf(backpackIcon, cam);

        Vector3 world = cam.ScreenToWorldPoint(
            new Vector3(
                screen.x,
                screen.y,
                flightDistance));

        return cam.transform.InverseTransformPoint(world);
    }

    // ====================================================================
    // 尺寸计算
    // ====================================================================

    private float ResolveFlightScale(GameObject ball)
    {
        if (!autoFitScreenSize)
            return Mathf.Max(0.0001f, endScale);

        return ScaleForPixels(
            ball,
            flightScreenSize,
            flightDistance);
    }

    private float ScaleForPixels(
        GameObject target,
        float pixels,
        float distance)
    {
        float diameter = MeasureDiameter(target);

        float worldPerPixel =
            2f * distance *
            Mathf.Tan(
                cam.fieldOfView *
                0.5f *
                Mathf.Deg2Rad) /
            Mathf.Max(1, Screen.height);

        return Mathf.Max(
            0.0001f,
            pixels * worldPerPixel / diameter);
    }

    private static float MeasureDiameter(
        GameObject target)
    {
        float diameter = 1f;

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds bounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer current = renderers[i];

            if (current == null) continue;
            if (current is ParticleSystemRenderer) continue;

            if (!hasBounds)
            {
                bounds = current.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(current.bounds);
            }
        }

        if (hasBounds)
        {
            diameter = Mathf.Max(
                bounds.size.x,
                Mathf.Max(
                    bounds.size.y,
                    bounds.size.z));
        }

        if (diameter <= 0.0001f)
            diameter = 1f;

        return diameter;
    }
}