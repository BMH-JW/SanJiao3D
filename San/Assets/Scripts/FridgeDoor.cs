using UnityEngine;

public class FridgeDoor : MonoBehaviour
{
    [Header("门体")]
    public Transform doorPivot;      // 会旋转的门板/把手所在的父节点
    public float openAngle = -90f;   // 开门角度（跟你截图里 Door 的 -90 一致）
    public float openSpeed = 120f;   // 每秒旋转度数

    [Header("状态")]
    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion openRot;

    void Start()
    {
        if (doorPivot == null) doorPivot = transform;
        closedRot = doorPivot.localRotation;
        openRot = Quaternion.Euler(
            closedRot.eulerAngles.x,
            closedRot.eulerAngles.y + openAngle,
            closedRot.eulerAngles.z);
    }

    public void Toggle()
    {
        isOpen = !isOpen;
    }

    void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;
        doorPivot.localRotation = Quaternion.RotateTowards(
            doorPivot.localRotation, target, openSpeed * Time.deltaTime);
    }
}

