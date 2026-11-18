using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TaskHUD : MonoBehaviour
{
    [System.Serializable]
    public class FoodEntry
    {
        [Tooltip("必须和食材 IngredientPickup 的 Ingredient Id 完全一致")]
        public string ingredientId;

        [Tooltip("食物项根物体，例如 Food_Apple。它下面（任意层级）要有名为 Check 的物体")]
        public GameObject root;
    }

    private class BoundEntry
    {
        public FoodEntry config;
        public GameObject checkMark;
    }

    [Header("整体")]
    [Tooltip("HUD 根物体，不能是脚本自己所在的物体")]
    public GameObject panel;

    [Header("食物图标")]
    public FoodEntry[] foodEntries;

    [Header("调试")]
    public bool debugLog = true;

    private readonly List<BoundEntry> boundEntries = new List<BoundEntry>();
    private readonly HashSet<string> pickedIds = new HashSet<string>();
    private bool shown = false;

    private void Awake()
    {
        if (panel == null) panel = gameObject;

        BindEntries();

        if (panel == gameObject)
            Debug.LogError("[TaskHUD] Panel 不能和脚本在同一个物体上！", this);
        else
            panel.SetActive(false);

        SetAllMarks(false);
    }

    private void OnEnable() { IngredientPickup.Picked += OnIngredientPicked; }
    private void OnDisable() { IngredientPickup.Picked -= OnIngredientPicked; }

    // ---------------- 对外接口 ----------------

    public void Show()
    {
        shown = true;
        if (panel != null) panel.SetActive(true);

        for (int i = 0; i < boundEntries.Count; i++)
        {
            string id = boundEntries[i].config.ingredientId;
            if (!string.IsNullOrEmpty(id) && IngredientPickup.HasPicked(id))
                pickedIds.Add(id);
        }

        RefreshAll();
    }

    public void Hide()
    {
        shown = false;
        if (panel != null) panel.SetActive(false);
    }

    // ---------------- 绑定 ----------------

    private void BindEntries()
    {
        boundEntries.Clear();
        if (foodEntries == null) return;

        for (int i = 0; i < foodEntries.Length; i++)
        {
            FoodEntry cfg = foodEntries[i];
            if (cfg == null) continue;

            BoundEntry be = new BoundEntry { config = cfg };

            if (string.IsNullOrEmpty(cfg.ingredientId))
                Debug.LogError("[TaskHUD] 第 " + i + " 个条目没填 Ingredient Id。", this);

            if (cfg.root == null)
            {
                Debug.LogError("[TaskHUD] 第 " + i + " 个条目（" + cfg.ingredientId + "）Root 是空的，请把 Food_xxx 拖进去。", this);
            }
            else
            {
                Transform check = FindChildRecursive(cfg.root.transform, "Check");

                if (check == null)
                {
                    Debug.LogError("[TaskHUD] 在 " + GetPath(cfg.root.transform) +
                                   " 下面找不到 Check。它实际包含的物体有：" +
                                   ListAllChildren(cfg.root.transform), cfg.root);
                }
                else
                {
                    be.checkMark = check.gameObject;
                    if (debugLog)
                        Debug.Log("[TaskHUD] " + cfg.root.name + " 绑定成功，Check 路径 = " + GetPath(check), cfg.root);
                }
            }

            boundEntries.Add(be);
        }
    }

    /// <summary>递归查找所有后代（含未激活），忽略大小写和首尾空格</summary>
    private static Transform FindChildRecursive(Transform root, string childName)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == root) continue;

            if (string.Equals(t.name.Trim(), childName, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static string ListAllChildren(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        string s = "";

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == root) continue;
            s += "[" + all[i].name + "] ";
        }

        return s;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // ---------------- 刷新 ----------------

    private void OnIngredientPicked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        pickedIds.Add(id);

        if (debugLog) Debug.Log("[TaskHUD] 收到拾取事件：" + id, this);

        if (shown) RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < boundEntries.Count; i++)
        {
            BoundEntry be = boundEntries[i];
            string id = be.config.ingredientId;
            bool done = !string.IsNullOrEmpty(id) && pickedIds.Contains(id);

            if (be.checkMark != null)
            {
                if (be.checkMark.activeSelf != done)
                    be.checkMark.SetActive(done);

                if (done)
                {
                    Graphic g = be.checkMark.GetComponent<Graphic>();
                    if (g != null) g.enabled = true;

                    be.checkMark.transform.SetAsLastSibling();
                }
            }

            if (debugLog)
            {
                string n = be.config.root != null ? be.config.root.name : "null";
                Debug.Log("[TaskHUD] " + n + "  id=" + id + "  打勾=" + done +
                          "  Check=" + (be.checkMark != null ? "已绑定" : "未绑定"), this);
            }
        }
    }

    private void SetAllMarks(bool value)
    {
        for (int i = 0; i < boundEntries.Count; i++)
        {
            if (boundEntries[i].checkMark != null)
                boundEntries[i].checkMark.SetActive(value);
        }
    }
}