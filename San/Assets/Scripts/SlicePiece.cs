using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlicePiece : MonoBehaviour, IPointerClickHandler
{
    private RectTransform plateSlot;
    private CutGameManager manager;
    private RectTransform rt;
    private bool placed = false;

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        // 保证自己能被点到（Graphic 的 raycastTarget 必须开）
        Graphic g = GetComponent<Graphic>();
        if (g == null) g = gameObject.AddComponent<Image>();
        g.raycastTarget = true;
    }

    public void Init(RectTransform slot, CutGameManager gameManager)
    {
        plateSlot = slot;
        manager = gameManager;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (placed || manager == null || plateSlot == null) return;

        // 向 manager 取盘子下一个空位，排队不重叠
        Vector2 targetLocal = manager.GetNextPlateLocalPos(rt);

        rt.SetParent(plateSlot, false);

        // 强制中心锚点 + 本地缩放锁 1,1，保证尺寸和在菜板上一模一样（不随父节点缩放）
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = targetLocal;
        rt.localRotation = Quaternion.identity;  // 盘子上规整

        // 关闭射线，防止再次点；也避免盖住盘子上的其他碎片
        Graphic g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        placed = true;
        Debug.Log("碎片归位盘子");
    }
}
