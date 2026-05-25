using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ScoreUI : MonoBehaviour
{
	public TextMeshProUGUI scoreText;
	public TextMeshProUGUI speedText;
	public TextMeshProUGUI endText;
	public Slider timeScaleSlider;
	public TextMeshProUGUI timeScaleText;
	public Player player;

	void Start()
	{
		if (timeScaleSlider != null)
		{
			timeScaleSlider.onValueChanged.AddListener(SetGameplayTimeScale);

			if (RaceManager.Instance != null)
				timeScaleSlider.SetValueWithoutNotify(RaceManager.Instance.gameplayTimeScale);
		}

		UpdateTimeScaleText();
	}

	void Update()
	{
		UpdateTimeScaleText();

		if (RaceManager.Instance == null || scoreText == null || speedText == null || endText == null || player == null)
			return;

		int coins = RaceManager.Instance.score;
		int coinsMax = RaceManager.Instance.totalCoins;

		int cp = RaceManager.Instance.passedCheckpoints;
		int cpMax = RaceManager.Instance.totalCheckpoints;

		float speed = player.currentSpeed;
		float t = RaceManager.Instance.elapsedTime;

		scoreText.text =
			$"Coins: {coins}/{coinsMax}\n" +
			$"Checkpoints: {cp}/{cpMax}\n" +
			(RaceManager.Instance.finished ? "\n\nGG! Press P to restart" : "");

		speedText.text = $"Speed: {speed:0.0}\n" +
			$"Time: {t:0.0}s";

		endText.enabled = RaceManager.Instance.finished;
	}

	public void SetGameplayTimeScale(float value)
	{
		if (RaceManager.Instance != null)
			RaceManager.Instance.SetGameplayTimeScale(value);

		UpdateTimeScaleText();
	}

	private void UpdateTimeScaleText()
	{
		if (RaceManager.Instance == null)
			return;

		if (timeScaleSlider != null && !Mathf.Approximately(timeScaleSlider.value, RaceManager.Instance.gameplayTimeScale))
			timeScaleSlider.SetValueWithoutNotify(RaceManager.Instance.gameplayTimeScale);

		if (timeScaleText != null)
			timeScaleText.text = $"{RaceManager.Instance.gameplayTimeScale:0.0}x";
	}
}
