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
    public bool alwaysAccelerate = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private float debugTimer;

    public void Tick(BotAgent agent, Bot bot)
    {
        if (agent.currentTarget == null)
        {
            bot.SetAiInput(0f, 0f, true, false);
            return;
        }

        Vector3 targetPosition = agent.GetTargetPositionByState();
        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.sqrMagnitude < 1f)
        {
            bot.SetAiInput(0f, 0f, true, false);
            return;
        }

        /*
         * Important:
         * Bot.cs moves using transform.forward.
         * So the AI must rotate the ship toward the target.
         *
         * localDirection.x = target left/right
         * localDirection.y = target up/down
         *
         * Because Bot has invertedControls checked:
         * pitch input must be NEGATIVE when target is above.
         */
        Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);

        float yaw = Mathf.Clamp(localDirection.x * yawSensitivity, -maxYawInput, maxYawInput);
        float pitch = Mathf.Clamp(-localDirection.y * pitchSensitivity, -maxPitchInput, maxPitchInput);

        if (Mathf.Abs(yaw) < inputDeadZone)
            yaw = 0f;

        if (Mathf.Abs(pitch) < inputDeadZone)
            pitch = 0f;

        bool accelerating = alwaysAccelerate;
        bool braking = false;

        bot.SetAiInput(pitch, yaw, accelerating, braking);

        Debug.DrawLine(transform.position, targetPosition, Color.green);

        if (debugLogs)
        {
            debugTimer -= Time.deltaTime;

            if (debugTimer <= 0f)
            {
                debugTimer = 0.5f;

                float angle = Vector3.Angle(transform.forward, toTarget.normalized);

                Debug.Log(
                    $"{gameObject.name} FSM target={agent.currentTarget.name} " +
                    $"angle={angle:F1} yaw={yaw:F2} pitch={pitch:F2} speed={bot.CurrentSpeed:F1}"
                );
            }
        }
    }
}