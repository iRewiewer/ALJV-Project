using UnityEngine;

public class Bomb : MonoBehaviour
{
	public float bombAmplitude = 1.5f;
	public float bombSpeed = 1.8f;

	private Vector3 startPos;
	private float phase;
	private bool triggered;

	void Start()
	{
		startPos = transform.position;
		phase = Random.Range(0f, 1000f);
	}

	void Update()
	{
		float y = Mathf.Sin((Time.time + phase) * bombSpeed) * bombAmplitude;
		transform.position = startPos + Vector3.up * y;
	}

	void OnTriggerEnter(Collider other)
	{
		if (triggered)
			return;

		if (!other.transform.root.CompareTag("Player"))
			return;

		triggered = true;

		if (RaceManager.Instance != null)
			RaceManager.Instance.RespawnAtLastCheckpoint();

		Destroy(gameObject);
	}
}
