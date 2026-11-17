using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;   // Player 角色
    public Vector3 offset = new Vector3(0f, 2.5f, -2f);

    public float followSpeed = 15f;   // 跟随越大=越紧

    void LateUpdate()
    {
        if (target == null) return;

        // 不再更改旋转，只同步位置
        transform.position = Vector3.Lerp(
            transform.position,
            target.position + target.TransformDirection(offset),
            followSpeed * Time.deltaTime
        );

        transform.LookAt(target);   // 保持注视 Player
    }
}
