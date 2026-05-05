using UnityEngine;

public class FSMBotBrain : MonoBehaviour
{
    [Header("Steering")]
    public float yawSensitivity = 3.5f;
    public float pitchSensitivity = 2.5f;

    [Header("Input Limits")]
    public float maxYawInput = 1f;
    public float maxPitchInput = 0.45f;
    public float inputDeadZone = 0.02f;

    [Header("Speed")]
    public float cruiseSpeed = 120f;
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

    private float debugTimer;
    private float lastYawDirection = 1f;

    public void Tick(BotAgent agent, Bot bot)
    {
        if (agent.currentTarget == null)
        {
            bot.SetAiInput(0f, 0f, false, true);
            return;
        }

        Vector3 targetPosition = agent.GetTargetPositionByState();
        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.sqrMagnitude < 1f)
        {
            bot.SetAiInput(0f, 0f, false, true);
            return;
        }

        // Bot moves forward, so steer the nose at the target.
        Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);

        float yaw = GetYawInput(localDirection);
        float pitch = Mathf.Clamp(-localDirection.y * pitchSensitivity, -maxPitchInput, maxPitchInput);

        if (Mathf.Abs(yaw) < inputDeadZone)
            yaw = 0f;
        else
            lastYawDirection = Mathf.Sign(yaw);

        if (Mathf.Abs(pitch) < inputDeadZone)
            pitch = 0f;

        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        float distance = Vector3.Distance(transform.position, agent.GetCurrentTargetPosition());
        float desiredSpeed = GetDesiredSpeed(agent, bot, angle, distance);

        bool accelerating = bot.CurrentSpeed < desiredSpeed - speedTolerance;
        bool braking = bot.CurrentSpeed > desiredSpeed + speedTolerance;

        bot.SetAiInput(pitch, yaw, accelerating, braking);

        Debug.DrawLine(transform.position, targetPosition, Color.green);

        if (debugLogs)
        {
            debugTimer -= Time.deltaTime;

            if (debugTimer <= 0f)
            {
                debugTimer = 0.5f;

                Debug.Log(
                    $"{gameObject.name} FSM target={agent.currentTarget.name} " +
                    $"angle={angle:F1} yaw={yaw:F2} pitch={pitch:F2} speed={bot.CurrentSpeed:F1}/{desiredSpeed:F1}"
                );
            }
        }
    }

    private float GetDesiredSpeed(BotAgent agent, Bot bot, float angle, float distanceToCheckpoint)
    {
        float desiredSpeed = cruiseSpeed;

        if (angle > slowAngle)
            desiredSpeed = Mathf.Lerp(cruiseSpeed, turnSpeed, Mathf.InverseLerp(slowAngle, hardTurnAngle, angle));

        if (distanceToCheckpoint < slowCheckpointDistance)
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
            if (Mathf.Abs(localDirection.x) > inputDeadZone)
                return Mathf.Sign(localDirection.x) * maxYawInput;

            return lastYawDirection * maxYawInput;
        }

        return Mathf.Clamp(localDirection.x * yawSensitivity, -maxYawInput, maxYawInput);
    }
}
