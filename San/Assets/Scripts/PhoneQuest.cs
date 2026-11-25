using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 情绪球收集完成后：
/// 手机震动/响铃 → 弹出两句碎碎念 → 玩家靠近后切换到第四个镜头并闪屏 →
/// 点击手机 → 打开同场景中的 2D 面板。
/// </summary>
public class PhoneQuest : MonoBehaviour
{
    [Header("手机物体")]
    [Tooltip("需要震动的手机模型，建议拖模型子物体")]
    public Transform visualToVibrate;

    [Tooltip("独立的手机屏幕 Renderer")]
    public Renderer screenRenderer;

    [Tooltip("平时的屏幕材质")]
    public Material screenNormalMaterial;

    [Tooltip("靠近后闪烁使用的亮屏材质")]
    public Material screenFlashMaterial;

    [Header("震动")]
    [Tooltip("位置震动幅度，单位米")]
    public float vibrationAmplitude = 0.006f;

    [Tooltip("震动时的旋转角度")]
    public float vibrationAngle = 1.5f;

    public float vibrationSpeed = 32f;

    [Tooltip("真实移动设备是否也震动一次，电脑上无效")]
    public bool vibrateRealDevice = false;

    [Header("铃声")]
    [Tooltip("直接把音频文件拖到这里（AudioClip）")]
    public AudioClip ringClip;

    [Tooltip("可选：留空会自动在手机上创建 AudioSource")]
    public AudioSource ringAudio;

    public float ringVolume = 1f;

    [Tooltip("0 = 2D 音量，1 = 3D 随距离衰减")]
    public float ringSpatialBlend = 1f;

    [Header("靠近时切换镜头")]
    public bool switchCameraWhenNear = true;

    [Tooltip("靠近手机时切换到的第四个镜头，拖 Camera 组件")]
    public Camera approachCamera;

    [Tooltip("Approach Camera 留空时，按这些名字自动找")]
    public string[] approachCameraNames =
        new string[] { "Camera4", "Camera (4)", "Camera_4", "镜头4" };

    [Header("靠近与点击")]
    [Tooltip("玩家，留空自动找 PlayerMover")]
    public Transform player;

    [Tooltip("点击检测使用的相机，留空自动找")]
    public Camera viewCamera;

    [Tooltip("玩家进入这个距离后，切换镜头并开始闪烁")]
    public float approachDistance = 2.2f;

    [Tooltip("点击手机的最大射线距离")]
    public float clickDistance = 3f;

    [Tooltip("屏幕闪烁间隔")]
    public float flashInterval = 0.35f;

    public LayerMask clickMask = ~0;

    [Header("打开提示")]
    [Tooltip("靠近后显示的提示 UI，默认关闭")]
    public GameObject openPrompt;

    [Tooltip("提示文字，可留空自动找")]
    public Text openPromptText;

    [Header("响铃碎碎念弹窗")]
    [Tooltip("弹窗根物体（UI 或世界空间气泡都行），留空就不弹")]
    public GameObject popupRoot;

    [Tooltip("弹窗上的文字组件，留空自动在子物体里找 Text")]
    public Text popupLabel;

    [TextArea] public string firstPopupText = "诶！手机响了";
    [TextArea] public string secondPopupText = "去看看吧";

    [Tooltip("第一句显示多久（秒）")]
    public float firstPopupSeconds = 2f;

    [Tooltip("两句之间的间隔（秒）")]
    public float popupGapSeconds = 0.3f;

    [Tooltip("第二句显示多久（秒）")]
    public float secondPopupSeconds = 2f;

    [Header("进入 2D")]
    [Tooltip("点击手机后打开的 2D 面板，默认关闭")]
    public GameObject level2DPanel;

    [Tooltip("进入 2D 时要关闭的 3D 顶层物体")]
    public GameObject[] deactivateOn2D;

    public bool disablePlayerControl = true;

    [Tooltip("用于隐藏之前的任务 HUD，留空自动找")]
    public TaskHUD taskHUD;

    public bool debugLog = true;

    private PlayerMover playerMover;

    private Vector3 baseVisualPosition;
    private Quaternion baseVisualRotation;

    private bool eventStarted;
    private bool opened;
    private bool isNear;
    private bool flashOn;
    private float flashTimer;

    private Coroutine popupRoutine;

    // ---------------- 镜头切换状态 ----------------
    private class CameraState
    {
        public Camera cam;
        public bool enabled;
        public bool activeSelf;
    }

    private readonly List<CameraState> cameraStates = new List<CameraState>();
    private bool cameraSwitched;
    private bool approachWasEnabled;
    private bool approachWasActive;

    // ====================================================================

    void Awake()
    {
        if (visualToVibrate == null)
            visualToVibrate = transform;

        baseVisualPosition = visualToVibrate.localPosition;
        baseVisualRotation = visualToVibrate.localRotation;

        ResolveReferences();
        InitAudio();
    }

    void Start()
    {
        if (openPromptText == null && openPrompt != null)
            openPromptText = openPrompt.GetComponentInChildren<Text>(true);

        if (openPromptText != null)
            openPromptText.text = "点击打开手机";

        if (openPrompt != null)
            openPrompt.SetActive(false);

        // 碎碎念弹窗初始隐藏
        if (popupLabel == null && popupRoot != null)
            popupLabel = popupRoot.GetComponentInChildren<Text>(true);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        SetScreenFlash(false);

        if (switchCameraWhenNear && approachCamera == null)
            approachCamera = FindApproachCamera();

        if (switchCameraWhenNear && approachCamera == null)
        {
            Debug.LogWarning(
                "[Phone] 没找到第四个镜头，请把 Camera 拖到 Approach Camera",
                this);
        }
    }

    void OnDisable()
    {
        // 物体被意外关闭时，别把镜头一直锁在镜头4
        if (cameraSwitched) RestoreCamera();

        HidePopup();
    }

    // ====================================================================
    // 情绪球收集完成后，由 EmotionRewardFly 调用
    // ====================================================================

    public void StartPhoneEvent()
    {
        if (opened || eventStarted) return;

        ResolveReferences();
        InitAudio();

        eventStarted = true;
        isNear = false;
        flashOn = false;
        flashTimer = 0f;

        SetScreenFlash(false);
        PlayRing();

        // 手机响的同时，弹出两句碎碎念
        StopPopupRoutine();
        popupRoutine = StartCoroutine(PlayRingPopups());

        // Handheld 只在移动端平台的程序集里存在，WebGL / PC 构建时会报 CS0103
#if UNITY_ANDROID || UNITY_IOS
        if (vibrateRealDevice)
            Handheld.Vibrate();
#endif

        if (debugLog)
            Debug.Log("[Phone] 情绪球收集完成，手机开始震动 + 弹碎碎念", this);

        if (screenRenderer == null)
            Debug.LogWarning("[Phone] Screen Renderer 没拖，屏幕不会闪烁", this);
        else if (screenNormalMaterial == null || screenFlashMaterial == null)
            Debug.LogWarning("[Phone] 两个屏幕材质没拖完整，屏幕不会闪烁", this);
    }

    private IEnumerator PlayRingPopups()
    {
        if (popupRoot == null) yield break;

        // 第一句
        if (popupLabel != null) popupLabel.text = firstPopupText;
        popupRoot.SetActive(true);

        if (firstPopupSeconds > 0f)
            yield return new WaitForSecondsRealtime(firstPopupSeconds);

        popupRoot.SetActive(false);

        if (popupGapSeconds > 0f)
            yield return new WaitForSecondsRealtime(popupGapSeconds);

        // 第二句
        if (popupLabel != null) popupLabel.text = secondPopupText;
        popupRoot.SetActive(true);

        if (secondPopupSeconds > 0f)
            yield return new WaitForSecondsRealtime(secondPopupSeconds);

        popupRoot.SetActive(false);
        popupRoutine = null;
    }

    private void StopPopupRoutine()
    {
        if (popupRoutine != null)
        {
            StopCoroutine(popupRoutine);
            popupRoutine = null;
        }
    }

    private void HidePopup()
    {
        StopPopupRoutine();
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    void Update()
    {
        if (!eventStarted || opened) return;

        // 震动和铃声一直持续，与距离无关
        ApplyVibration();

        bool near = IsPlayerNear();

        if (near != isNear)
        {
            isNear = near;

            if (openPrompt != null)
                openPrompt.SetActive(near);

            if (near)
            {
                flashOn = true;
                flashTimer = 0f;
                SetScreenFlash(true);

                SwitchToApproachCamera();

                if (debugLog)
                    Debug.Log("[Phone] 玩家靠近，切镜头 + 屏幕闪烁", this);
            }
            else
            {
                flashOn = false;
                SetScreenFlash(false);

                RestoreCamera();

                if (debugLog)
                    Debug.Log("[Phone] 玩家远离，恢复原镜头", this);
            }
        }

        if (!isNear) return;

        // 屏幕闪烁
        flashTimer += Time.unscaledDeltaTime;

        if (flashTimer >= Mathf.Max(0.05f, flashInterval))
        {
            flashTimer = 0f;
            flashOn = !flashOn;
            SetScreenFlash(flashOn);
        }

        // 点击手机
        if (ClickPressed())
            TryClickPhone();
    }

    // ====================================================================
    // 镜头切换
    // ====================================================================

    private void SwitchToApproachCamera()
    {
        if (!switchCameraWhenNear) return;

        if (approachCamera == null)
            approachCamera = FindApproachCamera();

        if (approachCamera == null || cameraSwitched) return;

        cameraStates.Clear();

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Camera current = all[i];

            if (current == null || current == approachCamera)
                continue;

            cameraStates.Add(new CameraState
            {
                cam = current,
                enabled = current.enabled,
                activeSelf = current.gameObject.activeSelf
            });
        }

        approachWasEnabled = approachCamera.enabled;
        approachWasActive = approachCamera.gameObject.activeSelf;

        // 如果镜头4 或它的父级是关闭的，先打开
        ActivateChain(approachCamera.transform);

        for (int i = 0; i < cameraStates.Count; i++)
            cameraStates[i].cam.enabled = false;

        approachCamera.enabled = true;
        cameraSwitched = true;

        if (debugLog)
            Debug.Log("[Phone] 已切换到靠近镜头：" + approachCamera.name, this);
    }

    private void RestoreCamera()
    {
        if (!cameraSwitched) return;

        cameraSwitched = false;

        for (int i = 0; i < cameraStates.Count; i++)
        {
            CameraState state = cameraStates[i];

            if (state.cam == null) continue;

            state.cam.gameObject.SetActive(true);
            state.cam.enabled = state.enabled;
            state.cam.gameObject.SetActive(state.activeSelf);
        }

        cameraStates.Clear();

        if (approachCamera != null)
        {
            approachCamera.gameObject.SetActive(true);
            approachCamera.enabled = approachWasEnabled;
            approachCamera.gameObject.SetActive(approachWasActive);
        }

        if (debugLog)
            Debug.Log("[Phone] 已恢复原镜头");
    }

    private Camera FindApproachCamera()
    {
        if (approachCameraNames == null || approachCameraNames.Length == 0)
            return null;

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int n = 0; n < approachCameraNames.Length; n++)
        {
            string wanted = approachCameraNames[n];

            if (string.IsNullOrEmpty(wanted)) continue;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;

                if (string.Equals(
                        all[i].name.Trim(),
                        wanted.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    // 点击用的相机：切到镜头4 后就用镜头4 发射线
    private Camera CurrentClickCamera()
    {
        if (cameraSwitched &&
            approachCamera != null &&
            approachCamera.enabled)
        {
            return approachCamera;
        }

        if (viewCamera != null)
            return viewCamera;

        ResolveReferences();
        return viewCamera;
    }

    // ====================================================================
    // 点击并打开手机
    // ====================================================================

    private void TryClickPhone()
    {
        Camera clickCamera = CurrentClickCamera();

        if (clickCamera == null) return;

        Vector2 screenPosition;

        // 锁鼠标时从准星位置发射线
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            screenPosition = new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f);
        }
        else
        {
            screenPosition = PointerPosition();
        }

        Ray ray = clickCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                clickDistance,
                clickMask,
                QueryTriggerInteraction.Collide))
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                OpenPhone();
            }
        }
    }

    private void OpenPhone()
    {
        if (opened) return;

        opened = true;
        eventStarted = false;
        isNear = false;

        Time.timeScale = 1f;

        if (openPrompt != null)
            openPrompt.SetActive(false);

        // 打开手机时，把还没播完的碎碎念关掉
        HidePopup();

        flashOn = false;
        SetScreenFlash(false);
        ResetVisual();

        if (ringAudio != null)
            ringAudio.Stop();

        // 进入 2D 前恢复镜头
        RestoreCamera();

        if (disablePlayerControl && playerMover != null)
        {
            playerMover.SetControl(false, false);
            playerMover.LockCursor(false);
        }

        if (taskHUD == null)
            taskHUD = FindObjectOfType<TaskHUD>(true);

        if (taskHUD != null)
            taskHUD.Hide();

        if (deactivateOn2D != null)
        {
            for (int i = 0; i < deactivateOn2D.Length; i++)
            {
                GameObject target = deactivateOn2D[i];

                if (target != null)
                    target.SetActive(false);
            }
        }

        if (level2DPanel != null)
        {
            ActivateChain(level2DPanel.transform);

            if (debugLog)
            {
                Debug.Log(
                    "[Phone] 手机已打开，进入 2D 面板：" +
                    level2DPanel.name,
                    this);
            }
        }
        else
        {
            Debug.LogError("[Phone] Level 2D Panel 没拖，无法进入 2D", this);
        }
    }

    // ====================================================================
    // 手机效果
    // ====================================================================

    private void ApplyVibration()
    {
        if (visualToVibrate == null) return;

        float t = Time.unscaledTime * vibrationSpeed;

        float x = Mathf.Sin(t * 1.7f) * vibrationAmplitude;
        float z = Mathf.Cos(t * 2.3f) * vibrationAmplitude;
        float rotation = Mathf.Sin(t) * vibrationAngle;

        visualToVibrate.localPosition =
            baseVisualPosition + new Vector3(x, 0f, z);

        visualToVibrate.localRotation =
            baseVisualRotation * Quaternion.Euler(0f, 0f, rotation);
    }

    private void ResetVisual()
    {
        if (visualToVibrate == null) return;

        visualToVibrate.localPosition = baseVisualPosition;
        visualToVibrate.localRotation = baseVisualRotation;
    }

    private void SetScreenFlash(bool flashing)
    {
        if (screenRenderer == null) return;
        if (screenNormalMaterial == null || screenFlashMaterial == null) return;

        screenRenderer.sharedMaterial = flashing
            ? screenFlashMaterial
            : screenNormalMaterial;
    }

    private bool IsPlayerNear()
    {
        if (player == null)
            ResolveReferences();

        if (player == null) return false;

        return Vector3.Distance(player.position, transform.position)
               <= approachDistance;
    }

    // ====================================================================
    // 音频
    // ====================================================================

    private void InitAudio()
    {
        if (ringAudio == null)
            ringAudio = GetComponent<AudioSource>();

        if (ringAudio == null && ringClip != null)
            ringAudio = gameObject.AddComponent<AudioSource>();

        if (ringAudio == null) return;

        if (ringClip != null)
            ringAudio.clip = ringClip;

        ringAudio.loop = true;
        ringAudio.playOnAwake = false;
        ringAudio.spatialBlend = Mathf.Clamp01(ringSpatialBlend);
        ringAudio.volume = Mathf.Clamp01(ringVolume * AudioLoudness.GetGain(ringAudio.clip));
    }

    private void PlayRing()
    {
        if (ringAudio == null) return;

        if (ringClip != null)
            ringAudio.clip = ringClip;

        if (ringAudio.clip == null)
        {
            Debug.LogWarning("[Phone] 没设置铃声 Clip，手机只有震动没有声音", this);
            return;
        }

        ringAudio.loop = true;
        ringAudio.spatialBlend = Mathf.Clamp01(ringSpatialBlend);
        ringAudio.volume = Mathf.Clamp01(ringVolume * AudioLoudness.GetGain(ringAudio.clip));

        if (!ringAudio.isPlaying)
            ringAudio.Play();
    }

    // ====================================================================
    // 引用与输入
    // ====================================================================

    private void ResolveReferences()
    {
        if (playerMover == null)
            playerMover = FindObjectOfType<PlayerMover>();

        if (player == null && playerMover != null)
            player = playerMover.transform;

        if (taskHUD == null)
            taskHUD = FindObjectOfType<TaskHUD>(true);

        if (viewCamera == null &&
            playerMover != null &&
            playerMover.cameraTransform != null)
        {
            viewCamera = playerMover.cameraTransform.GetComponent<Camera>();
        }

        if (viewCamera == null &&
            taskHUD != null &&
            taskHUD.cameraOnReturn != null)
        {
            viewCamera = taskHUD.cameraOnReturn;
        }

        if (viewCamera == null)
            viewCamera = Camera.main;

        if (switchCameraWhenNear && approachCamera == null)
            approachCamera = FindApproachCamera();
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

    private bool ClickPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Mouse.current != null &&
               UnityEngine.InputSystem.Mouse.current
                   .leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    [ContextMenu("测试：启动手机事件")]
    private void TestStartPhoneEvent()
    {
        StartPhoneEvent();
    }
}