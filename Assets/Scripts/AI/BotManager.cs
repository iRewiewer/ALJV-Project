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

	[Header("Camera View")]
	public int viewedBotIndex = -1;

	[Header("Debug")]
	public bool showBotAiGizmos = true;

	private readonly List<GameObject> bots = new();
	private Transform playerCameraTarget;

	public int MAX_BOTS = 10;
	public int BotCount
	{
		get
		{
			RemoveMissingBots();
			return bots.Count;
		}
	}

	public void SpawnBot()
	{
		RemoveMissingBots();

		if (bots.Count >= MAX_BOTS)
			return;

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

		if (botObject.GetComponent<BotAgent>() == null)
		{
			// Fallback for simple movement prefab.
			bot.SetAiInput(0f, 0f, true, false);
		}
	}

	private void Awake()
	{
		BotAgent.showAiGizmos = showBotAiGizmos;
	}

	public GameObject GetBotObject(int index)
	{
		RemoveMissingBots();

		if (index < 0 || index >= bots.Count)
			return null;

		return bots[index];
	}

	public void SwitchViewToBot()
	{
		RemoveMissingBots();

		FollowCamera followCamera = GetFollowCamera();

		if (followCamera == null)
		{
			Debug.LogWarning("BotManager: No FollowCamera found.");
			return;
		}

		CachePlayerCameraTarget(followCamera);

		if (bots.Count == 0)
		{
			SwitchViewToPlayer();
			return;
		}

		viewedBotIndex++;

		if (viewedBotIndex >= bots.Count)
		{
			SwitchViewToPlayer();
			return;
		}

		GameObject botObject = bots[viewedBotIndex];

		if (botObject == null)
			return;

		followCamera.SetTarget(botObject.transform, true);
	}

	public void SwitchViewToPlayer()
	{
		FollowCamera followCamera = GetFollowCamera();

		if (followCamera == null)
			return;

		CachePlayerCameraTarget(followCamera);

		if (playerCameraTarget == null)
			return;

		viewedBotIndex = -1;
		followCamera.SetTarget(playerCameraTarget, true);
	}

	public void ToggleBotAIGizmos()
	{
		showBotAiGizmos = !showBotAiGizmos;
		BotAgent.showAiGizmos = showBotAiGizmos;
	}

	public void RemoveBot()
	{
		if (bots.Count == 0)
			return;

		int lastIndex = bots.Count - 1;
		GameObject botObject = bots[lastIndex];
		bool removingViewedBot = IsCameraTarget(botObject);

		bots.RemoveAt(lastIndex);

		if (botObject != null)
			Destroy(botObject);

		if (removingViewedBot)
			SwitchViewToPlayer();

		if (viewedBotIndex >= bots.Count)
			viewedBotIndex = bots.Count - 1;
	}

	public void RemoveAllBots()
	{
		SwitchViewToPlayer();

		foreach (GameObject botObject in bots)
		{
			if (botObject != null)
				Destroy(botObject);
		}

		bots.Clear();
		viewedBotIndex = -1;
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

	private FollowCamera GetFollowCamera()
	{
		if (cameraRig != null)
		{
			FollowCamera cameraFromRig = cameraRig.GetComponent<FollowCamera>();

			if (cameraFromRig != null)
				return cameraFromRig;

			cameraFromRig = cameraRig.GetComponentInParent<FollowCamera>();

			if (cameraFromRig != null)
				return cameraFromRig;
		}

		if (RaceManager.Instance != null && RaceManager.Instance.followCamera != null)
			return RaceManager.Instance.followCamera;

		return FindFirstObjectByType<FollowCamera>();
	}

	private void CachePlayerCameraTarget(FollowCamera followCamera)
	{
		if (playerCameraTarget != null)
			return;

		if (RaceManager.Instance != null && RaceManager.Instance.player != null)
		{
			playerCameraTarget = RaceManager.Instance.player;
			return;
		}

		if (followCamera != null)
			playerCameraTarget = followCamera.Target;
	}

	private bool IsCameraTarget(GameObject botObject)
	{
		if (botObject == null)
			return false;

		FollowCamera followCamera = GetFollowCamera();

		return followCamera != null && followCamera.Target == botObject.transform;
	}

	private void RemoveMissingBots()
	{
		for (int i = bots.Count - 1; i >= 0; i--)
		{
			if (bots[i] == null)
				bots.RemoveAt(i);
		}

		if (viewedBotIndex >= bots.Count)
			viewedBotIndex = bots.Count - 1;
	}
}
