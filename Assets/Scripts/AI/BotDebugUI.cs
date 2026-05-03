using TMPro;
using UnityEngine;

public class BotDebugUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text debugText;

    [Header("Settings")]
    public bool showDebug = true;
    public float refreshRate = 0.15f;

    private float refreshTimer;
    private BotAgent trackedAgent;
    private Bot trackedBot;
    private BotRewardSystem trackedReward;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
            showDebug = !showDebug;

        refreshTimer -= Time.deltaTime;

        if (refreshTimer > 0f)
            return;

        refreshTimer = refreshRate;

        RefreshReferences();
        UpdateDebugText();
    }

    private void RefreshReferences()
    {
        if (trackedAgent != null && trackedBot != null && trackedReward != null)
            return;

        trackedAgent = FindFirstObjectByType<BotAgent>();

        if (trackedAgent == null)
            return;

        trackedBot = trackedAgent.GetComponent<Bot>();
        trackedReward = trackedAgent.GetComponent<BotRewardSystem>();
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

        if (trackedAgent == null)
        {
            debugText.text =
                "BOT DEBUG\n" +
                "No bot spawned.\n" +
                "Press B to spawn bot.";
            return;
        }

        string targetName = trackedAgent.currentTarget != null
            ? trackedAgent.currentTarget.name
            : "None";

        string coinName = trackedAgent.nearestCoin != null
            ? trackedAgent.nearestCoin.name
            : "None";

        string bombName = trackedAgent.nearestBomb != null
            ? trackedAgent.nearestBomb.name
            : "None";

        float speed = trackedBot != null
            ? trackedBot.CurrentSpeed
            : 0f;

        float reward = trackedReward != null
            ? trackedReward.totalReward
            : 0f;

        debugText.text =
            "BOT DEBUG\n" +
            $"State: {trackedAgent.currentState}\n" +
            $"Checkpoint Index: {trackedAgent.currentCheckpointIndex}\n" +
            $"Target: {targetName}\n" +
            $"Nearest Coin: {coinName}\n" +
            $"Nearest Bomb: {bombName}\n" +
            $"Speed: {speed:F1}\n" +
            $"Reward: {reward:F1}\n\n" +
            "B = Spawn Bot\n" +
            "M = Remove Bot\n" +
            "P = Toggle Debug UI";
    }
}