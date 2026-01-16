using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 

public class Task3ParcelManager : MonoBehaviour
{
    [Header("Parcel Logic")]
    public int ParcelCount = 0;
    public int MaxParcels = 10;
    public float BaseSpeed = 8.0f;

    [Header("Visuals")]
    public Transform ParcelStackParent; // Where to stack logs (e.g., right side)
    public GameObject WoodLogVisualPrefab; // Prefab to show
    public Vector3 StackOffset = new Vector3(0.5f, 0, 0); // Side offset
    public Vector3 StackSpacing = new Vector3(0, 0.25f, 0); // Vertical spacing

    [Header("UI")]
    public TMP_Text ParcelCountText; // Assign a World Space canvas text if needed

    private List<GameObject> m_SpawnedVisuals = new List<GameObject>();

    public void AddParcel()
    {
        if (ParcelCount < MaxParcels)
        {
            ParcelCount++;
            UpdateVisuals();
            UpdateUI();
        }
    }

    public void ClearParcels()
    {
        ParcelCount = 0;
        foreach (var vis in m_SpawnedVisuals)
        {
            if (vis) Destroy(vis);
        }
        m_SpawnedVisuals.Clear();
        UpdateUI();
    }

    public float GetModifiedSpeed()
    {
        // Reduce speed by 10% per parcel, min 10% speed
        float penalty = ParcelCount * 0.1f;
        float multiplier = Mathf.Clamp(1.0f - penalty, 0.1f, 1.0f);
        return BaseSpeed * multiplier;
    }

    private void UpdateVisuals()
    {
        // Ensure we match the count
        // For simplicity, just instantiate the new one
        if (m_SpawnedVisuals.Count < ParcelCount)
        {
            if (WoodLogVisualPrefab != null && ParcelStackParent != null)
            {
                GameObject newLog = Instantiate(WoodLogVisualPrefab, ParcelStackParent);
                // Position logic: Local position based on index
                int index = m_SpawnedVisuals.Count; // 0 based
                newLog.transform.localPosition = StackOffset + (StackSpacing * index);
                newLog.transform.localRotation = Quaternion.identity;
                m_SpawnedVisuals.Add(newLog);
            }
        }
    }

    private void UpdateUI()
    {
        if (ParcelCountText != null)
        {
            // Simple overlay of info
            string stats = $"Logs: {ParcelCount}/{MaxParcels}\n";
            if (!string.IsNullOrEmpty(m_LatestStats))
            {
                stats += m_LatestStats;
            }
            ParcelCountText.text = stats;
        }
    }

    private string m_LatestStats = "";
    public void UpdateStats(float distance, float timeStartToDel, float timeReturnPhase)
    {
        // Format times
        string tDel = timeStartToDel > 0 ? timeStartToDel.ToString("F1") + "s" : "--";
        string tRet = timeReturnPhase > 0 ? timeReturnPhase.ToString("F1") + "s" : "--";
        
        m_LatestStats = $"Dist: {distance:F1}m\nToDel: {tDel}\nRet: {tRet}";
        UpdateUI();
    }
}
