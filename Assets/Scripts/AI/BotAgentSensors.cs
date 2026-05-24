using UnityEngine;

public partial class BotAgent
{
    private void ScanEnvironment()
    {
        nearestCheckpoint = FindNearestCheckpoint();
        nearestCoin = FindBestPathCoin();
        nearestBomb = FindDangerBombInFront();
    }

    private Transform FindNearestCheckpoint()
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return null;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Transform checkpoint in checkpoints)
        {
            if (checkpoint == null)
                continue;

            float distance = Vector3.Distance(transform.position, checkpoint.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = checkpoint;
            }
        }

        return nearest;
    }

    private Transform FindDangerBombInFront()
    {
        GameObject[] bombs;

        try
        {
            bombs = GameObject.FindGameObjectsWithTag("Bomb");
        }
        catch
        {
            return null;
        }

        Transform bestBomb = null;
        float bestScore = float.MaxValue;

        foreach (GameObject bomb in bombs)
        {
            if (bomb == null)
                continue;

            Vector3 toBomb = bomb.transform.position - transform.position;
            float distanceToBomb = toBomb.magnitude;

            if (distanceToBomb > bombDetectionRadius)
                continue;

            Vector3 localBomb = transform.InverseTransformPoint(bomb.transform.position);

            // Ignore bombs already behind us.
            if (localBomb.z < 8f)
                continue;

            if (Mathf.Abs(localBomb.x) > bombAvoidSideRange ||
                Mathf.Abs(localBomb.y) > bombAvoidVerticalRange)
                continue;

            float angleToBomb = Vector3.Angle(transform.forward, toBomb.normalized);

            // Too sideways, usually not a real danger.
            if (angleToBomb > 40f)
                continue;

            float centerDanger = Mathf.Abs(localBomb.x) + Mathf.Abs(localBomb.y);
            float score = distanceToBomb + centerDanger * 0.5f + angleToBomb * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                bestBomb = bomb.transform;
            }
        }

        return bestBomb;
    }

    private Transform FindBestPathCoin()
    {
        GameObject[] coins;

        try
        {
            coins = GameObject.FindGameObjectsWithTag("Coin");
        }
        catch
        {
            return null;
        }

        if (currentTarget == null)
            return null;

        Transform bestCoin = null;
        float bestScore = float.MaxValue;

        Vector3 toCheckpoint = currentTarget.position - transform.position;
        float distanceToCheckpoint = toCheckpoint.magnitude;

        foreach (GameObject coin in coins)
        {
            if (coin == null || collectedCoins.Contains(coin))
                continue;

            Vector3 toCoin = coin.transform.position - transform.position;
            float distanceToCoin = toCoin.magnitude;

            if (distanceToCoin > coinDetectionRadius || distanceToCoin < minCoinDistance)
                continue;

            Vector3 localCoin = transform.InverseTransformPoint(coin.transform.position);

            if (localCoin.z < 5f)
                continue;

            float angleToCoin = Vector3.Angle(transform.forward, toCoin.normalized);

            if (angleToCoin > maxCoinAngle)
                continue;

            // Keep coin chasing near the race line.
            if (distanceToCoin > distanceToCheckpoint)
                continue;

            float angleCoinToCheckpoint = Vector3.Angle(toCoin.normalized, toCheckpoint.normalized);

            if (angleCoinToCheckpoint > 70f)
                continue;

            float score =
                distanceToCoin +
                angleToCoin * 4f +
                angleCoinToCheckpoint * 2f +
                Mathf.Abs(localCoin.x) * 0.8f +
                Mathf.Abs(localCoin.y) * 0.5f;

            if (score < bestScore)
            {
                bestScore = score;
                bestCoin = coin.transform;
            }
        }

        return bestCoin;
    }
}
