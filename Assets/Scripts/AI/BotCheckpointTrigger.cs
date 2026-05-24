using UnityEngine;

public class BotCheckpointTrigger : MonoBehaviour
{
    [Header("Checkpoint Order")]
    public int checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        BotAgent botAgent = other.GetComponent<BotAgent>();

        if (botAgent == null)
            botAgent = other.GetComponentInParent<BotAgent>();

        if (botAgent == null)
            return;

        botAgent.TryCompleteCheckpointIndex(checkpointIndex);
    }
}