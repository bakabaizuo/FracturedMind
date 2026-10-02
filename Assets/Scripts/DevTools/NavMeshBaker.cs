using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation; // Use UnityEngine.AI for the legacy system

public class NavMeshBaker : EditorWindow
{
    [MenuItem("Tools/Rebake All NavMeshes")]
    public static void RebakeAll()
    {
        // For the new AI Navigation system
        NavMeshSurface[] surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        
        if (surfaces.Length == 0)
        {
            // Fallback for the legacy system if no surfaces are found
            #pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            #pragma warning restore CS0618
            Debug.Log("Legacy NavMesh globally rebaked!");
            return;
        }

        foreach (var surface in surfaces)
        {
            surface.RemoveData(); // Clears old data
            surface.BuildNavMesh(); // Bakes fresh data
        }
        Debug.Log($"Successfully rebaked {surfaces.Length} NavMesh Surfaces!");
    }
}