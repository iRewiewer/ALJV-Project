using UnityEngine;

public class Coin : MonoBehaviour
{
	public int value = 1;

	void Update()
	{
		transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
	}

	void OnTriggerEnter(Collider other)
	{
		if (!other.transform.root.CompareTag("Player"))
			return;

		if (RaceManager.Instance != null)
			RaceManager.Instance.AddScore(value);

		Destroy(gameObject);
	}
}
