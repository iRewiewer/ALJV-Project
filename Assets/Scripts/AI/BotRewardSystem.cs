using UnityEngine;

public class BotRewardSystem : MonoBehaviour
{
    [Header("Reward Values")]
    public float checkpointReward = 30f;
    public float coinReward = 0f;
    public float bombPenalty = -5f;
    public float timePenaltyPerSecond = -0.1f;

    [Header("Racing Rewards")]
    public float progressRewardMultiplier = 0.02f;
    public float alignedSpeedReward = 0.03f;
    public float wrongDirectionPenalty = -0.03f;

    [Header("Debug")]
    public float totalReward;
    public int checkpointsReached;
    public int coinsCollected;
    public int bombsHit;
    public bool verboseLogs = false;

    public void AddProgressReward(float progressAmount)
    {
        if (progressAmount > 0f)
        {
            totalReward += progressAmount * progressRewardMultiplier;
        }
        else
        {
            totalReward += wrongDirectionPenalty;
        }
    }

    public void AddSpeedReward(bool alignedAndAccelerating)
    {
        if (alignedAndAccelerating)
            totalReward += alignedSpeedReward;
    }

    public void AddCheckpointReward()
    {
        checkpointsReached++;
        AddReward(checkpointReward, "Checkpoint", true);
    }

    public void AddCoinReward(int value = 1)
    {
        coinsCollected++;
        AddReward(coinReward * Mathf.Max(1, value), "Coin", true);
    }

    public void AddBombPenalty()
    {
        bombsHit++;
        AddReward(bombPenalty, "Bomb", true);
    }

    public void AddTimePenalty(float deltaTime)
    {
        totalReward += timePenaltyPerSecond * deltaTime;
    }

    public void AddReward(float value, string reason, bool importantLog = false)
    {
        totalReward += value;

        if (importantLog || verboseLogs)
            Debug.Log($"{gameObject.name} reward {value} for {reason}. Total: {totalReward}");
    }

    public void ResetReward()
    {
        totalReward = 0f;
        checkpointsReached = 0;
        coinsCollected = 0;
        bombsHit = 0;
    }
}
