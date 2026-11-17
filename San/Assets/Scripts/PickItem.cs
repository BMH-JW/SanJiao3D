using UnityEngine;

/// <summary>
/// 可拾取食材：
/// - 玩家靠近时显示「按 S 拾取」提示。
/// - 按 S 拾取后，通知 TaskManager 打勾，并销毁自身。
/// 把需要找的 3 个食材在场景里各放一个，设置对应的 ingredientName。
/// </summary>
public class PickupItem : MonoBehaviour
{
    [Header("食材名称（要与 TaskManager.ingredients 里的一致）")]
    public string ingredientName = "鸡蛋";

    [Header("交互")]
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.S;
    public GameObject promptCanvas;   // 世界空间「按 S 拾取」提示

    private Transform player;
    private bool inRange = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        if (promptCanvas != null) promptCanvas.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        inRange = dist < pickupRange;

        if (promptCanvas != null) promptCanvas.SetActive(inRange);

        if (inRange && Input.GetKeyDown(pickupKey))
        {
            if (TaskManager.Instance != null)
                TaskManager.Instance.MarkFound(ingredientName);
            Destroy(gameObject);
        }
    }
}
