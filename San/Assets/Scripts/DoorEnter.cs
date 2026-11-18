using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进门剧情：冻结玩家 + 只启用固定的第三人称相机 + 碎碎念气泡，
/// 碎碎念消失后再弹出任务弹窗，点确定恢复第一人称和移动。
/// 第三人称相机的 Transform 完全不动，以场景里摆好的为准。
/// </summary>
public class DoorEntrySequence : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("留空会自动查找场景里的 PlayerMover")]
    public PlayerMover playerMover;

    [Header("相机")]
    [Tooltip("玩家原本的第一人称主相机")]
    public Camera firstPersonCamera;
    [Tooltip("场景里摆好的固定第三人称相机（不要填主相机）")]
    public Camera thirdPersonCamera;

    [Header("碎碎念气泡（WorldSpace Canvas）")]
    [Tooltip("整个气泡的根物体（Canvas 或包含背景的 Panel），不要只填文字")]
    public GameObject thoughtBubble;
    public Text thoughtLabel;
    [TextArea] public string thoughtText = "门自己关上了……先四处看看。";
    [Min(0.1f)] public float thoughtDuration = 1.5f;
    [Tooltip("碎碎念消失后，等多久再弹出任务弹窗")]
    public float thoughtHideDelay = 0.25f;

    [Header("任务弹窗（屏幕空间 UI）")]
    public GameObject taskPanel;
    public Text taskLabel;
    [TextArea] public string taskText = "任务：调查这个房间";
    public Button confirmButton;

    [Header("调试")]
    [Tooltip("开启后会在 Console 打印所有相机的状态")]
    public bool debugLog = false;

    private bool playing = false;
    private Camera[] allCameras;
    private bool[] cameraEnabledCache;
    private bool thirdPersonObjectWasActive = true;

    public bool IsPlaying => playing;

    void Awake()
    {
        if (playerMover == null)
            playerMover = FindObjectOfType<PlayerMover>();

        if (firstPersonCamera != null && thirdPersonCamera != null &&
            firstPersonCamera == thirdPersonCamera)
        {
            Debug.LogError("[DoorEntrySequence] 第一人称和第三人称拖成了同一个相机，请重新指定！", this);
        }

        // 记录场景里所有相机的初始启用状态，结束时恢复
        allCameras = FindObjectsOfType<Camera>();
        cameraEnabledCache = new bool[allCameras.Length];
        for (int i = 0; i < allCameras.Length; i++)
            cameraEnabledCache[i] = allCameras[i].enabled;

        if (thoughtBubble != null) thoughtBubble.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

        if (thirdPersonCamera != null)
        {
            thirdPersonObjectWasActive = thirdPersonCamera.gameObject.activeSelf;
            thirdPersonCamera.enabled = false;
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void Play()
    {
        if (playing) return;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        playing = true;

        // 1. 冻结玩家，解锁鼠标（后面要点“确定”）
        if (playerMover != null)
        {
            playerMover.SetControl(false, false);
            playerMover.LockCursor(false);
        }

        // 2. 只保留固定的第三人称相机
        if (thirdPersonCamera != null)
        {
            // 关掉除目标外的所有相机，防止主相机继续盖住画面
            for (int i = 0; i < allCameras.Length; i++)
            {
                if (allCameras[i] == null || allCameras[i] == thirdPersonCamera) continue;
                allCameras[i].enabled = false;
            }

            thirdPersonCamera.gameObject.SetActive(true);
            thirdPersonCamera.enabled = true;
        }
        else
        {
            Debug.LogError("[DoorEntrySequence] Third Person Camera 没有赋值，无法切换视角。", this);
        }

        if (debugLog) LogCameraState();

        // 3. 显示碎碎念
        if (thoughtLabel != null) thoughtLabel.text = thoughtText;
        if (thoughtBubble != null) thoughtBubble.SetActive(true);
        if (debugLog) Debug.Log("[DoorEntrySequence] 显示碎碎念");

        yield return new WaitForSeconds(thoughtDuration);

        // 4. 先让碎碎念消失
        if (thoughtBubble != null) thoughtBubble.SetActive(false);

        // 等一帧，确保它已经真正隐藏
        yield return null;

        // 再等一小段间隔，避免和任务弹窗同帧出现
        if (thoughtHideDelay > 0f)
            yield return new WaitForSeconds(thoughtHideDelay);

        // 5. 碎碎念消失后才出现任务弹窗
        if (taskLabel != null) taskLabel.text = taskText;
        if (taskPanel != null) taskPanel.SetActive(true);
        if (debugLog) Debug.Log("[DoorEntrySequence] 碎碎念已消失，显示任务弹窗，等待确定");
    }

    private void OnConfirmClicked()
    {
        if (!playing) return;
        StartCoroutine(EndSequence());
    }

    private IEnumerator EndSequence()
    {
        if (taskPanel != null) taskPanel.SetActive(false);
        if (thoughtBubble != null) thoughtBubble.SetActive(false);

        // 恢复所有相机原来的启用状态
        if (allCameras != null)
        {
            for (int i = 0; i < allCameras.Length; i++)
            {
                if (allCameras[i] == null) continue;
                allCameras[i].enabled = cameraEnabledCache[i];
            }
        }

        // 第三人称关掉
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.enabled = false;
            if (!thirdPersonObjectWasActive)
                thirdPersonCamera.gameObject.SetActive(false);
        }

        // 第一人称确保打开
        if (firstPersonCamera != null)
            firstPersonCamera.enabled = true;

        // 恢复移动和鼠标锁定
        if (playerMover != null)
        {
            playerMover.SetControl(true, true);
            playerMover.LockCursor(true);
        }

        playing = false;
        if (debugLog) LogCameraState();

        yield break;
    }

    private void LogCameraState()
    {
        foreach (Camera c in allCameras)
        {
            if (c == null) continue;
            Debug.Log($"[Camera] {c.name} | active={c.gameObject.activeInHierarchy} " +
                      $"| enabled={c.enabled} | depth={c.depth} " +
                      $"| pos={c.transform.position}");
        }

        if (thirdPersonCamera != null)
        {
            Debug.Log($"[DoorEntrySequence] 目标第三人称 = {thirdPersonCamera.name}, " +
                      $"enabled={thirdPersonCamera.enabled}, " +
                      $"pos={thirdPersonCamera.transform.position}, " +
                      $"rot={thirdPersonCamera.transform.eulerAngles}");
        }
    }

    void LateUpdate()
    {
        // 碎碎念气泡始终面向当前相机
        if (thoughtBubble != null && thoughtBubble.activeSelf)
        {
            Transform cam = null;
            if (thirdPersonCamera != null && thirdPersonCamera.enabled)
                cam = thirdPersonCamera.transform;
            else if (Camera.main != null)
                cam = Camera.main.transform;

            if (cam != null)
            {
                Vector3 dir = thoughtBubble.transform.position - cam.position;
                if (dir.sqrMagnitude > 0.0001f)
                    thoughtBubble.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}