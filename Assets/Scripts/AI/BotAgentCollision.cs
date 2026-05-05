using UnityEngine;

public partial class BotAgent
{
    private void OnTriggerEnter(Collider other)
    {
        HandlePickupOrHazard(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePickupOrHazard(collision.gameObject);
    }

    private void HandlePickupOrHazard(GameObject other)
    {
        if (other == null)
            return;

        if (TryHandleCheckpoint(other))
            return;

        Coin coin = FindPickupComponent<Coin>(other);

        if (coin != null || HasTag(other, "Coin"))
        {
            GameObject coinObject = coin != null ? coin.gameObject : FindTaggedObject(other, "Coin");
            int value = coin != null ? coin.value : 1;

            CollectCoin(coinObject, value);

            if (coinObject != null)
                Destroy(coinObject);

            return;
        }

        Bomb bomb = FindPickupComponent<Bomb>(other);

        if (bomb != null || HasTag(other, "Bomb"))
        {
            GameObject bombObject = bomb != null ? bomb.gameObject : FindTaggedObject(other, "Bomb");

            if (rewardSystem != null)
                rewardSystem.AddBombPenalty();

            TeleportToLastSafeCheckpoint();

            if (nearestBomb != null && bombObject != null && nearestBomb.IsChildOf(bombObject.transform))
                nearestBomb = null;

            if (bombObject != null)
                Destroy(bombObject);
        }
    }

    private bool TryHandleCheckpoint(GameObject other)
    {
        BotCheckpointTrigger botCheckpoint = FindPickupComponent<BotCheckpointTrigger>(other);

        if (botCheckpoint != null)
        {
            TryCompleteCheckpointIndex(botCheckpoint.checkpointIndex);
            return true;
        }

        Checkpoint checkpoint = FindPickupComponent<Checkpoint>(other);

        if (checkpoint != null)
        {
            if (!TryCompleteCheckpointIndex(checkpoint.GetCheckpointIndex()))
                TryCompleteCheckpointTransform(checkpoint.transform);

            return true;
        }

        if (currentTarget != null && IsTransformFromObject(other.transform, currentTarget))
        {
            TryCompleteCheckpointTransform(other.transform);
            return true;
        }

        return false;
    }

    private bool IsTransformFromObject(Transform touched, Transform expectedRoot)
    {
        if (touched == null || expectedRoot == null)
            return false;

        return touched == expectedRoot ||
            touched.IsChildOf(expectedRoot) ||
            expectedRoot.IsChildOf(touched);
    }

    private T FindPickupComponent<T>(GameObject obj) where T : Component
    {
        if (obj == null)
            return null;

        T component = obj.GetComponent<T>();

        if (component != null)
            return component;

        component = obj.GetComponentInParent<T>();

        if (component != null)
            return component;

        return obj.GetComponentInChildren<T>();
    }

    private GameObject FindTaggedObject(GameObject obj, string tag)
    {
        if (obj == null)
            return null;

        Transform current = obj.transform;

        while (current != null)
        {
            if (HasTag(current.gameObject, tag))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private bool HasTag(GameObject obj, string tag)
    {
        if (obj == null)
            return false;

        Transform current = obj.transform;

        while (current != null)
        {
            try
            {
                if (current.CompareTag(tag))
                    return true;
            }
            catch
            {
                return false;
            }

            current = current.parent;
        }

        return false;
    }

    private void TeleportToLastSafeCheckpoint()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            ResetToStart();
            return;
        }

        int safeIndex = Mathf.Clamp(lastSafeCheckpointIndex, 0, checkpoints.Length - 1);
        Transform safeCheckpoint = checkpoints[safeIndex];

        if (safeCheckpoint == null)
        {
            ResetToStart();
            return;
        }

        Vector3 safePosition = GetCheckpointAimPosition(safeCheckpoint);
        int nextIndex = safeIndex + 1;

        if (nextIndex >= checkpoints.Length)
            nextIndex = 0;

        Transform nextCheckpoint = checkpoints[nextIndex];

        if (nextCheckpoint != null)
        {
            Vector3 directionToNext = nextCheckpoint.position - safeCheckpoint.position;

            if (directionToNext.sqrMagnitude > 1f)
            {
                directionToNext.Normalize();

                // Small push forward, stops instant retrigger.
                safePosition += directionToNext * 25f;
                transform.rotation = Quaternion.LookRotation(directionToNext, Vector3.up);
            }
            else
            {
                transform.rotation = safeCheckpoint.rotation;
            }
        }
        else
        {
            transform.rotation = safeCheckpoint.rotation;
        }

        transform.position = safePosition;
        bot.ResetSpeedToDefault();

        if (currentTarget != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
    }

    private void ResetToStart()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        bot.ResetSpeedToDefault();
    }
}
