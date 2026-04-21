using UnityEngine;

public class Checkpoint : MonoBehaviour
{
	public GameObject portalSurface;
	public float rotationSpeed = 80f;

	private bool passed;
	private bool isDisabled = false;

	void Update()
	{
		if(!isDisabled)
		{
			portalSurface.gameObject.transform.Rotate(0f, 0f, Time.deltaTime * rotationSpeed, Space.Self);
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (passed)
			return;

		if (RaceManager.Instance == null)
			return;

		if (!other.transform.root.CompareTag("Player"))
			return;

		passed = true;
		RaceManager.Instance.SetLastCheckpoint(transform);
		RaceManager.Instance.RegisterCheckpointPassed();

		// disable collider and portal VFX after passing
		gameObject.GetComponent<BoxCollider>().enabled = false;
		portalSurface.SetActive(false);

		isDisabled = true;
	}
}
