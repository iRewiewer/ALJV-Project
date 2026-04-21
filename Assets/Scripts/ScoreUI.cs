using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
	public TextMeshProUGUI scoreText;
	public TextMeshProUGUI speedText;
	public TextMeshProUGUI endText;
	public Player player;

	void Update()
	{
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
}
