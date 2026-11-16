using UnityEngine;
using UnityEngine.EventSystems;

public class DynamicLineClick : MonoBehaviour, IPointerClickHandler
{
    public CutGameManager manager;

    void Awake()
    {
        if (manager == null)
            manager = FindObjectOfType<CutGameManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("DynamicLineClick ÊÕµ½µã»÷");
        if (manager != null)
            manager.OnCutClick(eventData.position);
    }
}
