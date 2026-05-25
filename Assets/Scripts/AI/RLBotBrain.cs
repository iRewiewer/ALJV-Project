using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class RLBotBrain : MonoBehaviour
{
    [Header("RL Basics")]
    public float decisionInterval = 0.4f;
    public float explorationRate = 1f;
    public float learningRate = 0.1f;
    public float discountFactor = 0.9f;
    public float explorationDecayPerEpisode = 0.995f;
    public float minimumExplorationRate = 0.25f;

    [Header("State Binning")]
    public float closeCheckpointDistance = 140f;
    public float mediumCheckpointDistance = 320f;
    public float stuckRiskTime = 15f;
    public float stuckDangerTime = 30f;

    [Header("Steering")]
    public float yawSensitivity = 3f;
    public float pitchSensitivity = 2f;
    public float maxYawInput = 1f;
    public float maxPitchInput = 0.45f;
    public float actionYawBias = 0.35f;
    public float hardTurnYawBias = 0.85f;
    public float coastPitchMultiplier = 0.7f;
    public float brakePitchMultiplier = 0.25f;
    public float recoveryPitchMultiplier = 0.25f;

    [Header("Training Episodes")]
    public bool trainingEnabled = true;
    public bool learningEnabled = true;
    public float maxEpisodeDuration = 300f;
    public bool endEpisodeAfterAllCheckpoints = true;
    public float stuckEpisodeTimeout = 45f;

    [Header("Training Logs")]
    public bool logEpisodesToConsole = true;
    public bool writeTrainingLogCsv = true;
    public bool saveTrainingFilesInProjectRoot = true;
    public string trainingLogFileName = "training_log.csv";

    [Header("Q-Table Persistence")]
    public bool saveQTable = false;
    public bool useSharedQTable = true;
    public string qTableFileName = "q_table.json";
    public bool saveBestQTableSnapshot = true;
    public string bestQTableFileName = "q_table_best.json";

    [Header("Training Identity")]
    public string trainingBotId;
    public string trainingBotName;

    [Header("Debug")]
    public bool debugLogs = false;

    private const int ActionCount = 8;
    private const int QTableVersion = 6;

    private enum BotAction
    {
        AccelerateForward = 0,
        TurnLeftAndAccelerate = 1,
        TurnRightAndAccelerate = 2,
        Brake = 3,
        HardLeft = 4,
        HardRight = 5,
        Coast = 6,
        RecoveryTurn = 7
    }

    private readonly Dictionary<string, float[]> qTable = new Dictionary<string, float[]>();
    private static readonly Dictionary<string, SharedTrainingState> sharedTrainingStates = new Dictionary<string, SharedTrainingState>();

    private class SharedTrainingState
    {
        public readonly Dictionary<string, float[]> QTable = new Dictionary<string, float[]>();
        public int EpisodeNumber = 1;
        public float ExplorationRate = 1f;
        public int BestCheckpointsReached;
        public float BestEpisodeReward = float.MinValue;
        public bool Loaded;
    }

    [System.Serializable]
    private class SavedQTable
    {
        public int version = QTableVersion;
        public int actionCount;
        public int episodeNumber;
        public float epsilon;
        public int bestCheckpointsReached;
        public float bestEpisodeReward;
        public List<SavedQState> states = new List<SavedQState>();
    }

    [System.Serializable]
    private class SavedQState
    {
        public string stateKey;
        public float[] actionValues;
    }

    private float decisionTimer;
    private string currentStateKey;
    private BotAction selectedAction;
    private string previousStateKey;
    private BotAction previousAction;
    private float previousReward;
    private bool hasPreviousDecision;
    private float lastYawDirection = 1f;
    private int episodeNumber = 1;
    private float episodeDuration;
    private int lastObservedCheckpointCount;
    private float timeSinceCheckpointProgress;
    private string trainingLogPath;
    private string qTablePath;
    private string bestQTablePath;
    private SharedTrainingState sharedTrainingState;
    private int bestCheckpointsReached;
    private float bestEpisodeReward = float.MinValue;
    private bool episodeStarted;
    private bool qTableLoaded;
    private bool resetEpisodeCounterAfterQTableLoad;

    public int EpisodeNumber => ActiveEpisodeNumber;
    public float EpisodeDuration => episodeDuration;
    public float Epsilon => ActiveExplorationRate;
    public int LearnedStateCount => ActiveQTable.Count;
    public string CurrentStateKey => currentStateKey;
    public string CurrentActionName => selectedAction.ToString();
    public string TrainingLogPath => trainingLogPath;
    public string QTablePath => qTablePath;
    public bool IsUsingSharedQTable => useSharedQTable;
    public bool IsLearningEnabled => learningEnabled;
    public float TimeSinceCheckpointProgress => timeSinceCheckpointProgress;
    public string TrainingBotId => string.IsNullOrWhiteSpace(trainingBotId) ? gameObject.name : trainingBotId;
    public string TrainingBotName => string.IsNullOrWhiteSpace(trainingBotName) ? gameObject.name : trainingBotName;
    public bool HasTrainingIdentity => !string.IsNullOrWhiteSpace(trainingBotId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedTrainingStates()
    {
        sharedTrainingStates.Clear();
    }

    private bool UsesSharedTraining => useSharedQTable && sharedTrainingState != null;
    private Dictionary<string, float[]> ActiveQTable => UsesSharedTraining ? sharedTrainingState.QTable : qTable;

    private int ActiveEpisodeNumber
    {
        get => UsesSharedTraining ? sharedTrainingState.EpisodeNumber : episodeNumber;
        set
        {
            if (UsesSharedTraining)
                sharedTrainingState.EpisodeNumber = value;

            episodeNumber = value;
        }
    }

    private float ActiveExplorationRate
    {
        get => UsesSharedTraining ? sharedTrainingState.ExplorationRate : explorationRate;
        set
        {
            float clampedValue = Mathf.Max(minimumExplorationRate, Mathf.Clamp01(value));

            if (UsesSharedTraining)
                sharedTrainingState.ExplorationRate = clampedValue;

            explorationRate = clampedValue;
        }
    }

    private int ActiveBestCheckpointsReached
    {
        get => UsesSharedTraining ? sharedTrainingState.BestCheckpointsReached : bestCheckpointsReached;
        set
        {
            if (UsesSharedTraining)
                sharedTrainingState.BestCheckpointsReached = value;

            bestCheckpointsReached = value;
        }
    }

    private float ActiveBestEpisodeReward
    {
        get => UsesSharedTraining ? sharedTrainingState.BestEpisodeReward : bestEpisodeReward;
        set
        {
            if (UsesSharedTraining)
                sharedTrainingState.BestEpisodeReward = value;

            bestEpisodeReward = value;
        }
    }

    private bool ActiveQTableLoaded
    {
        get => UsesSharedTraining ? sharedTrainingState.Loaded : qTableLoaded;
        set
        {
            if (UsesSharedTraining)
                sharedTrainingState.Loaded = value;

            qTableLoaded = value;
        }
    }

    private void Awake()
    {
        RefreshFilePaths();
        EnsureSharedTrainingState();

        if (saveQTable)
            LoadQTable();
        else
            ClearActiveQTable();
    }

    private void OnApplicationQuit()
    {
        SaveQTable();
    }

    private void OnValidate()
    {
        maxEpisodeDuration = Mathf.Max(0f, maxEpisodeDuration);
        stuckEpisodeTimeout = Mathf.Max(0f, stuckEpisodeTimeout);
        stuckRiskTime = Mathf.Max(0f, stuckRiskTime);
        stuckDangerTime = Mathf.Max(stuckRiskTime, stuckDangerTime);
    }

    public void ConfigurePersistence(
        bool shouldSaveQTable,
        bool shouldUseSharedQTable,
        string qTableSaveFileName,
        bool shouldSaveBestQTableSnapshot,
        string bestQTableSaveFileName,
        bool shouldWriteTrainingLogCsv,
        string csvFileName,
        bool useProjectRootFolder,
        bool resetEpisodeCounterAfterLoad = false
    )
    {
        bool wasSavingQTable = saveQTable;
        string previousQTableFileName = qTableFileName;
        bool previousUseProjectRootFolder = saveTrainingFilesInProjectRoot;
        bool previousUseSharedQTable = useSharedQTable;
        string previousTrainingStateKey = GetTrainingStateKey();

        saveQTable = shouldSaveQTable;
        useSharedQTable = shouldUseSharedQTable;
        qTableFileName = string.IsNullOrWhiteSpace(qTableSaveFileName) ? qTableFileName : qTableSaveFileName;
        saveBestQTableSnapshot = shouldSaveBestQTableSnapshot;
        bestQTableFileName = string.IsNullOrWhiteSpace(bestQTableSaveFileName) ? bestQTableFileName : bestQTableSaveFileName;
        writeTrainingLogCsv = shouldWriteTrainingLogCsv;
        trainingLogFileName = string.IsNullOrWhiteSpace(csvFileName) ? trainingLogFileName : csvFileName;
        saveTrainingFilesInProjectRoot = useProjectRootFolder;
        resetEpisodeCounterAfterQTableLoad = resetEpisodeCounterAfterLoad;

        RefreshFilePaths();
        EnsureSharedTrainingState();

        if (saveQTable)
        {
            bool qTableTargetChanged = previousQTableFileName != qTableFileName ||
                previousUseProjectRootFolder != saveTrainingFilesInProjectRoot ||
                previousUseSharedQTable != useSharedQTable ||
                previousTrainingStateKey != GetTrainingStateKey();

            bool shouldReloadQTable = !wasSavingQTable ||
                !ActiveQTableLoaded ||
                (qTableTargetChanged && !UsesSharedTraining);

            if (shouldReloadQTable)
                LoadQTable();
        }
        else
        {
            ClearActiveQTable();
            ActiveQTableLoaded = false;
        }
    }

    public void ConfigureExploration(float startExplorationRate, float decayPerEpisode, float minimumRate, bool overrideCurrentExplorationRate)
    {
        minimumExplorationRate = Mathf.Clamp01(minimumRate);
        explorationDecayPerEpisode = Mathf.Clamp01(decayPerEpisode);

        if (overrideCurrentExplorationRate)
            ActiveExplorationRate = startExplorationRate;
    }

    public void ConfigureTrainingIdentity(string botName, string botId)
    {
        if (!string.IsNullOrWhiteSpace(botName))
        {
            trainingBotName = botName;
            gameObject.name = botName;
        }

        if (!string.IsNullOrWhiteSpace(botId))
            trainingBotId = botId;
    }

    private void EnsureSharedTrainingState()
    {
        if (!useSharedQTable)
        {
            sharedTrainingState = null;
            return;
        }

        string trainingStateKey = GetTrainingStateKey();

        if (!sharedTrainingStates.TryGetValue(trainingStateKey, out sharedTrainingState))
        {
            sharedTrainingState = new SharedTrainingState
            {
                EpisodeNumber = episodeNumber,
                ExplorationRate = explorationRate,
                BestCheckpointsReached = bestCheckpointsReached,
                BestEpisodeReward = bestEpisodeReward
            };

            sharedTrainingStates[trainingStateKey] = sharedTrainingState;
        }

        episodeNumber = sharedTrainingState.EpisodeNumber;
        explorationRate = sharedTrainingState.ExplorationRate;
        bestCheckpointsReached = sharedTrainingState.BestCheckpointsReached;
        bestEpisodeReward = sharedTrainingState.BestEpisodeReward;
    }

    private string GetTrainingStateKey()
    {
        return string.IsNullOrEmpty(qTablePath)
            ? qTableFileName
            : qTablePath;
    }

    private void ClearActiveQTable()
    {
        ActiveQTable.Clear();
    }

    public void Tick(BotAgent agent, Bot bot, BotRewardSystem rewardSystem)
    {
        if (agent == null || bot == null)
            return;

        if (!episodeStarted)
            StartEpisode(rewardSystem);

        episodeDuration += Time.deltaTime;
        UpdateCheckpointProgressTimer(agent);
        decisionTimer -= Time.deltaTime;

        if (decisionTimer <= 0f)
        {
            decisionTimer = decisionInterval;

            currentStateKey = GetDiscreteStateKey(agent, bot);
            LearnFromReward(rewardSystem);

            selectedAction = ChooseAction(currentStateKey);

            previousStateKey = currentStateKey;
            previousAction = selectedAction;
            previousReward = rewardSystem != null ? rewardSystem.totalReward : 0f;
            hasPreviousDecision = true;

            if (debugLogs)
            {
                float reward = rewardSystem != null ? rewardSystem.totalReward : 0f;
                Debug.Log($"{gameObject.name} RL state={currentStateKey} action={selectedAction} reward={reward:F2}");
            }
        }

        ApplyAction(agent, bot, selectedAction);

        if (trainingEnabled && TryGetEpisodeEndReason(agent, out string endReason))
            EndEpisode(agent, bot, rewardSystem, endReason);
    }

    public void Observe(BotAgent agent, BotRewardSystem rewardSystem)
    {
        if (!debugLogs || agent == null)
            return;

        float reward = rewardSystem != null ? rewardSystem.totalReward : 0f;
        Debug.Log($"{gameObject.name} RL observe state={GetDiscreteStateKey(agent, GetComponent<Bot>())} reward={reward:F2}");
    }

    public bool CompleteRaceAtFinishLine(BotAgent agent)
    {
        if (!trainingEnabled || agent == null || !agent.HasFinishedTrainingRace())
            return false;

        EndEpisode(agent, GetComponent<Bot>(), GetComponent<BotRewardSystem>(), "RaceComplete");
        return true;
    }

    private void LearnFromReward(BotRewardSystem rewardSystem)
    {
        if (!learningEnabled || !hasPreviousDecision || rewardSystem == null)
            return;

        float rewardDelta = rewardSystem.totalReward - previousReward;
        float[] previousValues = GetActionValues(previousStateKey);
        float bestNext = GetBestActionValue(currentStateKey);

        int actionIndex = (int)previousAction;
        previousValues[actionIndex] += learningRate *
            (rewardDelta + discountFactor * bestNext - previousValues[actionIndex]);
    }

    private string GetDiscreteStateKey(BotAgent agent, Bot bot)
    {
        string speedBucket = GetSpeedBucket(bot);
        string progressBucket = GetProgressBucket();

        if (agent.currentTarget == null)
        {
            return $"cp:none|dist:none|angle:none|speed:{speedBucket}|progress:{progressBucket}|" +
                $"bomb:{HasNearbyBomb(agent)}|fsm:{agent.currentState}";
        }

        Vector3 checkpointPosition = agent.GetCurrentTargetPosition();
        Vector3 toCheckpoint = checkpointPosition - transform.position;
        float checkpointDistance = toCheckpoint.magnitude;
        string distanceBucket = GetCheckpointDistanceBucket(checkpointDistance);
        string checkpointBucket = agent.HasPassedAllCheckpoints()
            ? "finish"
            : agent.currentCheckpointIndex.ToString(CultureInfo.InvariantCulture);

        string angleBucket = "forward";

        if (toCheckpoint.sqrMagnitude > 1f)
        {
            Vector3 localDirection = transform.InverseTransformDirection(toCheckpoint.normalized);
            angleBucket = GetCheckpointAngleBucket(localDirection);
        }

        return $"cp:{checkpointBucket}|dist:{distanceBucket}|angle:{angleBucket}|speed:{speedBucket}|" +
            $"progress:{progressBucket}|bomb:{HasNearbyBomb(agent)}|fsm:{agent.currentState}";
    }

    private string GetCheckpointDistanceBucket(float checkpointDistance)
    {
        if (checkpointDistance <= closeCheckpointDistance)
            return "close";

        if (checkpointDistance <= mediumCheckpointDistance)
            return "medium";

        return "far";
    }

    private bool HasNearbyBomb(BotAgent agent)
    {
        return agent.nearestBomb != null;
    }

    private string GetCheckpointAngleBucket(Vector3 localDirection)
    {
        if (localDirection.z < -0.2f)
            return localDirection.x < 0f ? "behindLeft" : "behindRight";

        if (localDirection.x < -0.65f)
            return "hardLeft";

        if (localDirection.x > 0.65f)
            return "hardRight";

        if (localDirection.x < -0.25f)
            return "left";

        if (localDirection.x > 0.25f)
            return "right";

        return "forward";
    }

    private string GetSpeedBucket(Bot bot)
    {
        if (bot == null)
            return "unknown";

        float speedRange = Mathf.Max(1f, bot.MaxSpeed - bot.MinSpeed);
        float normalizedSpeed = Mathf.Clamp01((bot.CurrentSpeed - bot.MinSpeed) / speedRange);

        if (bot.CurrentSpeed <= bot.MinSpeed + 15f)
            return "slow";

        if (normalizedSpeed < 0.25f)
            return "cruise";

        if (normalizedSpeed < 0.55f)
            return "fast";

        return "max";
    }

    private string GetProgressBucket()
    {
        if (timeSinceCheckpointProgress < stuckRiskTime)
            return "fresh";

        if (timeSinceCheckpointProgress < stuckDangerTime)
            return "stale";

        return "danger";
    }

    private BotAction ChooseAction(string state)
    {
        if (Random.value < ActiveExplorationRate)
            return (BotAction)Random.Range(0, ActionCount);

        float[] values = GetActionValues(state);
        int bestAction = 0;
        float bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestAction = i;
            }
        }

        return (BotAction)bestAction;
    }

    private void ApplyAction(BotAgent agent, Bot bot, BotAction action)
    {
        Vector2 steer = GetSteerToTarget(agent);
        float pitch = steer.x;
        float yaw = steer.y;

        switch (action)
        {
            case BotAction.TurnLeftAndAccelerate:
                yaw -= actionYawBias;
                break;

            case BotAction.TurnRightAndAccelerate:
                yaw += actionYawBias;
                break;

            case BotAction.Brake:
                pitch *= brakePitchMultiplier;
                break;

            case BotAction.HardLeft:
                yaw -= hardTurnYawBias;
                pitch *= brakePitchMultiplier;
                break;

            case BotAction.HardRight:
                yaw += hardTurnYawBias;
                pitch *= brakePitchMultiplier;
                break;

            case BotAction.Coast:
                pitch *= coastPitchMultiplier;
                break;

            case BotAction.RecoveryTurn:
                yaw = GetRecoveryYaw(yaw);
                pitch *= recoveryPitchMultiplier;
                break;
        }

        pitch = Mathf.Clamp(pitch, -maxPitchInput, maxPitchInput);
        yaw = Mathf.Clamp(yaw, -maxYawInput, maxYawInput);

        bool braking = action == BotAction.Brake ||
            action == BotAction.HardLeft ||
            action == BotAction.HardRight ||
            action == BotAction.RecoveryTurn;
        bool accelerating = action != BotAction.Brake &&
            action != BotAction.HardLeft &&
            action != BotAction.HardRight &&
            action != BotAction.Coast &&
            action != BotAction.RecoveryTurn;

        bot.SetAiInput(pitch, yaw, accelerating, braking);
    }

    private float GetRecoveryYaw(float targetYaw)
    {
        float yawDirection = Mathf.Abs(targetYaw) > 0.05f
            ? Mathf.Sign(targetYaw)
            : lastYawDirection;

        return yawDirection * maxYawInput;
    }

    private Vector2 GetSteerToTarget(BotAgent agent)
    {
        Vector3 targetPosition = agent.GetTargetPositionByState();
        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.sqrMagnitude < 1f)
            return Vector2.zero;

        Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);

        float yaw = GetYawInput(localDirection);
        float pitch = Mathf.Clamp(-localDirection.y * pitchSensitivity, -maxPitchInput, maxPitchInput);

        if (Mathf.Abs(yaw) > 0.02f)
            lastYawDirection = Mathf.Sign(yaw);

        return new Vector2(pitch, yaw);
    }

    private float GetYawInput(Vector3 localDirection)
    {
        if (localDirection.z < 0f)
        {
            if (Mathf.Abs(localDirection.x) > 0.02f)
                return Mathf.Sign(localDirection.x) * maxYawInput;

            return lastYawDirection * maxYawInput;
        }

        return Mathf.Clamp(localDirection.x * yawSensitivity, -maxYawInput, maxYawInput);
    }

    private void StartEpisode(BotRewardSystem rewardSystem)
    {
        episodeStarted = true;
        episodeDuration = 0f;
        decisionTimer = 0f;
        hasPreviousDecision = false;
        previousReward = 0f;
        lastObservedCheckpointCount = 0;
        timeSinceCheckpointProgress = 0f;

        if (rewardSystem != null)
            rewardSystem.ResetReward();

        if (writeTrainingLogCsv)
            RefreshFilePaths();
    }

    private bool TryGetEpisodeEndReason(BotAgent agent, out string reason)
    {
        if (stuckEpisodeTimeout > 0f && timeSinceCheckpointProgress >= stuckEpisodeTimeout)
        {
            reason = "Stuck";
            return true;
        }

        if (maxEpisodeDuration > 0f && episodeDuration >= maxEpisodeDuration)
        {
            reason = "TimeLimit";
            return true;
        }

        if (endEpisodeAfterAllCheckpoints && agent.HasFinishedTrainingRace())
        {
            reason = "RaceComplete";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private void UpdateCheckpointProgressTimer(BotAgent agent)
    {
        if (agent.passedCheckpointCount > lastObservedCheckpointCount)
        {
            lastObservedCheckpointCount = agent.passedCheckpointCount;
            timeSinceCheckpointProgress = 0f;
            return;
        }

        timeSinceCheckpointProgress += Time.deltaTime;
    }

    private void EndEpisode(BotAgent agent, Bot bot, BotRewardSystem rewardSystem, string reason)
    {
        currentStateKey = GetDiscreteStateKey(agent, bot);
        LearnFromReward(rewardSystem);
        bool newBestEpisode = UpdateBestEpisodeStats(rewardSystem);
        LogEpisode(agent, rewardSystem, reason, newBestEpisode);

        if (learningEnabled)
            ActiveExplorationRate = ActiveExplorationRate * explorationDecayPerEpisode;

        ActiveEpisodeNumber++;

        if (learningEnabled)
        {
            SaveQTable();
            SaveBestQTableIfNeeded(newBestEpisode);
        }

        agent.ResetTrainingEpisode();

        episodeDuration = 0f;
        decisionTimer = 0f;
        hasPreviousDecision = false;
        previousReward = 0f;
        lastObservedCheckpointCount = 0;
        timeSinceCheckpointProgress = 0f;
        selectedAction = BotAction.AccelerateForward;
        currentStateKey = GetDiscreteStateKey(agent, bot);
    }

    private bool UpdateBestEpisodeStats(BotRewardSystem rewardSystem)
    {
        int checkpointsReached = rewardSystem != null ? rewardSystem.checkpointsReached : 0;
        float totalReward = rewardSystem != null ? rewardSystem.totalReward : 0f;

        if (checkpointsReached < ActiveBestCheckpointsReached)
            return false;

        if (checkpointsReached == ActiveBestCheckpointsReached && totalReward <= ActiveBestEpisodeReward)
            return false;

        ActiveBestCheckpointsReached = checkpointsReached;
        ActiveBestEpisodeReward = totalReward;
        return true;
    }

    private void LogEpisode(BotAgent agent, BotRewardSystem rewardSystem, string reason, bool newBestEpisode)
    {
        float totalReward = rewardSystem != null ? rewardSystem.totalReward : 0f;
        int checkpointsReached = rewardSystem != null ? rewardSystem.checkpointsReached : 0;
        int coinsCollected = rewardSystem != null ? rewardSystem.coinsCollected : 0;
        int bombsHit = rewardSystem != null ? rewardSystem.bombsHit : 0;
        int bestCheckpointIndex = agent != null && agent.passedCheckpointCount > 0
            ? agent.passedCheckpointCount - 1
            : -1;
        int targetCheckpointIndex = GetTargetCheckpointIndex(agent);
        string botName = GetCsvSafeText(TrainingBotName);
        string botId = GetCsvSafeText(TrainingBotId);

        if (logEpisodesToConsole)
        {
            Debug.Log(
                $"{gameObject.name} RL episode {ActiveEpisodeNumber} ended ({reason}). " +
                $"reward={totalReward:F2}, checkpoints={checkpointsReached}, coins={coinsCollected}, " +
                $"bombs={bombsHit}, duration={episodeDuration:F1}s, epsilon={ActiveExplorationRate:F3}"
            );
        }

        if (!writeTrainingLogCsv)
            return;

        try
        {
            bool writeHeader = !File.Exists(trainingLogPath);

            using (StreamWriter writer = new StreamWriter(trainingLogPath, true))
            {
                if (writeHeader)
                {
                    writer.WriteLine(
                        "Episode,BotName,BotId,SharedQTable,LearningEnabled,TotalReward,CheckpointsReached," +
                        "BestCheckpointIndex,TargetCheckpointIndex,CoinsCollected,BombsHit,Duration,Epsilon," +
                        "EndReason,LearnedStateCount,QTableFile,NewBest,TimeSinceCheckpointProgress,LastAction,LastState"
                    );
                }

                writer.WriteLine(string.Join(",", new[]
                {
                    ActiveEpisodeNumber.ToString(CultureInfo.InvariantCulture),
                    botName,
                    botId,
                    useSharedQTable.ToString(),
                    learningEnabled.ToString(),
                    totalReward.ToString("F3", CultureInfo.InvariantCulture),
                    checkpointsReached.ToString(CultureInfo.InvariantCulture),
                    bestCheckpointIndex.ToString(CultureInfo.InvariantCulture),
                    targetCheckpointIndex.ToString(CultureInfo.InvariantCulture),
                    coinsCollected.ToString(CultureInfo.InvariantCulture),
                    bombsHit.ToString(CultureInfo.InvariantCulture),
                    episodeDuration.ToString("F3", CultureInfo.InvariantCulture),
                    ActiveExplorationRate.ToString("F3", CultureInfo.InvariantCulture),
                    reason,
                    ActiveQTable.Count.ToString(CultureInfo.InvariantCulture),
                    Path.GetFileName(qTablePath),
                    newBestEpisode.ToString(),
                    timeSinceCheckpointProgress.ToString("F3", CultureInfo.InvariantCulture),
                    selectedAction.ToString(),
                    currentStateKey
                }));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"{gameObject.name} could not write RL training log to {trainingLogPath}: {ex.Message}");
        }
    }

    private int GetTargetCheckpointIndex(BotAgent agent)
    {
        if (agent == null)
            return -1;

        if (agent.HasPassedAllCheckpoints())
            return agent.checkpoints != null ? agent.checkpoints.Length : agent.currentCheckpointIndex;

        return agent.currentCheckpointIndex;
    }

    private string GetCsvSafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Bot"
            : value.Replace(",", " ");
    }

    private void LoadQTable()
    {
        ClearActiveQTable();
        ActiveQTableLoaded = true;

        if (!saveQTable)
            return;

        RefreshFilePaths();

        string qTablePathToLoad = GetExistingQTablePath();

        if (string.IsNullOrEmpty(qTablePathToLoad))
        {
            if (resetEpisodeCounterAfterQTableLoad)
                ActiveEpisodeNumber = 1;
            else
                LoadEpisodeNumberFromCsv();

            if (debugLogs)
            {
                Debug.Log(
                    $"{gameObject.name} no saved Q-table found at {qTablePath}. " +
                    $"Starting empty. Next episode: {ActiveEpisodeNumber}."
                );
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(qTablePathToLoad);
            SavedQTable savedTable = JsonUtility.FromJson<SavedQTable>(json);

            if (savedTable == null || savedTable.states == null)
                return;

            if (savedTable.version != QTableVersion)
            {
                if (resetEpisodeCounterAfterQTableLoad)
                    ActiveEpisodeNumber = 1;
                else if (savedTable.episodeNumber > 0)
                    ActiveEpisodeNumber = savedTable.episodeNumber;

                if (!resetEpisodeCounterAfterQTableLoad)
                    LoadEpisodeNumberFromCsv();

                if (debugLogs)
                {
                    Debug.Log(
                        $"{gameObject.name} found Q-table version {savedTable.version}, " +
                        $"but Stage 6 needs version {QTableVersion}. Starting with empty Q-values. " +
                        $"Next episode: {ActiveEpisodeNumber}."
                    );
                }

                return;
            }

            foreach (SavedQState savedState in savedTable.states)
            {
                if (savedState == null || string.IsNullOrEmpty(savedState.stateKey))
                    continue;

                float[] values = new float[ActionCount];

                if (savedState.actionValues != null)
                {
                    int valueCount = Mathf.Min(ActionCount, savedState.actionValues.Length);

                    for (int i = 0; i < valueCount; i++)
                        values[i] = savedState.actionValues[i];
                }

                ActiveQTable[savedState.stateKey] = values;
            }

            if (savedTable.epsilon > 0f)
                ActiveExplorationRate = savedTable.epsilon;

            if (resetEpisodeCounterAfterQTableLoad)
                ActiveEpisodeNumber = 1;
            else if (savedTable.episodeNumber > 0)
                ActiveEpisodeNumber = savedTable.episodeNumber;

            ActiveBestCheckpointsReached = savedTable.bestCheckpointsReached;
            ActiveBestEpisodeReward = savedTable.bestEpisodeReward;

            if (!resetEpisodeCounterAfterQTableLoad)
                LoadEpisodeNumberFromCsv();

            if (debugLogs)
            {
                Debug.Log(
                    $"{gameObject.name} loaded {ActiveQTable.Count} Q-table states from {qTablePathToLoad}. " +
                    $"Next episode: {ActiveEpisodeNumber}."
                );
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"{gameObject.name} could not load Q-table from {qTablePath}: {ex.Message}");
        }
    }

    private void LoadEpisodeNumberFromCsv()
    {
        string csvPathToRead = GetExistingTrainingLogPath();

        if (string.IsNullOrEmpty(csvPathToRead))
            return;

        try
        {
            int lastEpisode = 0;

            using (StreamReader reader = new StreamReader(csvPathToRead))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Episode"))
                        continue;

                    string[] columns = line.Split(',');

                    if (columns.Length == 0)
                        continue;

                    if (int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedEpisode))
                        lastEpisode = Mathf.Max(lastEpisode, parsedEpisode);
                }
            }

            if (lastEpisode > 0)
                ActiveEpisodeNumber = Mathf.Max(ActiveEpisodeNumber, lastEpisode + 1);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"{gameObject.name} could not read episode number from {csvPathToRead}: {ex.Message}");
        }
    }

    private string GetExistingQTablePath()
    {
        if (File.Exists(qTablePath))
            return qTablePath;

        string legacyPath = Path.Combine(GetLegacyTrainingFileDirectory(), qTableFileName);

        return File.Exists(legacyPath) ? legacyPath : string.Empty;
    }

    private string GetExistingTrainingLogPath()
    {
        if (File.Exists(trainingLogPath))
            return trainingLogPath;

        string legacyPath = Path.Combine(GetLegacyTrainingFileDirectory(), trainingLogFileName);

        return File.Exists(legacyPath) ? legacyPath : string.Empty;
    }

    private void SaveQTable()
    {
        if (!saveQTable || !learningEnabled)
            return;

        RefreshFilePaths();

        SaveQTableToPath(qTablePath);
    }

    private void SaveBestQTableIfNeeded(bool newBestEpisode)
    {
        if (!saveQTable || !saveBestQTableSnapshot || !newBestEpisode)
            return;

        RefreshFilePaths();
        SaveQTableToPath(bestQTablePath);
    }

    private void SaveQTableToPath(string savePath)
    {
        try
        {
            SavedQTable savedTable = new SavedQTable
            {
                version = QTableVersion,
                actionCount = ActionCount,
                episodeNumber = ActiveEpisodeNumber,
                epsilon = ActiveExplorationRate,
                bestCheckpointsReached = ActiveBestCheckpointsReached,
                bestEpisodeReward = ActiveBestEpisodeReward
            };

            foreach (KeyValuePair<string, float[]> entry in ActiveQTable)
            {
                float[] values = new float[ActionCount];

                if (entry.Value != null)
                {
                    int valueCount = Mathf.Min(ActionCount, entry.Value.Length);

                    for (int i = 0; i < valueCount; i++)
                        values[i] = entry.Value[i];
                }

                savedTable.states.Add(new SavedQState
                {
                    stateKey = entry.Key,
                    actionValues = values
                });
            }

            File.WriteAllText(savePath, JsonUtility.ToJson(savedTable, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"{gameObject.name} could not save Q-table to {savePath}: {ex.Message}");
        }
    }

    private void RefreshFilePaths()
    {
        if (string.IsNullOrWhiteSpace(trainingLogFileName))
            trainingLogFileName = "training_log.csv";

        if (string.IsNullOrWhiteSpace(qTableFileName))
            qTableFileName = "q_table.json";

        if (string.IsNullOrWhiteSpace(bestQTableFileName))
            bestQTableFileName = "q_table_best.json";

        string fileDirectory = GetTrainingFileDirectory();
        Directory.CreateDirectory(fileDirectory);

        trainingLogPath = Path.Combine(fileDirectory, trainingLogFileName);
        qTablePath = Path.Combine(fileDirectory, qTableFileName);
        bestQTablePath = Path.Combine(fileDirectory, bestQTableFileName);
    }

    private string GetTrainingFileDirectory()
    {
        if (!saveTrainingFilesInProjectRoot)
            return Application.persistentDataPath;

        return GetProjectRootDirectory();
    }

    private string GetLegacyTrainingFileDirectory()
    {
        return Path.Combine(GetProjectRootDirectory(), "TrainingData");
    }

    private string GetProjectRootDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private float GetBestActionValue(string state)
    {
        float[] values = GetActionValues(state);
        float bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
            bestValue = Mathf.Max(bestValue, values[i]);

        return bestValue;
    }

    private float[] GetActionValues(string state)
    {
        Dictionary<string, float[]> table = ActiveQTable;

        if (!table.TryGetValue(state, out float[] values))
        {
            values = new float[ActionCount];
            table[state] = values;
        }

        return values;
    }
}
