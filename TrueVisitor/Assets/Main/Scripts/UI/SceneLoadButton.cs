using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SceneLoadButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "MorningSegment";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(LoadScene);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(LoadScene);
        }
    }

    private void LoadScene()
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            sceneName = "MorningSegment";
        }
    }
}
