using System;
using System.Collections.Generic;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
	public GameObject portalSurface;
	public float rotationSpeed = 80f;
	public int checkpointIndex = -1;

	private readonly HashSet<Transform> passedRoots = new HashSet<Transform>();

	void Update()
	{
		if (portalSurface != null && portalSurface.activeSelf)
			portalSurface.transform.Rotate(0f, 0f, Time.deltaTime * rotationSpeed, Space.Self);
	}

	void OnTriggerEnter(Collider other)
	{
		BotAgent botAgent = other.GetComponent<BotAgent>();

		if (botAgent == null)
			botAgent = other.GetComponentInParent<BotAgent>();

		if (botAgent != null)
		{
			botAgent.TryCompleteCheckpointIndex(GetCheckpointIndex());
			return;
		}

		if (RaceManager.Instance == null)
			return;

		Transform root = other.transform.root;

		if (!root.CompareTag("Player"))
			return;

		if (passedRoots.Contains(root))
			return;

		passedRoots.Add(root);

		RaceManager.Instance.SetLastCheckpoint(transform);
		RaceManager.Instance.RegisterCheckpointPassed();

		// Just hide the portal visual, keep trigger alive.
		if (portalSurface != null)
			portalSurface.SetActive(false);
	}

	public int GetCheckpointIndex()
	{
		if (checkpointIndex >= 0)
			return checkpointIndex;

		Checkpoint[] checkpoints = FindObjectsByType<Checkpoint>(
			FindObjectsInactive.Exclude,
			FindObjectsSortMode.None
		);

		Array.Sort(checkpoints, CompareByHierarchyOrder);

		for (int i = 0; i < checkpoints.Length; i++)
		{
			if (checkpoints[i] == this)
				return i;
		}

		return 0;
	}

	private int CompareByHierarchyOrder(Checkpoint a, Checkpoint b)
	{
		return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
	}
}
