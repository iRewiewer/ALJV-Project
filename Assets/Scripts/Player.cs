using UnityEngine;

public class Player : MonoBehaviour
{
	[Header("Movement")]
	public float minSpeed = 10f;
	public float maxSpeed = 500f;
	public float acceleration = 20f;

	[Header("Rotation")]
	public float pitchSpeed = 60f;
	public float yawSpeed = 25f;
	public float rollSpeed = 90f;
	public float maxRollAngle = 65f;
	public float rollReturnSpeed = 2.5f;
	public float inputSmooth = 6f;

	[Header("Settings")]
	public bool invertedControls = true;

	[Header("Attributes")]
	public float currentSpeed;

	private float pitchInputSmoothed;
	private float yawInputSmoothed;


	void Start()
	{
		currentSpeed = minSpeed;
	}

	void Update()
	{
		HandleRotation();
		HandleSpeed();
		MoveForward();
	}

	void HandleRotation()
	{
		float pitchIn = Input.GetAxis("Vertical");
		float yawIn = Input.GetAxis("Horizontal");

		if(invertedControls)
			pitchIn = -pitchIn;

		// Smooth inputs
		pitchInputSmoothed = Mathf.Lerp(pitchInputSmoothed, pitchIn, Time.deltaTime * inputSmooth);
		yawInputSmoothed = Mathf.Lerp(yawInputSmoothed, yawIn, Time.deltaTime * inputSmooth);

		// Pitch (inverted)
		float pitchAmount = -pitchInputSmoothed * pitchSpeed * Time.deltaTime;

		// Small yaw (optional, helps aim)
		float yawAmount = yawInputSmoothed * yawSpeed * Time.deltaTime;

		transform.Rotate(pitchAmount, yawAmount, 0f, Space.Self);

		// Roll toward input, auto-level when no input
		float targetRoll = -yawInputSmoothed * maxRollAngle;
		float currentRoll = NormalizeAngle(transform.localEulerAngles.z);

		float newRoll = Mathf.LerpAngle(currentRoll, targetRoll, Time.deltaTime * rollReturnSpeed);

		// Apply roll as absolute Z (so it doesn’t accumulate forever)
		Vector3 e = transform.localEulerAngles;
		e.z = newRoll;
		transform.localEulerAngles = e;
	}

	float NormalizeAngle(float angle)
	{
		if (angle > 180f) angle -= 360f;
		return angle;
	}
	public void ResetSpeedToDefault()
	{
		currentSpeed = minSpeed;
	}
	void HandleSpeed()
	{
		if (Input.GetKey(KeyCode.LeftShift))
		{
			currentSpeed += acceleration * Time.deltaTime;
		}
		else if (Input.GetKey(KeyCode.LeftControl))
		{
			currentSpeed -= acceleration * Time.deltaTime;
		}

		currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
		//Debug.Log(currentSpeed);
	}

	void MoveForward()
	{
		transform.position += transform.forward * currentSpeed * Time.deltaTime;
	}
}
