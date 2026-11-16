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

        Vector2 targetLocal = manager.GetNextPlateLocalPos(rt);

        rt.SetParent(plateSlot, false);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = targetLocal;
        rt.localRotation = Quaternion.identity;

        Graphic g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        placed = true;

        // 通知 manager：本碎片已入盘（用于判断是否全部入盘、是否该清盘推进）
        manager.NotifyPiecePlaced();
    }
}
