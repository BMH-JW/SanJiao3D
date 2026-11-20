using UnityEngine;

/// <summary>
/// 极简兼容版：只保留 TaskManage / UIManage 调用的两个方法。
/// 不用挂到任何物体上，第一次被访问时自动创建。
/// </summary>
public class GameSequenceManager : MonoBehaviour
{
    private static GameSequenceManager instance;

    public static GameSequenceManager Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<GameSequenceManager>();
            if (instance == null)
            {
                GameObject go = new GameObject("GameSequenceManager (Auto)");
                instance = go.AddComponent<GameSequenceManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void OnAllIngredientsFound()
    {
        Debug.Log("[Sequence] 所有食材找齐");
    }

    public void OnNextStepClicked()
    {
        Debug.Log("[Sequence] 点击下一步");
    }
}