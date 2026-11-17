using UnityEngine;

/// <summary>
/// 在第一人称摄像机与第三人称摄像机之间切换。
/// 剧情进入房间时切到第三人称，任务弹窗结束后切回第一人称。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("摄像机引用")]
    public Camera firstPersonCamera;   // 玩家子物体上的 FP 摄像机
    public Camera thirdPersonCamera;   // 场景里的第三人称摄像机

    void Start()
    {
        SetFirstPerson();
    }

    public void SetFirstPerson()
    {
        if (firstPersonCamera != null) firstPersonCamera.gameObject.SetActive(true);
        if (thirdPersonCamera != null) thirdPersonCamera.gameObject.SetActive(false);
    }

    public void SetThirdPerson()
    {
        if (firstPersonCamera != null) firstPersonCamera.gameObject.SetActive(false);
        if (thirdPersonCamera != null) thirdPersonCamera.gameObject.SetActive(true);
    }
}