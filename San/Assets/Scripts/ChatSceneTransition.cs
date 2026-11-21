using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 和母亲 / 朋友聊完（ChatSequence.onFinished）后：
/// 渐暗 → 关掉 2D 聊天界面 → 切到 Camera5 → 渐显出 3D 场景。
/// 挂在常驻物体（比如 Camera5）上即可，会自动监听场景里所有 ChatSequence。
/// </summary>
public class ChatSceneTransition : MonoBehaviour
{
    [Header("镜头")]
    [Tooltip("过渡后要显示的镜头，不填会自动按名字找 Camera5")]
    public Camera targetCamera;

    [Tooltip("Target Camera 留空时按这些名字自动找")]
    public string[] targetCameraNames =
        new string[] { "Camera5", "Camera 5", "Camera_5", "镜头5" };

    [Header("结束时隐藏的面板")]
    [Tooltip("聊天结束后额外关掉的 2D 面板（比如“手机场景”）；刚才聊完的那个聊天面板会自动关掉")]
    public GameObject[] panelsToHide;

    [Header("过渡后显示")]
    [Tooltip("切到目标镜头后，重新显示背包图标和已获得的情绪球")]
    public bool showBackpackAndBalls = true;

    [Header("对话后 2D 碎碎念弹窗")]
    [Tooltip("通用弹窗容器，可留空")]
    public GameObject thinkPanel;

    [Tooltip("和母亲对话结束后用的弹窗（可留空，默认用 Think Panel）")]
    public GameObject motherThinkPanel;

    [Tooltip("和朋友对话结束后用的弹窗（可留空，默认用 Think Panel）")]
    public GameObject friendThinkPanel;

    [Tooltip("和母亲对话结束后显示的文案物体")]
    public GameObject motherThinkText;

    [Tooltip("和朋友对话结束后显示的文案物体")]
    public GameObject friendThinkText;

    [Tooltip("弹窗显示时长（秒）")]
    public float thinkSeconds = 3f;

    [Header("对话结束情绪球")]
    [Tooltip("母亲聊天面板，留空按名字自动识别")]
    public ChatSequence motherChat;

    [Tooltip("朋友聊天面板，留空按名字自动识别")]
    public ChatSequence friendChat;

    [Tooltip("母亲对话结束后播放「内耗」情绪球")]
    public bool playInternalFrictionAfterMother = true;

    [Tooltip("朋友对话结束后播放「得到释放」情绪球")]
    public bool playReleaseAfterFriend = true;

    [Tooltip("碎碎念弹窗消失后，等多久再弹情绪球（秒）")]
    public float storyBallDelaySeconds = 0.3f;

    [Header("过渡")]
    [Tooltip("最后一句消息出现后，等多久再开始渐暗（秒），方便看完最后一句")]
    public float finishDelaySeconds = 1.2f;

    [Tooltip("渐暗时长（秒）")]
    public float fadeOutSeconds = 0.35f;

    [Tooltip("全黑停留时长（秒）")]
    public float holdBlackSeconds = 0.15f;

    [Tooltip("渐显时长（秒）")]
    public float fadeInSeconds = 1.2f;

    [Tooltip("渐显时画面开始淡入的颜色，一般保持黑色")]
    public Color fadeColor = Color.black;

    [Tooltip("可留空，留空会自动创建一个全屏遮罩")]
    public Image fadeImage;

    [Tooltip("只播放一次")]
    public bool playOnlyOnce = true;

    [Header("玩家控制")]
    [Tooltip("过渡后用 Camera5 看场景时，可勾选禁止玩家再走动")]
    public bool disablePlayerControl = false;

    public bool debugLog = true;

    private GameObject fadeRoot;
    private bool playing;
    private bool played;

    // ====================================================================

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = FindTargetCamera();

        if (fadeImage != null)
            fadeRoot = fadeImage.gameObject;
    }

    private void Start()
    {
        ChatSequence[] chats = FindObjectsOfType<ChatSequence>(true);

        for (int i = 0; i < chats.Length; i++)
        {
            ChatSequence chat = chats[i];

            if (chat == null || chat.onFinished == null)
                continue;

            ChatSequence captured = chat;
            chat.onFinished.AddListener(() => Play(captured));
        }

        if (debugLog)
        {
            Debug.Log(
                "[聊天过渡] 已监听 " + chats.Length +
                " 个聊天流程，结束镜头=" +
                (targetCamera != null ? targetCamera.name : "未找到"),
                this);
        }
    }

    // 供 UnityEvent 直接调用（不带参数时只切场景）
    public void Play()
    {
        Play(null);
    }

    /// <summary>聊天结束后调用。</summary>
    public void Play(ChatSequence finishedChat)
    {
        if (playing) return;
        if (playOnlyOnce && played) return;

        StartCoroutine(Run(finishedChat));
    }

    private IEnumerator Run(ChatSequence finishedChat)
    {
        playing = true;
        played = true;

        // 2D 界面可能是暂停状态，进 3D 前恢复
        Time.timeScale = 1f;

        // 留一点时间给玩家看完最后一句
        if (finishDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(finishDelaySeconds);

        Image img = EnsureFadeImage();

        if (img == null)
        {
            Debug.LogError("[聊天过渡] 创建全屏遮罩失败，直接切镜头", this);
            HidePanels(finishedChat);
            SwitchToTargetCamera();
            playing = false;
            yield break;
        }

        fadeRoot.SetActive(true);
        SetAlpha(img, 0f);

        // 1. 画面渐暗
        yield return FadeTo(img, 1f, fadeOutSeconds);

        // 2. 关掉 2D 聊天界面
        HidePanels(finishedChat);

        // 3. 切到 Camera5
        SwitchToTargetCamera();

        // 4. 显示背包图标和之前拿到的情绪球
        if (showBackpackAndBalls)
            ShowBackpackAndBalls();

        if (holdBlackSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdBlackSeconds);

        // 5. 渐渐显出 3D 场景
        yield return FadeTo(img, 0f, fadeInSeconds);

        if (fadeRoot != null)
            fadeRoot.SetActive(false);

        // 6. 弹 2D 碎碎念（母亲 / 朋友各一句）
        if (finishedChat != null)
            yield return ShowThinkPopup(finishedChat);

        // 7. 根据聊完的人播放不同的情绪球
        if (finishedChat != null)
        {
            if (storyBallDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(
                    storyBallDelaySeconds);

            PlayStoryBall(finishedChat);
        }

        if (debugLog)
            Debug.Log("[聊天过渡] 过渡完成", this);

        playing = false;
    }

    // 母亲 / 朋友对话结束各弹一句属于自己的 2D 碎碎念
    private IEnumerator ShowThinkPopup(ChatSequence chat)
    {
        bool isMother = IsMotherChat(chat);
        bool isFriend = IsFriendChat(chat);

        if (!isMother && !isFriend)
            yield break;

        GameObject panel = isMother
            ? (motherThinkPanel != null ? motherThinkPanel : thinkPanel)
            : (friendThinkPanel != null ? friendThinkPanel : thinkPanel);

        GameObject text = isMother
            ? motherThinkText
            : friendThinkText;

        if (panel == null && text == null)
            yield break;

        if (panel != null)
            ActivateChain(panel.transform);

        if (motherThinkText != null)
        {
            ActivateChain(motherThinkText.transform);
            motherThinkText.SetActive(isMother);
        }

        if (friendThinkText != null)
        {
            ActivateChain(friendThinkText.transform);
            friendThinkText.SetActive(isFriend);
        }

        if (thinkSeconds > 0f)
            yield return new WaitForSecondsRealtime(thinkSeconds);

        if (motherThinkText != null)
            motherThinkText.SetActive(false);

        if (friendThinkText != null)
            friendThinkText.SetActive(false);

        HideThinkPanels();

        if (debugLog)
        {
            Debug.Log(
                "[聊天过渡] 已弹出 2D 碎碎念：" +
                (isMother ? "母亲" : "朋友"),
                this);
        }
    }

    private void HideThinkPanels()
    {
        if (thinkPanel != null)
            thinkPanel.SetActive(false);

        if (motherThinkPanel != null)
            motherThinkPanel.SetActive(false);

        if (friendThinkPanel != null)
            friendThinkPanel.SetActive(false);
    }

    // 母亲 → 内耗球；朋友 → 得到释放球
    private void PlayStoryBall(ChatSequence chat)
    {
        EmotionRewardFly emotion =
            FindObjectOfType<EmotionRewardFly>(true);

        if (emotion == null)
        {
            Debug.LogWarning(
                "[聊天过渡] 场景里没找到 EmotionRewardFly，" +
                "情绪球不会播放",
                this);
            return;
        }

        if (IsMotherChat(chat))
        {
            if (playInternalFrictionAfterMother)
                emotion.PlayInternalFrictionBall();

            return;
        }

        if (IsFriendChat(chat) && playReleaseAfterFriend)
            emotion.PlayReleaseBall();
    }

    private bool IsMotherChat(ChatSequence chat)
    {
        if (motherChat != null)
            return chat == motherChat;

        string name = chat.gameObject.name;

        return name.Contains("母亲") ||
               name.Contains("妈妈") ||
               name.Contains("Mother");
    }

    private bool IsFriendChat(ChatSequence chat)
    {
        if (friendChat != null)
            return chat == friendChat;

        string name = chat.gameObject.name;

        return name.Contains("朋友") ||
               name.Contains("Friend");
    }

    // ====================================================================

    private void HidePanels(ChatSequence finishedChat)
    {
        if (finishedChat != null)
            finishedChat.gameObject.SetActive(false);

        if (panelsToHide == null) return;

        for (int i = 0; i < panelsToHide.Length; i++)
        {
            if (panelsToHide[i] != null)
                panelsToHide[i].SetActive(false);
        }
    }

    private void SwitchToTargetCamera()
    {
        if (targetCamera == null)
            targetCamera = FindTargetCamera();

        if (targetCamera == null)
        {
            Debug.LogError("[聊天过渡] 没找到 Camera5，请拖到 Target Camera", this);
            return;
        }

        ActivateChain(targetCamera.transform);

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Camera current = all[i];

            if (current == null || current == targetCamera) continue;

            current.enabled = false;
        }

        targetCamera.enabled = true;

        if (disablePlayerControl)
        {
            PlayerMover mover = FindObjectOfType<PlayerMover>();

            if (mover != null)
            {
                mover.SetControl(false, false);
                mover.LockCursor(false);
            }
        }

        if (debugLog)
            Debug.Log("[聊天过渡] 已切换到镜头：" + targetCamera.name, this);
    }

    // 重新显示背包图标，让之前获得的情绪球跟随目标镜头显示
    private void ShowBackpackAndBalls()
    {
        EmotionRewardFly emotion =
            FindObjectOfType<EmotionRewardFly>(true);

        if (emotion == null)
        {
            Debug.LogWarning(
                "[聊天过渡] 场景里没找到 EmotionRewardFly，" +
                "背包图标不会显示",
                this);
            return;
        }

        emotion.ShowBackpackWithBalls();
    }

    private Camera FindTargetCamera()
    {
        if (targetCameraNames == null || targetCameraNames.Length == 0)
            return null;

        Camera[] all = FindObjectsOfType<Camera>(true);

        for (int n = 0; n < targetCameraNames.Length; n++)
        {
            string wanted = targetCameraNames[n];

            if (string.IsNullOrEmpty(wanted)) continue;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;

                if (string.Equals(
                        all[i].name.Trim(),
                        wanted.Trim(),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    // ====================================================================
    // 全屏遮罩
    // ====================================================================

    private Image EnsureFadeImage()
    {
        if (fadeImage != null)
        {
            if (fadeRoot == null)
                fadeRoot = fadeImage.gameObject;

            return fadeImage;
        }

        GameObject canvasGO = new GameObject("ChatTransitionFadeCanvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        GameObject imageGO = new GameObject("Fade");
        imageGO.transform.SetParent(canvasGO.transform, false);

        fadeImage = imageGO.AddComponent<Image>();
        fadeImage.raycastTarget = true;

        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeRoot = canvasGO;
        fadeRoot.SetActive(false);

        return fadeImage;
    }

    private void SetAlpha(Image img, float alpha)
    {
        Color color = fadeColor;
        color.a = alpha;
        img.color = color;
    }

    private IEnumerator FadeTo(Image img, float targetAlpha, float duration)
    {
        if (img == null) yield break;

        float startAlpha = img.color.a;

        if (duration <= 0f)
        {
            SetAlpha(img, targetAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(img, Mathf.Lerp(startAlpha, targetAlpha, t));

            yield return null;
        }

        SetAlpha(img, targetAlpha);
    }

    private static void ActivateChain(Transform target)
    {
        while (target != null)
        {
            if (!target.gameObject.activeSelf)
                target.gameObject.SetActive(true);

            target = target.parent;
        }
    }
}
