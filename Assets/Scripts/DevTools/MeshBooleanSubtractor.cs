using UnityEngine;

namespace FracturedStudios.DevTools
{
    /// <summary>
    /// Runtime helper: simple boolean-like subtraction by removing triangles
    /// whose centroids lie inside a provided Collider. This is an approximation
    /// and intended for editor-level or prototyping use on simple meshes
    /// (e.g. cube) — not a full CSG solution.
    /// </summary>
    public static class MeshBooleanSubtractor
    {
        public enum BoxTestMode
        {
            OrientedLocal, // transform point into box local space and compare to size/2
            WorldAABB // compute world-space AABB from box corners and test containment
        }
        /// <summary>
        /// Returns a new mesh with triangles removed when their centroid is inside the collider.
        /// This duplicates vertices per triangle for simplicity.
        /// </summary>
        /// <param name="useBoxTest">If true and the subtractor is a BoxCollider, perform an oriented box test instead of ClosestPoint.</param>
        public static Mesh SubtractTrianglesInsideCollider(Mesh source, Collider subtractor, bool useBoxTest = false, BoxTestMode boxMode = BoxTestMode.OrientedLocal)
        {
            if (source == null) return null;
            if (subtractor == null) return Object.Instantiate(source);

            var srcVerts = source.vertices;
            var srcNormals = source.normals;
            var srcUV = source.uv;
            var srcTris = source.triangles;

            var outVerts = new System.Collections.Generic.List<Vector3>();
            var outNormals = new System.Collections.Generic.List<Vector3>();
            var outUV = new System.Collections.Generic.List<Vector2>();
            var outTris = new System.Collections.Generic.List<int>();

            for (int t = 0; t < srcTris.Length; t += 3)
            {
                int i0 = srcTris[t + 0];
                int i1 = srcTris[t + 1];
                int i2 = srcTris[t + 2];

                Vector3 v0 = srcVerts[i0];
                Vector3 v1 = srcVerts[i1];
                Vector3 v2 = srcVerts[i2];

                // centroid in local space
                Vector3 centroidLocal = (v0 + v1 + v2) / 3f;
                // caller should have provided vertices in world space if testing against world-space collider
                Vector3 centroidWorld = centroidLocal;

                bool inside = false;
                if (useBoxTest && subtractor is BoxCollider box)
                {
                    if (boxMode == BoxTestMode.OrientedLocal)
                    {
                        Vector3 localPoint = box.transform.InverseTransformPoint(centroidWorld) - box.center;
                        Vector3 half = box.size * 0.5f;
                        inside = Mathf.Abs(localPoint.x) <= half.x && Mathf.Abs(localPoint.y) <= half.y && Mathf.Abs(localPoint.z) <= half.z;
                    }
                    else // WorldAABB
                    {
                        // Build world-space AABB by transforming the 8 corners of the BoxCollider
                        Vector3 half = box.size * 0.5f;
                        Vector3[] corners = new Vector3[8];
                        int idx = 0;
                        for (int xi = -1; xi <= 1; xi += 2)
                        for (int yi = -1; yi <= 1; yi += 2)
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            // Use the box's local size to compute corners; TransformPoint will apply the object's scale/rotation/position
                            Vector3 localCorner = box.center + new Vector3(xi * half.x, yi * half.y, zi * half.z);
                            corners[idx++] = box.transform.TransformPoint(localCorner);
                        }
                        Vector3 min = corners[0];
                        Vector3 max = corners[0];
                        for (int i = 1; i < corners.Length; i++)
                        {
                            min = Vector3.Min(min, corners[i]);
                            max = Vector3.Max(max, corners[i]);
                        }
                        inside = (centroidWorld.x >= min.x && centroidWorld.x <= max.x) && (centroidWorld.y >= min.y && centroidWorld.y <= max.y) && (centroidWorld.z >= min.z && centroidWorld.z <= max.z);
                    }
                }
                else
                {
                    // Use Collider.ClosestPoint to determine if point is inside the collider
                    Vector3 closest = subtractor.ClosestPoint(centroidWorld);
                    inside = Vector3.Distance(closest, centroidWorld) < 1e-4f;
                }

                if (inside)
                {
                    // skip triangle (treated as subtracted)
                    continue;
                }

                // keep triangle, duplicate vertices for simple indexing
                int baseIndex = outVerts.Count;
                outVerts.Add(v0);
                outVerts.Add(v1);
                outVerts.Add(v2);

                if (srcNormals != null && srcNormals.Length > 0)
                {
                    outNormals.Add(srcNormals[i0]);
                    outNormals.Add(srcNormals[i1]);
                    outNormals.Add(srcNormals[i2]);
                }
                if (srcUV != null && srcUV.Length > 0)
                {
                    outUV.Add(srcUV[i0]);
                    outUV.Add(srcUV[i1]);
                    outUV.Add(srcUV[i2]);
                }

                outTris.Add(baseIndex + 0);
                outTris.Add(baseIndex + 1);
                outTris.Add(baseIndex + 2);
            }

            Mesh outMesh = new Mesh();
            outMesh.name = source.name + "_subtracted";
            outMesh.SetVertices(outVerts);
            if (outNormals.Count == outVerts.Count) outMesh.SetNormals(outNormals);
            if (outUV.Count == outVerts.Count) outMesh.SetUVs(0, outUV);
            outMesh.SetTriangles(outTris, 0);
            outMesh.RecalculateBounds();
            if (outNormals.Count != outVerts.Count) outMesh.RecalculateNormals();
            return outMesh;
        }
    }
}
