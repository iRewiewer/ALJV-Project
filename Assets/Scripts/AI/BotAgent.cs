using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Bot))]
[RequireComponent(typeof(BotRewardSystem))]
public class BotAgent : MonoBehaviour
{
    [Header("AI Type")]
    public BotAIType aiType;

    [Header("Path")]
    public Transform[] checkpoints;
    public int currentCheckpointIndex;

    [Header("Sensors")]
    public float coinDetectionRadius = 140f;
    public float bombDetectionRadius = 90f;
    public float checkpointReachDistance = 180f;
    public float recoverDistanceFromTrack = 400f;

    [Header("Coin Guidance")]
    public float coinInfluence = 0.12f;
    public float minCoinDistance = 25f;
    public float maxCoinAngle = 45f;

    [Header("Bomb Avoidance")]
    public float bombAvoidDistance = 80f;
    public float bombAvoidStrength = 85f;

    [Header("Runtime")]
    public BotState currentState = BotState.Racing;
    public Transform currentTarget;
    public Transform nearestCoin;
    public Transform nearestBomb;

    private Bot bot;
    private BotRewardSystem rewardSystem;
    private FSMBotBrain fsmBrain;
    private RLBotBrain rlBrain;

    private float previousDistanceToTarget;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private int lastSafeCheckpointIndex = 0;

    private readonly HashSet<GameObject> collectedCoins = new HashSet<GameObject>();

    private void Awake()
    {
        bot = GetComponent<Bot>();
        rewardSystem = GetComponent<BotRewardSystem>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Start()
    {
        fsmBrain = GetComponent<FSMBotBrain>();
        rlBrain = GetComponent<RLBotBrain>();

        RefreshCurrentTarget();

        if (currentTarget != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        Debug.Log($"{gameObject.name} BotAgent START. checkpoints={(checkpoints == null ? 0 : checkpoints.Length)}, target={(currentTarget == null ? "NULL" : currentTarget.name)}");
    }

    private void Update()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no checkpoints.");
            bot.SetAiInput(0f, 0f, false, true);
            return;
        }

        if (currentTarget == null)
            RefreshCurrentTarget();

        rewardSystem.AddTimePenalty(Time.deltaTime);

        ScanEnvironment();
        UpdateState();
        ApplyProgressReward();

        if (fsmBrain == null)
            fsmBrain = GetComponent<FSMBotBrain>();

        if (rlBrain == null)
            rlBrain = GetComponent<RLBotBrain>();

        if (fsmBrain != null)
        {
            // FSM controlează efectiv nava.
            fsmBrain.Tick(this, bot);
        }
        else
        {
            Debug.LogError($"{gameObject.name} has no FSMBotBrain.");
            bot.SetAiInput(0f, 0f, false, true);
        }

        if (rlBrain != null)
        {
            // RL este doar skeleton/observer pentru Demo 3.
            rlBrain.Observe(this, rewardSystem);
        }
    }

    public void RefreshCurrentTarget()
    {
        if (checkpoints != null && checkpoints.Length > 0)
        {
            currentCheckpointIndex = Mathf.Clamp(currentCheckpointIndex, 0, checkpoints.Length - 1);
            currentTarget = checkpoints[currentCheckpointIndex];
        }
    }

    private void ScanEnvironment()
    {
        nearestCoin = FindBestPathCoin();

        // Important:
        // Pentru bombe nu mai folosim simplu "nearest",
        // pentru că poate lua o bombă din spate și botul continuă să o evite inutil.
        nearestBomb = FindDangerBombInFront();
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

            // Asta e fixul principal:
            // dacă bomba este în spatele navei, o ignorăm complet.
            if (localBomb.z < 8f)
                continue;

            float angleToBomb = Vector3.Angle(transform.forward, toBomb.normalized);

            // Ignoră bombe foarte laterale, care nu sunt pe traiectorie.
            if (angleToBomb > 75f)
                continue;

            // Preferă bombe apropiate și aproape de direcția de mers.
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
            if (coin == null)
                continue;

            if (collectedCoins.Contains(coin))
                continue;

            Vector3 toCoin = coin.transform.position - transform.position;
            float distanceToCoin = toCoin.magnitude;

            if (distanceToCoin > coinDetectionRadius)
                continue;

            if (distanceToCoin < minCoinDistance)
                continue;

            Vector3 localCoin = transform.InverseTransformPoint(coin.transform.position);

            // Ignore coins behind the bot.
            if (localCoin.z < 5f)
                continue;

            float angleToCoin = Vector3.Angle(transform.forward, toCoin.normalized);

            // Important:
            // Botul vrea coins doar dacă nu trebuie să vireze mult pentru ele.
            if (angleToCoin > maxCoinAngle)
                continue;

            // Ignore coins that are further than the current checkpoint.
            if (distanceToCoin > distanceToCheckpoint)
                continue;

            float angleCoinToCheckpoint = Vector3.Angle(toCoin.normalized, toCheckpoint.normalized);

            // Coin-ul trebuie să fie aproximativ pe drumul spre checkpoint.
            if (angleCoinToCheckpoint > 70f)
                continue;

            /*
             * Scoring:
             * - preferă coins din față
             * - preferă coins aproape de direcția actuală
             * - preferă coins aproape de traseul către checkpoint
             */
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

    private Transform FindNearestWithTagSafe(string tag, float radius)
    {
        GameObject[] objects;

        try
        {
            objects = GameObject.FindGameObjectsWithTag(tag);
        }
        catch
        {
            return null;
        }

        Transform nearest = null;
        float nearestDistance = radius;

        foreach (GameObject obj in objects)
        {
            if (obj == null)
                continue;

            float distance = Vector3.Distance(transform.position, obj.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = obj.transform;
            }
        }

        return nearest;
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

        if (nearestCoin != null)
        {
            currentState = BotState.Collect;
            return;
        }

        if (currentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

            if (distanceToTarget > recoverDistanceFromTrack)
            {
                currentState = BotState.Recover;
                return;
            }
        }

        currentState = BotState.Racing;
    }

    public Vector3 GetTargetPositionByState()
    {
        if (currentTarget == null)
            return transform.position + transform.forward * 100f;

        Vector3 checkpointTarget = currentTarget.position;
        Vector3 finalTarget = checkpointTarget;

        if (nearestCoin != null)
        {
            Vector3 toCoin = nearestCoin.position - transform.position;
            float angleToCoin = Vector3.Angle(transform.forward, toCoin.normalized);

            float dynamicCoinInfluence = 0f;

            if (angleToCoin < 12f)
            {
                // Coin almost straight ahead: strongly bias toward it.
                dynamicCoinInfluence = 0.75f;
            }
            else if (angleToCoin < 25f)
            {
                // Small turn: worth going for.
                dynamicCoinInfluence = 0.55f;
            }
            else if (angleToCoin < maxCoinAngle)
            {
                // Wider turn: try, but do not sacrifice the whole route.
                dynamicCoinInfluence = 0.35f;
            }

            Vector3 coinTarget = nearestCoin.position;

            // Let the bot adjust vertically more for coins, but not completely.
            coinTarget.y = Mathf.Lerp(checkpointTarget.y, nearestCoin.position.y, 0.65f);

            finalTarget = Vector3.Lerp(checkpointTarget, coinTarget, dynamicCoinInfluence);
        }
        /*
         * Bombs:
         * Deviație laterală, fără să abandonăm checkpoint-ul.
         */
        if (nearestBomb != null)
        {
            Vector3 localBomb = transform.InverseTransformPoint(nearestBomb.position);

            // Safety check: dacă bomba a ajuns în spate, o ignorăm.
            if (localBomb.z > 8f)
            {
                float bombDistance = Vector3.Distance(transform.position, nearestBomb.position);

                if (bombDistance < bombAvoidDistance)
                {
                    /*
                     * Dodge 3D:
                     * - dacă bomba e în stânga, mergem dreapta
                     * - dacă bomba e în dreapta, mergem stânga
                     * - dacă e pe centru sau rândul e lat, folosim și verticala
                     */

                    Vector3 localDodge = Vector3.zero;

                    // Componenta laterală.
                    if (Mathf.Abs(localBomb.x) > 3f)
                        localDodge.x = -Mathf.Sign(localBomb.x);

                    // Componenta verticală.
                    // Dacă bomba e sub bot, dodge în sus.
                    // Dacă bomba e peste bot, dodge în jos.
                    if (Mathf.Abs(localBomb.y) > 3f)
                        localDodge.y = -Mathf.Sign(localBomb.y);
                    else
                        localDodge.y = 1f; // dacă e pe aceeași înălțime, preferăm să trecem pe deasupra

                    /*
                     * Dacă bomba e aproape pe centru față de bot,
                     * verticala devine mai importantă decât laterala.
                     */
                    if (Mathf.Abs(localBomb.x) < 20f)
                    {
                        localDodge.x *= 0.35f;
                        localDodge.y *= 1.25f;
                    }

                    /*
                     * Dacă rândul de bombe e lat, dodge lateral nu ajută.
                     * Vertical dodge devine soluția principală.
                     */
                    if (Mathf.Abs(localBomb.x) < 35f && Mathf.Abs(localBomb.y) < 25f)
                    {
                        localDodge.x *= 0.2f;
                        localDodge.y = 1f;
                    }

                    localDodge.z = 0f;

                    if (localDodge.sqrMagnitude > 0.01f)
                    {
                        localDodge.Normalize();

                        Vector3 worldDodge = transform.TransformDirection(localDodge);

                        float avoidFactor = Mathf.InverseLerp(bombAvoidDistance, 8f, bombDistance);

                        finalTarget += worldDodge * bombAvoidStrength * avoidFactor;
                    }
                }
            }
        }

        return finalTarget;
    }
    public void CollectCoin(GameObject coinObject, int value)
    {
        if (coinObject == null)
            return;

        if (collectedCoins.Contains(coinObject))
            return;

        collectedCoins.Add(coinObject);

        if (rewardSystem != null)
        {
            rewardSystem.AddReward(rewardSystem.coinReward * value, "Coin", true);
        }

        Debug.Log($"{gameObject.name} collected coin: {coinObject.name}");
    }
    private void ApplyProgressReward()
    {
        if (currentTarget == null)
            return;

        float currentDistance = Vector3.Distance(transform.position, currentTarget.position);
        float progress = previousDistanceToTarget - currentDistance;

        rewardSystem.AddProgressReward(progress);

        Vector3 directionToTarget = (currentTarget.position - transform.position).normalized;
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

        bool aligned = angleToTarget < 35f;
        bool movingFast = bot.CurrentSpeed > bot.MinSpeed + 10f;

        rewardSystem.AddSpeedReward(aligned && movingFast);

        previousDistanceToTarget = currentDistance;
    }

    public bool TryCompleteCheckpointIndex(int checkpointIndex)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return false;

        if (checkpointIndex != currentCheckpointIndex)
        {
            Debug.Log($"{gameObject.name} touched wrong checkpoint. Touched index: {checkpointIndex}, expected: {currentCheckpointIndex}");
            return false;
        }

        rewardSystem.AddCheckpointReward();

        lastSafeCheckpointIndex = currentCheckpointIndex;

        currentCheckpointIndex++;

        if (currentCheckpointIndex >= checkpoints.Length)
            currentCheckpointIndex = 0;

        RefreshCurrentTarget();

        if (currentTarget != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        Debug.Log($"{gameObject.name} passed checkpoint index {checkpointIndex}. New target: {currentTarget.name}");

        return true;
    }

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

        if (other.CompareTag("Coin"))
        {
            return;
        }

        if (other.CompareTag("Bomb"))
        {
            rewardSystem.AddBombPenalty();

            Debug.Log($"{gameObject.name} hit bomb: {other.name}. Teleporting to last checkpoint.");

            TeleportToLastSafeCheckpoint();
        }
    }

    private void TeleportToLastSafeCheckpoint()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
            bot.ResetSpeedToDefault();
            return;
        }

        int safeIndex = Mathf.Clamp(lastSafeCheckpointIndex, 0, checkpoints.Length - 1);
        Transform safeCheckpoint = checkpoints[safeIndex];

        if (safeCheckpoint == null)
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
            bot.ResetSpeedToDefault();
            return;
        }

        Vector3 safePosition = safeCheckpoint.position;

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

                // Îl punem puțin după ultimul checkpoint, ca să nu re-trigger-uiască imediat.
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
    private void OnDrawGizmos()
    {
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawSphere(currentTarget.position, 6f);
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