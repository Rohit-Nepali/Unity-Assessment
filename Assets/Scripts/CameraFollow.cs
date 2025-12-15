using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // The agent/player to follow
    public Vector3 offset = new Vector3(0, 5, -7); // Default offset
    public float followSpeed = 10f;   // How fast the camera follows
    public float rotationSpeed = 5f;  // How fast the camera rotates

    private float currentYaw = 0f;    // Horizontal rotation around the target
    private float currentPitch = 20f;

    void LateUpdate()
    {
        if(target == null) return;
        // 1. Rotate the camera based on agent's direction
        currentYaw = Mathf.Lerp(currentYaw, target.eulerAngles.y, rotationSpeed * Time.deltaTime);

        // Optional: You can also allow mouse control here
        // currentYaw += Input.GetAxis("Mouse X") * rotationSpeed;
        // currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
        // currentPitch = Mathf.Clamp(currentPitch, 5f, 60f);

        // 2. Calculate rotation and position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        // 3. Smoothly move the camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // 4. Always look at the target
        transform.LookAt(target.position + Vector3.up * 1.5f);


        // // Desired camera position based on target + offset
        // Vector3 desiredPosition = target.position + offset;

        // // Smoothly move camera to desired position
        // Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // transform.position = smoothedPosition;

        // // Optional: make camera look at the character
        // transform.LookAt(target);
    }
}
