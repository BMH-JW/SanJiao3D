using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>铰链位置：左边缘 / 右边缘 / 门体中心 / 自定义偏移。</summary>
public enum HingeSide { Left, Right, Center, Custom }

/// <summary>计算旋转轴，并绕轴心旋转门体（门和冰箱共用）。</summary>
public static class HingeUtility
{
    /// <summary>返回铰链点在门体本地坐标里的位置。</summary>
    public static Vector3 ResolveLocal(Transform pivot, HingeSide side, Vector3 custom)
    {
        if (pivot == null) return Vector3.zero;
        if (side == HingeSide.Center) return Vector3.zero;
        if (side == HingeSide.Custom) return custom;

        int dir = side == HingeSide.Left ? -1 : 1;
        return EdgeLocal(pivot, dir);
    }

    /// <summary>取门体渲染范围在本地坐标里的最左（dir=-1）或最右（dir=1）边缘中点。</summary>
    public static Vector3 EdgeLocal(Transform pivot, int dir)
    {
        Renderer[] renderers = pivot.GetComponentsInChildren<Renderer>();
        bool has = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Bounds b = r.localBounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            Matrix4x4 toLocal = pivot.worldToLocalMatrix * r.transform.localToWorldMatrix;

            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = c + new Vector3(
                    (k & 1) == 0 ? -e.x : e.x,
                    (k & 2) == 0 ? -e.y : e.y,
                    (k & 4) == 0 ? -e.z : e.z);

                Vector3 local = toLocal.MultiplyPoint3x4(corner);
                if (!has)
                {
                    min = local;
                    max = local;
                    has = true;
                }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        if (!has) return Vector3.zero;

        float x = dir < 0 ? min.x : max.x;
        return new Vector3(x, (min.y + max.y) * 0.5f, (min.z + max.z) * 0.5f);
    }

    /// <summary>绕铰链点旋转：门体原点相对铰链的偏移跟着一起转，铰链点保持不动。</summary>
    public static void Apply(Transform pivot, Vector3 hingeWorld, Vector3 baseWorldPos,
                             Quaternion baseWorldRot, float angle)
    {
        if (pivot == null) return;

        Quaternion delta = Quaternion.Euler(0f, angle, 0f);
        pivot.rotation = delta * baseWorldRot;
        pivot.position = hingeWorld + delta * (baseWorldPos - hingeWorld);
    }
}

/// <summary>
/// 通用开关门脚本（门 / 抽屉 / 冰箱都能用）：
/// 1. 靠近 → 显示开门按钮（没拖 Proximity Trigger 就不弹）；
/// 2. 点击 → 旋转开关，同步开关阻挡碰撞体，播放开关门音效；
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

    [Header("铰链（旋转轴）")]
    [Tooltip("门轴位置：厨房门 / 柜子门选 Left，冰箱门选 Right；需要时可换成 Custom")]
    public HingeSide hingeSide = HingeSide.Left;
    [Tooltip("自定义轴心（门的本地坐标），只有 Hinge Side = Custom 时生效")]
    public Vector3 hingeOffset = Vector3.zero;

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

    [Header("音效（可选，不拖就不响）")]
    [Tooltip("留空会自动在当前物体上创建一个 AudioSource")]
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip closeClip;
    [Range(0f, 1f)] public float soundVolume = 1f;

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

    // 绕边缘旋转用：门初始的世界位置 / 朝向 / 铰链世界坐标
    private Vector3 baseWorldPos;
    private Quaternion baseWorldRot;
    private Vector3 hingeWorld;

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

        EnsureAudio();
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
        CaptureHinge();
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
        ApplyAngle(currentAngle);

        if (Mathf.Abs(currentAngle - targetAngle) < 0.01f)
        {
            currentAngle = targetAngle;
            isMoving = false;
            isOpen = Mathf.Abs(currentAngle - openAngle) < 0.01f;
            if (doorBlocker != null) doorBlocker.enabled = !isOpen;

            // 门真正关到位时播放关门音效（咔哒 / 砰）
            if (!isOpen) PlaySound(closeClip);

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

        PlaySound(openClip);   // 开门时立即播开门音效
    }

    private void StartClose()
    {
        isMoving = true;
        targetAngle = 0f;

        if (doorBlocker != null) doorBlocker.enabled = true;
    }

    // ---- 音效 ----
    private void EnsureAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        EnsureAudio();
        audioSource.PlayOneShot(clip, Mathf.Clamp01(soundVolume * AudioLoudness.GetGain(clip)));
    }

    /// <summary>记录初始位置，并按配置算出铰链点（世界坐标）。</summary>
    private void CaptureHinge()
    {
        if (doorPivot == null) return;

        baseWorldPos = doorPivot.position;
        baseWorldRot = doorPivot.rotation;

        Vector3 localHinge = HingeUtility.ResolveLocal(doorPivot, hingeSide, hingeOffset);
        hingeWorld = doorPivot.TransformPoint(localHinge);
    }

    /// <summary>绕铰链点旋转门体（左边缘 / 右边缘 / 中心 / 自定义）。</summary>
    private void ApplyAngle(float angle)
    {
        HingeUtility.Apply(doorPivot, hingeWorld, baseWorldPos, baseWorldRot, angle);
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