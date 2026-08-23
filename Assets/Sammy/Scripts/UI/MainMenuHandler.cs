using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _quitButton;

    private const string PLAY_SCENE_STRING = "goldscene";

    private void Awake()
    {
        _startButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene(PLAY_SCENE_STRING);
        });

        _quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }
}
