using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // The agent/player to follow
    public Vector3 offset = new Vector3(0, 2, -2); // Default offset
    public float followSpeed = 10f; // Smooth movement speed when moving
    public float stoppedFollowSpeed = 30f; // Faster speed when stopped to catch up quickly
    public float rotationSpeed = 5f; // Smooth rotation speed
    public float stoppedSpeedThreshold = 0.1f; // Speed below which we consider stopped

    private float currentYaw;
    private float currentPitch = 20f;
    private Agent agent; // Reference to agent's speed

    public enum CameraQuadrant
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    public CameraQuadrant quadrant;

    void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y; // Initialize to agent's yaw
            agent = target.GetComponent<Agent>(); // Get the Agent component for speed
        }

        // Set viewport based on quadrant
        Camera cam = GetComponent<Camera>();
        SetCameraViewport(cam);

        Debug.Log($"Camera {gameObject.name} set to quadrant: {quadrant}, viewport: {cam.rect}");
    }

    private void SetCameraViewport(Camera cam)
    {
        switch (quadrant)
        {
            case CameraQuadrant.TopLeft:
                cam.rect = new Rect(0f, 0.5f, 0.5f, 0.5f);
                break;
            case CameraQuadrant.TopRight:
                cam.rect = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
                break;
            case CameraQuadrant.BottomLeft:
                cam.rect = new Rect(0f, 0f, 0.5f, 0.5f);
                break;
            case CameraQuadrant.BottomRight:
                cam.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);
                break;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // Smoothly follow agent's rotation
        currentYaw = Mathf.Lerp(currentYaw, target.eulerAngles.y, rotationSpeed * Time.deltaTime);

        // Calculate rotation and desired position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        // Determine if agent is stopped and adjust follow speed
        float currentFollowSpeed = followSpeed;
        if (agent != null && agent.currentSpeed < stoppedSpeedThreshold)
        {
            currentFollowSpeed = stoppedFollowSpeed;
        }

        // Smoothly move camera with dynamic speed
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            currentFollowSpeed * Time.deltaTime
        );

        // Look at agent's head
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
