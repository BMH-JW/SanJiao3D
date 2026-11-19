using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PotCalibrationGame : MonoBehaviour
{
    [Header("校准条配置")]
    public RectTransform barBgRt;        // 进度条底板Rect
    public RectTransform pointerRt;      // 移动指针
    public RectTransform targetZoneRt;  // 绿色合格区间
    public float moveSpeed = 220f;       // 像素/秒

    [Header("结果图")]
    public Image perfectImg;
    public Image missImg;

    [Header("完成界面")]
    public GameObject completePanel;      // CompleteText 所在物体

    [Header("结果后关闭")]
    public GameObject canvasToClose;      // 要关闭的 Canvas（留空则自动取本物体父级 Canvas）
    public float closeDelay = 2f;         // 结果图显示后多久弹出 CompleteText
    public float completePanelCloseDelay = 2f; // CompleteText 显示后多久关闭 Canvas

    [Header("结束后返回 3D 房间")]
    public bool returnTo3DOnComplete = true;   // 关闭 2D 后恢复 3D 场景并切第三人称相机

    [Header("状态")]
    public bool isRunning = true;

    private bool resultShown;             // 防止重复判定/重复关闭

    private float barMinX;
    private float barMaxX;
    private float targetMinX;
    private float targetMaxX;
    private Vector3[] corners = new Vector3[4]; // 存四个角的数组

    void Start()
    {
        BeginCalibration();
    }

    // 开始/重新开始校准：重新读取边界、复位指针与结果图
    public void BeginCalibration()
    {
        StopAllCoroutines();
        RecalculateBounds();

        resultShown = false;
        isRunning = true;
        moveSpeed = Mathf.Abs(moveSpeed);

        if (perfectImg != null) perfectImg.gameObject.SetActive(false);
        if (missImg != null) missImg.gameObject.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);

        if (pointerRt != null && barBgRt != null)
            pointerRt.anchoredPosition = new Vector2(barBgRt.anchoredPosition.x, pointerRt.anchoredPosition.y);
    }

    // 重新读取底板与绿色区间的世界边界（Canvas 刚激活时也能拿到正确值）
    void RecalculateBounds()
    {
        if (barBgRt != null)
        {
            barBgRt.GetWorldCorners(corners);
            barMinX = corners[0].x;
            barMaxX = corners[2].x;
        }

        if (targetZoneRt != null)
        {
            targetZoneRt.GetWorldCorners(corners);
            targetMinX = corners[0].x;
            targetMaxX = corners[2].x;
        }
    }

    void Update()
    {
        if (!isRunning) return;

        pointerRt.anchoredPosition += Vector2.right * moveSpeed * Time.unscaledDeltaTime;
        float pointerWorldX = pointerRt.position.x;

        if (pointerWorldX >= barMaxX || pointerWorldX <= barMinX)
        {
            moveSpeed = -moveSpeed;
        }
    }

    public void StopCheck()
    {
        if (resultShown) return;
        resultShown = true;

        isRunning = false;
        float pointerWorldX = pointerRt.position.x;

        if (pointerWorldX >= targetMinX && pointerWorldX <= targetMaxX)
        {
            perfectImg.gameObject.SetActive(true);
            missImg.gameObject.SetActive(false);
        }
        else
        {
            perfectImg.gameObject.SetActive(false);
            missImg.gameObject.SetActive(true);
        }

        // 结果图 → CompleteText → 关闭当前 Canvas，全程不切换 Canvas
        StartCoroutine(ShowCompleteThenCloseCanvas());
    }

    IEnumerator ShowCompleteThenCloseCanvas()
    {
        yield return new WaitForSecondsRealtime(closeDelay);

        if (completePanel != null) completePanel.SetActive(true);
        else Debug.LogWarning("PotCalibrationGame：未指定 CompleteText 所在物体");

        yield return new WaitForSecondsRealtime(completePanelCloseDelay);
        if (completePanel != null) completePanel.SetActive(false);

        // 返回 3D 房间：TaskHUD 负责恢复 3D 物体并关闭 2D Canvas
        if (returnTo3DOnComplete)
        {
            // 场景里有一个被禁用的 TaskHUD 残留，FindObjectOfType 可能先拿到它，
            // 那样 ReturnTo3D 等于没执行，玩家会停在主相机的门外画面
            TaskHUD hud = null;
            TaskHUD[] huds = FindObjectsOfType<TaskHUD>(true);
            for (int i = 0; i < huds.Length; i++)
            {
                if (huds[i] != null && huds[i].isActiveAndEnabled)
                {
                    hud = huds[i];
                    break;
                }
            }

            if (hud != null)
            {
                Debug.Log("PotCalibrationGame：调用 TaskHUD.ReturnTo3D（" + hud.gameObject.name + "）", this);
                hud.ReturnTo3D();
                yield break;
            }
            Debug.LogWarning("PotCalibrationGame：场景里找不到启用的 TaskHUD，改为只关闭 2D Canvas");
        }

        GameObject target = canvasToClose;
        if (target == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) target = canvas.gameObject;
        }

        if (target != null) target.SetActive(false);
        else Debug.LogWarning("PotCalibrationGame：找不到要关闭的 Canvas，请手动指定 canvasToClose");
    }

    public void ResetGame()
    {
        BeginCalibration();
    }
}