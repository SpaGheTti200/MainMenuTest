using UnityEngine;

/// <summary>
/// Attach this script to any spawned item so we can track its original prefab.
/// </summary>
public class ItemIdentity : MonoBehaviour
{
    // Which prefab was used to create this item
    public GameObject originalPrefab;
}