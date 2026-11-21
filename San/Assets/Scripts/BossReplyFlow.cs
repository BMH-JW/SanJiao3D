using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 老板对话的"回复 → 选择"流程：
/// ① 点回复按钮 → 回复按钮消失，出现玩家回复图 + 两个选择按钮
/// ② 点某个选择按钮 → 隐藏回复阶段，显示对应的结果场景图
/// 初始状态由 Awake 自动摆好，不用手动勾。
/// </summary>
public class BossReplyFlow : MonoBehaviour
{
    [Header("回复阶段")]
    [Tooltip("玩家回复的图片，默认隐藏")]
    public GameObject playerReplyImage;

    [Tooltip("回复按钮，流程开始时可点")]
    public Button replyButton;

    [Tooltip("两个选择按钮，默认隐藏")]
    public GameObject[] choiceButtons;

    [Header("选择后显示的结果图（顺序对应上面的选择按钮）")]
    [Tooltip("第 0 张对应第 0 个选择按钮，第 1 张对应第 1 个")]
    public GameObject[] choiceResultImages;

    private void Awake()
    {
        if (replyButton != null)
            replyButton.onClick.AddListener(OnReplyClicked);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                GameObject go = choiceButtons[i];
                if (go == null) continue;

                Button btn = go.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => OnChoiceClicked(index));
                else
                    Debug.LogWarning(
                        "[BossReplyFlow] " + go.name + " 上没有 Button 组件，点了没反应",
                        go);
            }
        }

        ResetFlow();
    }

    /// <summary>把面板摆回初始状态：回复按钮可见，其余隐藏。</summary>
    public void ResetFlow()
    {
        if (playerReplyImage != null) playerReplyImage.SetActive(false);

        if (replyButton != null) replyButton.gameObject.SetActive(true);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
                if (choiceButtons[i] != null) choiceButtons[i].SetActive(false);
        }

        if (choiceResultImages != null)
        {
            for (int i = 0; i < choiceResultImages.Length; i++)
                if (choiceResultImages[i] != null) choiceResultImages[i].SetActive(false);
        }
    }

    /// <summary>点回复按钮：回复按钮消失，出现玩家回复图 + 两个选择按钮。</summary>
    public void OnReplyClicked()
    {
        if (replyButton != null) replyButton.gameObject.SetActive(false);

        if (playerReplyImage != null) playerReplyImage.SetActive(true);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
                if (choiceButtons[i] != null) choiceButtons[i].SetActive(true);
        }
    }

    /// <summary>点某个选择按钮：隐藏回复阶段，显示对应结果图。</summary>
    public void OnChoiceClicked(int index)
    {
        if (playerReplyImage != null) playerReplyImage.SetActive(false);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
                if (choiceButtons[i] != null) choiceButtons[i].SetActive(false);
        }

        if (choiceResultImages != null &&
            index >= 0 &&
            index < choiceResultImages.Length &&
            choiceResultImages[index] != null)
        {
            choiceResultImages[index].SetActive(true);
        }
    }
}