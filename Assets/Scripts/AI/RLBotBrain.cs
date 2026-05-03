using UnityEngine;

public class RLBotBrain : MonoBehaviour
{
    [Header("RL Skeleton")]
    public float decisionInterval = 0.5f;
    public float explorationRate = 0.1f;

    [Header("Debug")]
    public bool debugLogs = true;

    private float decisionTimer;
    private int currentState;
    private int selectedAction;

    public void Observe(BotAgent agent, BotRewardSystem rewardSystem)
    {
        decisionTimer -= Time.deltaTime;

        if (decisionTimer > 0f)
            return;

        decisionTimer = decisionInterval;

        currentState = GetDiscreteState(agent);
        selectedAction = ChooseAction(currentState);

        if (debugLogs)
        {
            Debug.Log(
                $"{gameObject.name} RL Skeleton | state={currentState} | action={selectedAction} | reward={rewardSystem.totalReward:F2}"
            );
        }
    }

    private int GetDiscreteState(BotAgent agent)
    {
        switch (agent.currentState)
        {
            case BotState.Racing:
                return 0;

            case BotState.Collect:
                return 1;

            case BotState.AvoidDanger:
                return 2;

            case BotState.Recover:
                return 3;

            default:
                return 0;
        }
    }

    private int ChooseAction(int state)
    {
        // Demo 3: skeleton.
        // Demo 4: aici se va înlocui cu Q-table / Q-learning.

        if (Random.value < explorationRate)
            return Random.Range(0, 5);

        switch (state)
        {
            case 0:
                return 0; // Racing action

            case 1:
                return 1; // Collect action

            case 2:
                return 2; // Avoid danger action

            case 3:
                return 3; // Recover action

            default:
                return 0;
        }
    }
}