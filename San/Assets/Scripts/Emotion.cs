using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Events;

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

    [Tooltip("多个落袋情绪球之间的屏幕像素间距（第二个排在第一个后面）")]
    public float landedScreenSpacing = 55f;

    [Tooltip("所有落袋情绪球的整体大小倍率")]
    public float landedSizeMultiplier = 0.8f;

    [Header("剧情情绪球（对话结束后播放）")]
    [Tooltip("母亲对话结束后播放的「内耗」球；留空则用失望球")]
    public GameObject internalFrictionBallPrefab;

    [Tooltip("朋友对话结束后播放的「得到释放」球；留空则用开心球")]
    public GameObject releaseBallPrefab;

    [Header("客厅关卡情绪球")]
    [Tooltip("倒计时内找到遥控器的「惊喜」球；留空则用开心球")]
    public GameObject surpriseBallPrefab;

    [Tooltip("倒计时结束后没找到的「愤怒」球；留空则用失望球")]
    public GameObject angryBallPrefab;

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

    [Tooltip("切回第一视角时恢复玩家移动、视角控制和光标锁定")]
    public bool restorePlayerControlOnReturn = true;

    [Header("回到第一视角后")]
    [Tooltip("情绪球落入背包、视角切回第一视角后触发；可绑下一关提示面板")]
    public UnityEvent onReturnedToFirstPerson;

    [Header("下一关入口")]
    [Tooltip("本关情绪球到手后要开启的客厅触发区；留空则不处理")]
    public LivingRoomQuest livingRoomQuest;

    [Header("收集完成后的手机事件")]
    [Tooltip("情绪球落袋后要震动的手机，留空自动找")]
    public PhoneQuest phoneQuest;

    [Tooltip("情绪球落袋后是否触发手机事件；客厅关卡给球时取消勾选")]
    public bool triggerPhoneEvent = true;

    [Header("闭幕效果")]
    [Tooltip("落袋球达到这个数量时自动触发闭幕（0 = 不自动，手动调 StartClosing()）")]
    public int autoClosingAtBallCount = 3;

    [Tooltip("背包图标渐隐时长（秒）；0 = 直接隐藏不渐隐")]
    public float backpackFadeSeconds = 1.5f;

    [Tooltip("黑场遮罩 CanvasGroup；场景里放一个全屏黑色 Image 加 CanvasGroup，拖进来即生效")]
    public CanvasGroup closingFadeMask;

    [Tooltip("黑场遮罩渐显时长（秒）；会与背包渐隐同时开始")]
    public float maskFadeSeconds = 1.5f;

    [Tooltip("闭幕开始时触发（例如播结束音乐）")]
    public UnityEvent onClosingStart;

    [Tooltip("背包完全隐藏后触发（例如播字幕、切场景）")]
    public UnityEvent onClosingDone;

    [Header("闭幕中场：三球列队 → 合拢 → 闪光弹")]
    [Tooltip("左右两球离屏幕中央的距离（占屏幕宽度的比例，0.5 = 贴到屏幕边缘）")]
    [Range(0.05f, 0.5f)]
    public float closingLineSpacing = 0.38f;

    [Tooltip("每个球移动到指定位置的时间（秒）")]
    public float closingMoveSeconds = 0.6f;

    [Tooltip("三个球依次出发的间隔（秒）")]
    public float closingMoveStagger = 0.2f;

    [Tooltip("三个球站好后等待多久再向中间靠齐（秒）")]
    public float closingHoldSeconds = 1.5f;

    [Tooltip("左右两个球向中间靠齐的时间（秒）")]
    public float closingConvergeSeconds = 0.8f;

    [Tooltip("三球合拢后切换成的闪光弹预制体")]
    public GameObject flashbangBallPrefab;

    [Tooltip("闪光弹在屏幕上的直径（像素）")]
    public float flashbangScreenSize = 160f;

    [Header("闪光弹白场 + 结局旁白")]
    [Tooltip("闪白到全白所需时间（秒）")]
    public float flashWhiteFadeSeconds = 0.4f;

    [Tooltip("旁白字体（字魂夏日诗歌体）")]
    public Font narrationFont;

    [Tooltip("旁白文字颜色")]
    public Color narrationColor = Color.black;

    [Tooltip("每一句的淡入、淡出时间（秒）")]
    public float narrationFadeSeconds = 0.5f;

    [Tooltip("每一句停留多久（秒），到时自动淡出，再播下一句")]
    public float narrationLineSeconds = 3f;

    [Tooltip("上一句消失后停顿多久再播下一句（秒）")]
    public float narrationLineGapSeconds = 0.3f;

    [Tooltip("旁白字号")]
    public int narrationFontSize = 40;

    [Header("结局旁白（可手动添加多句）")]
    [Tooltip("好的情绪球大于 2 个时，按顺序逐句播放")]
    [TextArea(1, 3)]
    public List<string> goodEndingNarration = new List<string>();

    [Tooltip("坏的情绪球大于 2 个时，按顺序逐句播放")]
    [TextArea(1, 3)]
    public List<string> badEndingNarration = new List<string>();

    [Tooltip("好坏混合时，按顺序逐句播放")]
    [TextArea(1, 3)]
    public List<string> mixedEndingNarration = new List<string>();

    [Header("结尾音乐")]
    [Tooltip("进入黑屏时切换播放的结尾音乐（Assets/Music/结尾.mp3）")]
    public AudioClip endingMusic;

    [Tooltip("结尾音乐淡入、以及原音乐淡出的时长（秒）")]
    public float endingMusicFadeSeconds = 2f;

    [Range(0f, 1f)]
    [Tooltip("结尾音乐音量")]
    public float endingMusicVolume = 0.6f;

    private bool closingMode;
    private bool closingStarted;
    private bool endingSequence;

    private CanvasGroup endingWhiteGroup;
    private Text endingNarrationText;
    private AudioSource endingMusicSource;

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
    private Camera overlayStackCamera;

    private class LandedBall
    {
        public Transform transform;
        public float diameter;
        public bool happy;
    }

    private readonly List<LandedBall> landedBalls =
        new List<LandedBall>();

    // ====================================================================

    public void Play(bool happy)
    {
        if (playing) return;
        StartCoroutine(PlayRoutine(happy));
    }

    private IEnumerator PlayRoutine(bool happy)
    {
        playing = true;

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

        if (alwaysRenderOnTop && overlayCam != null)
            overlayCam.enabled = true;

        cam = ResolveCamera();

        if (cam == null)
        {
            Debug.LogError(
                "[Emotion] 找不到可用相机，情绪球无法飞行",
                this);

            playing = false;
            yield break;
        }

        if (backpackIcon != null && activateBackpackOnReturn)
            ActivateChain(backpackIcon);

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

        if (hasMumble && ballDelayAfterMumble > 0f)
        {
            yield return new WaitForSecondsRealtime(
                ballDelayAfterMumble);
        }

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

        yield return FlyBallAnimation(
            prefab,
            happy ? "EmotionBall_Happy" : "EmotionBall_Sad");

        SwitchToFirstPerson();
        SpawnLandedBall(happy);

        if (triggerPhoneEvent)
        {
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
        }

        playing = false;
    }

    // ====================================================================
    // 共用的情绪球飞行动画
    // ====================================================================

    private IEnumerator FlyBallAnimation(
        GameObject prefab,
        string ballName)
    {
        GameObject ball = Instantiate(prefab);
        ball.name = ballName;

        ball.SetActive(true);

        if (alwaysRenderOnTop)
        {
            EnsureOverlayCamera();
            SetLayerRecursively(ball, overlayLayer);
        }

        ball.transform.SetParent(cam.transform, true);
        ball.transform.localScale = Vector3.one;

        float ballScale = ResolveFlightScale(ball);

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

        if (destroyDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                destroyDelay);
        }

        Destroy(ball);

        if (debugLog)
            Debug.Log("[Emotion] 飞行球已销毁");
    }

    // ====================================================================
    // 剧情情绪球
    // ====================================================================

    public void PlayInternalFrictionBall()
    {
        GameObject prefab = internalFrictionBallPrefab != null
            ? internalFrictionBallPrefab
            : sadBallPrefab;

        PlayStoryBall(prefab, false, "EmotionBall_InternalFriction");
    }

    public void PlayReleaseBall()
    {
        GameObject prefab = releaseBallPrefab != null
            ? releaseBallPrefab
            : happyBallPrefab;

        PlayStoryBall(prefab, true, "EmotionBall_Release");
    }

    public void PlaySurpriseBall()
    {
        GameObject prefab = surpriseBallPrefab != null
            ? surpriseBallPrefab
            : happyBallPrefab;

        PlayStoryBall(prefab, true, "EmotionBall_Surprise");
    }

    public void PlayAngryBall()
    {
        GameObject prefab = angryBallPrefab != null
            ? angryBallPrefab
            : sadBallPrefab;

        PlayStoryBall(prefab, false, "EmotionBall_Angry");
    }

    private void PlayStoryBall(
        GameObject prefab,
        bool landedHappy,
        string ballName)
    {
        if (playing) return;

        if (prefab == null)
        {
            Debug.LogError(
                "[Emotion] " + ballName +
                " 的预制体没拖，动画无法播放",
                this);

            return;
        }

        StartCoroutine(
            PlayStoryBallRoutine(prefab, landedHappy, ballName));
    }

    private IEnumerator PlayStoryBallRoutine(
        GameObject prefab,
        bool landedHappy,
        string ballName)
    {
        playing = true;

        cam = ResolveCamera();

        if (cam == null)
        {
            Debug.LogError(
                "[Emotion] 找不到可用相机，剧情情绪球无法播放",
                this);

            playing = false;
            yield break;
        }

        if (alwaysRenderOnTop)
            EnsureOverlayCamera();

        if (backpackIcon != null)
            ActivateChain(backpackIcon);

        yield return FlyBallAnimation(prefab, ballName);

        SwitchToFirstPerson();
        SpawnLandedBall(landedHappy, prefab);

        onReturnedToFirstPerson?.Invoke();

        if (livingRoomQuest != null)
            livingRoomQuest.EnableTrigger();

        if (debugLog)
            Debug.Log("[Emotion] 剧情情绪球已落入背包：" + ballName);

        playing = false;
    }

    // ====================================================================
    // LateUpdate：落袋球跟随相机 + 闭幕特殊处理
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

            // 闭幕模式：球保持显示，不再跟随背包显隐
            bool visible;

            if (closingMode)
            {
                visible = true;
            }
            else
            {
                visible =
                    backpackIcon != null &&
                    backpackIcon.gameObject.activeInHierarchy;
            }

            if (entry.transform.gameObject.activeSelf != visible)
                entry.transform.gameObject.SetActive(visible);

            if (!visible) continue;

            // 闭幕中场动画自己接管位置，这里不要再把球拉回背包
            if (endingSequence) continue;

            PlaceLandedBall(entry, activeCam, i);
        }
    }

    private void PlaceLandedBall(
        LandedBall entry,
        Camera activeCam,
        int index = 0)
    {
        if (entry == null ||
            entry.transform == null ||
            activeCam == null)
        {
            return;
        }

        Vector2 screen;

        float slotOffset = 0f;

        for (int j = 0; j < index && j < landedBalls.Count; j++)
        {
            float width = Mathf.Max(
                ScreenDiameterOf(landedBalls[j], activeCam),
                landedScreenSpacing);

            slotOffset += width + 4f;
        }

        if (backpackIcon != null)
        {
            screen =
                ScreenOf(backpackIcon, activeCam) +
                landedScreenOffset +
                new Vector2(slotOffset, 0f);
        }
        else
        {
            screen =
                new Vector2(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f) +
                landedScreenOffset +
                new Vector2(slotOffset, 0f);
        }

        Vector3 world = activeCam.ScreenToWorldPoint(
            new Vector3(
                screen.x,
                screen.y,
                landedDistance));

        entry.transform.position = world;

        float worldPerPixel =
            2f * landedDistance *
            Mathf.Tan(
                activeCam.fieldOfView *
                0.5f * Mathf.Deg2Rad) /
            Mathf.Max(1, Screen.height);

        if (landedAutoSize && entry.diameter > 0.0001f)
        {
            float scale = Mathf.Max(
                0.0001f,
                landedScreenSize * worldPerPixel /
                entry.diameter);

            entry.transform.localScale =
                Vector3.one * scale * Mathf.Max(0.01f, landedSizeMultiplier);
        }
        else
        {
            entry.transform.localScale =
                Vector3.one * Mathf.Max(
                    0.0001f,
                    landedScale * Mathf.Max(0.01f, landedSizeMultiplier));
        }
    }

    private float ScreenDiameterOf(
        LandedBall entry,
        Camera activeCam)
    {
        if (entry == null || activeCam == null)
            return 0f;

        float multiplier = Mathf.Max(0.01f, landedSizeMultiplier);

        if (landedAutoSize && entry.diameter > 0.0001f)
            return landedScreenSize * multiplier;

        float worldPerPixel =
            2f * landedDistance *
            Mathf.Tan(
                activeCam.fieldOfView *
                0.5f * Mathf.Deg2Rad) /
            Mathf.Max(1, Screen.height);

        if (worldPerPixel <= 0.0000001f)
            return 0f;

        return landedScale * multiplier *
               entry.diameter / worldPerPixel;
    }

    // ====================================================================
    // 置顶渲染
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

        ActivateChain(target.transform);

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Camera current = all[i];

            if (current == null || current == target || current == overlayCam) continue;

            if (current.enabled)
                current.enabled = false;
        }

        target.enabled = true;
        cam = target;
        RestorePlayerControl();

        if (debugLog)
            Debug.Log("[Emotion] 已切回第一视角：" + target.name, this);
    }

    private void RestorePlayerControl()
    {
        if (!restorePlayerControlOnReturn) return;

        Time.timeScale = 1f;

        PlayerMover pm = FindObjectOfType<PlayerMover>(true);

        if (pm == null)
        {
            Debug.LogWarning("[Emotion] 找不到 PlayerMover，玩家控制未恢复", this);
            return;
        }

        pm.SetControl(true, true);
        pm.LockCursor(true);

        if (debugLog)
            Debug.Log("[Emotion] 已恢复玩家控制", this);
    }

    // ====================================================================
    // 落袋球 + 闭幕自动触发
    // ====================================================================

    private void SpawnLandedBall(
        bool happy,
        GameObject prefabOverride = null)
    {
        GameObject prefab = prefabOverride != null
            ? prefabOverride
            : landedBallPrefab;

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

        ball.transform.SetParent(null, true);
        ball.transform.localScale = Vector3.one;

        LandedBall entry = new LandedBall
        {
            transform = ball.transform,
            diameter = MeasureDiameter(ball),
            happy = happy
        };

        landedBalls.Add(entry);

        PlaceLandedBall(
            entry,
            ActiveCamera(),
            landedBalls.Count - 1);

        if (landedSpin)
            StartCoroutine(SpinLanded(ball.transform));

        if (debugLog)
        {
            Debug.Log(
                "[Emotion] 背包旁边已生成 3D 情绪球：" +
                ball.name,
                ball);
        }

        // ★ 自动闭幕检测
        if (!closingMode &&
            autoClosingAtBallCount > 0 &&
            landedBalls.Count >= autoClosingAtBallCount)
        {
            StartClosing();
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

    public void ShowBackpackWithBalls()
    {
        if (backpackIcon != null)
            ActivateChain(backpackIcon);

        if (alwaysRenderOnTop && landedBalls.Count > 0)
            EnsureOverlayCamera();

        if (debugLog)
        {
            Camera active = ActiveCamera();

            Debug.Log(
                "[Emotion] 已显示背包与情绪球：相机=" +
                (active != null ? active.name : "无") +
                "，落袋球数=" + landedBalls.Count,
                this);
        }
    }

    // ====================================================================
    // 闭幕系统：背包渐隐 + 黑场渐显 + 情绪球保留
    // ====================================================================

    public void StartClosing()
    {
        if (closingStarted) return;
        closingStarted = true;
        StartCoroutine(ClosingRoutine());
    }

    private IEnumerator ClosingRoutine()
    {
        // 1. 进入闭幕模式：LateUpdate 里球不再跟随背包显隐
        closingMode = true;

        // 1.2 黑屏后切到 2D 演出：冻结玩家移动和鼠标视角，避免画面跳动
        FreezePlayerForEnding();

        // 1.3 切换结尾音乐：原音乐淡出，结尾音乐淡入
        PlayEndingMusic();

        // 1.5 让黑幕排到情绪球的置顶相机之前，球才会留在黑幕上方
        SendMaskBehindBallOverlay();

        // 2. 通知外部
        onClosingStart?.Invoke();

        // 3. 强制每个球保持显示
        for (int i = 0; i < landedBalls.Count; i++)
        {
            if (landedBalls[i] != null && landedBalls[i].transform != null)
                landedBalls[i].transform.gameObject.SetActive(true);
        }

        // 4. 同步渐隐背包 + 渐显黑场
        CanvasGroup backpackCG = null;

        if (backpackIcon != null)
        {
            backpackCG = backpackIcon.GetComponent<CanvasGroup>();
            if (backpackCG == null)
                backpackCG = backpackIcon.GetComponentInParent<CanvasGroup>();
        }

        // 黑场先激活但 alpha=0，不会穿帮
        if (closingFadeMask != null)
        {
            closingFadeMask.gameObject.SetActive(true);
            closingFadeMask.alpha = 0f;
        }

        bool needFade =
            (backpackIcon != null && backpackFadeSeconds > 0f) ||
            (closingFadeMask != null && maskFadeSeconds > 0f);

        if (needFade)
        {
            float t = 0f;
            float total = Mathf.Max(backpackFadeSeconds, maskFadeSeconds, 0.0001f);
            float backpackStart = backpackCG != null ? backpackCG.alpha : 1f;

            while (t < total)
            {
                t += Time.unscaledDeltaTime;

                if (backpackCG != null && backpackFadeSeconds > 0f)
                {
                    float bk = Mathf.Clamp01(t / Mathf.Max(backpackFadeSeconds, 0.0001f));
                    backpackCG.alpha = Mathf.Lerp(backpackStart, 0f, bk);
                }

                if (closingFadeMask != null && maskFadeSeconds > 0f)
                {
                    float mk = Mathf.Clamp01(t / Mathf.Max(maskFadeSeconds, 0.0001f));
                    closingFadeMask.alpha = Mathf.Lerp(0f, 1f, mk);
                }

                yield return null;
            }

            if (backpackCG != null) backpackCG.alpha = 0f;
            if (closingFadeMask != null) closingFadeMask.alpha = 1f;
        }

        // 5. 彻底隐藏背包
        if (backpackIcon != null)
            backpackIcon.gameObject.SetActive(false);

        // 6. 通知外部完成
        onClosingDone?.Invoke();

        if (debugLog)
        {
            Debug.Log(
                "[Emotion] 闭幕完成：背包已隐藏，情绪球保留（" +
                landedBalls.Count + " 个）",
                this);
        }

        // 7. 三球列队 → 合拢 → 闪光弹
        StartCoroutine(EndingBallSequenceRoutine());
    }

    // ====================================================================
    // 把闭幕黑幕放到情绪球置顶相机之前
    // ====================================================================

    /// <summary>
    /// 黑幕原本是 Screen Space - Overlay 画布，会画在所有相机（包括情绪球的
    /// 置顶相机）之后，所以球会被盖住。改成 Screen Space - Camera 后，黑幕由
    /// 主相机渲染，置顶相机在其后渲染，球就一直在黑幕上方。
    /// </summary>
    private void SendMaskBehindBallOverlay()
    {
        if (closingFadeMask == null) return;

        // 没有置顶相机时球本来就在主相机里，改了也挡不住，直接跳过
        if (overlayCam == null) return;

        Canvas maskCanvas = closingFadeMask.GetComponentInParent<Canvas>();

        if (maskCanvas == null) return;

        Camera active = ActiveCamera();

        if (active == null) return;

        if (maskCanvas.renderMode != RenderMode.ScreenSpaceCamera)
            maskCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        maskCanvas.worldCamera = active;

        // 贴近近裁剪面，避免有物体刚好钻到黑幕前面
        maskCanvas.planeDistance =
            Mathf.Max(0.01f, active.nearClipPlane + 0.01f);
    }

    // ====================================================================
    // 闭幕中场：三球依次移动到屏幕中间 → 向中间靠齐 → 闪光弹
    // ====================================================================

    private IEnumerator EndingBallSequenceRoutine()
    {
        endingSequence = true;

        yield return null;

        Camera active = ActiveCamera();

        if (active == null || landedBalls.Count < 3)
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "[Emotion] 闭幕中场需要 3 个情绪球，当前 " +
                    landedBalls.Count + " 个，跳过列队动画",
                    this);
            }

            endingSequence = false;
            yield break;
        }

        // 三球统一挂到相机下，用局部坐标做动画，镜头再动也不会跑位
        for (int i = 0; i < landedBalls.Count; i++)
        {
            if (landedBalls[i] != null && landedBalls[i].transform != null)
                landedBalls[i].transform.SetParent(active.transform, true);
        }

        // 第一个 → 中间右边，第二个 → 正中央，第三个 → 中间左边
        Vector2 center = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f);

        // 按屏幕宽度的比例撑开，三个球接近占满整个屏幕
        float spacing =
            Mathf.Clamp(closingLineSpacing, 0.05f, 0.5f) * Screen.width;

        Vector2[] targets =
        {
            center + new Vector2(spacing, 0f),
            center,
            center - new Vector2(spacing, 0f)
        };

        // 依次移动到对应位置
        for (int i = 0; i < 3; i++)
        {
            LandedBall ball = landedBalls[i];

            if (ball == null || ball.transform == null) continue;

            yield return MoveBallToLocal(
                ball.transform,
                active,
                targets[i],
                closingMoveSeconds);

            if (closingMoveStagger > 0f)
                yield return new WaitForSecondsRealtime(closingMoveStagger);
        }

        // 站好后等待 1~2 秒
        if (closingHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(closingHoldSeconds);

        // 左右两个球向中间靠齐（第二个本来就在中间）
        Coroutine rightBall = null;
        Coroutine leftBall = null;

        if (landedBalls[0] != null && landedBalls[0].transform != null)
        {
            rightBall = StartCoroutine(
                MoveBallToLocal(
                    landedBalls[0].transform,
                    active,
                    center,
                    closingConvergeSeconds));
        }

        if (landedBalls[2] != null && landedBalls[2].transform != null)
        {
            leftBall = StartCoroutine(
                MoveBallToLocal(
                    landedBalls[2].transform,
                    active,
                    center,
                    closingConvergeSeconds));
        }

        if (rightBall != null) yield return rightBall;
        if (leftBall != null) yield return leftBall;

        // 统计好坏情绪球，决定用哪段旁白（要在清空前统计）
        int goodCount = 0;
        int badCount = 0;

        for (int i = 0; i < landedBalls.Count; i++)
        {
            if (landedBalls[i] == null) continue;

            if (landedBalls[i].happy) goodCount++;
            else badCount++;
        }

        // 三球合拢后切换成闪光弹
        SpawnFlashbang(active);

        // 闪光弹爆白 → 全白 → 播放对应结局旁白
        yield return PlayFlashbangEnding(goodCount, badCount);

        endingSequence = false;
    }

    private IEnumerator MoveBallToLocal(
        Transform ball,
        Camera activeCam,
        Vector2 screenPos,
        float duration)
    {
        if (ball == null || activeCam == null) yield break;

        Vector3 start = ball.localPosition;
        Vector3 end = LocalFromScreen(activeCam, screenPos);

        float d = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / d);
            k = k * k * (3f - 2f * k); // 平滑进出

            if (ball == null) yield break;

            ball.localPosition = Vector3.LerpUnclamped(start, end, k);

            yield return null;
        }

        if (ball != null)
            ball.localPosition = end;
    }

    private Vector3 LocalFromScreen(Camera activeCam, Vector2 screenPos)
    {
        Vector3 world = activeCam.ScreenToWorldPoint(
            new Vector3(
                screenPos.x,
                screenPos.y,
                landedDistance));

        return activeCam.transform.InverseTransformPoint(world);
    }

    private void SpawnFlashbang(Camera activeCam)
    {
        // 先收掉三个小球
        for (int i = 0; i < landedBalls.Count; i++)
        {
            if (landedBalls[i] != null && landedBalls[i].transform != null)
                Destroy(landedBalls[i].transform.gameObject);
        }

        landedBalls.Clear();

        if (flashbangBallPrefab == null)
        {
            Debug.LogWarning(
                "[Emotion] 闪光弹预制体没拖，无法切换",
                this);
            return;
        }

        Camera camRef = activeCam != null ? activeCam : ActiveCamera();

        GameObject ball = Instantiate(flashbangBallPrefab);
        ball.name = "EmotionBall_Flashbang";
        ball.SetActive(true);

        if (alwaysRenderOnTop)
        {
            EnsureOverlayCamera();
            SetLayerRecursively(ball, overlayLayer);
        }

        if (camRef != null)
        {
            ball.transform.SetParent(camRef.transform, false);
            ball.transform.localPosition =
                new Vector3(0f, 0f, landedDistance);
            ball.transform.localRotation = Quaternion.identity;

            float worldPerPixel =
                2f * landedDistance *
                Mathf.Tan(
                    camRef.fieldOfView *
                    0.5f * Mathf.Deg2Rad) /
                Mathf.Max(1, Screen.height);

            float diameter =
                Mathf.Max(0.0001f, MeasureDiameter(ball));

            ball.transform.localScale =
                Vector3.one *
                Mathf.Max(
                    0.0001f,
                    Mathf.Max(1f, flashbangScreenSize) *
                    worldPerPixel / diameter);
        }
        else
        {
            ball.transform.position = transform.position;
            ball.transform.localScale =
                Vector3.one * Mathf.Max(0.01f, landedScale);
        }

        if (landedSpin)
            StartCoroutine(SpinLanded(ball.transform));

        if (debugLog)
            Debug.Log("[Emotion] 三球合拢，已切换成闪光弹", ball);
    }

    // ====================================================================
    // 黑屏后冻结玩家 → 闪白 → 结局旁白
    // ====================================================================

    private void FreezePlayerForEnding()
    {
        PlayerMover pm = FindObjectOfType<PlayerMover>(true);

        if (pm == null) return;

        // 关掉移动和鼠标视角，画面变成固定的 2D 演出；光标保持隐藏
        pm.SetControl(false, false);
        pm.LockCursor(true);

        if (debugLog)
            Debug.Log("[Emotion] 闭幕开始，已冻结玩家视角", this);
    }

    // ====================================================================
    // 结尾音乐（交叉淡入淡出）
    // ====================================================================

    private void PlayEndingMusic()
    {
        if (endingMusic == null) return;

        float fade = Mathf.Max(0.01f, endingMusicFadeSeconds);

        // 当前场景音乐先淡出，和结尾音乐形成交叉淡入淡出
        if (SceneFader.Instance != null)
            SceneFader.Instance.StopMusic(fade);

        if (endingMusicSource == null)
        {
            GameObject go = new GameObject("EndingMusic");
            DontDestroyOnLoad(go);

            endingMusicSource = go.AddComponent<AudioSource>();
            endingMusicSource.playOnAwake = false;
            endingMusicSource.loop = true;
            endingMusicSource.spatialBlend = 0f;
        }

        endingMusicSource.clip = endingMusic;
        endingMusicSource.volume = 0f;
        endingMusicSource.Play();

        StartCoroutine(FadeEndingMusic(endingMusicVolume, fade));

        if (debugLog)
            Debug.Log("[Emotion] 已切换结尾音乐：" + endingMusic.name, this);
    }

    private IEnumerator FadeEndingMusic(float target, float duration)
    {
        if (endingMusicSource == null) yield break;

        float d = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;

            if (endingMusicSource == null) yield break;

            endingMusicSource.volume =
                Mathf.Lerp(0f, target, Mathf.Clamp01(t / d));

            yield return null;
        }

        if (endingMusicSource != null)
            endingMusicSource.volume = target;
    }

    /// <summary>淡出结尾音乐（例如以后要接字幕结束、返回主菜单时调用）。</summary>
    public void FadeOutEndingMusic(float duration = 2f)
    {
        if (endingMusicSource == null) return;

        StartCoroutine(FadeOutEndingMusicRoutine(duration));
    }

    private IEnumerator FadeOutEndingMusicRoutine(float duration)
    {
        float from = endingMusicSource.volume;
        float d = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;

            if (endingMusicSource == null) yield break;

            endingMusicSource.volume =
                Mathf.Lerp(from, 0f, Mathf.Clamp01(t / d));

            yield return null;
        }

        if (endingMusicSource != null)
        {
            endingMusicSource.Stop();
            endingMusicSource.volume = 0f;
        }
    }

    private IEnumerator PlayFlashbangEnding(int goodCount, int badCount)
    {
        CanvasGroup white = EnsureEndingWhiteCanvas();

        if (white == null) yield break;

        // 闪光弹爆开：快速泛白
        float d = Mathf.Max(0.01f, flashWhiteFadeSeconds);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / d);
            k = k * k; // 先慢后快，像闪光弹炸开

            white.alpha = Mathf.Lerp(0f, 1f, k);

            yield return null;
        }

        white.alpha = 1f;

        // 全白之后按顺序逐句播放对应结局旁白
        List<string> lines = ResolveEndingNarration(goodCount, badCount);

        if (endingNarrationText == null || lines == null || lines.Count == 0)
            yield break;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (string.IsNullOrEmpty(line)) continue;

            // 上一句淡出、消失并停顿后才会走到下一句
            yield return ShowNarrationLine(line);
        }

        if (debugLog)
        {
            Debug.Log(
                "[Emotion] 结局旁白：好=" + goodCount +
                "，坏=" + badCount +
                "，共 " + lines.Count + " 句",
                this);
        }
    }

    private IEnumerator ShowNarrationLine(string line)
    {
        if (endingNarrationText == null) yield break;

        endingNarrationText.text = line;

        Color color = narrationColor;
        color.a = 0f;
        endingNarrationText.color = color;

        float fade = Mathf.Max(0.01f, narrationFadeSeconds);
        float t = 0f;

        // 淡入
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;

            color.a = Mathf.Clamp01(t / fade);
            endingNarrationText.color = color;

            yield return null;
        }

        color.a = 1f;
        endingNarrationText.color = color;

        // 停留
        if (narrationLineSeconds > 0f)
            yield return new WaitForSecondsRealtime(narrationLineSeconds);

        // 淡出
        t = 0f;

        while (t < fade)
        {
            t += Time.unscaledDeltaTime;

            color.a = 1f - Mathf.Clamp01(t / fade);
            endingNarrationText.color = color;

            yield return null;
        }

        color.a = 0f;
        endingNarrationText.color = color;

        // 完全消失后再停顿，然后才轮到下一句
        if (narrationLineGapSeconds > 0f)
            yield return new WaitForSecondsRealtime(narrationLineGapSeconds);
    }

    private List<string> ResolveEndingNarration(int goodCount, int badCount)
    {
        // 好/坏任意一方大于 2 个，用对应的一组文案
        if (goodCount > 2 && goodEndingNarration.Count > 0)
            return goodEndingNarration;

        if (badCount > 2 && badEndingNarration.Count > 0)
            return badEndingNarration;

        // 三个球里好坏混合（例如 2:1）时按多数决定
        if (goodCount > badCount && goodEndingNarration.Count > 0)
            return goodEndingNarration;

        if (badCount > goodCount && badEndingNarration.Count > 0)
            return badEndingNarration;

        if (mixedEndingNarration.Count > 0)
            return mixedEndingNarration;

        // 兜底：谁有文案就用谁
        if (goodEndingNarration.Count > 0)
            return goodEndingNarration;

        if (badEndingNarration.Count > 0)
            return badEndingNarration;

        return null;
    }

    private CanvasGroup EnsureEndingWhiteCanvas()
    {
        if (endingWhiteGroup != null) return endingWhiteGroup;

        GameObject canvasGo = new GameObject(
            "Ending_WhiteCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 高于普通 UI、低于 SceneFader 的 32767，切场景时黑幕仍能盖住
        canvas.sortingOrder = 32000;

        GameObject imageGo = new GameObject(
            "Ending_WhiteImage",
            typeof(Image));

        imageGo.transform.SetParent(canvasGo.transform, false);

        Image image = imageGo.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        endingWhiteGroup = canvasGo.AddComponent<CanvasGroup>();
        endingWhiteGroup.alpha = 0f;
        endingWhiteGroup.blocksRaycasts = false;
        endingWhiteGroup.interactable = false;

        GameObject textGo = new GameObject(
            "Ending_Narration",
            typeof(Text));

        textGo.transform.SetParent(canvasGo.transform, false);

        Text text = textGo.GetComponent<Text>();
        text.font = narrationFont != null
            ? narrationFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = FontStyle.Bold;
        text.fontSize = Mathf.Max(1, narrationFontSize);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = narrationColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.12f, 0.2f);
        textRect.anchorMax = new Vector2(0.88f, 0.8f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        endingNarrationText = text;

        return endingWhiteGroup;
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