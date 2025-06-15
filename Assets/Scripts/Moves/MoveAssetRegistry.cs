using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MoveAssetRegistry", menuName = "Game/Move Asset Registry")]
public class MoveAssetRegistry : ScriptableObject
{
    [System.Serializable]
    public struct MoveAssetEntry
    {
        public AvailableMoves moveType;
        public MoveAsset moveAsset;
    }
    
    [SerializeField] private MoveAssetEntry[] moveEntries;
    private Dictionary<AvailableMoves, MoveAsset> moveLookup;
    
    public void Initialize()
    {
        if (moveLookup != null) return;
        
        moveLookup = new Dictionary<AvailableMoves, MoveAsset>();
        foreach (var entry in moveEntries)
        {
            if (entry.moveAsset != null)
            {
                moveLookup[entry.moveType] = entry.moveAsset;
            }
        }
    }
    
    public MoveAsset GetMoveAsset(AvailableMoves move)
    {
        Initialize();
        return moveLookup.TryGetValue(move, out MoveAsset asset) ? asset : null;
    }
    
    public bool HasMoveAsset(AvailableMoves move)
    {
        Initialize();
        return moveLookup.ContainsKey(move);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Auto-Populate from Assets")]
    private void AutoPopulateFromResources()
    {
        // Usa l'AssetDatabase per trovare tutti i MoveAsset nel progetto
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MoveAsset");
        List<MoveAssetEntry> entries = new List<MoveAssetEntry>();

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            MoveAsset moveAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<MoveAsset>(path);
            if (moveAsset != null)
            {
                entries.Add(new MoveAssetEntry
                {
                    moveType = moveAsset.move,
                    moveAsset = moveAsset
                });
            }
        }

        moveEntries = entries.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Auto-populated {entries.Count} move assets from Assets");
    }
#endif
}
