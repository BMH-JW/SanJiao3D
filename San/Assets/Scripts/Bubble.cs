using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 跟随玩家的世界空间碎碎念气泡：平滑跟随 + 始终面向相机 + 定时自动隐藏。
/// </summary>
public class MumbleFollower : MonoBehaviour
{
    [Header("跟随")]
    [Tooltip("玩家物体，留空自动找 PlayerMover；还找不到就退化为跟随相机")]
    public Transform target;

    [Tooltip("勾选后相对相机偏移，保证一定在视野里；取消后按玩家头顶偏移")]
    public bool relativeToCamera = true;

    [Tooltip("相对相机：X 右、Y 上、Z 前（米）。Z 越小越贴脸")]
    public Vector3 cameraOffset = new Vector3(0.32f, -0.08f, 0.9f);

    [Tooltip("相对玩家：世界空间偏移")]
    public Vector3 worldOffset = new Vector3(0.25f, 1.55f, 0f);

    [Tooltip("跟随平滑速度，越大越跟手")]
    public float followLerp = 10f;

    [Tooltip("脚本启用时直接贴到位，不从远处飞过来")]
    public bool snapOnEnable = true;

    [Tooltip("当前气泡和目标差超过这个距离就直接瞬移，避免从很远慢慢飘")]
    public float snapDistance = 2.5f;

    [Header("朝向")]
    public Camera viewCamera;

    [Tooltip("只绕 Y 轴转向相机，文字永远保持竖直")]
    public bool uprightOnly = true;

    [Header("显示")]
    public Text label;

    [Tooltip("Say 不传时长时，默认显示多久")]
    public float defaultDuration = 3f;

    public bool debugLog = true;

    private float hideAtUnscaled;
    private bool autoHiding;
    private bool warnedNoTarget;

    void Awake()
    {
        ResolveTargetAndCamera();

        if (label == null) label = GetComponentInChildren<Text>(true);

        if (label == null) Debug.LogWarning("[Mumble] 没找到 Text，请手动拖 Label", this);
    }

    void OnEnable()
    {
        autoHiding = false;

        ResolveTargetAndCamera();
        if (snapOnEnable) SnapNow();

        if (debugLog)
        {
            Debug.Log("[Mumble] 已显示" +
                      " | 目标=" + (target != null ? target.name : "空") +
                      " | 相机=" + (viewCamera != null ? viewCamera.name : "空") +
                      " | 世界位置=" + transform.position +
                      " | 相对相机=" + relativeToCamera, this);
        }
    }

    void LateUpdate()
    {
        if (target == null) ResolveTargetAndCamera();
        // 过场相机（thirdcamre）会一直留在场景里，但它关掉后就不能再作为朝向/偏移的参考
        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            viewCamera = null;
            ResolveViewCamera();
        }
        if (target == null) return;

        // 1. 跟随：差太远直接瞬移，否则平滑靠近
        Vector3 desired = DesiredPosition();

        if ((transform.position - desired).sqrMagnitude > snapDistance * snapDistance)
        {
            transform.position = desired;
        }
        else
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, followLerp) * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, k);
        }

        // 2. 面向相机
        FaceCamera();

        // 3. 自动隐藏
        if (autoHiding && Time.unscaledTime >= hideAtUnscaled)
        {
            autoHiding = false;
            gameObject.SetActive(false);
        }
    }

    // ====================================================================

    /// <summary>显示一句碎碎念；duration &lt; 0 用 defaultDuration，duration = 0 一直显示。</summary>
    public void Say(string text, float duration = -1f)
    {
        if (label == null) label = GetComponentInChildren<Text>(true);
        if (label != null) label.text = text;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        ResolveTargetAndCamera();
        if (snapOnEnable) SnapNow();

        if (debugLog)
            Debug.Log("[Mumble] Say：" + text +
                      " | 目标=" + (target != null ? target.name : "空") +
                      " | 世界位置=" + transform.position, this);

        if (duration < 0f) duration = defaultDuration;

        if (duration > 0f)
        {
            hideAtUnscaled = Time.unscaledTime + duration;
            autoHiding = true;
        }
        else
        {
            autoHiding = false;
        }
    }

    public void Hide()
    {
        autoHiding = false;
        gameObject.SetActive(false);
    }

    // ====================================================================

    private void ResolveTargetAndCamera()
    {
        ResolveViewCamera();

        // 优先用玩家第一人称相机的位置，其次用玩家本体
        if (target == null)
        {
            PlayerMover pm = FindObjectOfType<PlayerMover>();
            if (pm != null)
                target = pm.cameraTransform != null ? pm.cameraTransform : pm.transform;
        }

        // 实在找不到玩家，就跟着相机，保证气泡一直在视野里
        if (target == null && viewCamera != null)
        {
            target = viewCamera.transform;
            if (!warnedNoTarget)
            {
                warnedNoTarget = true;
                Debug.LogWarning("[Mumble] 场景里没找到 PlayerMover，气泡改为跟随相机", this);
            }
        }

        if (target == null && !warnedNoTarget)
        {
            warnedNoTarget = true;
            Debug.LogWarning("[Mumble] 找不到玩家也找不到相机，请手动把玩家拖到 Target", this);
        }
    }

    private Camera ResolveViewCamera()
    {
        // 场景里序列化的可能是过场用的固定相机，它已经关闭时不能用，否则气泡会钉在门口
        if (viewCamera != null && viewCamera.isActiveAndEnabled) return viewCamera;
        viewCamera = null;

        PlayerMover pm = FindObjectOfType<PlayerMover>();
        if (pm != null && pm.cameraTransform != null)
        {
            Camera c = pm.cameraTransform.GetComponent<Camera>();
            if (c != null)
            {
                viewCamera = c;
                return c;
            }
        }

        if (Camera.main != null)
        {
            viewCamera = Camera.main;
            return viewCamera;
        }

        Camera[] all = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled)
            {
                viewCamera = all[i];
                return viewCamera;
            }
        }

        return null;
    }

    private Vector3 DesiredPosition()
    {
        if (target == null) return transform.position;

        // 目标本身就是相机时，强制按相机偏移算
        bool useCamera = relativeToCamera ||
                         (viewCamera != null && target == viewCamera.transform);

        if (useCamera)
        {
            if (viewCamera == null) viewCamera = ResolveViewCamera();
            if (viewCamera == null) return target.position + worldOffset;

            Transform c = viewCamera.transform;
            return c.position
                 + c.right * cameraOffset.x
                 + c.up * cameraOffset.y
                 + c.forward * cameraOffset.z;
        }

        return target.position + worldOffset;
    }

    private void FaceCamera()
    {
        if (viewCamera == null) return;

        Vector3 dir = transform.position - viewCamera.transform.position;
        if (uprightOnly) dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private void SnapNow()
    {
        if (target == null) return;
        if (viewCamera == null) viewCamera = ResolveViewCamera();

        transform.position = DesiredPosition();
        FaceCamera();
    }
}