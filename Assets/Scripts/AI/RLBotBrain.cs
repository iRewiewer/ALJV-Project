using System.Collections.Generic;
using UnityEngine;

public class RLBotBrain : MonoBehaviour
{
    [Header("RL Basics")]
    public float decisionInterval = 0.4f;
    public float explorationRate = 0.15f;
    public float learningRate = 0.25f;
    public float discountFactor = 0.85f;

    [Header("Steering")]
    public float yawSensitivity = 3f;
    public float pitchSensitivity = 2f;
    public float maxYawInput = 1f;
    public float maxPitchInput = 0.45f;
    public float actionYawBias = 0.35f;
    public float actionPitchBias = 0.25f;

    [Header("Speed")]
    public float cruiseSpeed = 110f;
    public float turnSpeed = 55f;
    public float checkpointSpeed = 45f;
    public float avoidSpeed = 65f;
    public float slowAngle = 35f;
    public float hardTurnAngle = 70f;
    public float recoverySpeed = 35f;
    public float slowCheckpointDistance = 180f;
    public float speedTolerance = 5f;

    [Header("Debug")]
    public bool debugLogs = false;

    private const int ActionCount = 5;

    private readonly Dictionary<int, float[]> qTable = new Dictionary<int, float[]>();

    private float decisionTimer;
    private int currentState;
    private int selectedAction;
    private int previousState;
    private int previousAction;
    private float previousReward;
    private bool hasPreviousDecision;
    private float lastYawDirection = 1f;

    public void Tick(BotAgent agent, Bot bot, BotRewardSystem rewardSystem)
    {
        if (agent == null || bot == null)
            return;

        decisionTimer -= Time.deltaTime;

        if (decisionTimer <= 0f)
        {
            decisionTimer = decisionInterval;

            currentState = GetDiscreteState(agent);
            LearnFromReward(rewardSystem);

            selectedAction = ChooseAction(currentState);

            previousState = currentState;
            previousAction = selectedAction;
            previousReward = rewardSystem != null ? rewardSystem.totalReward : 0f;
            hasPreviousDecision = true;

            if (debugLogs)
            {
                float reward = rewardSystem != null ? rewardSystem.totalReward : 0f;
                Debug.Log($"{gameObject.name} RL state={currentState} action={selectedAction} reward={reward:F2}");
            }
        }

        ApplyAction(agent, bot, selectedAction);
    }

    public void Observe(BotAgent agent, BotRewardSystem rewardSystem)
    {
        if (!debugLogs || agent == null)
            return;

        float reward = rewardSystem != null ? rewardSystem.totalReward : 0f;
        Debug.Log($"{gameObject.name} RL observe state={GetDiscreteState(agent)} reward={reward:F2}");
    }

    private void LearnFromReward(BotRewardSystem rewardSystem)
    {
        if (!hasPreviousDecision || rewardSystem == null)
            return;

        float rewardDelta = rewardSystem.totalReward - previousReward;
        float[] previousValues = GetActionValues(previousState);
        float bestNext = GetBestActionValue(currentState);

        previousValues[previousAction] += learningRate *
            (rewardDelta + discountFactor * bestNext - previousValues[previousAction]);
    }

    private int GetDiscreteState(BotAgent agent)
    {
        int state = (int)agent.currentState;

        if (agent.currentTarget == null)
            return state;

        Vector3 targetPosition = agent.GetTargetPositionByState();
        Vector3 localDirection = transform.InverseTransformDirection((targetPosition - transform.position).normalized);

        int horizontal = localDirection.x < -0.2f ? 0 : localDirection.x > 0.2f ? 2 : 1;
        int vertical = localDirection.y < -0.2f ? 0 : localDirection.y > 0.2f ? 2 : 1;

        // Small state space, enough for demo use.
        return state * 9 + vertical * 3 + horizontal;
    }

    private int ChooseAction(int state)
    {
        if (Random.value < explorationRate)
            return Random.Range(0, ActionCount);

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

        return bestAction;
    }

    private void ApplyAction(BotAgent agent, Bot bot, int action)
    {
        Vector2 steer = GetSteerToTarget(agent);
        float pitch = steer.x;
        float yaw = steer.y;

        switch (action)
        {
            case 1:
                yaw -= actionYawBias;
                break;

            case 2:
                yaw += actionYawBias;
                break;

            case 3:
                pitch -= actionPitchBias;
                break;

            case 4:
                pitch += actionPitchBias;
                break;
        }

        pitch = Mathf.Clamp(pitch, -maxPitchInput, maxPitchInput);
        yaw = Mathf.Clamp(yaw, -maxYawInput, maxYawInput);

        float desiredSpeed = GetDesiredSpeed(agent, bot);
        bool accelerating = bot.CurrentSpeed < desiredSpeed - speedTolerance;
        bool braking = bot.CurrentSpeed > desiredSpeed + speedTolerance;

        bot.SetAiInput(pitch, yaw, accelerating, braking);
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

    private float GetDesiredSpeed(BotAgent agent, Bot bot)
    {
        Vector3 targetPosition = agent.GetTargetPositionByState();
        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.sqrMagnitude < 1f)
            return bot.MinSpeed;

        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        float distance = agent.currentTarget != null
            ? Vector3.Distance(transform.position, agent.GetCurrentTargetPosition())
            : slowCheckpointDistance;

        float desiredSpeed = cruiseSpeed;

        if (angle > slowAngle)
            desiredSpeed = Mathf.Lerp(cruiseSpeed, turnSpeed, Mathf.InverseLerp(slowAngle, hardTurnAngle, angle));

        if (distance < slowCheckpointDistance)
            desiredSpeed = Mathf.Min(desiredSpeed, checkpointSpeed);

        if (agent.currentState == BotState.AvoidDanger)
            desiredSpeed = Mathf.Min(desiredSpeed, avoidSpeed);

        if (agent.currentState == BotState.Recover)
            desiredSpeed = recoverySpeed;

        if (angle > hardTurnAngle)
            desiredSpeed = Mathf.Min(desiredSpeed, recoverySpeed);

        return Mathf.Clamp(desiredSpeed, bot.MinSpeed, Mathf.Min(bot.MaxSpeed, cruiseSpeed));
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

    private float GetBestActionValue(int state)
    {
        float[] values = GetActionValues(state);
        float bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
            bestValue = Mathf.Max(bestValue, values[i]);

        return bestValue;
    }

    private float[] GetActionValues(int state)
    {
        if (!qTable.TryGetValue(state, out float[] values))
        {
            values = new float[ActionCount];
            qTable[state] = values;
        }

        return values;
    }
}
