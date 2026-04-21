using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
	void OnTriggerEnter(Collider other)
	{
		if (RaceManager.Instance == null)
			return;

		if (!other.transform.root.CompareTag("Player"))
			return;

		RaceManager.Instance.TryFinish();
	}
}
