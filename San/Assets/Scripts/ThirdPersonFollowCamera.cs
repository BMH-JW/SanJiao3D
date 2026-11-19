using UnityEngine;

/// <summary>
/// 第三人称跟随相机：保持进入时相机与玩家的相对位置，玩家移动时相机跟着走。
/// 由 TaskHUD 返回 3D 时自动添加并初始化，也可以手动挂到 thirdcamera1 上。
/// </summary>
public class ThirdPersonFollowCamera : MonoBehaviour
{
    [Tooltip("跟随目标，一般是玩家根物体")]
    public Transform target;

    [Tooltip("是否跟随玩家朝向旋转（勾上后始终保持在玩家的同一侧）")]
    public bool followYaw = true;

    [Tooltip("跟随平滑速度，越大越紧跟；0 表示完全硬跟随")]
    public float positionLerp = 10f;

    [Header("固定偏移（推荐）")]
    [Tooltip("勾选后不再使用相机摆放的位置，而是按下面这个相对玩家的偏移定位")]
    public bool useCustomOffset = false;
    [Tooltip("相对玩家的偏移（玩家本地坐标，z 负值表示身后）")]
    public Vector3 customOffset = new Vector3(0f, 1.7f, -2.2f);
    [Tooltip("是否始终看向玩家")]
    public bool lookAtTarget = true;
    [Tooltip("看向玩家的高度")]
    public float lookHeight = 1.2f;

    [Header("防穿墙")]
    [Tooltip("开启后相机会被墙体挡住，避免跑到门外或墙后")]
    public bool avoidWalls = true;
    [Tooltip("参与遮挡检测的层，默认全部")]
    public LayerMask wallMask = ~0;
    [Tooltip("相机与墙之间保留的距离")]
    public float wallPadding = 0.15f;
    [Tooltip("检测用的小球半径，越大越不容易卡进墙角")]
    public float probeRadius = 0.2f;

    private Vector3 localOffset;
    private bool initialized;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
        CaptureOffset();
        ApplyImmediately();
    }

    /// <summary>立即把相机放到目标位置，不做平滑（用于刚切换时避免从旧机位飞过去）</summary>
    public void ApplyImmediately()
    {
        if (target == null || !initialized) return;

        Vector3 offset = followYaw
            ? Quaternion.Euler(0f, target.eulerAngles.y, 0f) * localOffset
            : localOffset;

        transform.position = ClampByWalls(target.position + offset);
        LookAtTargetNow();
    }

    /// <summary>若相机与玩家之间被墙挡住，就把相机拉到墙内侧（防止跑到门外/墙后）</summary>
    private Vector3 ClampByWalls(Vector3 desired)
    {
        if (!avoidWalls || target == null) return desired;

        Vector3 pivot = target.position + Vector3.up * lookHeight;
        Vector3 dir = desired - pivot;
        float dist = dir.magnitude;
        if (dist < 0.01f) return desired;
        dir /= dist;

        int count = Physics.SphereCastNonAlloc(
            pivot, probeRadius, dir, hitBuffer, dist, wallMask, QueryTriggerInteraction.Ignore);

        float nearest = dist;
        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null) continue;
            // 忽略玩家自身的碰撞体
            if (col.transform == target || col.transform.IsChildOf(target)) continue;
            if (hitBuffer[i].distance < nearest) nearest = hitBuffer[i].distance;
        }

        if (nearest >= dist) return desired;

        float safe = Mathf.Max(0.1f, nearest - wallPadding);
        return pivot + dir * safe;
    }

    private void LookAtTargetNow()
    {
        if (!lookAtTarget || target == null) return;

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Vector3 dir = lookPoint - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    /// <summary>记录当前相机相对玩家的位置（只取水平朝向，避免俯仰影响高度）</summary>
    public void CaptureOffset()
    {
        if (target == null) return;

        if (useCustomOffset)
        {
            localOffset = customOffset;
            initialized = true;
            return;
        }

        Vector3 offset = transform.position - target.position;
        if (followYaw)
        {
            Quaternion yawRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            localOffset = Quaternion.Inverse(yawRot) * offset;
        }
        else
        {
            localOffset = offset;
        }
        initialized = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (!initialized) CaptureOffset();

        Vector3 offset = followYaw
            ? Quaternion.Euler(0f, target.eulerAngles.y, 0f) * localOffset
            : localOffset;

        Vector3 desired = ClampByWalls(target.position + offset);

        if (positionLerp > 0f)
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * positionLerp);
        else
            transform.position = desired;

        if (lookAtTarget)
            LookAtTargetNow();
    }
}
