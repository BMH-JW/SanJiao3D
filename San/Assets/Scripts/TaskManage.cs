using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务管理器：记录需要寻找的食材清单，
/// 每拾取一个就在清单上打勾，全部找齐后通知 GameSequenceManager 弹出「下一步」。
/// </summary>
public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    [Header("食材清单（可在 Inspector 增删改名）")]
    public List<string> ingredients = new List<string> { "鸡蛋", "番茄", "洋葱" };

    private Dictionary<string, bool> found = new Dictionary<string, bool>();
    private Dictionary<string, Text> rowTexts = new Dictionary<string, Text>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>剧情切回第一人称后调用，生成清单 UI 行。</summary>
    public void StartTask()
    {
        found.Clear();
        rowTexts.Clear();

        foreach (var name in ingredients)
        {
            found[name] = false;
            CreateIngredientRow(name);
        }
    }

    void CreateIngredientRow(string name)
    {
        if (UIManager.Instance == null ||
            UIManager.Instance.ingredientListContainer == null ||
            UIManager.Instance.ingredientItemPrefab == null) return;

        GameObject row = Instantiate(
            UIManager.Instance.ingredientItemPrefab,
            UIManager.Instance.ingredientListContainer);

        Text t = row.GetComponentInChildren<Text>();
        if (t != null) t.text = name + "   [ ]";
        rowTexts[name] = t;
    }

    /// <summary>PickupItem 拾取时调用。</summary>
    public void MarkFound(string ingredientName)
    {
        if (!found.ContainsKey(ingredientName)) return;
        if (found[ingredientName]) return;

        found[ingredientName] = true;

        if (rowTexts.ContainsKey(ingredientName) && rowTexts[ingredientName] != null)
            rowTexts[ingredientName].text = ingredientName + "   [√]";

        CheckAllFound();
    }

    void CheckAllFound()
    {
        foreach (var v in found.Values)
            if (!v) return;

        if (GameSequenceManager.Instance != null)
            GameSequenceManager.Instance.OnAllIngredientsFound();
    }
}
