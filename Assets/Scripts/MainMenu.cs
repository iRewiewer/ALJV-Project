using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
	public string gameSceneName = "Game";

	public void Play()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene("Scenes/Game");
	}

	public void Quit()
	{
		Application.Quit();
	}
}
