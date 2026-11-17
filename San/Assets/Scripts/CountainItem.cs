using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 可旋转打开的容器（抽屉、冰箱门、柜门等统一处理）：
/// - 玩家靠近时显示「按 E 打开」。
/// - 打开后提示变成「按 E 关闭」，只能关闭。
/// - 所有容器统一用 90° 旋转方式打开。
///
/// 注意：要让旋转看起来正确，movingPart（门板）的 Pivot 必须放在门的侧边边缘，
/// 这样旋转时才会像真实门一样绕侧边打开，而不是绕中心转。
/// </summary>
public class ContainerInteraction : MonoBehaviour
{
    [Header("运动参数")]
    [Tooltip("真正旋转的门板 Pivot（放在门的侧边边缘）")]
    public Transform movingPart;
    [Tooltip("打开角度，统一 90°")]
    public float openAngle = 90f;
    [Tooltip("动画速度")]
    public float animSpeed = 3f;

    [Header("交互")]
    public float interactRange = 2.5f;
    [Tooltip("世界空间提示 Canvas（含 Text）")]
    public GameObject promptCanvas;
    public Text promptText;

    private bool isOpen = false;
    private float closedYRot;
    private Transform player;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (promptCanvas != null) promptCanvas.SetActive(false);

        if (movingPart != null)
            closedYRot = movingPart.localEulerAngles.y;
        else
            Debug.LogWarning("[ContainerInteraction] " + gameObject.name + " 的 movingPart 未设置！");
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
        StartCoroutine(AnimateRotate(isOpen ? closedYRot + openAngle : closedYRot));
    }

    IEnumerator AnimateRotate(float targetY)
    {
        float start = movingPart.localEulerAngles.y;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animSpeed;
            movingPart.localRotation = Quaternion.Euler(0f, Mathf.Lerp(start, targetY, t), 0f);
            yield return null;
        }
        movingPart.localRotation = Quaternion.Euler(0f, targetY, 0f);
    }
}
