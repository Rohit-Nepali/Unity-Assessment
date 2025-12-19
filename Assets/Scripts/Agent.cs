using UnityEngine;

public class Agent : MonoBehaviour
{
    private Animator anim;

    public float currentSpeed { get; private set; } // Expose speed

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    public void ForceIdle()
    {
        anim.SetFloat("speed", 0f);
        currentSpeed = 0f;
    }

    public void SetCurrentSpeed(float speed)
    {
        currentSpeed = speed; // store for QuadrantUIManager
        anim.SetFloat("speed", speed); // update animation
    }
}
