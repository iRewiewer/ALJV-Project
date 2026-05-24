using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Bot))]
[RequireComponent(typeof(BotRewardSystem))]
[RequireComponent(typeof(Rigidbody))]
public partial class BotAgent : MonoBehaviour
{
    public static bool showAiGizmos = true;

    [Header("AI Type")]
    public BotAIType aiType = BotAIType.FiniteStateMachine;

    [Header("Path")]
    public bool autoFindCheckpoints = true;
    public Transform[] checkpoints;
    public int currentCheckpointIndex;
    public int passedCheckpointCount;
    public float checkpointPassDotThreshold = 0.15f;

    [Header("Sensors")]
    public float coinDetectionRadius = 140f;
    public float bombDetectionRadius = 55f;
    public float checkpointReachDistance = 70f;
    public float recoverDistanceFromTrack = 400f;
    public float recoverAngleFromTarget = 105f;

    [Header("Recovery")]
    public float recoveryPastCheckpointDistance = 90f;
    public float recoverySetupDistance = 300f;
    public float recoverySetupReachedDistance = 90f;
    public float recoveryReadyAngle = 35f;
    public float recoveryMinCheckpointDistance = 140f;

    [Header("Coin Guidance")]
    public float coinInfluence = 0.12f;
    public float minCoinDistance = 25f;
    public float maxCoinAngle = 45f;

    [Header("Bomb Avoidance")]
    public float bombAvoidDistance = 45f;
    public float bombAvoidStrength = 45f;
    public float bombAvoidSideRange = 28f;
    public float bombAvoidVerticalRange = 22f;

    [Header("Runtime")]
    public BotState currentState = BotState.Racing;
    public Transform currentTarget;
    public Transform nearestCheckpoint;
    public Transform nearestCoin;
    public Transform nearestBomb;
    public bool recoveringCheckpoint;
    public Vector3 recoveryApproachPoint;

    private Bot bot;
    private BotRewardSystem rewardSystem;
    private FSMBotBrain fsmBrain;
    private RLBotBrain rlBrain;

    private float previousDistanceToTarget;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private int lastSafeCheckpointIndex;

    private readonly HashSet<GameObject> collectedCoins = new HashSet<GameObject>();
    private readonly HashSet<GameObject> consumedBombs = new HashSet<GameObject>();

    private void Awake()
    {
        bot = GetComponent<Bot>();
        rewardSystem = GetComponent<BotRewardSystem>();
        EnsureTriggerEventsWork();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Start()
    {
        CacheBrains();
        FindCheckpointsIfNeeded();
        RefreshCurrentTarget();

        if (currentTarget != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, GetCurrentTargetPosition());
    }

    private void Update()
    {
        FindCheckpointsIfNeeded();

        if (!HasPath())
        {
            bot.SetAiInput(0f, 0f, false, true);
            return;
        }

        if (currentTarget == null)
            RefreshCurrentTarget();

        if (rewardSystem != null)
            rewardSystem.AddTimePenalty(Time.deltaTime);

        ScanEnvironment();
        TryHandlePickupsAndHazardsByOverlap();
        TryCompleteCurrentCheckpointByOverlap();
        UpdateState();
        ApplyProgressReward();
        TickSelectedBrain();
    }

    public void RefreshCurrentTarget()
    {
        FindCheckpointsIfNeeded();

        if (!HasPath())
            return;

        currentCheckpointIndex = Mathf.Clamp(currentCheckpointIndex, 0, checkpoints.Length - 1);
        currentTarget = checkpoints[currentCheckpointIndex];
    }

    private bool HasPath()
    {
        return checkpoints != null && checkpoints.Length > 0;
    }

    private void FindCheckpointsIfNeeded()
    {
        if (!autoFindCheckpoints)
            return;

        if (HasPath())
            return;

        BotCheckpointTrigger[] botTriggers = FindObjectsByType<BotCheckpointTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (botTriggers.Length > 0)
        {
            checkpoints = botTriggers
                .OrderBy(trigger => trigger.checkpointIndex)
                .Select(trigger => trigger.transform)
                .ToArray();

            return;
        }

        Checkpoint[] sceneCheckpoints = FindObjectsByType<Checkpoint>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (sceneCheckpoints.Length > 0)
        {
            // Fallback if no bot-only checkpoint triggers exist.
            checkpoints = sceneCheckpoints
                .OrderBy(checkpoint => checkpoint.transform.GetSiblingIndex())
                .Select(checkpoint => checkpoint.transform)
                .ToArray();
        }
    }

    private void CacheBrains()
    {
        if (fsmBrain == null)
            fsmBrain = GetComponent<FSMBotBrain>();

        if (rlBrain == null)
            rlBrain = GetComponent<RLBotBrain>();
    }

    private void EnsureTriggerEventsWork()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void TickSelectedBrain()
    {
        CacheBrains();

        switch (aiType)
        {
            case BotAIType.ReinforcementLearning:
                TickRlBrain();
                break;

            case BotAIType.FiniteStateMachine:
            default:
                TickFsmBrain();
                break;
        }
    }

    private void TickFsmBrain()
    {
        if (fsmBrain == null)
        {
            Debug.LogError($"{gameObject.name} has no FSMBotBrain.");
            bot.SetAiInput(0f, 0f, false, true);
            return;
        }

        fsmBrain.Tick(this, bot);
    }

    private void TickRlBrain()
    {
        if (rlBrain == null)
        {
            Debug.LogWarning($"{gameObject.name} has no RLBotBrain, using FSM fallback.");
            TickFsmBrain();
            return;
        }

        rlBrain.Tick(this, bot, rewardSystem);
    }

    private void UpdateState()
    {
        if (nearestBomb != null)
        {
            float bombDistance = Vector3.Distance(transform.position, nearestBomb.position);

            if (bombDistance < bombAvoidDistance)
            {
                currentState = BotState.AvoidDanger;
                return;
            }
        }

        if (UpdateCheckpointRecoveryState())
            return;

        if (nearestCoin != null)
        {
            currentState = BotState.Collect;
            return;
        }

        currentState = BotState.Racing;
    }

    private bool UpdateCheckpointRecoveryState()
    {
        if (currentTarget == null)
        {
            recoveringCheckpoint = false;
            return false;
        }

        if (recoveringCheckpoint)
        {
            if (IsRecoveryReadyToExit())
            {
                recoveringCheckpoint = false;
                return false;
            }

            currentState = BotState.Recover;
            return true;
        }

        Vector3 toTarget = GetCurrentTargetPosition() - transform.position;

        if (toTarget.sqrMagnitude < 1f)
            return false;

        float distanceToTarget = toTarget.magnitude;
        float angleToTarget = Vector3.Angle(transform.forward, toTarget.normalized);

        if (distanceToTarget > recoverDistanceFromTrack ||
            HasPassedCheckpointPlane(recoveryPastCheckpointDistance) ||
            (angleToTarget > recoverAngleFromTarget && distanceToTarget > recoveryMinCheckpointDistance && HasPassedCheckpointPlane(0f)))
        {
            StartCheckpointRecovery();
            currentState = BotState.Recover;
            return true;
        }

        return false;
    }

    private bool HasPassedCheckpointPlane(float margin)
    {
        if (currentTarget == null)
            return false;

        Vector3 passDirection = GetCurrentCheckpointPassDirection();

        if (passDirection.sqrMagnitude < 1f)
            return false;

        passDirection.Normalize();

        float pastAmount = Vector3.Dot(transform.position - GetCurrentTargetPosition(), passDirection);

        return pastAmount > margin;
    }

    private void StartCheckpointRecovery()
    {
        recoveringCheckpoint = true;
        recoveryApproachPoint = GetCheckpointRecoveryApproachPoint();
    }

    private bool IsRecoveryReadyToExit()
    {
        Vector3 checkpointPosition = GetCurrentTargetPosition();
        Vector3 toCheckpoint = checkpointPosition - transform.position;

        if (toCheckpoint.sqrMagnitude < 1f)
            return true;

        float setupDistance = Vector3.Distance(transform.position, recoveryApproachPoint);
        float checkpointDistance = toCheckpoint.magnitude;
        float checkpointAngle = Vector3.Angle(transform.forward, toCheckpoint.normalized);

        return setupDistance <= recoverySetupReachedDistance &&
            checkpointDistance >= recoveryMinCheckpointDistance &&
            checkpointAngle <= recoveryReadyAngle;
    }

    private void OnDrawGizmos()
    {
        if (!showAiGizmos)
            return;

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Vector3 targetPosition = GetCurrentTargetPosition();
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawSphere(targetPosition, 6f);
        }

        if (recoveringCheckpoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, recoveryApproachPoint);
            Gizmos.DrawSphere(recoveryApproachPoint, 7f);
        }

        if (nearestCoin != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, nearestCoin.position);
            Gizmos.DrawSphere(nearestCoin.position, 4f);
        }

        if (nearestBomb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, nearestBomb.position);
            Gizmos.DrawSphere(nearestBomb.position, 5f);
        }
    }
}
