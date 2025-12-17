using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // The agent/player to follow
    public Vector3 offset = new Vector3(0, 5, -7); // Default offset
    public float followSpeed = 10f;   // Smooth movement speed
    public float rotationSpeed = 5f;  // Smooth rotation speed

    private float currentYaw;
    private float currentPitch = 20f;

    void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y; // Initialize to agent's yaw
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Smoothly follow agent's rotation
        currentYaw = Mathf.Lerp(currentYaw, target.eulerAngles.y, rotationSpeed * Time.deltaTime);

        // Calculate rotation and desired position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // Look at agent's head
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
