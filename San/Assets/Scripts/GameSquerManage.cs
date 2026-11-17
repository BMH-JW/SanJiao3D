using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 剧情流程编排：
/// 1. 玩家进入房间 → 切第三人称、冻结输入
/// 2. 等待 → 显示对话弹窗
/// 3. 等待 → 对话消失，显示任务弹窗
/// 4. 等待 → 任务弹窗消失，切回第一人称，显示任务清单图标
/// 5. 玩家找齐 3 个食材 → 弹出「下一步」按钮
/// 6. 点击「下一步」→ 加载 2D 关卡小游戏场景
///
/// 说明：如果 Inspector 里没拖引用，Start 会自动查找场景中的组件，
/// 避免空引用报错。
/// </summary>
public class GameSequenceManager : MonoBehaviour
{
    // 单例：如果场景里没挂这个脚本，会自动创建一个，避免"不存在 GameSequenceManager"报错
    private static GameSequenceManager _instance;
    public static GameSequenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameSequenceManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameSequenceManager_AutoCreated");
                    _instance = go.AddComponent<GameSequenceManager>();
                    Debug.LogWarning("[GameSequenceManager] 场景中未找到，已自动创建。建议手动建一个空物体挂上此脚本。");
                }
            }
            return _instance;
        }
    }

    [Header("引用（可不拖，会自动查找）")]
    public CameraController cameraController;
    public PlayerMover playerMover;
    public UIManager uiManager;
    public TaskManager taskManager;

    [Header("时间（秒）")]
    public float beforeDialogue = 1.5f;   // 进房后到对话弹出的间隔
    public float dialogueDuration = 2f;   // 对话停留时长
    public float taskPopupDuration = 2f;  // 任务提示停留时长

    [Header("文案")]
    [TextArea] public string dialogueText = "欢迎来到厨房，去寻找今天做菜需要的食材吧。";
    [TextArea] public string taskText = "任务：找到 3 种食材";

    [Header("2D 小游戏场景名")]
    public string miniGameSceneName = "2DMiniGame";

    void Awake()
    {
        if (_instance == null) _instance = this;
    }

    void Start()
    {
        // ---- 自动查找引用，避免空引用报错 ----
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (cameraController == null)
                Debug.LogWarning("[GameSequenceManager] 场景中找不到 CameraController，请检查。");
        }
        if (playerMover == null)
        {
            playerMover = FindObjectOfType<PlayerMover>();
            if (playerMover == null)
                Debug.LogWarning("[GameSequenceManager] 场景中找不到 PlayerMover，请检查 Player 是否挂了脚本。");
        }
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
                Debug.LogWarning("[GameSequenceManager] 场景中找不到 UIManager。");
        }
        if (taskManager == null)
        {
            taskManager = FindObjectOfType<TaskManager>();
            if (taskManager == null)
                Debug.LogWarning("[GameSequenceManager] 场景中找不到 TaskManager。");
        }

        Debug.Log("[GameSequenceManager] 初始化完成，等待玩家进入房间。");
    }

    /// <summary>DoorInteraction 检测到玩家穿过门后调用。</summary>
    public void OnPlayerEnteredRoom()
    {
        Debug.Log("[GameSequenceManager] 玩家进入房间，开始剧情流程。");
        StartCoroutine(EnterRoomSequence());
    }

    IEnumerator EnterRoomSequence()
    {
        // 冻结玩家输入
        if (playerMover != null)
        {
            playerMover.SetControl(false, false);
            playerMover.LockCursor(false);
        }

        // 切到第三人称
        if (cameraController != null) cameraController.SetThirdPerson();

        // 等 1~2 秒
        yield return new WaitForSeconds(beforeDialogue);

        // 弹出对话
        if (uiManager != null) uiManager.ShowDialogue(dialogueText);
        yield return new WaitForSeconds(dialogueDuration);
        if (uiManager != null) uiManager.HideDialogue();

        // 弹出任务提示
        if (uiManager != null) uiManager.ShowTaskPopup(taskText);
        yield return new WaitForSeconds(taskPopupDuration);
        if (uiManager != null) uiManager.HideTaskPopup();

        // 切回第一人称，恢复操作
        if (cameraController != null) cameraController.SetFirstPerson();
        if (playerMover != null)
        {
            playerMover.LockCursor(true);
            playerMover.SetControl(true, true);
        }

        // 显示任务清单图标，初始化清单
        if (uiManager != null) uiManager.ShowTaskListIcon();
        if (taskManager != null) taskManager.StartTask();
    }

    /// <summary>TaskManager 检测到 3 个食材全部找齐后调用。</summary>
    public void OnAllIngredientsFound()
    {
        Debug.Log("[GameSequenceManager] 全部食材已找到，弹出下一步按钮。");
        if (uiManager != null) uiManager.ShowNextStepButton();
    }

    /// <summary>「下一步」按钮点击后调用。</summary>
    public void OnNextStepClicked()
    {
        Debug.Log("[GameSequenceManager] 加载 2D 小游戏场景：" + miniGameSceneName);
        if (!string.IsNullOrEmpty(miniGameSceneName))
            SceneManager.LoadScene(miniGameSceneName);
    }
}
