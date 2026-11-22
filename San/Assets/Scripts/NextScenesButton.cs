using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NextSceneButton : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("从 Project 窗口把目标场景文件拖到这里")]
    [SerializeField] private SceneAsset targetScene;
#endif

    [HideInInspector, SerializeField] private string targetSceneName;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetScene != null)
            targetSceneName = targetScene.name;
    }
#endif

    public void GoToNextScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("没有拖入目标场景，请检查 Inspector 的 Target Scene");
            return;
        }

        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(targetSceneName);
        else
            SceneManager.LoadScene(targetSceneName);
    }
}