using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryZoneVisuals : MonoBehaviour
{
    [Header("Visual Configuration")]
    public GameObject WoodLogPrefab;
    public Transform StackParent;
    public Vector3 SpawnOffset = new Vector3(0f, 0.5f, 0f); // Start a bit off ground
    public Vector3 StackSpacing = new Vector3(0.2f, 0.2f, 0.2f); // Offset per log to make a pile
    public int MaxRowSize = 5;

    private int m_CurrentCount = 0;

    public void AddLogs(int count, GameObject prefabFallback = null)
    {
        GameObject prefabToUse = WoodLogPrefab != null ? WoodLogPrefab : prefabFallback;

        if (prefabToUse == null)
        {
            Debug.LogWarning($"[DeliveryZoneVisuals] No WoodLogPrefab assigned to {name} or passed from Agent!");
            return;
        }

        if (StackParent == null) StackParent = this.transform;

        for (int i = 0; i < count; i++)
        {
            SpawnLog(prefabToUse);
        }
    }

    private void SpawnLog(GameObject prefab)
    {
        // Simple stacking logic: 5xN grid or just a messy pile? 
        // Let's do a simple offset pile for now.
        
        Vector3 pos = StackParent.position + SpawnOffset;
        
        // Calculate a simple grid/pile offset based on count
        int row = m_CurrentCount % MaxRowSize;
        int col = (m_CurrentCount / MaxRowSize) % MaxRowSize;
        int level = m_CurrentCount / (MaxRowSize * MaxRowSize);

        pos += new Vector3(row * StackSpacing.x, level * StackSpacing.y, col * StackSpacing.z);

        // Add some random rotation for realism
        Quaternion rot = Quaternion.Euler(0, Random.Range(0, 360), 0);

        GameObject log = Instantiate(prefab, pos, rot, StackParent);
        m_CurrentCount++;
    }
}
