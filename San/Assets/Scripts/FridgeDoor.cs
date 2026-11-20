using UnityEngine;

public class FridgeDoor : MonoBehaviour
{
    [Header("门体")]
    public Transform doorPivot;      // 会旋转的门板/把手所在的父节点
    public float openAngle = -90f;   // 开门角度
    public float openSpeed = 120f;   // 每秒旋转度数

    [Header("铰链（旋转轴）")]
    [Tooltip("冰箱门默认绕右边缘旋转；需要时可改成 Left / Center / Custom")]
    public HingeSide hingeSide = HingeSide.Right;
    [Tooltip("自定义轴心（门的本地坐标），只有 Hinge Side = Custom 时生效")]
    public Vector3 hingeOffset = Vector3.zero;

    [Header("状态")]
    private bool isOpen = false;
    private float currentAngle;

    private Vector3 baseWorldPos;
    private Quaternion baseWorldRot;
    private Vector3 hingeWorld;

    void Start()
    {
        // 冲突检查：门板所在的父级已经有 DoorInteraction 在旋转同一个门，
        // 两个脚本会同时写 Transform，表现为旋转轴乱跳 / 看起来没绕边转
        if (HasDrivingDoorInteraction())
        {
            Debug.LogWarning("[FridgeDoor] " + name +
                " 的父级已有 DoorInteraction 在控制这块门板，已自动停用 FridgeDoor，避免重复旋转", this);
            enabled = false;
            return;
        }

        if (doorPivot == null) doorPivot = transform;

        baseWorldPos = doorPivot.position;
        baseWorldRot = doorPivot.rotation;

        Vector3 localHinge = HingeUtility.ResolveLocal(doorPivot, hingeSide, hingeOffset);
        hingeWorld = doorPivot.TransformPoint(localHinge);
    }

    private bool HasDrivingDoorInteraction()
    {
        DoorInteraction[] doors = GetComponentsInParent<DoorInteraction>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            DoorInteraction d = doors[i];
            if (d == null || !d.isActiveAndEnabled || d.doorPivot == null) continue;
            if (transform.IsChildOf(d.doorPivot)) return true;
        }
        return false;
    }

    public void Toggle()
    {
        isOpen = !isOpen;
    }

    void Update()
    {
        float target = isOpen ? openAngle : 0f;
        currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * Time.deltaTime);
        HingeUtility.Apply(doorPivot, hingeWorld, baseWorldPos, baseWorldRot, currentAngle);
    }
}
