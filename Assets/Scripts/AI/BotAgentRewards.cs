using UnityEngine;

public partial class BotAgent
{
    public void CollectCoin(GameObject coinObject, int value)
    {
        if (coinObject == null || collectedCoins.Contains(coinObject))
            return;

        collectedCoins.Add(coinObject);

        if (rewardSystem != null)
            rewardSystem.AddCoinReward(value);

        if (nearestCoin != null && coinObject != null && nearestCoin.IsChildOf(coinObject.transform))
            nearestCoin = null;
    }

    private void ApplyProgressReward()
    {
        if (currentTarget == null || rewardSystem == null || bot == null)
            return;

        Vector3 targetPosition = GetCurrentTargetPosition();
        float currentDistance = Vector3.Distance(transform.position, targetPosition);
        float progress = previousDistanceToTarget - currentDistance;

        rewardSystem.AddProgressReward(progress);

        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

        bool aligned = angleToTarget < 35f;
        bool movingFast = bot.CurrentSpeed > bot.MinSpeed + 10f;

        rewardSystem.AddSpeedReward(aligned && movingFast);

        previousDistanceToTarget = currentDistance;
    }

    private void TryCompleteCurrentCheckpointByOverlap()
    {
        if (HasPassedAllCheckpoints())
            return;

        if (currentTarget == null)
            return;

        if (!TryGetBotBounds(out Bounds botBounds))
            return;

        if (!TryGetCheckpointPassBounds(currentTarget, out Bounds checkpointBounds))
            return;

        if (botBounds.Intersects(checkpointBounds))
            TryCompleteCheckpointIndex(currentCheckpointIndex);
    }

    private bool TryCompleteFinishLineByOverlap()
    {
        if (!HasPassedAllCheckpoints() || finishLinePassed)
            return false;

        Transform finishTarget = finishLineTarget != null ? finishLineTarget : currentTarget;
        if (finishTarget == null)
            return false;

        if (!TryGetBotBounds(out Bounds botBounds))
            return false;

        if (!TryGetCheckpointPassBounds(finishTarget, out Bounds finishBounds))
            return false;

        if (!botBounds.Intersects(finishBounds))
            return false;

        return TryCompleteFinishLine();
    }

    private bool TryGetBotBounds(out Bounds botBounds)
    {
        botBounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;

        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;

            if (!hasBounds)
            {
                botBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                botBounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
            return true;

        return TryGetBounds(transform, out botBounds);
    }

    private bool TryGetCheckpointPassBounds(Transform checkpointTransform, out Bounds checkpointBounds)
    {
        checkpointBounds = new Bounds(Vector3.zero, Vector3.zero);

        if (checkpointTransform == null)
            return false;

        BotCheckpointTrigger botTrigger = checkpointTransform.GetComponent<BotCheckpointTrigger>();

        if (botTrigger == null)
            botTrigger = checkpointTransform.GetComponentInParent<BotCheckpointTrigger>();

        if (botTrigger != null && TryGetColliderBounds(botTrigger.transform, out checkpointBounds))
            return true;

        Checkpoint checkpoint = checkpointTransform.GetComponent<Checkpoint>();

        if (checkpoint == null)
            checkpoint = checkpointTransform.GetComponentInParent<Checkpoint>();

        if (checkpoint != null)
        {
            if (TryGetColliderBounds(checkpoint.transform, out checkpointBounds))
                return true;

            if (checkpoint.portalSurface != null && TryGetBounds(checkpoint.portalSurface.transform, out checkpointBounds))
                return true;
        }

        return TryGetColliderBounds(checkpointTransform, out checkpointBounds) ||
            TryGetBounds(checkpointTransform, out checkpointBounds);
    }

    private bool TryGetColliderBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        if (root == null)
            return false;

        bool hasBounds = false;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    public bool TryCompleteCheckpointIndex(int checkpointIndex)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return false;

        if (HasPassedAllCheckpoints())
            return false;

        if (checkpointIndex != currentCheckpointIndex)
        {
            if (checkpointIndex == lastSafeCheckpointIndex)
                return false;

            Debug.Log($"{gameObject.name} touched checkpoint {checkpointIndex}, expected {currentCheckpointIndex}.");
            return false;
        }

        if (!IsMovingThroughCheckpointForward())
            return false;

        if (rewardSystem != null)
            rewardSystem.AddCheckpointReward();

        passedCheckpointCount = Mathf.Min(passedCheckpointCount + 1, checkpoints.Length);
        lastSafeCheckpointIndex = currentCheckpointIndex;
        currentCheckpointIndex++;

        if (currentCheckpointIndex >= checkpoints.Length)
            currentCheckpointIndex = 0;

        RefreshCurrentTarget();

        if (currentTarget != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, GetCurrentTargetPosition());

        return true;
    }

    private bool IsMovingThroughCheckpointForward()
    {
        Vector3 passDirection = GetCurrentCheckpointPassDirection();

        if (passDirection.sqrMagnitude < 1f)
            return true;

        passDirection.Normalize();

        return Vector3.Dot(transform.forward, passDirection) >= checkpointPassDotThreshold;
    }

    public bool TryCompleteCheckpointTransform(Transform checkpointTransform)
    {
        if (HasPassedAllCheckpoints())
            return false;

        if (checkpointTransform == null || currentTarget == null)
            return false;

        if (!IsSameCheckpoint(checkpointTransform, currentTarget))
            return false;

        return TryCompleteCheckpointIndex(currentCheckpointIndex);
    }

    private bool IsSameCheckpoint(Transform touchedCheckpoint, Transform expectedCheckpoint)
    {
        if (touchedCheckpoint == null || expectedCheckpoint == null)
            return false;

        if (touchedCheckpoint == expectedCheckpoint)
            return true;

        if (touchedCheckpoint.IsChildOf(expectedCheckpoint))
            return true;

        if (expectedCheckpoint.IsChildOf(touchedCheckpoint))
            return true;

        Checkpoint touchedRegular = touchedCheckpoint.GetComponentInParent<Checkpoint>();
        Checkpoint expectedRegular = expectedCheckpoint.GetComponentInParent<Checkpoint>();

        if (touchedRegular != null && expectedRegular != null && touchedRegular == expectedRegular)
            return true;

        BotCheckpointTrigger touchedBot = touchedCheckpoint.GetComponentInParent<BotCheckpointTrigger>();
        BotCheckpointTrigger expectedBot = expectedCheckpoint.GetComponentInParent<BotCheckpointTrigger>();

        return touchedBot != null && expectedBot != null && touchedBot == expectedBot;
    }

    public bool HasPassedAllCheckpoints()
    {
        return checkpoints != null &&
            checkpoints.Length > 0 &&
            passedCheckpointCount >= checkpoints.Length;
    }
}
