using UnityEngine;

public class Part2_ParcelSystem : MonoBehaviour
{
    [Header("Parcel Settings")]
    [Range(0, 10)]
    public int parcelCount = 0;

    public float baseSpeed = 2.5f;

    public float CurrentSpeedMultiplier { get; private set; } = 1f;

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
}
