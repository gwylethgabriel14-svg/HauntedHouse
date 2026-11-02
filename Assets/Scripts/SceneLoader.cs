using UnityEngine;
using UnityEngine.SceneManagement;

// Used by the main menu buttons and the Main Menu button on the win/lose panels.
public class SceneLoader : MonoBehaviour
{
    public string gameScene = "House";
    public string menuScene = "MainMenu";

    public void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameScene);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(menuScene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
