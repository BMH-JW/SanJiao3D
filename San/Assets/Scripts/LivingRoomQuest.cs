using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 客厅限时找遥控板：
/// 进入 → 巡视一段时间 → 弹提示 → 弹任务面板 → 点确认开始倒计时（带音频）
/// → 时间到或找到遥控板结束。
/// 可设置一开始禁用触发，等手机关卡完成后再调用 EnableTrigger() 开启。
/// </summary>
public class LivingRoomQuest : MonoBehaviour
{
    [Header("触发")]
    [Tooltip("勾上：玩家进入触发区自动开始（本物体需要有 Collider 并勾 Is Trigger）")]
    public bool startOnPlayerEnter = false;

    [Tooltip("开始时禁用触发，等手机关卡完成后再调用 EnableTrigger() 开启")]
    public bool startDisabled = true;

    [Tooltip("触发用的 Collider；留空自动取本物体上的 Collider")]
    public Collider triggerCollider;

    [Tooltip("玩家的 Tag")]
    public string playerTag = "Player";

    [Tooltip("进入后先让玩家自由巡视多久（秒）")]
    public float lookAroundSeconds = 1.5f;

    [Header("提示")]
    [Tooltip("「诶，我遥控板去哪里了？！」这张提示")]
    public GameObject hintPanel;

    [Tooltip("提示显示多久（秒）")]
    public float hintSeconds = 2f;

    [Header("任务面板")]
    [Tooltip("任务面板（上面写「30 秒内找到遥控板」）")]
    public GameObject taskPanel;

    [Tooltip("任务面板上的确认按钮")]
    public Button confirmButton;

    [Tooltip("点确认后隐藏任务面板（倒计时改放到 HUD 上时勾选）")]
    public bool hideTaskPanelOnConfirm = false;

    [Header("倒计时")]
    [Tooltip("限时秒数")]
    public float timeLimit = 30f;

    [Tooltip("显示倒计时的 Text（放在任务面板合适位置）")]
    public Text countdownText;

    [Tooltip("倒计时文字格式，{0} 是剩余秒数")]
    public string countdownFormat = "剩余 {0} 秒";

    [Header("音频")]
    [Tooltip("倒计时循环音效（滴答声），留空则静音")]
    public AudioClip tickLoopClip;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("时间到的音效")]
    public AudioClip timeUpClip;

    [Header("玩家控制")]
    [Tooltip("任务面板弹出时冻结移动/解锁鼠标，确认后恢复")]
    public bool managePlayerControl = true;

    [Header("事件")]
    [Tooltip("倒计时结束（失败）时触发")]
    public UnityEvent onTimeUp;

    [Tooltip("提前找到遥控板、任务完成时触发")]
    public UnityEvent onQuestComplete;

    private AudioSource audioSource;
    private Coroutine introRoutine;
    private Coroutine timerRoutine;
    private bool started;
    private bool running;

    private void Awake()
    {
        if (hintPanel != null) hintPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        // 触发默认关闭，避免干扰前面的关卡
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (startDisabled && triggerCollider != null)
            triggerCollider.enabled = false;

        EnsureAudio();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!startOnPlayerEnter) return;
        if (other.CompareTag(playerTag)) StartQuest();
    }

    /// <summary>开启触发区（手机关卡完成后调用）。</summary>
    public void EnableTrigger()
    {
        gameObject.SetActive(true);

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.enabled = true;
    }

    /// <summary>关闭触发区。</summary>
    public void DisableTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.enabled = false;
    }

    /// <summary>开始整个流程（也可由外部触发器调用）。</summary>
    public void StartQuest()
    {
        if (started) return;
        started = true;

        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 1. 巡视
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, lookAroundSeconds));

        // 2. 提示
        if (hintPanel != null) hintPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hintSeconds));
        if (hintPanel != null) hintPanel.SetActive(false);

        // 3. 任务面板
        if (taskPanel != null) taskPanel.SetActive(true);
        if (managePlayerControl) SetPlayerControl(false);

        introRoutine = null;
    }

    /// <summary>确认按钮。</summary>
    public void OnConfirmClicked()
    {
        if (running) return;

        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        if (hideTaskPanelOnConfirm && taskPanel != null)
            taskPanel.SetActive(false);

        if (managePlayerControl) SetPlayerControl(true);

        StartTimer();
    }

    public void StartTimer()
    {
        if (running) return;

        // 确认后才显示倒计时（它必须放在任务面板外面，面板收起后才看得见）
        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        running = true;

        float timeLeft = timeLimit;
        UpdateCountdown(timeLeft);
        PlayTick();

        while (timeLeft > 0f)
        {
            timeLeft -= Time.unscaledDeltaTime;
            if (timeLeft < 0f) timeLeft = 0f;

            UpdateCountdown(timeLeft);
            yield return null;
        }

        running = false;
        timerRoutine = null;

        StopTick();

        if (timeUpClip != null && audioSource != null)
            audioSource.PlayOneShot(timeUpClip, volume);

        onTimeUp?.Invoke();
    }

    /// <summary>找到遥控板时调用，结束任务。</summary>
    public void CompleteQuest()
    {
        if (!running) return;

        running = false;

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }

        StopTick();

        if (countdownText != null)
            countdownText.text = "完成";

        onQuestComplete?.Invoke();
    }

    private void UpdateCountdown(float seconds)
    {
        if (countdownText != null)
            countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(seconds));
    }

    private void SetPlayerControl(bool enable)
    {
        PlayerMover pm = FindObjectOfType<PlayerMover>(true);
        if (pm == null) return;

        pm.SetControl(enable, enable);
        pm.LockCursor(enable);
    }

    private void EnsureAudio()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    private void PlayTick()
    {
        if (audioSource == null || tickLoopClip == null) return;

        audioSource.clip = tickLoopClip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.Play();
    }

    private void StopTick()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}
