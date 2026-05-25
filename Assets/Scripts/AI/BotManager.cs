using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
	public string botNamePrefix = "Bot";

	[Header("Finish Line")]
	public GameObject finishLinePrefab;
	public Transform finishLineTarget;

	[Header("References")]
	public Transform cameraRig;

	[Header("Camera View")]
	public int viewedBotIndex = -1;
	public KeyCode nextBotViewKey = KeyCode.Y;
	public KeyCode previousBotViewKey = KeyCode.T;

	[Header("RL Training Files")]
	public bool saveQTable = true;
	[Min(0)]
	public int trainingDataVersion = 6;
	public bool useVersionedTrainingFiles = true;
	public bool sharedRlTrainingTable = true;
	public bool learningEnabled = true;
	public bool evaluationMode;
	public bool loadBestQTableForEvaluation = true;
	public string qTableFileName = "q_table.json";
	public bool saveBestQTableSnapshot = true;
	public string bestQTableFileName = "q_table_best.json";
	public bool writeTrainingLogCsv = true;
	public string trainingLogFileName = "training_log.csv";
	public string evaluationLogFileName = "evaluation_log.csv";
	public bool saveTrainingFilesInProjectRoot = true;

	[Header("RL Exploration / Evaluation")]
	[FormerlySerializedAs("explorationRate")]
	[Range(0f, 1f)]
	public float epsilon = 1f;
	[Range(0f, 1f)]
	public float evaluationEpsilon = 0.05f;
	[Range(0f, 1f)]
	public float explorationDecayPerEpisode = 0.995f;
	[Range(0f, 1f)]
	public float minimumExplorationRate = 0.25f;
	public bool overrideLoadedExplorationRate;

	[Header("Debug")]
	public bool showBotAiGizmos = true;

	private readonly List<GameObject> bots = new();
	private Transform playerCameraTarget;
	private int nextBotSerial = 1;

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
		AssignBotIdentity(botObject);
		ApplyRlTrainingSettings(botObject);
		bots.Add(botObject);

		Bot bot = botObject.GetComponent<Bot>();

		if (bot == null)
		{
			Debug.LogWarning("BotManager: Spawned prefab does not have Bot.cs attached.");
			return;
		}

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

	private void Start()
	{
		ApplyRlTrainingSettingsToSceneBots();
	}

	private void Update()
	{
		if (Input.GetKeyDown(nextBotViewKey))
			SwitchViewToNextBot();

		if (Input.GetKeyDown(previousBotViewKey))
			SwitchViewToPreviousBot();
	}

	private void OnValidate()
	{
		trainingDataVersion = Mathf.Max(0, trainingDataVersion);
		epsilon = Mathf.Clamp01(epsilon);
		evaluationEpsilon = Mathf.Clamp01(evaluationEpsilon);

		if (Application.isPlaying)
			ApplyRlTrainingSettingsToSceneBots();
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
		SwitchViewToNextBot();
	}

	public void SwitchViewToNextBot()
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

	public void SwitchViewToPreviousBot()
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

		if (viewedBotIndex < 0)
		{
			viewedBotIndex = bots.Count - 1;
		}
		else
		{
			viewedBotIndex--;
		}

		if (viewedBotIndex < 0)
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

	private void ApplyRlTrainingSettingsToSceneBots()
	{
		RLBotBrain[] rlBrains = FindObjectsByType<RLBotBrain>(
			FindObjectsInactive.Exclude,
			FindObjectsSortMode.None
		);

		foreach (RLBotBrain rlBrain in rlBrains)
		{
			if (rlBrain != null)
			{
				AssignBotIdentity(rlBrain.gameObject);
				ConfigureBotAgent(rlBrain.GetComponent<BotAgent>());
				ConfigureRlBrain(rlBrain);
			}
		}
	}

	private void ApplyRlTrainingSettings(GameObject botObject)
	{
		if (botObject == null)
			return;

		ConfigureBotAgent(botObject.GetComponent<BotAgent>());

		RLBotBrain rlBrain = botObject.GetComponent<RLBotBrain>();

		if (rlBrain != null)
			ConfigureRlBrain(rlBrain);
	}

	private void ConfigureBotAgent(BotAgent botAgent)
	{
		if (botAgent == null)
			return;

		botAgent.SetFinishLineTarget(GetFinishLineTarget());
	}

	private void ConfigureRlBrain(RLBotBrain rlBrain)
	{
		bool effectiveLearningEnabled = learningEnabled && !evaluationMode;
		bool useEvaluationTable = evaluationMode && loadBestQTableForEvaluation;
		string activeQTableFileName = useEvaluationTable ? GetBestQTableFileName() : GetQTableFileName();
		string activeTrainingLogFileName = evaluationMode ? GetEvaluationLogFileName() : GetTrainingLogFileName();

		rlBrain.learningEnabled = effectiveLearningEnabled;

		rlBrain.ConfigurePersistence(
			saveQTable,
			sharedRlTrainingTable,
			activeQTableFileName,
			saveBestQTableSnapshot && effectiveLearningEnabled,
			GetBestQTableFileName(),
			writeTrainingLogCsv,
			activeTrainingLogFileName,
			saveTrainingFilesInProjectRoot,
			evaluationMode
		);

		rlBrain.ConfigureExploration(
			evaluationMode ? evaluationEpsilon : epsilon,
			explorationDecayPerEpisode,
			minimumExplorationRate,
			evaluationMode || overrideLoadedExplorationRate || !saveQTable || rlBrain.LearnedStateCount == 0
		);
	}

	private void AssignBotIdentity(GameObject botObject)
	{
		if (botObject == null)
			return;

		RLBotBrain rlBrain = botObject.GetComponent<RLBotBrain>();

		if (rlBrain != null && rlBrain.HasTrainingIdentity)
		{
			ApplyBotDisplayName(botObject, rlBrain.TrainingBotName);
			return;
		}

		int serial = nextBotSerial++;
		string displayName = $"{GetBotNamePrefix()} {serial:00}";
		string trainingId = $"B{serial:00}";

		if (rlBrain != null)
			rlBrain.ConfigureTrainingIdentity(displayName, trainingId);

		ApplyBotDisplayName(botObject, displayName);
	}

	private void ApplyBotDisplayName(GameObject botObject, string displayName)
	{
		if (botObject == null || string.IsNullOrWhiteSpace(displayName))
			return;

		botObject.name = displayName;

		Bot bot = botObject.GetComponent<Bot>();

		if (bot != null)
			bot.SetBotName(displayName, GetMainCamera());
	}

	private string GetBotNamePrefix()
	{
		return string.IsNullOrWhiteSpace(botNamePrefix) ? "Bot" : botNamePrefix.Trim();
	}

	private string GetQTableFileName()
	{
		if (!useVersionedTrainingFiles)
			return qTableFileName;

		return $"q_table_v{trainingDataVersion}.json";
	}

	private string GetBestQTableFileName()
	{
		if (!useVersionedTrainingFiles)
			return bestQTableFileName;

		return $"q_table_v{trainingDataVersion}_best.json";
	}

	private string GetTrainingLogFileName()
	{
		if (!useVersionedTrainingFiles)
			return trainingLogFileName;

		return $"training_log_v{trainingDataVersion}.csv";
	}

	private string GetEvaluationLogFileName()
	{
		if (!useVersionedTrainingFiles)
			return evaluationLogFileName;

		return $"evaluation_log_v{trainingDataVersion}.csv";
	}

	private Transform GetFinishLineTarget()
	{
		if (finishLineTarget != null)
			return finishLineTarget;

		if (finishLinePrefab != null && finishLinePrefab.scene.IsValid() && finishLinePrefab.scene.isLoaded)
		{
			finishLineTarget = finishLinePrefab.transform;
			return finishLineTarget;
		}

		FinishTrigger sceneFinish = FindFirstObjectByType<FinishTrigger>();

		if (sceneFinish != null)
		{
			finishLineTarget = sceneFinish.transform;
			return finishLineTarget;
		}

		if (finishLinePrefab != null && RaceManager.Instance != null && RaceManager.Instance.finishPoint != null)
		{
			GameObject finishLineObject = Instantiate(
				finishLinePrefab,
				RaceManager.Instance.finishPoint.position,
				RaceManager.Instance.finishPoint.rotation
			);

			finishLineTarget = finishLineObject.transform;
			RaceManager.Instance.finishPoint = finishLineTarget;
			return finishLineTarget;
		}

		if (RaceManager.Instance != null && RaceManager.Instance.finishPoint != null)
		{
			finishLineTarget = RaceManager.Instance.finishPoint;
			return finishLineTarget;
		}

		return null;
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
