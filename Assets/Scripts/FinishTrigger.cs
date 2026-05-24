using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
	void OnTriggerEnter(Collider other)
	{
		if (RaceManager.Instance == null)
			return;

		BotAgent botAgent = other.GetComponent<BotAgent>();

		if (botAgent == null)
			botAgent = other.GetComponentInParent<BotAgent>();

		if (botAgent != null)
		{
			if (botAgent.HasPassedAllCheckpoints())
			{
				RaceManager.Instance.FinishRace($"{botAgent.gameObject.name} finished. All checkpoints passed.");
			}
			else
			{
				Debug.Log($"{botAgent.gameObject.name} reached finish without all checkpoints.");
			}

			return;
		}

		if (!other.transform.root.CompareTag("Player"))
			return;

		RaceManager.Instance.TryFinish();
	}
}
