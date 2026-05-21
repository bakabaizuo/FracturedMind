using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FracturedStudios.Environment;

namespace FracturedStudios.EditorTools
{
    [CustomEditor(typeof(ChapterOneMaskTerrainBuilder))]
    public sealed class ChapterOneMaskTerrainBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var builder = (ChapterOneMaskTerrainBuilder)target;

            EditorGUILayout.Space();
            if (builder.SourceMask == null)
            {
                EditorGUILayout.HelpBox("Assign the chapter map PNG first. Green areas become flat ground mesh. White areas stay empty and can be used to strip placed objects.", MessageType.Info);
            }
            else if (!builder.SourceMask.isReadable)
            {
                EditorGUILayout.HelpBox("The source mask must have Read/Write Enabled turned on in the texture importer.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Use Mesh Resolution Longest Side for tessellation density, Anti Alias Samples Per Axis for smoother shoreline detection, and Min Ground Coverage to decide how much of a cell must be green before it becomes terrain. Field Smooth Iterations and Field Smooth Strength soften the coastline before contouring. Terrain Shape controls edge lift, center lift, enclosed-hole filling, and wall depth. Noise adds breakup across the top surface. Surface Texturing lets you assign separate top and cliff materials or just base textures, and the builder will apply them to separate submeshes.", MessageType.None);
            }

            if (GUILayout.Button("Build Mesh From Mask"))
            {
                BuildMesh(builder);
            }

            if (GUILayout.Button("Open Terrain Tool"))
            {
                ChapterOneMaskTerrainToolWindow.Open(builder);
            }

            if (GUILayout.Button("Clear Applied Mesh"))
            {
                ClearMesh(builder);
            }

            using (new EditorGUI.DisabledScope(builder.PlacementRoot == null))
            {
                if (GUILayout.Button("Remove Placements Where White"))
                {
                    RemovePlacementsWhereWhite(builder);
                }
            }
        }

        private static void BuildMesh(ChapterOneMaskTerrainBuilder builder)
        {
            Mesh mesh = builder.BuildMesh();
            if (mesh == null)
                return;

            EnsureFolderExists(builder.MeshAssetFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(builder.MeshAssetFolder, builder.gameObject.name + "_Chapter1MaskMesh.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(mesh, assetPath);

            Undo.RecordObject(builder, "Build Chapter 1 Terrain Mesh");
            MeshFilter meshFilter = builder.GetComponent<MeshFilter>();
            if (meshFilter != null)
                Undo.RecordObject(meshFilter, "Build Chapter 1 Terrain Mesh");

            MeshCollider meshCollider = builder.GetComponent<MeshCollider>();
            if (meshCollider != null)
                Undo.RecordObject(meshCollider, "Build Chapter 1 Terrain Mesh");

            builder.ApplyMesh(mesh);
            EditorUtility.SetDirty(builder);
            if (meshFilter != null)
                EditorUtility.SetDirty(meshFilter);
            if (meshCollider != null)
                EditorUtility.SetDirty(meshCollider);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        private static void ClearMesh(ChapterOneMaskTerrainBuilder builder)
        {
            Undo.RecordObject(builder, "Clear Chapter 1 Terrain Mesh");

            MeshFilter meshFilter = builder.GetComponent<MeshFilter>();
            if (meshFilter != null)
                Undo.RecordObject(meshFilter, "Clear Chapter 1 Terrain Mesh");

            MeshCollider meshCollider = builder.GetComponent<MeshCollider>();
            if (meshCollider != null)
                Undo.RecordObject(meshCollider, "Clear Chapter 1 Terrain Mesh");

            builder.ClearAppliedMesh();
            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        private static void RemovePlacementsWhereWhite(ChapterOneMaskTerrainBuilder builder)
        {
            Transform placementRoot = builder.PlacementRoot;
            if (placementRoot == null)
                return;

            int removedCount = 0;
            for (int i = placementRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = placementRoot.GetChild(i);
                if (!builder.IsWhiteAtWorldPosition(child.position))
                    continue;

                Undo.DestroyObjectImmediate(child.gameObject);
                removedCount++;
            }

            Debug.Log($"[ChapterOneMaskTerrainBuilder] Removed {removedCount} placed object(s) on white pixels.", builder);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        private static void EnsureFolderExists(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder))
                return;

            string normalizedFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalizedFolder))
                return;

            string[] parts = normalizedFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}