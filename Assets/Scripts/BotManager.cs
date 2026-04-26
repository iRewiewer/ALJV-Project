using System.Collections.Generic;
using UnityEngine;

public enum BotOp
{
	Add,
	Remove
}

public class BotManager : MonoBehaviour
{
	[Header("Bot Setup")]
	public GameObject botPrefab;
	public Transform botSpawnPoint;

	[Header("References")]
	public Transform cameraRig;

	private readonly List<GameObject> bots = new();

	public int MAX_BOTS = 10;

	public void SpawnBot()
	{
		if (botPrefab == null)
		{
			Debug.LogError("BotManager: Bot Prefab is not assigned.");
			return;
		}

		Vector3 spawnPosition = GetBotSpawnPosition();
		Quaternion spawnRotation = GetBotSpawnRotation();

		GameObject botObject = Instantiate(botPrefab, spawnPosition, spawnRotation);
		bots.Add(botObject);

		Bot bot = botObject.GetComponent<Bot>();

		if (bot == null)
		{
			Debug.LogWarning("BotManager: Spawned prefab does not have Bot.cs attached.");
			return;
		}

		bot.SetBotName($"Bot {bots.Count}", GetMainCamera());

		// Temporary Demo 2 behaviour.
		// Later this will be replaced by checkpoint/FSM/Q-learning logic.
		bot.SetAiInput(0f, 0f, true, false);
	}

	public void RemoveBot()
	{
		if (bots.Count == 0)
			return;

		int lastIndex = bots.Count - 1;
		GameObject botObject = bots[lastIndex];

		bots.RemoveAt(lastIndex);

		if (botObject != null)
			Destroy(botObject);
	}

	public void RemoveAllBots()
	{
		foreach (GameObject botObject in bots)
		{
			if (botObject != null)
				Destroy(botObject);
		}

		bots.Clear();
	}

	public void HandleBots(BotOp operation, int count)
	{
		for (int i = 0; i < count; i++)
		{
			if (operation == BotOp.Add)
			{
				SpawnBot();
			}
			else
			{
				RemoveBot();
			}
		}
	}

	public void SetSpawnPoint(Transform newSpawnPoint)
	{
		botSpawnPoint = newSpawnPoint;
	}

	public void ResetBotsToSpawnPoint()
	{
		foreach (GameObject botObject in bots)
		{
			if (botObject == null)
				continue;

			botObject.transform.position = GetBotSpawnPosition();
			botObject.transform.rotation = GetBotSpawnRotation();

			Bot bot = botObject.GetComponent<Bot>();

			if (bot != null)
				bot.ResetSpeedToDefault();
		}
	}

	private Vector3 GetBotSpawnPosition()
	{
		if (botSpawnPoint != null)
			return botSpawnPoint.position;

		return transform.position;
	}

	private Quaternion GetBotSpawnRotation()
	{
		if (botSpawnPoint != null)
			return botSpawnPoint.rotation;

		return transform.rotation;
	}

	private Camera GetMainCamera()
	{
		if (cameraRig != null)
		{
			Camera cameraFromRig = cameraRig.GetComponentInChildren<Camera>();

			if (cameraFromRig != null)
				return cameraFromRig;
		}

		return Camera.main;
	}
}