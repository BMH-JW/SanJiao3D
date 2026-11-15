using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class VegItem : MonoBehaviour
{
    [Header("引用")]
    public Button btn;
    public RectTransform boardTarget;
    public CutGameManager manager;

    [Header("参数")]
    public float moveDuration = 0.35f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsOnBoard { get; private set; }

    void Awake()
    {
        if (btn == null) btn = GetComponentInChildren<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClickVeg);
    }

    void OnClickVeg()
    {
        if (IsOnBoard || manager == null || manager.IsBusy) return;
        StartCoroutine(MoveToBoard());
    }

    IEnumerator MoveToBoard()
    {
        manager.IsBusy = true;
        if (btn != null) btn.interactable = false;

        Vector3 start = transform.position;
        Vector3 end = boardTarget.position;
        float t = 0f;

        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = moveCurve.Evaluate(Mathf.Clamp01(t / moveDuration));
            transform.position = Vector3.Lerp(start, end, k);
            yield return null;
        }

        transform.position = end;

        if (transform.parent != null && transform.parent.parent != null)
            transform.SetParent(transform.parent.parent, true);

        IsOnBoard = true;
        manager.SetCurrentVeg(this);
        manager.IsBusy = false;
    }
}