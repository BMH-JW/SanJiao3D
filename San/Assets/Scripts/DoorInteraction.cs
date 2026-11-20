using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用开关门脚本（门 / 抽屉 / 冰箱都能用）：
/// 1. 靠近 → 显示开门按钮（没拖 Proximity Trigger 就不弹）；
/// 2. 点击 → 旋转开关，同步开关阻挡碰撞体；
/// 3. 只有拖了 Enter Trigger 的那份（厨房门），玩家进入后会触发入场剧情 + 延迟自动关门，且只触发一次。
/// </summary>
public class DoorInteraction : MonoBehaviour
{
    [Header("门体")]
    public Transform doorPivot;
    public float openAngle = -90f;
    public float openSpeed = 120f;
    [Tooltip("关门时阻挡玩家的碰撞体，开门后禁用。抽屉/冰箱可留空")]
    public Collider doorBlocker;

    [Header("开门按钮 UI（WorldSpace Canvas）")]
    public GameObject openButtonCanvas;
    public Button openDoorButton;

    [Header("触发器（都可留空）")]
    [Tooltip("门附近区域，留空则不会自动弹开门按钮")]
    public Collider proximityTrigger;
    [Tooltip("门后房间区域。只有厨房门需要拖")]
    public Collider enterTrigger;

    [Header("引用")]
    public PlayerMover playerMover;

    [Header("进门剧情")]
    [Tooltip("挂着 DoorEntrySequence（DoorEnter）的物体，玩家进入后调用它的 Play()")]
    public DoorEntrySequence entryStory;

    [Header("进入检测")]
    [Tooltip("除脚底外再检测的高度（米），避免触发器悬空")]
    public float playerProbeHeight = 0.9f;

    [Header("进入后自动关门")]
    public bool autoCloseAfterEnter = true;
    public float autoCloseDelay = 1.5f;

    [Header("剧情只触发一次")]
    public bool storyOnlyOnce = true;
    public string storyKey = "";

    [Header("调试")]
    public bool debugLog = true;

    private static readonly HashSet<string> finishedStoryKeys = new HashSet<string>();

    private bool isOpen = false;
    private bool isMoving = false;
    private float currentAngle = 0f;
    private float targetAngle = 0f;
    private bool entered = false;
    private Transform player;
    private string resolvedKey;

    public bool StoryFinished { get; private set; }
    public bool IsOpen { get { return isOpen; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStatics()
    {
        finishedStoryKeys.Clear();
    }

    // =====================================================================

    private void Awake()
    {
        resolvedKey = string.IsNullOrEmpty(storyKey)
            ? gameObject.scene.name + "/" + gameObject.name
            : storyKey;

        if (storyOnlyOnce && finishedStoryKeys.Contains(resolvedKey))
        {
            StoryFinished = true;
            entered = true;
        }
    }

    private void Start()
    {
        if (openButtonCanvas != null) openButtonCanvas.SetActive(false);

        if (openDoorButton != null)
        {
            openDoorButton.onClick.RemoveListener(OpenDoor);
            openDoorButton.onClick.AddListener(OpenDoor);
        }

        if (entryStory == null)
            entryStory = FindObjectOfType<DoorEntrySequence>();

        ResolvePlayer();
    }

    private void Update()
    {
        UpdateDoorAnimation();

        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }

        Vector3 feet = player.position;
        Vector3 chest = feet + Vector3.up * playerProbeHeight;

        bool inProximity = Inside(proximityTrigger, feet) || Inside(proximityTrigger, chest);

        if (inProximity && !isOpen && !isMoving)
        {
            if (openButtonCanvas != null && !openButtonCanvas.activeSelf)
            {
                openButtonCanvas.SetActive(true);
                if (playerMover != null)
                {
                    playerMover.SetControl(true, true);
                    playerMover.LockCursor(false);
                }
            }
        }
        else
        {
            if (openButtonCanvas != null && openButtonCanvas.activeSelf)
            {
                openButtonCanvas.SetActive(false);
                if (!isOpen && !isMoving && playerMover != null)
                {
                    playerMover.SetControl(true, true);
                    playerMover.LockCursor(true);
                }
            }
        }

        // 没拖 enterTrigger 的物体（抽屉/冰箱）直接跳过
        if (enterTrigger == null) return;
        if (StoryFinished || entered) return;

        bool inEnter = Inside(enterTrigger, feet) || Inside(enterTrigger, chest);
        if (!inEnter) return;

        bool doorReady = isOpen || doorBlocker == null || !doorBlocker.enabled;
        if (!doorReady) return;

        entered = true;
        MarkStoryFinished();

        if (debugLog) Debug.Log("[Door] 玩家进入房间", this);

        // 触发金毛剧情
        if (entryStory != null)
        {
            entryStory.Play();
            if (debugLog) Debug.Log("[Door] 已调用进场剧情 Play()", this);
        }
        else
        {
            Debug.LogWarning("[Door] Entry Story 没拖，进场剧情不会播放", this);
        }

        if (autoCloseAfterEnter) StartCoroutine(AutoCloseRoutine());
    }

    private IEnumerator AutoCloseRoutine()
    {
        if (autoCloseDelay > 0f)
            yield return new WaitForSeconds(autoCloseDelay);

        if (isOpen && !isMoving)
        {
            StartClose();
            if (debugLog) Debug.Log("[Door] 已自动关门", this);
        }
    }

    // =====================================================================
    // 对外方法
    // =====================================================================

    public void OpenDoor()
    {
        if (isOpen || isMoving) return;
        StartOpen();
    }

    public void CloseDoor()
    {
        if (!isOpen || isMoving) return;
        StartClose();
    }

    public void ToggleDoor()
    {
        if (isMoving) return;
        if (isOpen) StartClose();
        else StartOpen();
    }

    public void ToggleDoor(bool open)
    {
        if (isMoving) return;
        if (open)
        {
            if (!isOpen) StartOpen();
        }
        else
        {
            if (isOpen) StartClose();
        }
    }

    public void MarkStoryFinished()
    {
        if (StoryFinished) return;

        StoryFinished = true;
        entered = true;

        if (storyOnlyOnce && !string.IsNullOrEmpty(resolvedKey))
            finishedStoryKeys.Add(resolvedKey);
    }

    // =====================================================================
    // 内部
    // =====================================================================

    private void UpdateDoorAnimation()
    {
        if (!isMoving) return;

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, openSpeed * Time.deltaTime);
        if (doorPivot != null)
            doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

        if (Mathf.Abs(currentAngle - targetAngle) < 0.01f)
        {
            currentAngle = targetAngle;
            isMoving = false;
            isOpen = Mathf.Abs(currentAngle - openAngle) < 0.01f;
            if (doorBlocker != null) doorBlocker.enabled = !isOpen;

            if (debugLog)
                Debug.Log("[Door] 门现在是：" + (isOpen ? "打开" : "关闭"), this);
        }
    }

    private void StartOpen()
    {
        isMoving = true;
        targetAngle = openAngle;

        if (openButtonCanvas != null) openButtonCanvas.SetActive(false);

        if (playerMover != null)
        {
            playerMover.SetControl(true, true);
            playerMover.LockCursor(true);
        }
    }

    private void StartClose()
    {
        isMoving = true;
        targetAngle = 0f;

        if (doorBlocker != null) doorBlocker.enabled = true;
    }

    private static bool Inside(Collider trigger, Vector3 point)
    {
        if (trigger == null) return false;
        return trigger.bounds.Contains(point);
    }

    private void ResolvePlayer()
    {
        if (player != null) return;

        if (playerMover == null)
            playerMover = FindObjectOfType<PlayerMover>();

        if (playerMover != null)
        {
            player = playerMover.transform;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }
}