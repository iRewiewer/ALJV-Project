using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
	void OnTriggerEnter(Collider other)
	{
		BotAgent botAgent = other.GetComponent<BotAgent>();

		if (botAgent == null)
			botAgent = other.GetComponentInParent<BotAgent>();

		if (botAgent != null)
		{
			if (botAgent.TryCompleteFinishLine())
				Debug.Log($"{botAgent.gameObject.name} reached the finish line after all checkpoints.");
			else
				Debug.Log($"{botAgent.gameObject.name} reached finish without all checkpoints.");

			return;
		}

		if (RaceManager.Instance == null)
			return;

		if (!other.transform.root.CompareTag("Player"))
			return;

		RaceManager.Instance.TryFinish();
	}
}
