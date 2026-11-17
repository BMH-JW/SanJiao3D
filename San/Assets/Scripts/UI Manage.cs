using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一管理所有 UI 弹窗：
/// - 剧情对话弹窗（ShowDialogue / HideDialogue）
/// - 任务提示弹窗（ShowTaskPopup / HideTaskPopup）
/// - 任务清单图标 + 清单面板（ShowTaskListIcon / ToggleTaskList）
/// - 下一步按钮（ShowNextStepButton）
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("对话弹窗")]
    public GameObject dialoguePanel;
    public Text dialogueText;

    [Header("任务提示弹窗")]
    public GameObject taskPopupPanel;
    public Text taskPopupText;

    [Header("任务清单")]
    [Tooltip("右下角任务清单图标按钮")]
    public GameObject taskListIcon;
    [Tooltip("点开后出现的列表面板")]
    public GameObject taskListPanel;
    [Tooltip("清单内容的容器（挂 VerticalLayoutGroup）")]
    public Transform ingredientListContainer;
    [Tooltip("单个食材行预制体（含 Text，用于显示名称 + 勾选）")]
    public GameObject ingredientItemPrefab;

    [Header("下一步")]
    public GameObject nextStepPanel;
    public Button nextStepButton;

    private bool listVisible = false;

    void Awake()
    {
        if (Instance == null) Instance = this;

        // 初始全部隐藏
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (taskPopupPanel) taskPopupPanel.SetActive(false);
        if (taskListIcon) taskListIcon.SetActive(false);
        if (taskListPanel) taskListPanel.SetActive(false);
        if (nextStepPanel) nextStepPanel.SetActive(false);
    }

    void Start()
    {
        // 下一步按钮 → 通知 GameSequenceManager 加载 2D 小游戏
        if (nextStepButton != null)
            nextStepButton.onClick.AddListener(() =>
            {
                if (GameSequenceManager.Instance != null)
                    GameSequenceManager.Instance.OnNextStepClicked();
            });

        // 任务清单图标 → 切换列表面板
        if (taskListIcon != null)
        {
            Button btn = taskListIcon.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(ToggleTaskList);
        }
    }

    // ===== 对话弹窗 =====
    public void ShowDialogue(string text)
    {
        if (dialogueText) dialogueText.text = text;
        if (dialoguePanel) dialoguePanel.SetActive(true);
    }
    public void HideDialogue()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
    }

    // ===== 任务提示弹窗 =====
    public void ShowTaskPopup(string text)
    {
        if (taskPopupText) taskPopupText.text = text;
        if (taskPopupPanel) taskPopupPanel.SetActive(true);
    }
    public void HideTaskPopup()
    {
        if (taskPopupPanel) taskPopupPanel.SetActive(false);
    }

    // ===== 任务清单 =====
    public void ShowTaskListIcon()
    {
        if (taskListIcon) taskListIcon.SetActive(true);
    }

    public void ToggleTaskList()
    {
        listVisible = !listVisible;
        if (taskListPanel) taskListPanel.SetActive(listVisible);
    }

    // ===== 下一步 =====
    public void ShowNextStepButton()
    {
        if (nextStepPanel) nextStepPanel.SetActive(true);
    }
}