using UnityEngine;
using UnityEngine.UI;

public class SlicePiece : MonoBehaviour
{
    public Transform plateTarget;
    public CutGameManager manager;
    private Button btn;

    public void Init(Transform target, CutGameManager mgr)
    {
        plateTarget = target;
        manager = mgr;

        btn = GetComponent<Button>();
        if (btn == null) btn = GetComponentInChildren<Button>();

        if (btn != null)
            btn.onClick.AddListener(OnClick);

        Debug.Log("碎片初始化完成");
    }

    void OnClick()
    {
        if (plateTarget == null) return;
        transform.SetParent(plateTarget);
        transform.position = plateTarget.position;
    }
}