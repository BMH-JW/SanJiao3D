using UnityEngine;

/// <summary>
/// 第一人称角色移动 + 鼠标视角控制。
/// 鼠标 X 控制身体左右旋转（Yaw），鼠标 Y 控制摄像机上下俯仰（Pitch）。
/// 提供 SetControl / LockCursor 给剧情流程在过场时冻结输入。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMover : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 3f;

    [Header("鼠标视角参数")]
    public float mouseSensitivity = 2f;
    public float minPitch = -60f;
    public float maxPitch = 60f;

    [Header("屏幕边缘旋转（光标未锁定时生效）")]
    [Tooltip("鼠标贴到屏幕边缘时持续旋转视角，解决光标到边缘就转不动的问题")]
    public bool enableEdgeRotate = true;

    [Tooltip("距离屏幕边缘多少像素以内开始旋转")]
    public float edgeSize = 24f;

    [Tooltip("左右边缘的转向速度（度/秒）")]
    public float edgeYawSpeed = 90f;

    [Tooltip("上下边缘的俯仰速度（度/秒）")]
    public float edgePitchSpeed = 60f;

    [Tooltip("鼠标悬停在 UI 上时不做边缘旋转")]
    public bool ignoreEdgeRotateOverUI = true;

    [Header("引用")]
    [Tooltip("第一人称摄像机，挂在玩家子物体（眼睛位置）")]
    public Transform cameraTransform;

    private Rigidbody rb;
    private Animator anim;
    private float yaw = 0f;
    private float pitch = 0f;

    private bool canMove = true;
    private bool canLook = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;          // 防止刚体被外力带倒
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // ---- 鼠标视角 ----
        if (canLook)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;

            // 光标未锁定时（CustomCursor 会强制解锁），鼠标贴到屏幕边缘也能持续转视角
            if (enableEdgeRotate && Cursor.lockState != CursorLockMode.Locked)
            {
                bool overUI = ignoreEdgeRotateOverUI &&
                    UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

                if (!overUI)
                {
                    Vector3 mousePos = Input.mousePosition;
                    float edgeX = 0f;
                    float edgeY = 0f;

                    // 鼠标在游戏窗口内才做边缘旋转，避免切出窗口后视角自己转
                    bool insideScreen =
                        mousePos.x >= 0f && mousePos.x < Screen.width &&
                        mousePos.y >= 0f && mousePos.y < Screen.height;

                    if (insideScreen)
                    {
                        if (mousePos.x <= edgeSize) edgeX = -1f;
                        else if (mousePos.x >= Screen.width - edgeSize) edgeX = 1f;

                        if (mousePos.y <= edgeSize) edgeY = -1f;
                        else if (mousePos.y >= Screen.height - edgeSize) edgeY = 1f;
                    }

                    yaw += edgeX * edgeYawSpeed * Time.deltaTime;
                    pitch -= edgeY * edgePitchSpeed * Time.deltaTime;
                }
            }

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // 身体只转 Yaw
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // 摄像机子物体转 Pitch
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // ---- 动画 ----
        if (anim != null)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            bool isMoving = (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f);
            anim.SetBool("IsMoving", isMoving);
        }
    }

    void FixedUpdate()
    {
        if (!canMove)
        {
            // 冻结时清掉水平惯性，避免过场期间人还在滑、最后滑出门外
            if (rb != null)
            {
                Vector3 stop = rb.velocity;
                stop.x = 0f;
                stop.z = 0f;
                rb.velocity = stop;
            }
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 相对身体朝向移动（第一人称）
        Vector3 moveDir = (transform.forward * v + transform.right * h).normalized;
        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = rb.velocity.y;       // 保留重力
        rb.velocity = velocity;
    }

    /// <summary>剧情/过场时冻结或恢复输入。</summary>
    public void SetControl(bool move, bool look)
    {
        canMove = move;
        canLook = look;
    }

    /// <summary>把玩家瞬移回指定位置和朝向（同步内部 yaw，避免下一次转视角跳回）。</summary>
    public void Teleport(Vector3 position, float yawDegrees)
    {
        yaw = yawDegrees;
        pitch = 0f;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
        }
        transform.position = position;
    }

    /// <summary>锁定/解锁鼠标光标。UI 按钮可点击时解锁。</summary>
    public void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}