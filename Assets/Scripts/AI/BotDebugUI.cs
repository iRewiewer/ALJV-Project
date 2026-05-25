using TMPro;
using UnityEngine;

public class BotDebugUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text debugText;

    [Header("References")]
    public BotManager botManager;

    [Header("Settings")]
    public bool showDebug = true;
    public float refreshRate = 0.15f;
    public int trackedBotIndex;

    private float refreshTimer;
    private BotAgent trackedAgent;
    private Bot trackedBot;
    private BotRewardSystem trackedReward;
    private RLBotBrain trackedRlBrain;
    private bool trackingViewedBot;

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;

        if (refreshTimer > 0f)
            return;

        refreshTimer = refreshRate;

        RefreshReferences();
        UpdateDebugText();
    }

    private void RefreshReferences()
    {
        if (botManager == null)
            botManager = FindFirstObjectByType<BotManager>();

        trackedAgent = null;
        trackedBot = null;
        trackedReward = null;
        trackedRlBrain = null;
        trackingViewedBot = false;

        GameObject botObject = GetTrackedBotObject();

        if (botObject == null)
            return;

        trackedAgent = botObject.GetComponent<BotAgent>();
        trackedBot = botObject.GetComponent<Bot>();
        trackedReward = botObject.GetComponent<BotRewardSystem>();
        trackedRlBrain = botObject.GetComponent<RLBotBrain>();
    }

    private GameObject GetTrackedBotObject()
    {
        if (botManager == null)
            return GetFirstSceneBot();

        if (botManager.BotCount == 0)
            return null;

        if (botManager.viewedBotIndex >= 0 && botManager.viewedBotIndex < botManager.BotCount)
        {
            trackedBotIndex = botManager.viewedBotIndex;
            trackingViewedBot = true;

            GameObject viewedBotObject = botManager.GetBotObject(trackedBotIndex);

            if (viewedBotObject != null)
                return viewedBotObject;
        }

        trackedBotIndex = Mathf.Clamp(trackedBotIndex, 0, botManager.BotCount - 1);

        GameObject botObject = botManager.GetBotObject(trackedBotIndex);

        if (botObject != null)
            return botObject;

        return GetFirstSceneBot();
    }

    private GameObject GetFirstSceneBot()
    {
        BotAgent firstAgent = FindFirstObjectByType<BotAgent>();

        if (firstAgent != null)
            return firstAgent.gameObject;

        Bot firstBot = FindFirstObjectByType<Bot>();

        return firstBot != null ? firstBot.gameObject : null;
    }

    private void UpdateDebugText()
    {
        if (debugText == null)
            return;

        if (!showDebug)
        {
            debugText.text = "";
            return;
        }

        if (trackedBot == null)
        {
            debugText.text =
                "BOT DEBUG\n" +
                "Bots: 0\n" +
                "Use pause menu to add bot.";
            return;
        }

        string botName = trackedBot.gameObject.name;
        string stateName = trackedAgent != null ? trackedAgent.currentState.ToString() : "No Agent";
        string aiType = trackedAgent != null ? trackedAgent.aiType.ToString() : "None";
        string targetName = GetTransformName(trackedAgent != null ? trackedAgent.currentTarget : null);
        string nearestCheckpointName = GetTransformName(trackedAgent != null ? trackedAgent.nearestCheckpoint : null);
        string coinName = GetTransformName(trackedAgent != null ? trackedAgent.nearestCoin : null);
        string bombName = GetTransformName(trackedAgent != null ? trackedAgent.nearestBomb : null);

        int checkpointIndex = trackedAgent != null ? trackedAgent.currentCheckpointIndex : -1;
        int passedCheckpoints = trackedAgent != null ? trackedAgent.passedCheckpointCount : 0;
        int checkpointCount = trackedAgent != null && trackedAgent.checkpoints != null
            ? trackedAgent.checkpoints.Length
            : 0;

        float speed = trackedBot.CurrentSpeed;
        float reward = trackedReward != null ? trackedReward.totalReward : 0f;
        int botCount = botManager != null ? botManager.BotCount : 1;
        string trackingLabel = trackingViewedBot ? "Viewed" : "Tracked";
        string rlStats = "";

        if (trackedRlBrain != null && trackedAgent != null && trackedAgent.aiType == BotAIType.ReinforcementLearning)
        {
            rlStats =
                $"RL Episode: {trackedRlBrain.EpisodeNumber}\n" +
                $"RL Time: {trackedRlBrain.EpisodeDuration:F1}s\n" +
                $"RL CP Timer: {trackedRlBrain.TimeSinceCheckpointProgress:F1}s\n" +
                $"RL Epsilon: {trackedRlBrain.Epsilon:F2}\n" +
                $"RL Learning: {trackedRlBrain.IsLearningEnabled}\n" +
                $"RL States: {trackedRlBrain.LearnedStateCount}\n" +
                $"RL Shared: {trackedRlBrain.IsUsingSharedQTable}\n" +
                $"RL Action: {trackedRlBrain.CurrentActionName}\n";
        }

        debugText.text =
            "BOT DEBUG\n" +
            $"Bots: {botCount}\n" +
            $"{trackingLabel}: {trackedBotIndex + 1}/{Mathf.Max(botCount, 1)} {botName}\n" +
            $"AI: {aiType}\n" +
            $"State: {stateName}\n" +
            $"Checkpoint: {checkpointIndex}/{checkpointCount}\n" +
            $"Passed CP: {passedCheckpoints}/{checkpointCount}\n" +
            $"Target: {targetName}\n" +
            $"Nearest CP: {nearestCheckpointName}\n" +
            $"Coin: {coinName}\n" +
            $"Bomb: {bombName}\n" +
            $"Speed: {speed:F1}\n" +
            $"Reward: {reward:F1}\n" +
            rlStats;
    }

    private string GetTransformName(Transform target)
    {
        return target != null ? target.name : "None";
    }
}
