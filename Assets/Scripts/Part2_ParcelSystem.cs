using System.Collections.Generic;
using UnityEngine;

public class Part2_ParcelSystem : MonoBehaviour
{
    [Header("Parcel Settings")]
    [Range(0, 10)]
    public int parcelCount = 0;

    public int maxParcels = 10;

    public float baseSpeed = 2.5f;

    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    [Header("Parcel Visuals")]
    public Transform parcelVisualParent; // parent to attach parcel visuals
    public GameObject parcelVisualPrefab; // prefab for a single parcel visual

    [Header("UI Display")]
    public bool showParcelCountAboveAgent = true;
    public GameObject parcelCountTextPrefab; // TextMeshPro prefab for showing count

    private List<GameObject> parcelVisuals = new List<GameObject>();
    private GameObject parcelCountTextObject;
    private TMPro.TMP_Text parcelCountText;

    void Start()
    {
        UpdateSpeedMultiplier();
        UpdateVisuals();
        CreateParcelCountText();
    }

    void Update()
    {
        UpdateSpeedMultiplier();
        UpdateParcelCountText();
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
            Debug.Log($"{gameObject.name} picked up parcel! Total: {parcelCount}");

            UpdateVisuals();
        }
        else
        {
            Debug.Log($"{gameObject.name} parcel limit reached!");
        }
    }

    public void RemoveParcel()
    {
        if (parcelCount > 0)
        {
            parcelCount--;
            UpdateVisuals();
        }
    }

    public void ClearParcels()
    {
        parcelCount = 0;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Remove old visuals
        foreach (var visual in parcelVisuals)
        {
            if (visual != null)
                Destroy(visual);
        }
        parcelVisuals.Clear();

        // Create new visuals
        if (parcelVisualPrefab != null && parcelVisualParent != null)
        {
            for (int i = 0; i < parcelCount; i++)
            {
                GameObject parcelVisual = Instantiate(parcelVisualPrefab, parcelVisualParent);
                parcelVisual.transform.localPosition = new Vector3(0, 0.3f + (i * 0.3f), 0);
                parcelVisuals.Add(parcelVisual);
            }
        }
    }

    private void CreateParcelCountText()
    {
        if (!showParcelCountAboveAgent || parcelCountTextPrefab == null)
            return;

        // Create text object as child
        parcelCountTextObject = Instantiate(parcelCountTextPrefab, transform);
        parcelCountTextObject.transform.localPosition = new Vector3(0, 2.5f, 0);
        parcelCountText = parcelCountTextObject.GetComponent<TMPro.TextMeshPro>();

        if (parcelCountText == null)
        {
            parcelCountText = parcelCountTextObject.GetComponent<TMPro.TextMeshProUGUI>();
        }
    }

    private void UpdateParcelCountText()
    {
        if (parcelCountText != null)
        {
            parcelCountText.text = $"Parcels: {parcelCount}";
        }
    }
}