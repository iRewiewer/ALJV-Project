using UnityEngine;

public class FollowCamera : MonoBehaviour
{
	[Header("Transforms")]
	public Transform player;

	[Header("Smoothing")]
	public float positionSmoothTime = 0.18f;
	public float rotationSmoothTime = 0.12f;

	private Vector3 posVelocity;

	// Saved camera pos relative to the player
	private Vector3 relativePosLocal;
	private Quaternion relativeRotLocal;

	void Start()
	{
		if (player == null) return;

		// Save the rig's initial pose relative to the target
		relativePosLocal = Quaternion.Inverse(player.rotation) * (transform.position - player.position);
		relativeRotLocal = Quaternion.Inverse(player.rotation) * transform.rotation;
	}

	void LateUpdate()
	{
		if (player == null) return;

		// Reconstruct the desired rig pose from the target pose + saved relative pose.
		Vector3 desiredPos = player.position + player.rotation * relativePosLocal;
		Quaternion desiredRot = player.rotation * relativeRotLocal;

		// Smooth pos + rot
		transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelocity, positionSmoothTime);
		transform.rotation = SmoothSlerp(transform.rotation, desiredRot, rotationSmoothTime);
	}

	Quaternion SmoothSlerp(Quaternion current, Quaternion targetRot, float smoothTime)
	{
		float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothTime));
		return Quaternion.Slerp(current, targetRot, t);
	}

	public void SnapToTargetNow()
	{
		if (player == null)
			return;

		Vector3 desiredPos = player.position + player.rotation * relativePosLocal;
		Quaternion desiredRot = player.rotation * relativeRotLocal;

		transform.position = desiredPos;
		transform.rotation = desiredRot;
	}
}
