using TMPro;
using UnityEngine;

public class Bot : MonoBehaviour
{
	[Header("Movement")]
	public float minSpeed = 10f;
	public float maxSpeed = 500f;
	public float acceleration = 20f;
	public float brakingAcceleration = 70f;

	[Header("Rotation")]
	public float pitchSpeed = 60f;
	public float yawSpeed = 45f;
	public float maxRollAngle = 65f;
	public float rollReturnSpeed = 2.5f;
	public float inputSmooth = 6f;

	[Header("Settings")]
	public bool invertedControls = true;
	public bool usePlayerInputForTesting = false;

	[Header("Attributes")]
	public float currentSpeed;

	public float CurrentSpeed => currentSpeed;
	public float MinSpeed => minSpeed;
	public float MaxSpeed => maxSpeed;

	private TMP_Text nameText;
	private Camera targetCamera;

	private float pitchInputSmoothed;
	private float yawInputSmoothed;

	private float aiPitchInput;
	private float aiYawInput;
	private bool aiAccelerating;
	private bool aiBraking;

	private void Awake()
	{
		nameText = GetComponentInChildren<TMP_Text>();
	}

	private void Start()
	{
		currentSpeed = minSpeed;
	}

	private void Update()
	{
		HandleRotation();
		HandleSpeed();
		MoveForward();
		UpdateNameplate();
	}

	public void SetBotName(string botName, Camera cameraToFace)
	{
		targetCamera = cameraToFace;

		if (nameText != null)
			nameText.text = botName;
	}

	public void SetAiInput(float pitch, float yaw, bool accelerating, bool braking)
	{
		aiPitchInput = Mathf.Clamp(pitch, -1f, 1f);
		aiYawInput = Mathf.Clamp(yaw, -1f, 1f);
		aiAccelerating = accelerating;
		aiBraking = braking;
	}

	public void ResetSpeedToDefault()
	{
		currentSpeed = minSpeed;
	}

	private void HandleRotation()
	{
		float pitchIn;
		float yawIn;

		if (usePlayerInputForTesting)
		{
			pitchIn = Input.GetAxis("Vertical");
			yawIn = Input.GetAxis("Horizontal");
		}
		else
		{
			pitchIn = aiPitchInput;
			yawIn = aiYawInput;
		}

		if (invertedControls)
			pitchIn = -pitchIn;

		pitchInputSmoothed = Mathf.Lerp(pitchInputSmoothed, pitchIn, Time.deltaTime * inputSmooth);
		yawInputSmoothed = Mathf.Lerp(yawInputSmoothed, yawIn, Time.deltaTime * inputSmooth);

		float pitchAmount = -pitchInputSmoothed * pitchSpeed * Time.deltaTime;
		float yawAmount = yawInputSmoothed * yawSpeed * Time.deltaTime;

		transform.Rotate(pitchAmount, yawAmount, 0f, Space.Self);

		float targetRoll = -yawInputSmoothed * maxRollAngle;
		float currentRoll = NormalizeAngle(transform.localEulerAngles.z);
		float newRoll = Mathf.LerpAngle(currentRoll, targetRoll, Time.deltaTime * rollReturnSpeed);

		Vector3 eulerAngles = transform.localEulerAngles;
		eulerAngles.z = newRoll;
		transform.localEulerAngles = eulerAngles;
	}

	private void HandleSpeed()
	{
		bool accelerating;
		bool braking;

		if (usePlayerInputForTesting)
		{
			accelerating = Input.GetKey(KeyCode.LeftShift);
			braking = Input.GetKey(KeyCode.LeftControl);
		}
		else
		{
			accelerating = aiAccelerating;
			braking = aiBraking;
		}

		if (accelerating)
			currentSpeed += acceleration * Time.deltaTime;
		else if (braking)
			currentSpeed -= brakingAcceleration * Time.deltaTime;

		currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
	}

	private void MoveForward()
	{
		transform.position += transform.forward * currentSpeed * Time.deltaTime;
	}

	private void UpdateNameplate()
	{
		if (nameText == null || targetCamera == null)
			return;

		Transform canvasTransform = nameText.transform.parent;

		if (canvasTransform == null)
			return;

		canvasTransform.LookAt(targetCamera.transform);
		canvasTransform.Rotate(0f, 180f, 0f);
	}

	private float NormalizeAngle(float angle)
	{
		if (angle > 180f)
			angle -= 360f;

		return angle;
	}
}
