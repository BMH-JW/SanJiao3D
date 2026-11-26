using UnityEngine;
using System.Collections.Generic;

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

    [Header("调试")]
    [Tooltip("按下 WASD 时在 Console 打印驱动相机与各启用相机的朝向，用于排查方向错乱")]
    public bool debugLog = true;

    private Rigidbody rb;
    private Animator anim;
    private float yaw = 0f;
    private float pitch = 0f;

    private bool canMove = true;
    private bool canLook = true;

    // 本帧渲染到屏幕(targetTexture 为空)的相机，按渲染顺序记录——最后一个就是画在最上层、玩家实际看到的画面
    private readonly List<Camera> screenCamerasThisFrame = new List<Camera>();
    private int screenCamerasFrame = -1;
    private float nextDebugTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;          // 防止刚体被外力带倒
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        Camera.onPostRender += OnCameraPostRender;
    }

    void OnDisable()
    {
        Camera.onPostRender -= OnCameraPostRender;
    }

    /// <summary>记录本帧渲染到屏幕的相机（targetTexture 为空），按渲染顺序保存。</summary>
    private void OnCameraPostRender(Camera cam)
    {
        if (cam == null || cam.targetTexture != null) return;

        int frame = Time.frameCount;
        if (screenCamerasFrame != frame)
        {
            screenCamerasThisFrame.Clear();
            screenCamerasFrame = frame;
        }
        if (!screenCamerasThisFrame.Contains(cam))
            screenCamerasThisFrame.Add(cam);
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

        // 以当前真正渲染画面的相机为准计算移动方向：
        // 视角旋转后 forward/right 始终对应屏幕方向，避免出现 W 变左、A 变前之类的错乱
        Camera viewCam = ResolveViewCamera();
        Vector3 camForward = viewCam != null ? viewCam.transform.forward : transform.forward;
        Vector3 camRight = viewCam != null ? viewCam.transform.right : transform.right;
        camForward.y = 0f;
        camRight.y = 0f;

        Vector3 moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = rb.velocity.y;       // 保留重力
        rb.velocity = velocity;

        // 调试：有输入且到节流时间时，打印驱动相机与本帧所有屏幕相机的朝向
        if (debugLog && (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f) && Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + 0.5f;
            string camName = viewCam != null ? viewCam.name : "(null)";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PlayerMover] 输入 h=" + h.ToString("F2") + " v=" + v.ToString("F2"));
            sb.AppendLine("  驱动相机: " + camName + "  forward=" + camForward.ToString("F3") + "  right=" + camRight.ToString("F3"));
            sb.Append("  本帧屏幕相机(按渲染顺序): ");
            for (int i = 0; i < screenCamerasThisFrame.Count; i++)
            {
                Camera c = screenCamerasThisFrame[i];
                if (c == null) continue;
                Vector3 f = c.transform.forward; f.y = 0f;
                sb.Append("[" + c.name + " depth=" + c.depth + " fwd=" + f.normalized.ToString("F2") + "] ");
            }
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>
    /// 取当前真正在渲染画面的相机（玩家实际看到的视角），让 WASD 永远跟随它。
    /// 依据：本帧通过 Camera.onPostRender 捕捉到的、渲染到屏幕(targetTexture 为空)的相机。
    /// 规则：取 depth 最大的那一层里【最后渲染】的相机——它画在最上层，就是玩家看到的画面；
    /// 优先选透视相机，避免被正交 UI 相机带偏。
    /// </summary>
    private Camera ResolveViewCamera()
    {
        Camera assigned = (cameraTransform != null) ? cameraTransform.GetComponent<Camera>() : null;

        // 1) 从本帧真正画到屏幕的相机里，找最上层那台
        float maxDepth = float.NegativeInfinity;
        for (int i = 0; i < screenCamerasThisFrame.Count; i++)
        {
            Camera c = screenCamerasThisFrame[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            if (c.depth > maxDepth) maxDepth = c.depth;
        }

        if (maxDepth > float.NegativeInfinity)
        {
            // 同 depth 下，最后渲染的画在最上面 → 倒序遍历，优先取透视的（跳过正交 UI 相机）
            Camera fallback = null;
            for (int i = screenCamerasThisFrame.Count - 1; i >= 0; i--)
            {
                Camera c = screenCamerasThisFrame[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if (!Mathf.Approximately(c.depth, maxDepth)) continue;
                if (!c.orthographic) return c;   // 透视相机直接用
                if (fallback == null) fallback = c;
            }
            if (fallback != null) return fallback;
        }

        // 2) 兜底：还没捕捉到渲染时，用指定相机或主相机
        if (assigned != null && assigned.isActiveAndEnabled) return assigned;
        return Camera.main;
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