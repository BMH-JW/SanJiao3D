using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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

    [Header("2D 碎碎念弹窗（优先于 3D 气泡）")]
    [Tooltip("弹窗容器，可留空；填了会在显示结束后一起关掉")]
    public GameObject thinkPanel;

    [Tooltip("成功时显示的文案物体（场景里写好文字，直接拖进来）")]
    public GameObject happyThinkText;

    [Tooltip("失败时显示的文案物体（场景里写好文字，直接拖进来）")]
    public GameObject sadThinkText;

    [Tooltip("只用一个 Text 切换文案时使用；上面两个文案槽位都不填才生效")]
    public Text thinkLabel;

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

    [Header("置顶渲染")]
    [Tooltip("情绪球始终显示在所有 3D 物体前面，不会被墙/家具挡住")]
    public bool alwaysRenderOnTop = true;

    [Tooltip("置顶渲染专用层；不要和场景里其它物体共用（默认用空白的 31 层）")]
    [Range(8, 31)]
    public int overlayLayer = 31;

    public bool debugLog = true;

    private bool playing;
    private Camera cam;
    private Camera overlayCam;
    private Camera overlayStackCamera;   // 置顶相机当前叠在哪个主相机上

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

        // ReturnTo3D 会关掉其它相机，这里把置顶相机重新打开
        if (alwaysRenderOnTop && overlayCam != null)
            overlayCam.enabled = true;

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

        // 4. 显示碎碎念：优先用 2D 弹窗，没配才用 3D 气泡
        bool useThinkTexts = happyThinkText != null || sadThinkText != null;
        bool useThinkPanel = useThinkTexts || thinkPanel != null || thinkLabel != null;
        GameObject mumbleGO = mumbleRoot != null
            ? mumbleRoot
            : (mumbleAnchor != null
                ? mumbleAnchor.gameObject
                : null);

        bool hasMumble = useThinkPanel || mumbleGO != null;

        if (useThinkTexts)
        {
            // 两句话分别做成两个物体，成功/失败只切显隐，文案在场景里直接改
            if (thinkPanel != null) ActivateChain(thinkPanel.transform);

            if (happyThinkText != null)
            {
                ActivateChain(happyThinkText.transform);
                happyThinkText.SetActive(happy);
            }

            if (sadThinkText != null)
            {
                ActivateChain(sadThinkText.transform);
                sadThinkText.SetActive(!happy);
            }

            if (debugLog)
            {
                Debug.Log(
                    "[Emotion] 2D 弹窗：" +
                    (happy ? "成功文案" : "失败文案"),
                    this);
            }
        }
        else if (useThinkPanel)
        {
            // 单 Text 模式：直接改文字
            Transform anchor = thinkPanel != null
                ? thinkPanel.transform
                : thinkLabel.transform;
            ActivateChain(anchor);

            if (thinkLabel == null && thinkPanel != null)
                thinkLabel = thinkPanel.GetComponentInChildren<Text>(true);

            if (thinkLabel != null)
            {
                thinkLabel.text = happy ? happyMumble : sadMumble;

                if (debugLog)
                {
                    Debug.Log(
                        "[Emotion] 2D 弹窗：" + thinkLabel.text +
                        " | 物体=" + thinkLabel.name,
                        thinkLabel);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[Emotion] Think Panel 里没找到旧版 UI Text",
                    thinkPanel);
            }
        }
        else if (!hasMumble)
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

        if (hasMumble)
        {
            if (useThinkTexts)
            {
                if (happyThinkText != null) happyThinkText.SetActive(false);
                if (sadThinkText != null) sadThinkText.SetActive(false);
                if (thinkPanel != null) thinkPanel.SetActive(false);
            }
            else if (useThinkPanel)
            {
                if (thinkPanel != null) thinkPanel.SetActive(false);
                else if (thinkLabel != null) thinkLabel.gameObject.SetActive(false);
            }
            else if (controlMumbleVisibility && mumbleGO != null)
            {
                mumbleGO.SetActive(false);
            }

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

        // 置顶渲染：换到专用层，由叠加相机画在场景最前面
        if (alwaysRenderOnTop)
        {
            EnsureOverlayCamera();
            SetLayerRecursively(ball, overlayLayer);
        }

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
        SyncOverlayCamera();

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

    // ====================================================================
    // 置顶渲染：用一个只画情绪球层的叠加相机，保证球不被任何物体挡住
    // ====================================================================

    private Camera EnsureOverlayCamera()
    {
        if (!alwaysRenderOnTop) return null;

        if (overlayCam == null)
        {
            GameObject go = new GameObject("EmotionBall_OverlayCamera");
            overlayCam = go.AddComponent<Camera>();
            overlayCam.clearFlags = CameraClearFlags.Depth;
            overlayCam.cullingMask = 1 << overlayLayer;
            overlayCam.allowHDR = true;
            overlayCam.allowMSAA = true;
            overlayCam.enabled = true;

            // URP：作为 Overlay 相机叠到主相机上，而不是独立出一张画面
            UniversalAdditionalCameraData data =
                overlayCam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;

            if (debugLog)
                Debug.Log("[Emotion] 已创建置顶渲染相机，层=" + overlayLayer, overlayCam);
        }

        overlayCam.enabled = true;
        return overlayCam;
    }

    private void SyncOverlayCamera()
    {
        if (overlayCam == null || !overlayCam.enabled) return;

        Camera source = ActiveCamera();
        if (source == null || source == overlayCam) return;

        overlayCam.transform.SetPositionAndRotation(
            source.transform.position,
            source.transform.rotation);

        overlayCam.orthographic = source.orthographic;
        overlayCam.orthographicSize = source.orthographicSize;
        overlayCam.fieldOfView = source.fieldOfView;
        overlayCam.nearClipPlane = source.nearClipPlane;
        overlayCam.farClipPlane = source.farClipPlane;
        overlayCam.cullingMask = 1 << overlayLayer;

        AttachOverlayTo(source);
    }

    // 把置顶相机挂到当前主相机的 URP 相机栈里
    private void AttachOverlayTo(Camera source)
    {
        if (source == null || overlayCam == null) return;

        if (overlayStackCamera != null && overlayStackCamera != source)
        {
            if (overlayStackCamera != overlayCam)
            {
                UniversalAdditionalCameraData oldData =
                    overlayStackCamera.GetUniversalAdditionalCameraData();

                if (oldData != null && oldData.cameraStack.Contains(overlayCam))
                    oldData.cameraStack.Remove(overlayCam);
            }
        }

        UniversalAdditionalCameraData sourceData =
            source.GetUniversalAdditionalCameraData();

        if (sourceData.renderType != CameraRenderType.Base)
            sourceData.renderType = CameraRenderType.Base;

        if (!sourceData.cameraStack.Contains(overlayCam))
            sourceData.cameraStack.Add(overlayCam);

        overlayStackCamera = source;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;

        Transform[] all = go.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.layer = layer;
    }

    // 当前真正在渲染的相机
    private Camera ActiveCamera()
    {
        if (cam != null && cam.isActiveAndEnabled && cam != overlayCam)
            return cam;

        if (Camera.main != null && Camera.main.isActiveAndEnabled && Camera.main != overlayCam)
            return Camera.main;

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled && all[i] != overlayCam)
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

            if (current == null || current == target || current == overlayCam) continue;

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

        if (alwaysRenderOnTop)
        {
            EnsureOverlayCamera();
            SetLayerRecursively(ball, overlayLayer);
        }

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
        if (worldCamera != null && worldCamera.isActiveAndEnabled)
            return worldCamera;

        if (taskHUD == null)
            taskHUD = FindObjectOfType<TaskHUD>(true);

        if (taskHUD != null &&
            taskHUD.cameraOnReturn != null &&
            taskHUD.cameraOnReturn.isActiveAndEnabled)
        {
            return taskHUD.cameraOnReturn;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        // 优先玩家身上的第一视角相机
        PlayerMover pm = FindObjectOfType<PlayerMover>();

        if (pm != null && pm.cameraTransform != null)
        {
            Camera fp = pm.cameraTransform.GetComponent<Camera>();

            if (fp != null && fp.isActiveAndEnabled && fp != overlayCam)
                return fp;
        }

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled && all[i] != overlayCam)
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