using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 可旋转打开的容器（抽屉、冰箱门、柜门等统一处理）：
/// - 玩家靠近时显示「按 E 打开」。
/// - 打开后提示变成「按 E 关闭」，只能关闭。
/// - 所有容器统一用 90° 旋转方式打开。
///
/// 注意：movingPart（门板）默认绕左边缘旋转，不再依赖 Pivot 是否摆在边沿；
/// 如果某扇门要绕右边转，把 Hinge Side 改成 Right 即可。
/// </summary>
public class ContainerInteraction : MonoBehaviour
{
    [Header("运动参数")]
    [Tooltip("真正旋转的门板，默认绕它渲染范围的左边缘打开")]
    public Transform movingPart;
    [Tooltip("打开角度，统一 90°")]
    public float openAngle = 90f;
    [Tooltip("动画速度")]
    public float animSpeed = 3f;

    [Header("铰链（旋转轴）")]
    [Tooltip("柜门默认是左边缘；需要时可改 Right / Center / Custom")]
    public HingeSide hingeSide = HingeSide.Left;
    [Tooltip("自定义轴心（门板的本地坐标），只有 Hinge Side = Custom 时生效")]
    public Vector3 hingeOffset = Vector3.zero;

    [Header("交互")]
    public float interactRange = 2.5f;
    [Tooltip("世界空间提示 Canvas（含 Text）")]
    public GameObject promptCanvas;
    public Text promptText;

    private bool isOpen = false;
    private Transform player;

    private Vector3 baseWorldPos;
    private Quaternion baseWorldRot;
    private Vector3 hingeWorld;
    private float currentAngle;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (promptCanvas != null) promptCanvas.SetActive(false);

        if (movingPart != null)
        {
            baseWorldPos = movingPart.position;
            baseWorldRot = movingPart.rotation;

            Vector3 localHinge = HingeUtility.ResolveLocal(movingPart, hingeSide, hingeOffset);
            hingeWorld = movingPart.TransformPoint(localHinge);
        }
        else
        {
            Debug.LogWarning("[ContainerInteraction] " + gameObject.name + " 的 movingPart 未设置！");
        }
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist < interactRange;

        if (promptCanvas != null)
        {
            promptCanvas.SetActive(inRange);
            if (promptText != null)
                promptText.text = isOpen ? "按 E 关闭" : "按 E 打开";
        }

        if (inRange && Input.GetKeyDown(KeyCode.E))
        {
            Toggle();
        }
    }

    void Toggle()
    {
        if (movingPart == null) return;

        isOpen = !isOpen;
        StopAllCoroutines();
        StartCoroutine(AnimateRotate(isOpen ? openAngle : 0f));
    }

    IEnumerator AnimateRotate(float targetAngle)
    {
        float start = currentAngle;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animSpeed;
            currentAngle = Mathf.Lerp(start, targetAngle, t);
            HingeUtility.Apply(movingPart, hingeWorld, baseWorldPos, baseWorldRot, currentAngle);
            yield return null;
        }

        currentAngle = targetAngle;
        HingeUtility.Apply(movingPart, hingeWorld, baseWorldPos, baseWorldRot, currentAngle);
    }
}
