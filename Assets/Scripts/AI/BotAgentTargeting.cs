using UnityEngine;

public partial class BotAgent
{
    public Vector3 GetTargetPositionByState()
    {
        if (currentTarget == null)
            return transform.position + transform.forward * 100f;

        Vector3 finalTarget = GetBaseTargetPosition();

        finalTarget = ApplyCoinBias(finalTarget);
        finalTarget = ApplyBombAvoidance(finalTarget);

        return finalTarget;
    }

    private Vector3 GetBaseTargetPosition()
    {
        if (recoveringCheckpoint)
            return GetRecoveryTargetPosition();

        return GetCurrentTargetPosition();
    }

    private Vector3 GetRecoveryTargetPosition()
    {
        float setupDistance = Vector3.Distance(transform.position, recoveryApproachPoint);

        if (setupDistance > recoverySetupReachedDistance)
            return recoveryApproachPoint;

        return GetCurrentTargetPosition();
    }

    public Vector3 GetCurrentTargetPosition()
    {
        if (currentTarget == null)
            return transform.position + transform.forward * 100f;

        return GetCheckpointAimPosition(currentTarget);
    }

    private Vector3 GetCheckpointAimPosition(Transform checkpointTransform)
    {
        if (checkpointTransform == null)
            return transform.position;

        Checkpoint checkpoint = checkpointTransform.GetComponent<Checkpoint>();

        if (checkpoint == null)
            checkpoint = checkpointTransform.GetComponentInParent<Checkpoint>();

        if (checkpoint != null)
        {
            if (checkpoint.portalSurface != null && TryGetBoundsCenter(checkpoint.portalSurface.transform, out Vector3 portalCenter))
                return portalCenter;

            if (TryGetBoundsCenter(checkpoint.transform, out Vector3 checkpointCenter))
                return checkpointCenter;
        }

        if (TryGetBoundsCenter(checkpointTransform, out Vector3 center))
            return center;

        return checkpointTransform.position;
    }

    private Vector3 GetCheckpointRecoveryApproachPoint()
    {
        Vector3 checkpointPosition = GetCurrentTargetPosition();
        Vector3 passDirection = GetCurrentCheckpointPassDirection();

        if (passDirection.sqrMagnitude < 1f)
            passDirection = currentTarget != null ? currentTarget.forward : transform.forward;

        passDirection.Normalize();

        return checkpointPosition - passDirection * recoverySetupDistance;
    }

    private Vector3 GetCurrentCheckpointPassDirection()
    {
        if (currentTarget == null)
            return transform.forward;

        Vector3 checkpointPosition = GetCurrentTargetPosition();
        Transform previousCheckpoint = GetPreviousCheckpointTransform();

        if (previousCheckpoint != null)
            return checkpointPosition - GetCheckpointAimPosition(previousCheckpoint);

        if ((checkpointPosition - startPosition).sqrMagnitude > 1f)
            return checkpointPosition - startPosition;

        return currentTarget.forward;
    }

    private Transform GetPreviousCheckpointTransform()
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return null;

        int previousIndex = currentCheckpointIndex - 1;

        if (previousIndex >= 0 && previousIndex < checkpoints.Length)
            return checkpoints[previousIndex];

        return null;
    }

    private bool TryGetBoundsCenter(Transform root, out Vector3 center)
    {
        center = Vector3.zero;

        if (!TryGetBounds(root, out Bounds bounds))
            return false;

        center = bounds.center;
        return true;
    }

    private bool TryGetBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        if (root == null)
            return false;

        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                if (collider == null)
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
        }

        if (!hasBounds)
            return false;

        return true;
    }

    private Vector3 ApplyCoinBias(Vector3 checkpointTarget)
    {
        if (nearestCoin == null || currentState == BotState.Recover)
            return checkpointTarget;

        Vector3 toCoin = nearestCoin.position - transform.position;
        float angleToCoin = Vector3.Angle(transform.forward, toCoin.normalized);
        float dynamicCoinInfluence = 0f;

        if (angleToCoin < 12f)
            dynamicCoinInfluence = 0.75f;
        else if (angleToCoin < 25f)
            dynamicCoinInfluence = 0.55f;
        else if (angleToCoin < maxCoinAngle)
            dynamicCoinInfluence = 0.35f;

        Vector3 coinTarget = nearestCoin.position;

        // More vertical freedom for coins.
        coinTarget.y = Mathf.Lerp(checkpointTarget.y, nearestCoin.position.y, 0.65f);

        return Vector3.Lerp(checkpointTarget, coinTarget, dynamicCoinInfluence);
    }

    private Vector3 ApplyBombAvoidance(Vector3 target)
    {
        if (nearestBomb == null)
            return target;

        Vector3 localBomb = transform.InverseTransformPoint(nearestBomb.position);

        if (localBomb.z <= 8f)
            return target;

        if (Mathf.Abs(localBomb.x) > bombAvoidSideRange ||
            Mathf.Abs(localBomb.y) > bombAvoidVerticalRange)
            return target;

        float bombDistance = Vector3.Distance(transform.position, nearestBomb.position);

        if (bombDistance >= bombAvoidDistance)
            return target;

        Vector3 localDodge = GetLocalBombDodge(localBomb);

        if (localDodge.sqrMagnitude <= 0.01f)
            return target;

        localDodge.Normalize();

        Vector3 worldDodge = transform.TransformDirection(localDodge);
        float avoidFactor = Mathf.InverseLerp(bombAvoidDistance, 8f, bombDistance);

        return target + worldDodge * bombAvoidStrength * avoidFactor;
    }

    private Vector3 GetLocalBombDodge(Vector3 localBomb)
    {
        Vector3 localDodge = Vector3.zero;

        if (Mathf.Abs(localBomb.x) > 3f)
            localDodge.x = -Mathf.Sign(localBomb.x);

        if (Mathf.Abs(localBomb.y) > 3f)
            localDodge.y = -Mathf.Sign(localBomb.y);
        else
            localDodge.y = 1f;

        // Center bombs need more vertical dodge.
        if (Mathf.Abs(localBomb.x) < 20f)
        {
            localDodge.x *= 0.35f;
            localDodge.y *= 1.25f;
        }

        // Wide rows are better passed over/under.
        if (Mathf.Abs(localBomb.x) < 35f && Mathf.Abs(localBomb.y) < 25f)
        {
            localDodge.x *= 0.2f;
            localDodge.y = 1f;
        }

        return localDodge;
    }
}
