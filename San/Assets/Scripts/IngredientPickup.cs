using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IngredientPickup : MonoBehaviour
{
    [Header("身份")]
    [Tooltip("食材 ID，必须和 TaskHUD 里的 Ingredient Id 完全一致；留空则用物体名字")]
    public string ingredientId = "";

    [Header("提示 UI")]
    [Tooltip("提示物体。建议指向独立的提示子物体（如 Canvas/PickupPrompt），不要指向整个屏幕 Canvas")]
    public GameObject promptRoot;
    [Tooltip("可选：提示文字组件")]
    public Text promptLabel;
    public string promptText = "按 E 拾取";

    [Header("检测")]
    public Transform player;
    public float pickupRange = 2.0f;
    public float maxHeightDiff = 2.0f;

    [Header("按键")]
    public KeyCode pickupKey = KeyCode.E;

    [Header("拾取后")]
    [Tooltip("留空 = 隐藏脚本所在物体。只能填食材模型，不要填 Canvas / HUD")]
    public GameObject objectToHide;
    public bool destroyInsteadOfHide = false;

    [Header("提示朝向")]
    public bool faceCamera = true;

    [Header("调试")]
    public bool debugLog = true;

    private bool picked = false;
    private bool promptVisible = false;
    private bool promptUnsafe = false;
    private Text[] promptTexts;

    private static readonly List<IngredientPickup> allPickups = new List<IngredientPickup>();
    private static readonly HashSet<string> pickedIds = new HashSet<string>();

    public static event System.Action<string> Picked;

    public static bool HasPicked(string id)
    {
        return !string.IsNullOrEmpty(id) && pickedIds.Contains(id);
    }

    public string GetId()
    {
        return string.IsNullOrEmpty(ingredientId) ? gameObject.name : ingredientId;
    }

    private void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null)
        {
            PlayerMover pm = FindObjectOfType<PlayerMover>();
            if (pm != null) player = pm.transform;
        }
        if (promptRoot == null && promptLabel != null) promptRoot = promptLabel.gameObject;

        CheckPromptSafety();
    }

    private void OnEnable()
    {
        if (!allPickups.Contains(this)) allPickups.Add(this);
    }

    private void OnDisable()
    {
        allPickups.Remove(this);
        SetPromptVisible(false, true);
    }

    private void Start()
    {
        if (player == null)
            Debug.LogError("[IngredientPickup] 没找到玩家，请把玩家拖到 Player 字段。", this);
        if (promptRoot == null)
            Debug.LogError("[IngredientPickup] 没有指定 Prompt Root，提示不会显示。", this);

        if (promptLabel != null) promptLabel.text = promptText;
        SetPromptVisible(false, true);
    }

    private void Update()
    {
        if (picked)
        {
            SetPromptVisible(false);
            return;
        }
        if (player == null) return;

        bool isNearest = IsInRange() && FindNearestPickup() == this;
        SetPromptVisible(isNearest);

        if (isNearest && PickupKeyPressed())
            PickUp();
    }

    private void LateUpdate()
    {
        if (!promptVisible || !faceCamera || promptRoot == null || promptUnsafe) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        promptRoot.transform.rotation = cam.transform.rotation;
    }

    // ---------------- 拾取 ----------------

    private void PickUp()
    {
        if (picked) return;
        picked = true;

        SetPromptVisible(false);

        string id = GetId();
        pickedIds.Add(id);
        if (Picked != null) Picked(id);

        GameObject target = objectToHide != null ? objectToHide : gameObject;

        if (debugLog)
            Debug.Log("[IngredientPickup] 拾取 " + id + "，即将隐藏：" + GetPath(target.transform), this);

        if (destroyInsteadOfHide)
        {
            if (ContainsUI(target))
            {
                Debug.LogError("[IngredientPickup] 目标里有 UI，已取消销毁：" + GetPath(target.transform), target);
                HideVisualOnly(target);
            }
            else
            {
                Destroy(target);
            }
            return;
        }

        if (ContainsUI(target))
        {
            Debug.LogWarning("[IngredientPickup] 目标里含有 UI，改为只隐藏模型：" + GetPath(target.transform) +
                             "。请把 Object To Hide 改成食材模型本身。", target);
            HideVisualOnly(target);
        }
        else
        {
            target.SetActive(false);
        }
    }

    private void HideVisualOnly(GameObject target)
    {
        Renderer[] rs = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++) rs[i].enabled = false;

        Collider[] cs = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cs.Length; i++) cs[i].enabled = false;
    }

    // ---------------- 提示安全检测 ----------------

    private static bool ContainsUI(GameObject go)
    {
        if (go.GetComponentInChildren<TaskHUD>(true) != null) return true;

        Canvas[] canvases = go.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode != RenderMode.WorldSpace) return true;
        }
        return false;
    }

    private void CheckPromptSafety()
    {
        promptUnsafe = false;
        promptTexts = null;
        if (promptRoot == null) return;

        if (!ContainsUI(promptRoot)) return;

        promptUnsafe = true;

        // 优先只用指定的 Prompt Label
        if (promptLabel != null)
        {
            promptTexts = new Text[] { promptLabel };
            Debug.LogWarning("[IngredientPickup] Prompt Root 是屏幕 Canvas（" + GetPath(promptRoot.transform) +
                             "），已改为只开关 Prompt Label：" + GetPath(promptLabel.transform) +
                             "。建议把 Prompt Root 指向独立的提示子物体。", this);
            return;
        }

        // 没指定 Label：按名字找提示文字
        Text[] all = promptRoot.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name.ToLowerInvariant();
            if (n.Contains("prompt") || n.Contains("pickup") || n.Contains("拾取") || n.Contains("提示"))
            {
                promptTexts = new Text[] { all[i] };
                Debug.LogWarning("[IngredientPickup] Prompt Root 是屏幕 Canvas，已改为只开关文字：" +
                                 GetPath(all[i].transform), this);
                return;
            }
        }

        Debug.LogError("[IngredientPickup] Prompt Root 指向屏幕 Canvas，但找不到提示文字。请把 Prompt Root 指向提示文字所在的子物体，或指定 Prompt Label。", this);
    }

    // ---------------- 范围与最近判定 ----------------

    private bool IsInRange()
    {
        if (player == null) return false;

        Vector3 d = player.position - transform.position;
        if (Mathf.Abs(d.y) > maxHeightDiff) return false;

        d.y = 0f;
        return d.sqrMagnitude <= pickupRange * pickupRange;
    }

    private float SqrDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    private IngredientPickup FindNearestPickup()
    {
        IngredientPickup best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allPickups.Count; i++)
        {
            IngredientPickup p = allPickups[i];
            if (p == null || p.picked) continue;
            if (!p.IsInRange()) continue;

            float d = p.SqrDistanceToPlayer();

            if (d < bestDist ||
                (Mathf.Approximately(d, bestDist) && best != null && p.GetInstanceID() < best.GetInstanceID()))
            {
                bestDist = d;
                best = p;
            }
        }

        return best;
    }

    // ---------------- 提示显示 ----------------

    private void SetPromptVisible(bool visible, bool force = false)
    {
        if (!force && promptVisible == visible) return;
        promptVisible = visible;

        if (promptRoot == null) return;

        if (promptUnsafe)
        {
            if (promptTexts == null) return;
            for (int i = 0; i < promptTexts.Length; i++)
            {
                if (promptTexts[i] != null) promptTexts[i].enabled = visible;
            }
            return;
        }

        promptRoot.SetActive(visible);
    }

    // ---------------- 工具 ----------------

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

    private bool PickupKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return false;

        switch (pickupKey)
        {
            case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
            case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
            case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
            case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(pickupKey);
#endif
    }
}