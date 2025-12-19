using UnityEngine;

public class Part2_ParcelSystem : MonoBehaviour
{
    [Header("Parcel Settings")]
    [Range(0, 10)]
    public int parcelCount = 0;

    public int maxParcels = 10; // <-- declare maxParcels

    public float baseSpeed = 2.5f;

    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    [Header("Parcel Visuals")]
    public Transform parcelVisualParent; // parent to attach parcel visuals
    public GameObject parcelVisualPrefab; // prefab for a single parcel visual

    void Update()
    {
        UpdateSpeedMultiplier();
    }

    void UpdateSpeedMultiplier()
    {
        float reduction = parcelCount * 0.1f;
        reduction = Mathf.Clamp(reduction, 0f, 0.9f);

        CurrentSpeedMultiplier = 1f - reduction;
    }

    public float GetModifiedSpeed()
    {
        return baseSpeed * CurrentSpeedMultiplier;
    }

    public void AddParcel()
    {
        if (parcelCount < maxParcels)
        {
            parcelCount++;
            Debug.Log($"Parcel picked up! Total: {parcelCount}");

            // Spawn visual if prefab and parent exist
            if (parcelVisualPrefab != null && parcelVisualParent != null)
            {
                GameObject parcelVisual = Instantiate(parcelVisualPrefab, parcelVisualParent);
                parcelVisual.transform.localPosition = new Vector3(0, 0.3f * parcelCount, 0); // stack nicely
            }
        }
        else
        {
            Debug.Log("Parcel limit reached!");
        }
    }
}
