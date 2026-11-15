using UnityEngine;

public class DynamicLineClick : MonoBehaviour
{
    public CutGameManager manager;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (manager != null)
            {
                manager.OnPlayerClick(Input.mousePosition);
            }
        }
    }
}
