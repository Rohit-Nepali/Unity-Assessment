using UnityEngine;

public class Agent : MonoBehaviour
{
    private Animator anim;
    private Vector3 lastPos;

    void Start()
    {
        anim = GetComponent<Animator>();
        lastPos = transform.position;
    }

    void Update()
    {
        // Calculate movement speed manually
        float speed = (transform.position - lastPos).magnitude / Time.deltaTime;

        // Ignore tiny values (noise)
        if (speed < 0.05f)
        {
            speed = 0f;
        }

        // Send it to animator
        anim.SetFloat("speed", speed);

        // Update previous position
        lastPos = transform.position;
    }
}
