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

        [Tooltip("食物项根物体，例如 Food_Apple。下面（任意层级）要有名为 Check 的物体")]
        public GameObject root;
    }

    private class BoundEntry
    {
        public FoodEntry config;
        public GameObject checkMark;
    }

    // ---------------- HUD ----------------
    [Header("HUD")]
    [Tooltip("HUD 根物体（背包图标 + 食物图标），必须是子物体，不能是脚本自己所在的物体")]
    public GameObject panel;

    public FoodEntry[] foodEntries;

    // ---------------- 完成弹窗 ----------------
    [Header("完成弹窗")]
    [Tooltip("完成弹窗根物体，默认关闭")]
    public GameObject completePanel;

    [Tooltip("弹窗文字，可选")]
    public Text completeLabel;

    [Tooltip("确定按钮，可选（脚本会自动绑定 OnClick）")]
    public Button confirmButton;

    // ---------------- 2D 切换 ----------------
    [Header("2D 关卡（同场景 Overlay Canvas 切换）")]
    [Tooltip("2D 面板的根物体，例如 Canvas 下的 Panel2D。运行前手动关掉")]
    public GameObject level2DPanel;

    [Tooltip("3D 部分要关掉的顶层物体，例如 第一场景、Player、door (2)、dingzi (2)")]
    public GameObject[] deactivateOn2D;

    // ---------------- 行为 ----------------
    [Header("行为")]
    [Tooltip("完成弹窗出现时暂停时间，切 2D 时恢复")]
    public bool pauseOnComplete = true;

    [Tooltip("切到 2D 时关闭玩家控制并解锁鼠标")]
    public bool disablePlayerControl = true;

    [Tooltip("打印调试日志")]
    public bool debugLog = true;

    // ---------------- 内部 ----------------
    private readonly List<BoundEntry> boundEntries = new List<BoundEntry>();
    private readonly HashSet<string> pickedIds = new HashSet<string>();
    private bool shown = false;
    private bool completed = false;

    // ========================================================================

    private void Awake()
    {
        // 清掉上次运行遗留的暂停状态，避免一开始就卡住
        Time.timeScale = 1f;

        // Panel 指向自己时自动找子物体
        if (panel == null || panel == gameObject)
        {
            Transform found = transform.Find("TaskHUDPanel");
            if (found != null) panel = found.gameObject;
        }

        if (panel == null || panel == gameObject)
        {
            Debug.LogWarning("[TaskHUD] Panel 指向了脚本自己，HUD 不会自动隐藏。请把 Panel 拖到子物体 TaskHUDPanel。", this);
        }
        else
        {
            panel.SetActive(false);
        }

        BindEntries();

        if (completePanel != null) completePanel.SetActive(false);
        if (level2DPanel != null) level2DPanel.SetActive(false);

        // 自动绑定确定按钮
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        else
        {
            Transform autoBtn = FindChildRecursive(transform, "Confirm");
            if (autoBtn == null) autoBtn = FindChildRecursive(transform, "确定");
            if (autoBtn != null)
            {
                confirmButton = autoBtn.GetComponent<Button>();
                if (confirmButton != null)
                {
                    confirmButton.onClick.RemoveListener(OnConfirmClicked);
                    confirmButton.onClick.AddListener(OnConfirmClicked);
                    if (debugLog) Debug.Log("[TaskHUD] 自动绑定确定按钮：" + autoBtn.name);
                }
            }
        }

        SetAllMarks(false);
    }

    private void OnEnable() { IngredientPickup.Picked += OnIngredientPicked; }
    private void OnDisable() { IngredientPickup.Picked -= OnIngredientPicked; }

    // ========================================================================
    // 对外接口
    // ========================================================================

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
        CheckCompletion();
    }

    public void Hide()
    {
        shown = false;
        if (panel != null) panel.SetActive(false);
    }

    // ========================================================================
    // 事件
    // ========================================================================

    private void OnIngredientPicked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        pickedIds.Add(id);

        if (debugLog) Debug.Log("[TaskHUD] 拾取：" + id, this);

        if (shown)
        {
            RefreshAll();
            CheckCompletion();
        }
    }

    private void OnConfirmClicked()
    {
        if (pauseOnComplete) Time.timeScale = 1f;

        Debug.Log("========== [TaskHUD] 切换到 2D ==========", this);

        // 1. 关弹窗和 HUD
        if (completePanel != null) completePanel.SetActive(false);
        if (panel != null) panel.SetActive(false);
        Debug.Log("[TaskHUD] 弹窗 & HUD 已关");

        // 2. 关 3D 顶层物体
        if (deactivateOn2D != null)
        {
            for (int i = 0; i < deactivateOn2D.Length; i++)
            {
                GameObject go = deactivateOn2D[i];
                if (go != null)
                {
                    go.SetActive(false);
                    Debug.Log("[TaskHUD] 关闭 3D：" + go.name);
                }
            }
        }

        // 3. 打开 2D 面板
        if (level2DPanel != null)
        {
            Transform t = level2DPanel.transform;
            while (t != null && t.parent != null)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t = t.parent;
            }

            level2DPanel.SetActive(true);
            Debug.Log("[TaskHUD] 打开 2D 面板：" + level2DPanel.name);
        }
        else
        {
            Debug.LogError("[TaskHUD] level2DPanel 没拖！请把 Canvas 下的 2D 面板根物体拖进来", this);
        }

        // 4. 关玩家控制 + 解锁鼠标
        if (disablePlayerControl)
        {
            PlayerMover pm = FindObjectOfType<PlayerMover>();
            if (pm != null)
            {
                pm.SetControl(false, false);
                pm.LockCursor(false);
                Debug.Log("[TaskHUD] 玩家控制已关");
            }
        }

        Debug.Log("========== [TaskHUD] 完成 ==========");
    }

    // ========================================================================
    // 绑定 + 刷新
    // ========================================================================

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
                Debug.LogError("[TaskHUD] 第 " + i + " 个条目（" + cfg.ingredientId + "）Root 是空的。", this);
            }
            else
            {
                Transform check = FindChildRecursive(cfg.root.transform, "Check");
                if (check == null)
                {
                    Debug.LogError("[TaskHUD] " + cfg.root.name + " 下找不到 Check，实际：" + ListAllChildren(cfg.root.transform), cfg.root);
                }
                else
                {
                    be.checkMark = check.gameObject;
                    if (debugLog) Debug.Log("[TaskHUD] " + cfg.root.name + " 绑定 Check 成功", cfg.root);
                }
            }

            boundEntries.Add(be);
        }
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
                if (be.checkMark.activeSelf != done) be.checkMark.SetActive(done);

                if (done)
                {
                    Graphic g = be.checkMark.GetComponent<Graphic>();
                    if (g != null) g.enabled = true;
                    be.checkMark.transform.SetAsLastSibling();
                }
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

    // ========================================================================
    // 完成检测
    // ========================================================================

    private void CheckCompletion()
    {
        if (completed) return;
        if (boundEntries.Count == 0) return;

        int need = 0, got = 0;
        for (int i = 0; i < boundEntries.Count; i++)
        {
            string id = boundEntries[i].config.ingredientId;
            if (string.IsNullOrEmpty(id)) continue;
            need++;
            if (pickedIds.Contains(id)) got++;
        }

        if (debugLog) Debug.Log("[TaskHUD] 进度 " + got + "/" + need);

        if (need > 0 && got >= need)
        {
            completed = true;
            ShowCompletePanel();
        }
    }

    private void ShowCompletePanel()
    {
        Debug.Log("[TaskHUD] 收集完成，弹出完成弹窗", this);

        if (completePanel != null)
        {
            completePanel.SetActive(true);

            Transform t = completePanel.transform;
            while (t != null && t.parent != null)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t = t.parent;
            }
        }
        else
        {
            Debug.LogError("[TaskHUD] completePanel 没拖！", this);
        }

        if (pauseOnComplete) Time.timeScale = 0f;

        if (completeLabel != null && string.IsNullOrEmpty(completeLabel.text))
            completeLabel.text = "食材收集完成！";
    }

    // ========================================================================
    // 工具
    // ========================================================================

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
}