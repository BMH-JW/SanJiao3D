﻿﻿﻿﻿using System.Collections.Generic;
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

    // ---------------- 返回 3D ----------------
    [Header("返回 3D 房间")]
    [Tooltip("【把你想切回去的那台摄像机拖到这里】返回 3D 后只会启用这台，其它相机全部关掉。留空则用玩家身上的第一人称相机")]
    public Camera cameraOnReturn;

    [Tooltip("返回后是否恢复玩家的移动和转视角；关闭时保持冻结，只展示第一人称画面")]
    public bool restorePlayerControlOnReturn = true;

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
    private readonly List<GameObject> foodRoots = new List<GameObject>();
    private GameObject foodPanel;         // 食材图标所在的独立面板
    private bool shown = false;
    private bool completed = false;

    // 进入 2D 前的玩家位置，返回时原地放回
    private Vector3 savedPlayerPosition;
    private float savedPlayerYaw;
    private bool hasSavedPlayerPose = false;

    // ========================================================================

    private void Awake()
    {
        // 场景里残留了一个禁用的 TaskHUD，跳过它，避免重复报错
        if (!enabled) return;

        // 清掉上次运行遗留的暂停状态，避免一开始就卡住
        Time.timeScale = 1f;

        // Panel 指向自己或为空时，自动在子物体里找 TaskHUDPanel（递归，不限层级）
        if (panel == null || panel == gameObject)
        {
            Transform found = FindChildRecursive(transform, "TaskHUDPanel");
            if (found != null) panel = found.gameObject;
        }

        // 还找不到就在整个场景里按名字找，避免 HUD 一开始就露出来
        if (panel == null || panel == gameObject)
            panel = FindPanelInScene("TaskHUDPanel");

        // 最后兜底：用食材图标的父物体当面板
        if ((panel == null || panel == gameObject) && foodEntries != null)
        {
            for (int i = 0; i < foodEntries.Length; i++)
            {
                FoodEntry e = foodEntries[i];
                if (e == null || e.root == null || e.root.transform.parent == null) continue;
                panel = e.root.transform.parent.gameObject;
                break;
            }
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

        // 食材图标可能不在 panel 下，而是放在另一个面板（TaskHUDPanel (1)）里，
        // 这里单独把它也关掉，避免一进场景就提前显示
        foodPanel = ResolveFoodPanel();
        if (foodPanel != null && foodPanel != panel)
            foodPanel.SetActive(false);

        // 三个图标可能分散在不同父物体下，逐个也关掉一次
        foodRoots.Clear();
        if (foodEntries != null)
        {
            for (int i = 0; i < foodEntries.Length; i++)
            {
                FoodEntry e = foodEntries[i];
                if (e == null) continue;

                // 还找不到 Root 的，在全场景 UI 里按名字 + 是否有 Check 来找
                if (e.root == null && !string.IsNullOrEmpty(e.ingredientId))
                    e.root = FindUICheckRoot(e.ingredientId);

                if (e.root == null) continue;
                foodRoots.Add(e.root);
                e.root.SetActive(false);
            }
        }

        // TaskIcon 和三个食材图标同步显示/隐藏
        GameObject taskIcon = FindUIObject("TaskIcon");
        if (taskIcon != null && !foodRoots.Contains(taskIcon))
        {
            foodRoots.Add(taskIcon);
            taskIcon.SetActive(false);
        }

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
        if (foodPanel != null) foodPanel.SetActive(true);

        for (int i = 0; i < foodRoots.Count; i++)
            if (foodRoots[i] != null) foodRoots[i].SetActive(true);

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
        if (foodPanel != null) foodPanel.SetActive(false);

        for (int i = 0; i < foodRoots.Count; i++)
            if (foodRoots[i] != null) foodRoots[i].SetActive(false);

        // 对勾可能不在图标下面，单独关一次，保证切 2D 后不残留
        SetAllMarks(false);
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
        if (foodPanel != null) foodPanel.SetActive(false);
        for (int i = 0; i < foodRoots.Count; i++)
            if (foodRoots[i] != null) foodRoots[i].SetActive(false);

        // 对勾可能不在图标下面，单独关一次
        SetAllMarks(false);
        Debug.Log("[TaskHUD] 弹窗 & HUD 已关");

        // 1.5 记录玩家当前位置，2D 结束后原地恢复
        PlayerMover savePlayer = FindObjectOfType<PlayerMover>();
        if (savePlayer != null)
        {
            savedPlayerPosition = savePlayer.transform.position;
            savedPlayerYaw = savePlayer.transform.eulerAngles.y;
            hasSavedPlayerPose = true;
            Debug.Log("[TaskHUD] 记录玩家位置：" + savedPlayerPosition);
        }

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

    /// <summary>2D 小游戏结束后调用：恢复 3D 物体、切回指定摄像机、关闭 2D 面板。</summary>
    public void ReturnTo3D()
    {
        Debug.Log("========== [TaskHUD] 从 2D 返回 3D ==========", this);

        // 防止 2D 里残留的暂停状态把 3D 卡住
        Time.timeScale = 1f;

        // 1. 恢复之前关掉的 3D 顶层物体
        if (deactivateOn2D != null)
        {
            for (int i = 0; i < deactivateOn2D.Length; i++)
            {
                GameObject go = deactivateOn2D[i];
                if (go == null) continue;
                go.SetActive(true);
                Debug.Log("[TaskHUD] 恢复 3D：" + go.name);
            }
        }

        // 2. 把玩家放回进 2D 前的位置
        PlayerMover posePlayer = FindObjectOfType<PlayerMover>();
        if (posePlayer != null && hasSavedPlayerPose)
        {
            posePlayer.Teleport(savedPlayerPosition, savedPlayerYaw);
            Debug.Log("[TaskHUD] 玩家已回到进 2D 前的位置：" + savedPlayerPosition);
        }

        // 3. 切摄像机：优先用你手动拖进来的 cameraOnReturn
        Camera targetCam = cameraOnReturn;

        if (targetCam == null)
        {
            // 没拖就退回玩家身上的第一人称相机
            if (posePlayer != null && posePlayer.cameraTransform != null)
                targetCam = posePlayer.cameraTransform.GetComponent<Camera>();

            if (targetCam != null)
                Debug.LogWarning("[TaskHUD] 没指定 Camera On Return，改用玩家第一人称相机：" + targetCam.name);
            else
                Debug.LogError("[TaskHUD] 没指定 Camera On Return，玩家身上也找不到相机，画面可能黑屏或停在旧机位", this);
        }

        // 3.1 把目标相机所在的整条父级链打开（可能被 deactivateOn2D 关掉了）
        if (targetCam != null)
        {
            Transform t = targetCam.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(true);
                    if (debugLog) Debug.Log("[TaskHUD] 打开相机父级：" + t.name);
                }
                t = t.parent;
            }

            targetCam.enabled = true;
            Debug.Log("[TaskHUD] 启用目标相机：" + targetCam.name, targetCam);
        }

        // 3.2 其它相机全部关掉（含未激活的），避免多个相机叠在一起
        foreach (Camera c in FindObjectsOfType<Camera>(true))
        {
            if (c == null || c == targetCam) continue;
            if (c.enabled)
            {
                c.enabled = false;
                if (debugLog) Debug.Log("[TaskHUD] 关闭其它相机：" + c.name);
            }
        }

        if (debugLog)
        {
            foreach (Camera c in FindObjectsOfType<Camera>(true))
            {
                if (c == null) continue;
                Debug.Log("[TaskHUD] 相机 " + c.name + " 激活=" + c.gameObject.activeInHierarchy +
                          " 启用=" + c.enabled + " 位置=" + c.transform.position);
            }
        }

        // 4. 玩家控制
        PlayerMover pm = FindObjectOfType<PlayerMover>();
        if (pm != null)
        {
            pm.SetControl(restorePlayerControlOnReturn, restorePlayerControlOnReturn);
            pm.LockCursor(restorePlayerControlOnReturn);
            Debug.Log("[TaskHUD] 玩家控制：" + (restorePlayerControlOnReturn ? "恢复" : "保持冻结"));
        }

        // 5. 最后关闭 2D 面板
        if (level2DPanel != null) level2DPanel.SetActive(false);

        Debug.Log("========== [TaskHUD] 已回到 3D 房间 ==========");
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

            // Root 没填时，按 Ingredient Id 在 HUD 里自动找（优先找带 Check 的父物体）
            if (cfg.root == null && !string.IsNullOrEmpty(cfg.ingredientId))
            {
                GameObject resolved = ResolveFoodRoot(cfg.ingredientId);
                if (resolved != null)
                {
                    cfg.root = resolved;
                    if (debugLog) Debug.Log("[TaskHUD] " + cfg.ingredientId + " 自动找到 Root：" + resolved.name, resolved);
                }
            }

            if (cfg.root == null)
            {
                Debug.LogError("[TaskHUD] 第 " + i + " 个条目（" + cfg.ingredientId + "）Root 是空的。", this);
            }
            else
            {
                Transform check = FindChildRecursive(cfg.root.transform, "Check");

                // 可能拖成了图标子物体：往上一级找同名的父物体（父物体下面才有 Check）
                if (check == null)
                {
                    Transform parent = cfg.root.transform.parent;
                    if (parent != null &&
                        string.Equals(parent.name.Trim(), cfg.ingredientId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        check = FindChildRecursive(parent, "Check");
                        if (check != null && debugLog)
                            Debug.Log("[TaskHUD] " + cfg.root.name + " 的 Check 在父物体 " + parent.name + " 下，已自动改用", parent);
                    }
                }

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

    // 在全场景 UI 里按名字找物体（排除 Prefab 资源和 3D 物体）
    private static GameObject FindUIObject(string objectName)
    {
        string key = objectName.Trim().ToLowerInvariant();

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (!(t is RectTransform)) continue;
            if (t.name.Trim().ToLowerInvariant() != key) continue;

            return t.gameObject;
        }

        return null;
    }

    // 在全场景 UI 里按名字找带 Check 的食材图标根（排除 3D 物体同名的情况）
    private static GameObject FindUICheckRoot(string ingredientId)
    {
        string key = ingredientId.Trim().ToLowerInvariant();

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (!(t is RectTransform)) continue;             // 只找 UI
            if (t.name.Trim().ToLowerInvariant() != key) continue;
            if (FindChildRecursive(t, "Check") == null) continue;

            return t.gameObject;
        }

        return null;
    }

    // 食材图标所在的父面板（可能和 panel 字段指向的不是同一个物体）
    private GameObject ResolveFoodPanel()
    {
        if (foodEntries == null) return null;

        for (int i = 0; i < foodEntries.Length; i++)
        {
            FoodEntry e = foodEntries[i];
            if (e == null || e.root == null) continue;

            Transform p = e.root.transform.parent;
            if (p != null) return p.gameObject;
        }

        return null;
    }

    // 在整个已加载场景里按名字找物体（排除 Prefab 资源）
    private static GameObject FindPanelInScene(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null || !go.scene.IsValid()) continue;
            if (string.Equals(go.name.Trim(), objectName, System.StringComparison.OrdinalIgnoreCase))
                return go;
        }
        return null;
    }

    // 按 Ingredient Id 在 HUD 里找食物图标根物体（优先选下面带 Check 的父物体）
    private GameObject ResolveFoodRoot(string ingredientId)
    {
        Transform searchRoot = panel != null ? panel.transform : transform;
        string id = ingredientId.Trim().ToLowerInvariant();

        Transform fallback = null;
        Transform[] all = searchRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == searchRoot) continue;

            string n = t.name.Trim().ToLowerInvariant();
            if (n != id && !n.Contains(id)) continue;

            if (FindChildRecursive(t, "Check") != null)
                return t.gameObject;

            if (fallback == null) fallback = t;
        }

        return fallback != null ? fallback.gameObject : null;
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