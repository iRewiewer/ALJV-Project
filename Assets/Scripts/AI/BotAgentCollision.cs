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

            HandleCoinPickup(coinObject, value);

            return;
        }

        Bomb bomb = FindPickupComponent<Bomb>(other);

        if (bomb != null || HasTag(other, "Bomb"))
        {
            GameObject bombObject = bomb != null ? bomb.gameObject : FindTaggedObject(other, "Bomb");

            HandleBombHit(bombObject);
        }
    }

    private void TryHandlePickupsAndHazardsByOverlap()
    {
        if (!TryGetBotBounds(out Bounds botBounds))
            return;

        if (TryHandleBombOverlap(botBounds))
            return;

        TryHandleCoinOverlap(botBounds);
    }

    private bool TryHandleBombOverlap(Bounds botBounds)
    {
        Bomb[] bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Bomb bomb in bombs)
        {
            if (bomb == null)
                continue;

            if (!TryGetObjectBounds(bomb.gameObject, out Bounds bombBounds))
                continue;

            if (!botBounds.Intersects(bombBounds))
                continue;

            HandleBombHit(bomb.gameObject);
            return true;
        }

        foreach (GameObject bombObject in FindObjectsWithTagSafe("Bomb"))
        {
            if (bombObject == null)
                continue;

            if (!TryGetObjectBounds(bombObject, out Bounds bombBounds))
                continue;

            if (!botBounds.Intersects(bombBounds))
                continue;

            HandleBombHit(bombObject);
            return true;
        }

        return false;
    }

    private void TryHandleCoinOverlap(Bounds botBounds)
    {
        Coin[] coins = FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Coin coin in coins)
        {
            if (coin == null || collectedCoins.Contains(coin.gameObject))
                continue;

            if (!TryGetObjectBounds(coin.gameObject, out Bounds coinBounds))
                continue;

            if (!botBounds.Intersects(coinBounds))
                continue;

            HandleCoinPickup(coin.gameObject, coin.value);
            return;
        }

        foreach (GameObject coinObject in FindObjectsWithTagSafe("Coin"))
        {
            if (coinObject == null || collectedCoins.Contains(coinObject))
                continue;

            if (!TryGetObjectBounds(coinObject, out Bounds coinBounds))
                continue;

            if (!botBounds.Intersects(coinBounds))
                continue;

            HandleCoinPickup(coinObject, 1);
            return;
        }
    }

    private void HandleCoinPickup(GameObject coinObject, int value)
    {
        CollectCoin(coinObject, value);

        if (coinObject != null)
            Destroy(coinObject);
    }

    private void HandleBombHit(GameObject bombObject)
    {
        if (bombObject == null || consumedBombs.Contains(bombObject))
            return;

        consumedBombs.Add(bombObject);

        if (rewardSystem != null)
            rewardSystem.AddBombPenalty();

        TeleportToLastSafeCheckpoint();

        if (nearestBomb != null && bombObject != null && nearestBomb.IsChildOf(bombObject.transform))
            nearestBomb = null;

        if (bombObject != null)
            Destroy(bombObject);
    }

    private bool TryGetObjectBounds(GameObject obj, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        if (obj == null)
            return false;

        return TryGetColliderBounds(obj.transform, out bounds) ||
            TryGetBounds(obj.transform, out bounds);
    }

    private GameObject[] FindObjectsWithTagSafe(string tag)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tag);
        }
        catch
        {
            return new GameObject[0];
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
            previousDistanceToTarget = Vector3.Distance(transform.position, GetCurrentTargetPosition());
    }

    private void ResetToStart()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        bot.ResetSpeedToDefault();
    }
}
