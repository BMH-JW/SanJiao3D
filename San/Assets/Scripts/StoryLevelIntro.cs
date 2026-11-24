using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 从 2D 剧情界面进入 3D 关卡后，等 SceneFader 的开幕淡入真正结束，
/// 按顺序弹出自定义 2D 弹窗 prefab。
/// 弹窗 prefab 由你自己在 Inspector 拖入，可以是任意 UGUI 结构
/// （Image、Text、TMP_Text、挂脚本都行）。
/// 由 StorySequence 在切场景前调用 Request() 触发。
/// </summary>
public class StoryLevelIntro : MonoBehaviour
{
    [System.Serializable]
    public class DialoguePopup
    {
        [Tooltip("你自己制作的 2D 弹窗 prefab（UGUI）")]
        public GameObject popupPrefab;

        [Tooltip("上一个弹窗完全消失后，等多久才弹这一个（秒）。第一个 entry 忽略。")]
        public float delayAfterPrevious = 0.3f;

        [Tooltip("这个弹窗显示多久后自动销毁（秒）。0 或负数表示不自动销毁，等你自己管。")]
        public float duration = 2.5f;
    }

    [Header("弹窗序列（按顺序播放）")]
    public List<DialoguePopup> popups = new List<DialoguePopup>();

    [Header("挂载位置")]
    [Tooltip("弹窗实例化到哪个 Canvas 下。留空会自动找场景里的第一个 Canvas。")]
    public Transform popupParent;

    [Header("节奏")]
    [Tooltip("等待开幕淡入的最长时间（秒），超时就直接开始弹")]
    public float fadeTimeout = 5f;

    public bool debugLog = true;

    private static bool pending;
    private static bool requestLogged;

    /// <summary>切到 3D 关卡前调用：新场景加载后弹开场弹窗。</summary>
    public static void Request()
    {
        pending = true;
        requestLogged = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!pending) return;

        pending = false;

        GameObject host = new GameObject("StoryLevelIntro");
        host.AddComponent<StoryLevelIntro>();
    }

    private IEnumerator Start()
    {
        // 等 SceneFader 的开幕淡入结束；没有 SceneFader 或不在过渡中就直接开始
        float timeout = Mathf.Max(0.1f, fadeTimeout);

        while (timeout > 0f &&
               SceneFader.Instance != null &&
               SceneFader.Instance.IsTransitioning)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (debugLog && !requestLogged)
        {
            requestLogged = true;
            Debug.Log("[剧情开场] 开幕淡入结束，开始播放弹窗序列", this);
        }

        if (popups == null || popups.Count == 0)
        {
            Debug.LogWarning("[剧情开场] popups 列表为空，没东西可弹", this);
            yield break;
        }

        Transform parent = popupParent != null
            ? popupParent
            : FindObjectOfType<Canvas>()?.transform;

        if (parent == null)
        {
            Debug.LogError("[剧情开场] 没找到 Canvas，弹窗无处挂载", this);
            yield break;
        }

        for (int i = 0; i < popups.Count; i++)
        {
            DialoguePopup entry = popups[i];

            // 不是第一个才等 delay
            if (i > 0 && entry.delayAfterPrevious > 0f)
                yield return new WaitForSecondsRealtime(entry.delayAfterPrevious);

            if (entry.popupPrefab == null)
            {
                Debug.LogWarning($"[剧情开场] 第 {i + 1} 个 popup 的 prefab 为空，跳过", this);
                continue;
            }

            GameObject instance = Instantiate(entry.popupPrefab, parent);
            instance.name = entry.popupPrefab.name + "_Intro";

            if (debugLog)
                Debug.Log($"[剧情开场] 弹出第 {i + 1} 个: {entry.popupPrefab.name}", this);

            // 自动销毁
            if (entry.duration > 0f)
            {
                StartCoroutine(DestroyAfter(instance, entry.duration));
                yield return new WaitForSecondsRealtime(entry.duration);
            }
            else
            {
                // duration <= 0 表示交给用户自己管，不阻塞序列
                if (debugLog)
                    Debug.Log($"[剧情开场] 第 {i + 1} 个设为常驻，跳过等待", this);
            }
        }

        if (debugLog)
            Debug.Log("[剧情开场] 弹窗序列播放完毕", this);

        Destroy(gameObject);
    }

    private static IEnumerator DestroyAfter(GameObject target, float seconds)
    {
        if (target == null) yield break;
        yield return new WaitForSecondsRealtime(seconds);
        if (target != null) Destroy(target);
    }
}
