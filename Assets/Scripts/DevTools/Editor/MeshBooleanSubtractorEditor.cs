using UnityEngine;
using UnityEditor;
using System.IO;
using FracturedStudios.DevTools;

namespace FracturedStudios.DevTools.Editor
{
    public class MeshBooleanSubtractorEditor : EditorWindow
    {
        private MeshFilter sourceMeshFilter;
        private Collider subtractorCollider;
        private bool overwriteSource = false;
        private bool useBoxTest = true;
        private FracturedStudios.DevTools.MeshBooleanSubtractor.BoxTestMode boxTestMode = FracturedStudios.DevTools.MeshBooleanSubtractor.BoxTestMode.OrientedLocal;
        private string savePath = "Assets/GeneratedMeshes";

        [MenuItem("Tools/Dev/Mesh Boolean Subtractor")]
        public static void OpenWindow()
        {
            var w = GetWindow<MeshBooleanSubtractorEditor>("Mesh Subtractor");
            w.minSize = new Vector2(420, 160);
        }

        private void OnGUI()
        {
            GUILayout.Label("Simple Mesh Subtractor (triangle-culling)", EditorStyles.boldLabel);
            sourceMeshFilter = EditorGUILayout.ObjectField("Source MeshFilter", sourceMeshFilter, typeof(MeshFilter), true) as MeshFilter;
            subtractorCollider = EditorGUILayout.ObjectField("Subtractor Collider", subtractorCollider, typeof(Collider), true) as Collider;
            useBoxTest = EditorGUILayout.Toggle("Use Box Collider Test", useBoxTest);
            if (useBoxTest)
            {
                boxTestMode = (FracturedStudios.DevTools.MeshBooleanSubtractor.BoxTestMode)EditorGUILayout.EnumPopup("Box Test Mode", boxTestMode);
            }
            overwriteSource = EditorGUILayout.Toggle("Overwrite Source Mesh", overwriteSource);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("Save Folder", savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string p = EditorUtility.OpenFolderPanel("Select folder under Assets", "", "");
                if (!string.IsNullOrEmpty(p) && p.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + p.Substring(Application.dataPath.Length);
                }
                else if (!string.IsNullOrEmpty(p))
                {
                    EditorUtility.DisplayDialog("Invalid folder","Please choose a folder inside the project's Assets folder.","OK");
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Subtract and Save", GUILayout.Height(36)))
            {
                RunSubtract();
            }
        }

        private void RunSubtract()
        {
            if (sourceMeshFilter == null)
            {
                EditorUtility.DisplayDialog("Missing Source","Assign a MeshFilter for the source mesh.","OK");
                return;
            }
            if (subtractorCollider == null)
            {
                if (!EditorUtility.DisplayDialog("No collider assigned","No subtractor collider assigned — running will do nothing. Continue?","Yes","Cancel")) return;
            }

            Mesh src = sourceMeshFilter.sharedMesh;
            if (src == null)
            {
                EditorUtility.DisplayDialog("No mesh","Source MeshFilter has no mesh assigned.","OK");
                return;
            }

            // Convert collider and mesh to common space: world space test
            // We'll transform vertex positions to world using the meshFilter's transform
            Mesh tempSrc = Instantiate(src);
            Vector3[] v = tempSrc.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = sourceMeshFilter.transform.TransformPoint(v[i]);
            tempSrc.vertices = v;

            Mesh result = MeshBooleanSubtractor.SubtractTrianglesInsideCollider(tempSrc, subtractorCollider, useBoxTest, boxTestMode);
            if (result == null)
            {
                EditorUtility.DisplayDialog("Result null","Subtraction returned null.","OK");
                return;
            }

            // Diagnostic: count triangles kept vs removed and report world extents
            int totalTris = src.triangles.Length / 3;
            int keptTris = result.triangles.Length / 3;
            int removedTris = totalTris - keptTris;

            string diag = $"Triangles total: {totalTris}\nKept: {keptTris}\nRemoved: {removedTris}\n";
            // report mesh world bounds and subtractor bounds
            // Compute world bounds by transforming all vertices (robust to negative scale/rotation)
            Vector3[] verts = src.vertices;
            if (verts != null && verts.Length > 0)
            {
                Vector3 v0 = sourceMeshFilter.transform.TransformPoint(verts[0]);
                Vector3 min = v0;
                Vector3 max = v0;
                for (int i = 1; i < verts.Length; i++)
                {
                    Vector3 w = sourceMeshFilter.transform.TransformPoint(verts[i]);
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
                Vector3 size = max - min;
                diag += $"Mesh world bounds size: {size}\n";
            }
            else
            {
                diag += "Mesh world bounds size: <no vertices>\n";
            }
            if (subtractorCollider != null && subtractorCollider is BoxCollider b)
            {
                Vector3 worldHalf = Vector3.Scale(b.size * 0.5f, b.transform.lossyScale);
                diag += $"Box world half-extents (approx): {worldHalf}\nBox center (world): {b.transform.TransformPoint(b.center)}\n";
            }
            Debug.Log("MeshBooleanSubtractor diagnostics:\n" + diag);
            if (removedTris == totalTris)
            {
                EditorUtility.DisplayDialog("Warning","All triangles were classified as inside the subtractor.\nSee Console for diagnostic info.","OK");
            }

            if (overwriteSource)
            {
                // Overwrite shared mesh asset if possible
                string assetPath = AssetDatabase.GetAssetPath(src);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    Mesh newMesh = Instantiate(result);
                    newMesh.name = src.name;
                    // If the source is a .asset we can overwrite it. If it's an imported model (eg .fbx), create a new asset and assign it.
                    string ext = Path.GetExtension(assetPath).ToLowerInvariant();
                    if (ext == ".asset")
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                        AssetDatabase.CreateAsset(newMesh, assetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                        sourceMeshFilter.sharedMesh = saved;
                        EditorUtility.DisplayDialog("Done","Source mesh asset overwritten.","OK");
                    }
                    else
                    {
                        // Create new asset next to savePath and assign
                        if (string.IsNullOrWhiteSpace(savePath)) savePath = "Assets/GeneratedMeshes";
                        if (!savePath.StartsWith("Assets")) savePath = Path.Combine("Assets", savePath);
                        if (!AssetDatabase.IsValidFolder(savePath))
                        {
                            // create folder chain
                            string[] parts = savePath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                            string cur = parts[0];
                            for (int i = 1; i < parts.Length; i++)
                            {
                                string next = cur + "/" + parts[i];
                                if (!AssetDatabase.IsValidFolder(next))
                                {
                                    AssetDatabase.CreateFolder(cur, parts[i]);
                                }
                                cur = next;
                            }
                        }
                        string outName = src.name + "_subtracted.asset";
                        string outPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(savePath, outName));
                        AssetDatabase.CreateAsset(newMesh, outPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
                        sourceMeshFilter.sharedMesh = saved;
                        EditorUtility.DisplayDialog("Done","Created new mesh asset and assigned to MeshFilter: " + outPath,"OK");
                    }
                    return;
                }
            }

            // Save to new asset
            // Normalize save folder to be under Assets
            if (string.IsNullOrWhiteSpace(savePath)) savePath = "Assets/GeneratedMeshes";
            if (!savePath.StartsWith("Assets")) savePath = Path.Combine("Assets", savePath);

            // Ensure folder exists in AssetDatabase
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                string[] parts = savePath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                string cur = parts[0]; // should be "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        string parent = cur;
                        string newFolderName = parts[i];
                        AssetDatabase.CreateFolder(parent, newFolderName);
                    }
                    cur = next;
                }
            }

            string baseName = src.name + "_subtracted.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(savePath, baseName));
            Mesh outMesh = Instantiate(result);
            AssetDatabase.CreateAsset(outMesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Saved","New mesh saved to: " + path, "OK");
        }
    }
}
