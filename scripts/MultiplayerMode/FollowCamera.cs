using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target; // The target to follow.
    public Vector3 offset = new Vector3(0, 5, -7); // Offset from the target.
    public float smoothSpeed = 0.125f; // Smoothing factor.

    void LateUpdate()
    {
        if (target == null)
            return;

        // Calculate desired position based on the target’s position and rotation.
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Smoothly interpolate to the desired position.
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Make the camera look at the target.
        transform.LookAt(target);
    }
}
