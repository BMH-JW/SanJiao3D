using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 点击把手 / 门板开关冰箱、抽屉、门。
/// 场景里不需要挂载：进入场景后会自动创建一个实例，也可以手动挂到把手上。
/// 第一人称鼠标可能是锁定的，Input.mousePosition 固定屏幕中心，所以自己发射线，
/// 并且射线没打中时会在鼠标附近做一次辅助选取，让小把手也好点。
/// </summary>
public class HandleClick : MonoBehaviour
{
    [Header("点击设置")]
    public float maxDistance = 5f;      // 点击检测距离
    [Tooltip("射线没打中时，在鼠标周围多少像素内自动选中把手")]
    public float screenRadius = 45f;
    public bool showDebugInfo = true;   // 在 Console 打印点击命中的物体

    private static HandleClick instance;
    private static readonly List<RaycastResult> uiResults = new List<RaycastResult>();
    private Camera rayCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<HandleClick>() != null) return;
        GameObject go = new GameObject("FridgeClickHandler(Auto)");
        go.AddComponent<HandleClick>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        // 挂在把手上时，如果把手没有碰撞体就自动补一个，否则射线永远打不到
        EnsureCollider();

        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    void EnsureCollider()
    {
        if (GetComponent<Collider>() != null) return;
        if (GetComponentInChildren<Collider>() != null) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);

        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        if (showDebugInfo)
            Debug.Log($"[HandleClick] 给 {name} 自动补了点击碰撞体，尺寸 {box.size}");
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // 场景里没有可交互物体就不处理
        if (FindObjectOfType<DoorInteraction>() == null && FindObjectOfType<FridgeDoor>() == null)
            return;

        Camera cam = GetRayCamera();
        if (cam == null) return;

        Vector3 screenPos = Cursor.lockState == CursorLockMode.Locked
            ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
            : Input.mousePosition;

        // 只有点中真正的 UI 按钮时才交给 UI，其它 UI 图片不会挡住点击
        if (IsPointerOverButton(screenPos)) return;

        if (TryDirectClick(cam, screenPos)) return;
        TryAssistClick(cam, screenPos);
    }

    // 精确射线点击
    private bool TryDirectClick(Camera cam, Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);

        PlayerMover mover = FindObjectOfType<PlayerMover>();
        Transform playerRoot = mover != null ? mover.transform : null;

        foreach (RaycastHit hit in hits)
        {
            // 射线从玩家身体里出发，跳过玩家自己的碰撞体
            if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot)) continue;

            DoorInteraction door = hit.collider.GetComponentInParent<DoorInteraction>();
            if (door != null)
            {
                if (showDebugInfo) Debug.Log($"[HandleClick] 命中 {hit.collider.name} → {door.name} 开关");
                door.ToggleDoor();
                return true;
            }

            FridgeDoor fridge = hit.collider.GetComponentInParent<FridgeDoor>();
            if (fridge != null && fridge.isActiveAndEnabled)
            {
                if (showDebugInfo) Debug.Log($"[HandleClick] 命中 {hit.collider.name} → {fridge.name} 开关");
                fridge.Toggle();
                return true;
            }

            if (showDebugInfo) Debug.Log($"[HandleClick] 被 {hit.collider.name} 挡住，尝试辅助点击");
            break;
        }

        return false;
    }

    // 射线没打中时：在鼠标附近找最近的可交互物，方便点小把手
    private void TryAssistClick(Camera cam, Vector3 screenPos)
    {
        Vector3 camPos = cam.transform.position;
        PlayerMover mover = FindObjectOfType<PlayerMover>();
        Transform playerRoot = mover != null ? mover.transform : null;

        DoorInteraction bestDoor = null;
        FridgeDoor bestFridge = null;
        float bestScore = float.MaxValue;

        foreach (DoorInteraction door in FindObjectsOfType<DoorInteraction>())
        {
            Collider blocker = door.doorBlocker != null && door.doorBlocker.enabled ? door.doorBlocker : null;
            if (!TryGetBounds(door.gameObject, blocker, out Bounds bounds)) continue;
            if (bounds.SqrDistance(camPos) > maxDistance * maxDistance) continue;

            Vector3 center = bounds.center;
            if (Vector3.Dot(cam.transform.forward, (center - camPos).normalized) <= 0.2f) continue;
            if (!TryGetScreenDistance(cam, bounds, screenPos, out float screenDist, out float centerDist)) continue;
            if (screenDist > screenRadius) continue;
            if (IsBlocked(camPos, center, door.transform, playerRoot)) continue;

            float score = screenDist * 10f + centerDist + (center - camPos).magnitude * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = door;
                bestFridge = null;
            }
        }

        foreach (FridgeDoor fridge in FindObjectsOfType<FridgeDoor>())
        {
            // 被 DoorInteraction 接管后会自动停用，别再参与辅助点击
            if (fridge == null || !fridge.isActiveAndEnabled) continue;
            if (!TryGetBounds(fridge.gameObject, null, out Bounds bounds)) continue;
            if (bounds.SqrDistance(camPos) > maxDistance * maxDistance) continue;

            Vector3 center = bounds.center;
            if (Vector3.Dot(cam.transform.forward, (center - camPos).normalized) <= 0.2f) continue;
            if (!TryGetScreenDistance(cam, bounds, screenPos, out float screenDist, out float centerDist)) continue;
            if (screenDist > screenRadius) continue;
            if (IsBlocked(camPos, center, fridge.transform, playerRoot)) continue;

            float score = screenDist * 10f + centerDist + (center - camPos).magnitude * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = null;
                bestFridge = fridge;
            }
        }

        if (bestDoor != null)
        {
            if (showDebugInfo) Debug.Log($"[HandleClick] 辅助命中 → {bestDoor.name} 开关");
            bestDoor.ToggleDoor();
            return;
        }

        if (bestFridge != null)
        {
            if (showDebugInfo) Debug.Log($"[HandleClick] 辅助命中 → {bestFridge.name} 开关");
            bestFridge.Toggle();
            return;
        }

        if (showDebugInfo) Debug.Log("[HandleClick] 点击没有命中任何可交互物体");
    }

    // 优先用 doorBlocker，没有就合并子物体碰撞体 / 渲染包围盒
    private static bool TryGetBounds(GameObject root, Collider preferred, out Bounds bounds)
    {
        if (preferred != null)
        {
            bounds = preferred.bounds;
            return true;
        }

        bool has = false;
        bounds = new Bounds(root.transform.position, Vector3.zero);

        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            if (!has) { bounds = c.bounds; has = true; }
            else bounds.Encapsulate(c.bounds);
        }

        if (has) return true;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return has;
    }

    // 包围盒投影到屏幕上的矩形，返回鼠标到矩形的像素距离和到中心的像素距离
    private static bool TryGetScreenDistance(Camera cam, Bounds bounds, Vector3 screenPos,
        out float distance, out float centerDistance)
    {
        distance = float.MaxValue;
        centerDistance = float.MaxValue;

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool anyFront = false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);

            Vector3 sp = cam.WorldToScreenPoint(corner);
            if (sp.z <= 0f) continue;

            anyFront = true;
            min = Vector2.Min(min, sp);
            max = Vector2.Max(max, sp);
        }

        if (!anyFront) return false;

        Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        Vector3 centerSp = cam.WorldToScreenPoint(center);
        centerDistance = centerSp.z > 0f
            ? Vector2.Distance(screenPos, centerSp)
            : Vector2.Distance(screenPos, rect.center);

        if (rect.Contains(screenPos))
        {
            distance = 0f;
            return true;
        }

        float dx = Mathf.Max(Mathf.Max(rect.xMin - screenPos.x, 0f), screenPos.x - rect.xMax);
        float dy = Mathf.Max(Mathf.Max(rect.yMin - screenPos.y, 0f), screenPos.y - rect.yMax);
        distance = Mathf.Sqrt(dx * dx + dy * dy);
        return true;
    }

    private static bool IsBlocked(Vector3 camPos, Vector3 target, Transform targetRoot, Transform playerRoot)
    {
        Vector3 dir = target - camPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        RaycastHit[] hits = Physics.RaycastAll(camPos, dir / dist, dist);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.isTrigger) continue;
            if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot)) continue;

            // 目标自己的碰撞体不算遮挡
            if (hit.collider.transform.IsChildOf(targetRoot)) return false;

            return true;
        }

        return false;
    }

    // 只挡真正的 UI 按钮，普通 UI 图片不拦截 3D 点击
    private static bool IsPointerOverButton(Vector3 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        uiResults.Clear();
        EventSystem.current.RaycastAll(ped, uiResults);

        foreach (RaycastResult result in uiResults)
        {
            if (result.gameObject.GetComponentInParent<Button>() != null)
                return true;
        }

        return false;
    }

    // 必须用玩家自己的摄像机：场景里挂着 MainCamera 标签的相机不是玩家视角
    Camera GetRayCamera()
    {
        if (rayCamera != null && rayCamera.isActiveAndEnabled) return rayCamera;

        PlayerMover mover = FindObjectOfType<PlayerMover>();
        if (mover != null)
        {
            foreach (Camera c in mover.GetComponentsInChildren<Camera>(true))
            {
                if (c.isActiveAndEnabled)
                {
                    rayCamera = c;
                    return rayCamera;
                }
            }

            if (mover.cameraTransform != null)
            {
                Camera c = mover.cameraTransform.GetComponent<Camera>();
                if (c != null)
                {
                    rayCamera = c;
                    return rayCamera;
                }
            }
        }

        if (Camera.main != null) return Camera.main;

        foreach (Camera c in Camera.allCameras)
            if (c.isActiveAndEnabled) return c;

        return null;
    }
}
