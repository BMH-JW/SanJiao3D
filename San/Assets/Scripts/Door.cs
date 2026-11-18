using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个可交互物体（门 / 冰箱 / 柜子）：
/// 1. 靠近 → 显示自己的按钮，文字随状态在「开启 / 关闭」切换。
/// 2. 点击 → 开或关，关闭时播放音效。
/// 3. 门打开后玩家进入 enterTrigger → 触发进门剧情并自动关门。
/// </summary>
public class DoorInteraction : MonoBehaviour
{
    [Header("门体")]
    [Tooltip("真正旋转的门板子物体（Pivot）")]
    public Transform doorPivot;
    public float openAngle = -90f;
    public float openSpeed = 120f;
    [Tooltip("关门时阻挡玩家通过的碰撞体，门完全关闭后恢复")]
    public Collider doorBlocker;

    [Header("按钮 UI")]
    [Tooltip("这扇门专属的按钮根节点：必须是它自己的 Button")]
    public GameObject buttonRoot;
    [Tooltip("按钮所在的 WorldSpace Canvas（只用于检测是否被关闭）")]
    public GameObject openButtonCanvas;
    public Button openDoorButton;

    [Header("按钮文字")]
    [Tooltip("按钮上的文字组件；留空会自动在按钮子物体里找 UI Text")]
    public Text buttonLabel;
    public string openButtonText = "开启";
    public string closeButtonText = "关闭";

    [Header("触发器")]
    [Tooltip("这扇门自己的接近区域（isTrigger=true）")]
    public Collider proximityTrigger;
    [Range(0f, 0.5f)]
    [Tooltip("只补偿角色脚底在触发盒下方的情况，建议 0~0.2")]
    public float proximityTolerance = 0.3f;
    [Tooltip("门后房间的进入区域；冰箱 / 柜子请留空")]
    public Collider enterTrigger;

    [Header("自动关门")]
    [Tooltip("玩家进入 Enter Trigger 后自动关门并播放音效")]
    public bool autoCloseAfterEnter = true;

    [Header("进门剧情")]
    [Tooltip("玩家进入 Enter Trigger 时播放第三视角剧情；留空则退回调用 GameSequenceManager")]
    public DoorEntrySequence entrySequence;

    [Header("音效")]
    [Tooltip("关门音效")]
    public AudioClip closeSound;
    [Tooltip("留空会自动获取或添加 AudioSource")]
    public AudioSource audioSource;

    [Header("引用")]
    public PlayerMover playerMover;

    [Header("调试")]
    public bool showDebugInfo = false;

    private bool isOpen = false;          // 目标状态：true = 打开
    private bool isAnimating = false;     // 正在开 / 关中
    private float currentAngle = 0f;
    private float targetAngle = 0f;
    private bool entered = false;         // 剧情只触发一次
    private bool uiVisible = false;
    private bool lastDebugState = false;
    private Transform player;
    private Quaternion initialRotation;

    // 当前屏幕上有几个按钮在显示，全部消失后才锁鼠标
    private static int visibleUICount = 0;

    // 进门剧情是否正在播放
    private bool IsSequencePlaying => entrySequence != null && entrySequence.IsPlaying;

    void Awake()
    {
        if (doorPivot != null)
            initialRotation = doorPivot.localRotation;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null && closeSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        // 1) Inspector 引用 → 2) Player 标签 → 3) 全场景查找
        if (playerMover == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerMover = playerObj.GetComponent<PlayerMover>();
        }
        if (playerMover == null)
            playerMover = FindObjectOfType<PlayerMover>();

        player = playerMover != null ? playerMover.transform : null;

        if (player == null)
            Debug.LogError($"[{name}] 没找到 PlayerMover：请给这扇门填 Player Mover，或给玩家加 Player 标签。", this);
        if (proximityTrigger == null)
            Debug.LogError($"[{name}] Proximity Trigger 没有赋值，按钮永远不会出现。", this);
        if (buttonRoot == null)
            Debug.LogWarning($"[{name}] Button Root 没赋值：会退回控制整个 Canvas，可能显示别人的按钮。", this);
        if (openButtonCanvas != null && !openButtonCanvas.activeInHierarchy)
            Debug.LogWarning($"[{name}] Open Button Canvas 在场景里是关闭的，按钮不会显示。", this);

        if (openDoorButton != null)
            openDoorButton.onClick.AddListener(ToggleDoor);

        CacheButtonLabel();
        UpdateButtonText();

        SetUIVisible(false, true);
    }

    void Update()
    {
        // ---- 开关动画 ----
        if (isAnimating)
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, openSpeed * Time.deltaTime);
            if (doorPivot != null)
                doorPivot.localRotation = initialRotation * Quaternion.Euler(0f, currentAngle, 0f);

            if (Mathf.Abs(currentAngle - targetAngle) < 0.01f)
            {
                currentAngle = targetAngle;
                isAnimating = false;

                if (doorBlocker != null) doorBlocker.enabled = !isOpen;
                if (openDoorButton != null) openDoorButton.interactable = true;
                UpdateButtonText();
            }
        }

        if (player == null) return;

        // ---- 接近检测 ----
        bool inProximity = IsPlayerInProximity();

        if (showDebugInfo && inProximity != lastDebugState)
        {
            lastDebugState = inProximity;
            string triggerName = proximityTrigger != null ? proximityTrigger.name : "null";
            Debug.Log($"[{name}] inProximity={inProximity}, 触发器={triggerName}, player={player.position}");
        }

        // 剧情播放时不显示门的按钮
        SetUIVisible(inProximity && !IsSequencePlaying);

        // 有按钮时保持鼠标解锁
        if (uiVisible && playerMover != null && Cursor.lockState != CursorLockMode.None)
            playerMover.LockCursor(false);

        // ---- 进入检测：剧情 + 自动关门 ----
        if (isOpen && enterTrigger != null &&
            enterTrigger.bounds.Contains(player.position))
        {
            if (!entered)
            {
                entered = true;

                if (entrySequence != null)
                    entrySequence.Play();
                else if (GameSequenceManager.Instance != null)
                    GameSequenceManager.Instance.OnPlayerEnteredRoom();
            }

            if (autoCloseAfterEnter)
                CloseDoor();
        }
    }

    private bool IsPlayerInProximity()
    {
        if (proximityTrigger == null) return false;
        if (!proximityTrigger.enabled || !proximityTrigger.gameObject.activeInHierarchy) return false;

        Bounds b = proximityTrigger.bounds;
        Vector3 feet = player.position;

        if (b.Contains(feet)) return true;

        // 角色原点在脚底时，补测胸口位置
        if (b.Contains(feet + Vector3.up * 1.0f)) return true;

        return b.SqrDistance(feet) <= proximityTolerance * proximityTolerance;
    }

    /// <summary>按钮 OnClick 绑定此方法：按状态开或关。</summary>
    public void ToggleDoor()
    {
        if (isAnimating) return;
        if (isOpen) CloseDoor();
        else OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen || isAnimating) return;

        isOpen = true;
        targetAngle = openAngle;
        isAnimating = true;

        if (openDoorButton != null) openDoorButton.interactable = false;
        UpdateButtonText();
    }

    public void CloseDoor()
    {
        if (!isOpen || isAnimating) return;

        isOpen = false;
        targetAngle = 0f;
        isAnimating = true;

        if (openDoorButton != null) openDoorButton.interactable = false;
        UpdateButtonText();
        PlayCloseSound();
    }

    private void PlayCloseSound()
    {
        if (closeSound == null) return;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.PlayOneShot(closeSound);
    }

    private void CacheButtonLabel()
    {
        if (buttonLabel == null && openDoorButton != null)
            buttonLabel = openDoorButton.GetComponentInChildren<Text>(true);
    }

    private void UpdateButtonText()
    {
        if (buttonLabel == null) CacheButtonLabel();
        if (buttonLabel != null)
            buttonLabel.text = isOpen ? closeButtonText : openButtonText;
    }

    private void SetUIVisible(bool visible, bool force = false)
    {
        if (!force && uiVisible == visible) return;
        bool wasVisible = uiVisible;
        uiVisible = visible;

        GameObject target = buttonRoot != null ? buttonRoot : openButtonCanvas;
        if (target != null) target.SetActive(visible);

        bool becameVisible = visible && !wasVisible;
        bool becameHidden = !visible && wasVisible;

        if (becameVisible) visibleUICount++;
        if (becameHidden) visibleUICount = Mathf.Max(0, visibleUICount - 1);

        if (playerMover == null) return;

        if (becameVisible)
        {
            playerMover.SetControl(true, true);
            playerMover.LockCursor(false);
        }
        else if (becameHidden && visibleUICount == 0 && !IsSequencePlaying)
        {
            // 剧情播放时不抢鼠标，剧情自己会控制
            playerMover.SetControl(true, true);
            playerMover.LockCursor(true);
        }
    }
}