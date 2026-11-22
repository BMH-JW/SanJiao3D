using UnityEngine;

/// <summary>
/// 客厅可点击拾取的物品（杯子、靠枕、遥控器等）。
/// 第一次点击：物品移到视角中央，并一直跟随视角；
/// 再点击一次：物品平滑回到点击前的位置和旋转。
/// 点击检测由 HandleClick 统一发射线处理，本脚本不需要自己读鼠标。
/// </summary>
[DisallowMultipleComponent]
public class PickupItem : MonoBehaviour
{
    [Header("拾取位置")]
    [Tooltip("物品停在相机正前方多远（米）；靠枕这类大物体建议 0.8~1.0")]
    public float focusDistance = 0.8f;

    [Tooltip("相对视线中心的下移量（米），避免完全挡住准星")]
    public float focusVerticalOffset = -0.03f;

    [Header("动画")]
    [Tooltip("移动到视角中央的速度")]
    public float moveSpeed = 14f;

    [Tooltip("放回原位时的速度")]
    public float returnSpeed = 12f;

    [Tooltip("拾取时是否跟着相机转，让物品正对镜头")]
    public bool faceCamera = false;

    [Tooltip("需要额外调整朝向时填这里（例如模型正面朝 -Z 就填 0,180,0）")]
    public Vector3 heldRotationEuler = Vector3.zero;

    [Header("物理")]
    [Tooltip("物品上有 Rigidbody 时，拾取期间临时设为 Kinematic，放回后恢复")]
    public bool handleRigidbody = true;

    [Header("碰撞体")]
    [Tooltip("没有碰撞体时按模型包围盒自动补一个，保证能被点到")]
    public bool autoAddCollider = true;

    [Header("调试")]
    public bool debugLog = true;

    public bool IsHeld { get; private set; }

    // 全局只允许拿一件，拿新的会自动把旧的放回
    private static PickupItem currentHeld;

    // ★ 关卡解锁开关：默认关闭，进入客厅关卡时由 LivingRoomQuest 调用 SetPickupEnabled(true) 开启
    public static bool PickupEnabled { get; private set; } = false;

    private Transform originalParent;
    private Vector3 originalWorldPos;
    private Quaternion originalWorldRot;
    private Rigidbody rb;
    private bool rigidbodyWasKinematic;
    private Camera cam;
    private bool returning;

    private void Awake()
    {
        if (autoAddCollider) EnsureCollider();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>开启/关闭全局拾取。关掉时会把手里的物品放下。</summary>
    public static void SetPickupEnabled(bool value)
    {
        PickupEnabled = value;

        if (!value && currentHeld != null)
            currentHeld.PutBack();
    }

    /// <summary>点击空地时由 HandleClick 调用：放下手里的物品。</summary>
    public static void PutBackHeld()
    {
        if (currentHeld != null)
            currentHeld.PutBack();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetPickupGate()
    {
        PickupEnabled = false;
        currentHeld = null;
    }

    /// <summary>由 HandleClick 射线命中时调用。</summary>
    public void TogglePickup(Camera camera)
    {
        // 关卡限制：没进入客厅关卡之前，点了也不会拿
        if (!PickupEnabled)
        {
            if (debugLog)
                Debug.Log("[PickupItem] 当前关卡还不能拾取物品", this);
            return;
        }

        // 正在飞回原位时忽略点击，避免来回抽搐
        if (returning) return;

        // 手里已经有别的物品：
        // 点别的物品不再"换拿"，而是把手里的这个放下
        if (currentHeld != null && currentHeld != this)
        {
            currentHeld.PutBack();
            return;
        }

        Camera resolved = camera != null && camera.isActiveAndEnabled
            ? camera
            : ResolveActiveCamera();

        if (resolved == null) return;

        // 只允许玩家第一人称相机拾取，避免 2D 小游戏里点击时误拾取 3D 物品
        PlayerMover mover = FindObjectOfType<PlayerMover>();
        if (mover != null && !resolved.transform.IsChildOf(mover.transform))
            return;

        cam = resolved;

        if (!IsHeld)
            PickUp();
        else
            PutBack();
    }

    private void LateUpdate()
    {
        if (!IsHeld) return;

        if (returning)
        {
            ReturnToOrigin();
            return;
        }

        if (cam == null || !cam.isActiveAndEnabled)
            cam = ResolveActiveCamera();

        if (cam == null) return;

        Vector3 targetPos = cam.transform.position
            + cam.transform.forward * focusDistance
            + cam.transform.up * focusVerticalOffset;

        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);

        if (faceCamera)
        {
            Quaternion targetRot =
                cam.transform.rotation * Quaternion.Euler(heldRotationEuler);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
    }

    private void PickUp()
    {
        // 已经拿着别的物品时，先把旧的放回去（正常不会走到这里，入口已处理）
        if (currentHeld != null && currentHeld != this)
            currentHeld.PutBack();

        originalParent = transform.parent;
        originalWorldPos = transform.position;
        originalWorldRot = transform.rotation;

        if (handleRigidbody && rb != null)
        {
            rigidbodyWasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        // 脱离原父级，避免家具移动时把物品一起带走
        transform.SetParent(null, true);

        IsHeld = true;
        returning = false;
        currentHeld = this;

        if (debugLog)
            Debug.Log("[PickupItem] 拾取：" + name, this);
    }

    /// <summary>放回原位；也可以由外部调用。</summary>
    public void PutBack()
    {
        if (!IsHeld || returning) return;

        returning = true;
        if (currentHeld == this) currentHeld = null;

        if (debugLog)
            Debug.Log("[PickupItem] 放回原位：" + name, this);
    }

    private void ReturnToOrigin()
    {
        float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, originalWorldPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, originalWorldRot, t);

        // 还没到就继续飞
        if ((transform.position - originalWorldPos).sqrMagnitude > 0.0004f)
            return;

        transform.SetParent(originalParent, true);
        transform.position = originalWorldPos;
        transform.rotation = originalWorldRot;

        if (handleRigidbody && rb != null)
            rb.isKinematic = rigidbodyWasKinematic;

        returning = false;
        IsHeld = false;

        if (debugLog)
            Debug.Log("[PickupItem] 已放回：" + name, this);
    }

    private static Camera ResolveActiveCamera()
    {
        PlayerMover mover = FindObjectOfType<PlayerMover>();
        if (mover != null)
        {
            foreach (Camera c in mover.GetComponentsInChildren<Camera>(true))
                if (c.isActiveAndEnabled) return c;
        }

        if (Camera.main != null) return Camera.main;

        foreach (Camera c in Camera.allCameras)
            if (c.isActiveAndEnabled) return c;

        return null;
    }

    private void EnsureCollider()
    {
        // 只认启用中的碰撞体：有些导入模型自带的碰撞体是禁用状态，射线打不到
        Collider[] existing = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < existing.Length; i++)
            if (existing[i].enabled) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            if (debugLog)
                Debug.LogWarning("[PickupItem] " + name +
                    " 没有启用中的碰撞体，也没有 Renderer，无法被点击。请把组件挂到有模型的物体上，或手动加一个 Collider。", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);

        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        if (debugLog)
            Debug.Log("[PickupItem] 已为 " + name + " 自动补点击碰撞体", this);
    }

    /// <summary>模型在世界空间的包围盒，用于判断点击落在哪个物品上。</summary>
    public bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool has = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer) continue;

            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return has;
    }

    /// <summary>
    /// 射线命中后找对应的 PickupItem。
    /// 先找命中物体自身/父级；如果命中的是外层整体碰撞体（沙发、桌子、导入模型的合并碰撞体），
    /// 再逐级往上，找包围盒离点击点最近、且在容差范围内的物品。
    /// </summary>
    public static PickupItem FindAtClick(RaycastHit hit, float tolerance)
    {
        PickupItem direct = hit.collider.GetComponentInParent<PickupItem>();
        if (direct != null) return direct;

        Transform t = hit.collider.transform;
        float bestSqr = tolerance * tolerance;

        while (t != null)
        {
            PickupItem best = null;
            PickupItem[] items = t.GetComponentsInChildren<PickupItem>(true);

            for (int i = 0; i < items.Length; i++)
            {
                PickupItem item = items[i];
                if (item == null || !item.isActiveAndEnabled) continue;
                if (!item.TryGetWorldBounds(out Bounds bounds)) continue;

                float d = bounds.SqrDistance(hit.point);
                if (d <= bestSqr)
                {
                    bestSqr = d;
                    best = item;
                }
            }

            if (best != null) return best;
            t = t.parent;
        }

        return null;
    }
}