using UnityEngine;

public class Agent : MonoBehaviour
{
    private Animator anim;
    private Vector3 lastPos;

    public float currentSpeed { get; private set; } // Expose speed

    void Start()
    {
        anim = GetComponent<Animator>();
        lastPos = transform.position;
    }

    void Update()
    {
        // Calculate horizontal movement speed only (ignores gravity/vertical drift)
        Vector3 delta = transform.position - lastPos;
        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
        float speed = horizontalDelta.magnitude / Time.deltaTime;

        // Ignore tiny values (noise) - increased to 0.1f for better idle stability
        if (speed < 0.1f)
        {
            speed = 0f;
        }

        // Send it to animator
        anim.SetFloat("speed", speed);

        // Update previous position
        lastPos = transform.position;

        // Store for external access
        currentSpeed = speed;
    }

    public void ForceIdle()
    {
        anim.SetFloat("speed", 0f);
        lastPos = transform.position; // reset lastPos to prevent small residual movement
        Debug.Log("ForceIdle called - speed set to 0"); // Temp debug; remove later
    }
}
