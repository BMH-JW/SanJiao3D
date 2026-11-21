using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>聊天中的一轮。</summary>
[System.Serializable]
public class ChatStep
{
    [Tooltip("玩家这一轮发出的消息图，可留空（表示这轮玩家不发言，只有对方说）")]
    public GameObject playerMessage;

    [Tooltip("对方这一轮的回话图，会依次自动出现")]
    public GameObject[] npcReplies;

    [Tooltip("玩家消息出现后，隔多久对方开始回话（秒）")]
    public float playerToNpcDelay = 0.8f;

    [Tooltip("对方多条回话之间的间隔（秒）")]
    public float npcInterval = 0.8f;
}

/// <summary>
/// 多轮聊天流程：
/// 第一轮点【发送】→ 玩家消息出现 → 对方回话自动依次出现；
/// 还有后续轮次时按钮变【回复】；最后一轮结束按钮消失。
/// 母亲、朋友各挂一份，配各自的图。
/// </summary>
public class ChatSequence : MonoBehaviour
{
    [Header("聊天步骤（按顺序推进）")]
    public ChatStep[] steps;

    [Header("按钮")]
    [Tooltip("第一轮用的【发送】按钮")]
    public Button sendButton;

    [Tooltip("后续轮次用的【回复】按钮；留空则一直用发送按钮")]
    public Button replyButton;

    [Tooltip("最后一轮结束后隐藏按钮")]
    public bool hideButtonOnFinish = true;

    [Header("事件")]
    [Tooltip("全部步骤结束后触发（可在这里接情绪球奖励）")]
    public UnityEvent onFinished;

    private int stepIndex = -1;
    private bool playing;
    private Coroutine routine;

    private void Awake()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnActionClicked);

        if (replyButton != null)
            replyButton.onClick.AddListener(OnActionClicked);

        ResetFlow();
    }

    /// <summary>摆回初始状态：只显示发送按钮，所有图隐藏。</summary>
    public void ResetFlow()
    {
        StopRoutine();

        stepIndex = -1;
        playing = false;

        HideAllImages();

        if (sendButton != null) sendButton.gameObject.SetActive(true);
        if (replyButton != null) replyButton.gameObject.SetActive(false);
    }

    /// <summary>点按钮推进一轮。</summary>
    public void OnActionClicked()
    {
        if (playing) return;

        int next = stepIndex + 1;

        if (steps == null || next >= steps.Length)
            return;

        StopRoutine();
        routine = StartCoroutine(PlayStep(next));
    }

    private IEnumerator PlayStep(int index)
    {
        playing = true;
        stepIndex = index;

        // 播放期间收起按钮，防止连点
        if (sendButton != null) sendButton.gameObject.SetActive(false);
        if (replyButton != null) replyButton.gameObject.SetActive(false);

        ChatStep step = steps[index];

        if (step == null)
        {
            Finish();
            yield break;
        }

        // 1. 玩家这一轮的消息
        if (step.playerMessage != null)
            step.playerMessage.SetActive(true);

        // 2. 等一下，对方开始回话
        if (step.playerToNpcDelay > 0f)
            yield return new WaitForSecondsRealtime(step.playerToNpcDelay);

        // 3. 对方回话依次自动出现
        if (step.npcReplies != null)
        {
            for (int i = 0; i < step.npcReplies.Length; i++)
            {
                if (step.npcReplies[i] != null)
                    step.npcReplies[i].SetActive(true);

                if (step.npcInterval > 0f && i < step.npcReplies.Length - 1)
                    yield return new WaitForSecondsRealtime(step.npcInterval);
            }
        }

        playing = false;
        routine = null;

        // 4. 还有后续轮次 → 出现【回复】；否则结束
        if (stepIndex + 1 < steps.Length)
            ShowReplyButton();
        else
            Finish();
    }

    private void ShowReplyButton()
    {
        if (sendButton != null) sendButton.gameObject.SetActive(false);

        if (replyButton != null)
            replyButton.gameObject.SetActive(true);
        else if (sendButton != null)
            sendButton.gameObject.SetActive(true);   // 没配回复按钮就继续用发送按钮
    }

    private void Finish()
    {
        playing = false;

        if (hideButtonOnFinish)
        {
            if (sendButton != null) sendButton.gameObject.SetActive(false);
            if (replyButton != null) replyButton.gameObject.SetActive(false);
        }

        onFinished?.Invoke();
    }

    private void HideAllImages()
    {
        if (steps == null) return;

        for (int i = 0; i < steps.Length; i++)
        {
            ChatStep step = steps[i];
            if (step == null) continue;

            if (step.playerMessage != null)
                step.playerMessage.SetActive(false);

            if (step.npcReplies != null)
            {
                for (int j = 0; j < step.npcReplies.Length; j++)
                    if (step.npcReplies[j] != null)
                        step.npcReplies[j].SetActive(false);
            }
        }
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void OnDisable()
    {
        StopRoutine();
        playing = false;
    }
}