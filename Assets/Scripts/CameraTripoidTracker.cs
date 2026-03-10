using UnityEngine;

public class CameraTripoidTracker : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Position Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -6f);
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;

    [Header("Look Settings")]
    public Vector3 lookAtOffset = new Vector3(0f, 0.5f, 0f);

    private Vector3 currentVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position is offset relative to target's rotation
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Smoothly move camera to desired position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / followSpeed
        );

        // Smoothly rotate to look at the agent
        Quaternion desiredRotation = Quaternion.LookRotation(
            (target.position + lookAtOffset) - transform.position
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
