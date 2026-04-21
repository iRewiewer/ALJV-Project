using UnityEngine;

public class RaceManager : MonoBehaviour
{
	public static RaceManager Instance { get; private set; }

	[Header("References")]
	public Transform player;
	public FollowCamera followCamera;
	public Transform startPoint;
	public Transform finishPoint;

	[Header("Race State")]
	public int totalCheckpoints;
	public int passedCheckpoints;
	public int score;
	public int totalCoins;

	public float elapsedTime;
	public bool finished;

	private Vector3 lastCheckpointPos;
	private Quaternion lastCheckpointRot;

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		totalCoins = FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
	}

	void Start()
	{
		SaveStartFromPlayer();
		RecountCheckpointsInScene();
	}

	void Update()
	{
		if (!finished)
			elapsedTime += Time.deltaTime;

		if (finished && Input.GetKeyDown(KeyCode.P))
		{
			Time.timeScale = 1f;
			UnityEngine.SceneManagement.SceneManager.LoadScene(
				UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
			);
		}
	}
	public void SaveStartFromPlayer()
	{
		if (player == null || startPoint == null)
			return;

		startPoint.position = player.position;
		startPoint.rotation = player.rotation;

		lastCheckpointPos = startPoint.position;
		lastCheckpointRot = startPoint.rotation;
	}


	public void Respawn()
	{
		if (player == null || startPoint == null)
			return;

		player.position = startPoint.position;
		player.rotation = startPoint.rotation;

		Player p = player.GetComponent<Player>();
		if (p != null)
			p.ResetSpeedToDefault();

		if (followCamera != null)
			followCamera.SnapToTargetNow();
	}

	public void AddScore(int amount)
	{
		score += amount;
	}
	public void SetLastCheckpoint(Transform checkpoint)
	{
		lastCheckpointPos = checkpoint.position;
		lastCheckpointRot = checkpoint.rotation;
	}
	public void RespawnAtLastCheckpoint()
	{
		if (player == null)
			return;

		player.position = lastCheckpointPos;
		player.rotation = lastCheckpointRot;

		Player p = player.GetComponent<Player>();
		if (p != null)
			p.ResetSpeedToDefault();
	}

	public void RecountCheckpointsInScene()
	{
		totalCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
		passedCheckpoints = 0;
		finished = false;
	}

	public void RegisterCheckpointPassed()
	{
		if (finished)
			return;

		passedCheckpoints++;
		Debug.Log($"Checkpoint: {passedCheckpoints}/{totalCheckpoints}");
	}

	public bool AllCheckpointsPassed()
	{
		return passedCheckpoints >= totalCheckpoints;
	}

	public void TryFinish()
	{
		if (finished)
			return;

		if (!AllCheckpointsPassed())
		{
			Debug.Log("Finish reached but not all checkpoints passed.");
			return;
		}

		finished = true;
		Time.timeScale = 0f;
		Debug.Log("FINISHED! All checkpoints passed.");
	}
}
