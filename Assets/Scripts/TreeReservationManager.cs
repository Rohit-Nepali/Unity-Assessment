using System.Collections.Generic;
using UnityEngine;

public class TreeReservationManager : MonoBehaviour
{
    public static TreeReservationManager Instance;

    private HashSet<GameObject> reservedTrees = new HashSet<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public bool IsTreeReserved(GameObject tree)
    {
        return reservedTrees.Contains(tree);
    }

    public bool ReserveTree(GameObject tree)
    {
        if (tree == null || reservedTrees.Contains(tree))
            return false;

        reservedTrees.Add(tree);
        Debug.Log("Tree reserved: " + tree.name);
        return true;
    }

    public void ReleaseTree(GameObject tree)
    {
        if (tree == null)
            return;

        reservedTrees.Remove(tree);
        Debug.Log("Tree released: " + tree.name);
    }
}
