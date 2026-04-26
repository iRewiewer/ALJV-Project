using UnityEngine;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
	public static RaceManager Instance { get; private set; }

	[Header("References")]
	public Transform player;
	public FollowCamera followCamera;
	public Transform startPoint;
	public Transform finishPoint;
	public GameObject pauseMenu;
	public BotManager botManager;

	[Header("Race State")]
	public int totalCheckpoints;
	public int passedCheckpoints;
	public int score;
	public int totalCoins;

	public bool isPaused;
	public float elapsedTime;
	public bool finished;

	[Header("Settings")]
	public KeyCode pauseKey = KeyCode.P;
	public KeyCode restartKey = KeyCode.R;

	public Vector3 lastCheckpointPos;
	public Quaternion lastCheckpointRot;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		Time.timeScale = 1f;

		totalCoins = FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

		if (pauseMenu != null)
			pauseMenu.SetActive(false);
	}

	private void Start()
	{
		SaveStartFromPlayer();
		RecountCheckpointsInScene();

		if (botManager != null && startPoint != null)
			botManager.SetSpawnPoint(startPoint);
	}

	private void Update()
	{
		if (!finished && !isPaused)
			elapsedTime += Time.deltaTime;

		if (Input.GetKeyDown(restartKey))
			RestartRace();

		if (Input.GetKeyDown(pauseKey))
			TogglePause();
	}

	public void TogglePause()
	{
		if (finished)
		{
			RestartRace();
			return;
		}

		if (isPaused)
		{
			ResumeRace();
		}
		else
		{
			PauseRace();
		}
	}

	public void PauseRace()
	{
		isPaused = true;
		Time.timeScale = 0f;

		if (pauseMenu != null)
		{
			pauseMenu.SetActive(true);
		}
	}

	public void ResumeRace()
	{
		isPaused = false;
		Time.timeScale = 1f;

		if (pauseMenu != null)
		{
			pauseMenu.SetActive(false);
		}
	}

	public void RestartRace()
	{
		Time.timeScale = 1f;
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}

	public void SaveStartFromPlayer()
	{
		if (player == null || startPoint == null)
		{
			return;
		}

		startPoint.position = player.position;
		startPoint.rotation = player.rotation;

		lastCheckpointPos = startPoint.position;
		lastCheckpointRot = startPoint.rotation;
	}

	public void Respawn()
	{
		if (player == null || startPoint == null)
		{
			return;
		}

		player.position = startPoint.position;
		player.rotation = startPoint.rotation;

		Player p = player.GetComponent<Player>();

		if (p != null)
		{
			p.ResetSpeedToDefault();
		}

		if (followCamera != null)
		{
			followCamera.SnapToTargetNow();
		}
	}

	public void RespawnAtLastCheckpoint()
	{
		if (player == null)
		{
			return;
		}

		player.position = lastCheckpointPos;
		player.rotation = lastCheckpointRot;

		Player p = player.GetComponent<Player>();

		if (p != null)
		{
			p.ResetSpeedToDefault();
		}

		if (followCamera != null)
		{
			followCamera.SnapToTargetNow();
		}
	}

	public void AddScore(int amount)
	{
		score += amount;
	}

	public void SetLastCheckpoint(Transform checkpoint)
	{
		if (checkpoint == null)
		{
			return;
		}

		lastCheckpointPos = checkpoint.position;
		lastCheckpointRot = checkpoint.rotation;

		if (botManager != null)
		{
			botManager.SetSpawnPoint(checkpoint);
		}
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
		{
			return;
		}

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
		{
			return;
		}

		if (!AllCheckpointsPassed())
		{
			Debug.Log("Finish reached but not all checkpoints passed.");
			return;
		}

		finished = true;
		isPaused = false;
		Time.timeScale = 0f;

		Debug.Log("FINISHED! All checkpoints passed.");
	}
}