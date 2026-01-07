// WoodLogCollector.cs
using UnityEngine;

public class WoodLogCollector : MonoBehaviour
{
    public bool canBeCollected = false;
    public GameObject spawnedByAgent;
    private float spawnTime;

    void Start()
    {
        spawnTime = Time.time;
        canBeCollected = false;
        // Enable collection after 1 second (prevents instant collection)
        Invoke("EnableCollection", 1f);
    }

    void EnableCollection()
    {
        canBeCollected = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canBeCollected) return;

        if (other.CompareTag("Agent"))
        {
            PathfindingTester agent = other.GetComponent<PathfindingTester>();
            if (agent != null)
            {
                // Check parcel limit
                Part2_ParcelSystem parcelSystem = agent.GetComponent<Part2_ParcelSystem>();
                if (parcelSystem != null && parcelSystem.parcelCount < parcelSystem.maxParcels)
                {
                    parcelSystem.AddParcel();
                    Destroy(gameObject);
                }
            }
        }
    }
}