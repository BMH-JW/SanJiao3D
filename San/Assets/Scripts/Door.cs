using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 门交互逻辑：
/// 1. 玩家靠近门的「接近触发器」→ 弹出可点击的「开门」按钮，同时解锁鼠标。
/// 2. 点击按钮 → 门旋转打开，关闭阻挡碰撞体，重新锁定鼠标让玩家走进去。
/// 3. 玩家穿过门后的「进入触发器」→ 通知 GameSequenceManager 开始剧情。
/// </summary>
public class DoorInteraction : MonoBehaviour
{
    [Header("门体")]
    [Tooltip("真正旋转的门板子物体（Pivot）")]
    public Transform doorPivot;
    public float openAngle = -90f;     // 打开角度（负值向一侧开）
    public float openSpeed = 120f;     // 度/秒
    [Tooltip("关门时阻挡玩家通过的碰撞体，开门后禁用")]
    public Collider doorBlocker;

    [Header("开门按钮 UI（WorldSpace Canvas）")]
    public GameObject openButtonCanvas;
    public Button openDoorButton;

    [Header("触发器")]
    [Tooltip("门附近的接近区域 Collider（isTrigger=true）")]
    public Collider proximityTrigger;
    [Tooltip("门后房间内的进入区域 Collider（isTrigger=true）")]
    public Collider enterTrigger;

    [Header("引用")]
    public PlayerMover playerMover;

    private bool isOpen = false;
    private bool isOpening = false;
    private float currentAngle = 0f;
    private bool entered = false;
    private Transform player;

    void Start()
    {
        if (openButtonCanvas != null) openButtonCanvas.SetActive(false);
        if (openDoorButton != null) openDoorButton.onClick.AddListener(OpenDoor);

        if (playerMover == null)
            playerMover = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMover>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        // ---- 开门动画 ----
        if (isOpening)
        {
            currentAngle = Mathf.MoveTowards(currentAngle, openAngle, openSpeed * Time.deltaTime);
            doorPivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

            if (Mathf.Abs(currentAngle - openAngle) < 0.01f)
            {
                isOpening = false;
                isOpen = true;
                if (doorBlocker != null) doorBlocker.enabled = false;
            }
        }

        if (player == null) return;

        // ---- 接近检测：显示开门按钮 ----
        bool inProximity = proximityTrigger != null && proximityTrigger.bounds.Contains(player.position);

        if (inProximity && !isOpen && !isOpening)
        {
            if (openButtonCanvas != null && !openButtonCanvas.activeSelf)
            {
                openButtonCanvas.SetActive(true);
                if (playerMover != null)
                {
                    playerMover.SetControl(true, false);   // 还能移动，但不能转视角
                    playerMover.LockCursor(false);          // 解锁鼠标去点按钮
                }
            }
        }
        else
        {
            if (openButtonCanvas != null && openButtonCanvas.activeSelf)
            {
                openButtonCanvas.SetActive(false);
                // 只有在门没被打开时才重新锁定（打开后玩家要走进去）
                if (!isOpen && !isOpening && playerMover != null)
                {
                    playerMover.SetControl(true, true);
                    playerMover.LockCursor(true);
                }
            }
        }

        // ---- 进入检测：玩家穿过门 ----
        if (isOpen && !entered && enterTrigger != null)
        {
            if (enterTrigger.bounds.Contains(player.position))
            {
                entered = true;
                if (GameSequenceManager.Instance != null)
                    GameSequenceManager.Instance.OnPlayerEnteredRoom();
            }
        }
    }

    /// <summary>按钮 OnClick 绑定到此方法。</summary>
    public void OpenDoor()
    {
        if (isOpen || isOpening) return;
        isOpening = true;

        if (openButtonCanvas != null) openButtonCanvas.SetActive(false);

        // 重新锁定鼠标，让玩家能走进去
        if (playerMover != null)
        {
            playerMover.SetControl(true, true);
            playerMover.LockCursor(true);
        }
    }
}
