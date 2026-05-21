using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace FracturedStudios.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ChapterOneMaskTerrainBuilder : MonoBehaviour
    {
        private const float DistanceFieldMax = 1000000f;
        private const string DefaultTopMaterialName = "MTBC1_TopMaterial";
        private const string DefaultWallMaterialName = "MTBC1_WallMaterial";

        private struct PolygonVertex
        {
            public Vector3 Position;
            public Vector2 Uv;

            public PolygonVertex(Vector3 position, Vector2 uv)
            {
                Position = position;
                Uv = uv;
            }
        }

        private readonly struct ContourSegment
        {
            public readonly PolygonVertex Start;
            public readonly PolygonVertex End;

            public ContourSegment(PolygonVertex start, PolygonVertex end)
            {
                Start = start;
                End = end;
            }
        }

        public enum MaskSampleKind
        {
            Empty = 0,
            Ground = 1,
            White = 2,
        }

        [Header("Mask")]
        [FormerlySerializedAs("sourceMask")]
        [SerializeField] private Texture2D MTBC1_sourceMask;
        [FormerlySerializedAs("worldWidth")]
        [SerializeField, Min(1f)] private float MTBC1_worldWidth = 256f;
        [FormerlySerializedAs("worldDepth")]
        [SerializeField, Min(1f)] private float MTBC1_worldDepth = 256f;
        [FormerlySerializedAs("flatHeight")]
        [SerializeField] private float MTBC1_flatHeight;
        [FormerlySerializedAs("invertYAxis")]
        [SerializeField] private bool MTBC1_invertYAxis = true;

        [Header("Detection")]
        [FormerlySerializedAs("minGroundSaturation")]
        [SerializeField, Range(0f, 1f)] private float MTBC1_minGroundSaturation = 0.12f;
        [FormerlySerializedAs("minGroundGreenDominance")]
        [SerializeField, Range(0f, 1f)] private float MTBC1_minGroundGreenDominance = 0.05f;
        [FormerlySerializedAs("whiteValueThreshold")]
        [SerializeField, Range(0f, 1f)] private float MTBC1_whiteValueThreshold = 0.9f;
        [FormerlySerializedAs("whiteSaturationThreshold")]
        [SerializeField, Range(0f, 1f)] private float MTBC1_whiteSaturationThreshold = 0.08f;

        [Header("Smoothing")]
        [SerializeField, Min(16)] private int MTBC1_meshResolutionLongestSide = 768;
        [SerializeField, Range(1, 8)] private int MTBC1_antiAliasSamplesPerAxis = 3;
        [SerializeField, Range(0f, 1f)] private float MTBC1_minGroundCoverage = 0.3f;
        [SerializeField, Range(0, 4)] private int MTBC1_fieldSmoothIterations = 1;
        [SerializeField, Range(0f, 1f)] private float MTBC1_fieldSmoothStrength = 0.65f;

        [Header("Terrain Shape")]
        [SerializeField] private bool MTBC1_fillEnclosedHoles = true;
        [SerializeField, Min(0f)] private float MTBC1_edgeHeight = 6f;
        [SerializeField, Min(0f)] private float MTBC1_centerHeight = 14f;
        [SerializeField, Min(0.01f)] private float MTBC1_heightBlendExponent = 1.35f;
        [SerializeField, Min(0f)] private float MTBC1_edgeWallDepth = 16f;

        [Header("Noise")]
        [SerializeField, Min(0f)] private float MTBC1_noiseHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float MTBC1_noiseScale = 4f;
        [SerializeField, Range(0f, 1f)] private float MTBC1_noiseEdgeFade = 0.6f;
        [SerializeField] private int MTBC1_noiseSeed = 1337;

        [Header("Surface Texturing")]
        [SerializeField] private Material MTBC1_topMaterialTemplate;
        [SerializeField] private Material MTBC1_wallMaterialTemplate;
        [SerializeField] private Texture2D MTBC1_topBaseTexture;
        [SerializeField] private Texture2D MTBC1_wallBaseTexture;
        [SerializeField] private Color MTBC1_topTint = Color.white;
        [SerializeField] private Color MTBC1_wallTint = Color.white;
        [SerializeField] private Vector2 MTBC1_topTextureTiling = new Vector2(12f, 12f);
        [SerializeField] private Vector2 MTBC1_wallTextureTiling = new Vector2(3f, 3f);

        [Header("Generated Output")]
        [FormerlySerializedAs("buildMeshCollider")]
        [SerializeField] private bool MTBC1_buildMeshCollider = true;
        [FormerlySerializedAs("targetMeshFilter")]
        [SerializeField] private MeshFilter MTBC1_targetMeshFilter;
        [FormerlySerializedAs("targetMeshCollider")]
        [SerializeField] private MeshCollider MTBC1_targetMeshCollider;
        [FormerlySerializedAs("placementRoot")]
        [SerializeField] private Transform MTBC1_placementRoot;
        [FormerlySerializedAs("meshAssetFolder")]
        [SerializeField] private string MTBC1_meshAssetFolder = "Assets/Generated/Chapter1";

        private Material _generatedTopMaterial;
        private Material _generatedWallMaterial;

        public Texture2D SourceMask => MTBC1_sourceMask;
        public Transform PlacementRoot => MTBC1_placementRoot;
        public string MeshAssetFolder => MTBC1_meshAssetFolder;
        public float BaseHeight
        {
            get => MTBC1_flatHeight;
            set => MTBC1_flatHeight = value;
        }

        public MeshFilter TargetMeshFilter
        {
            get
            {
                if (MTBC1_targetMeshFilter == null)
                    MTBC1_targetMeshFilter = GetComponent<MeshFilter>();
                return MTBC1_targetMeshFilter;
            }
        }

        public MeshCollider TargetMeshCollider
        {
            get
            {
                if (MTBC1_targetMeshCollider == null)
                    MTBC1_targetMeshCollider = GetComponent<MeshCollider>();
                return MTBC1_targetMeshCollider;
            }
        }

        public MeshRenderer TargetMeshRenderer => GetComponent<MeshRenderer>();

        public Mesh EditableMesh => TargetMeshFilter != null ? TargetMeshFilter.sharedMesh : null;

        public Vector3 SurfaceNormal => transform.up;

        private void Reset()
        {
            MTBC1_targetMeshFilter = GetComponent<MeshFilter>();
            MTBC1_targetMeshCollider = GetComponent<MeshCollider>();
        }

        public Mesh BuildMesh()
        {
            if (MTBC1_sourceMask == null)
            {
                Debug.LogWarning("[ChapterOneMaskTerrainBuilder] No source mask assigned.", this);
                return null;
            }

            if (!MTBC1_sourceMask.isReadable)
            {
                Debug.LogError("[ChapterOneMaskTerrainBuilder] Source mask must be marked Read/Write Enabled in the texture importer.", this);
                return null;
            }

            int width = MTBC1_sourceMask.width;
            int height = MTBC1_sourceMask.height;
            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning("[ChapterOneMaskTerrainBuilder] Source mask has invalid dimensions.", this);
                return null;
            }

            GetOutputGridSize(width, height, out int gridWidth, out int gridHeight);

            float cellWidth = MTBC1_worldWidth / gridWidth;
            float cellDepth = MTBC1_worldDepth / gridHeight;
            float xStart = -MTBC1_worldWidth * 0.5f;
            float zStart = -MTBC1_worldDepth * 0.5f;
            float[] field = BuildGroundField(gridWidth, gridHeight);
            if (MTBC1_fillEnclosedHoles)
                FillEnclosedHoles(field, gridWidth, gridHeight);
            SmoothGroundField(field, gridWidth, gridHeight);

            float[] vertexHeights = BuildVertexHeights(field, gridWidth, gridHeight);

            var vertices = new List<Vector3>();
            var topTriangles = new List<int>();
            var sideTriangles = new List<int>();
            var uvs = new List<Vector2>();
            var polygon = new List<PolygonVertex>(6);
            var contourSegments = new List<ContourSegment>();

            for (int mapY = 0; mapY < gridHeight; mapY++)
            {
                float vMin = (float)mapY / gridHeight;
                float vMax = (float)(mapY + 1) / gridHeight;
                for (int mapX = 0; mapX < gridWidth; mapX++)
                {
                    float uMin = (float)mapX / gridWidth;
                    float uMax = (float)(mapX + 1) / gridWidth;

                    float xMin = xStart + (mapX * cellWidth);
                    float xMax = xMin + cellWidth;
                    float zMin = zStart + (mapY * cellDepth);
                    float zMax = zMin + cellDepth;

                    float bottomLeftValue = field[GetFieldIndex(mapX, mapY, gridWidth)];
                    float bottomRightValue = field[GetFieldIndex(mapX + 1, mapY, gridWidth)];
                    float topRightValue = field[GetFieldIndex(mapX + 1, mapY + 1, gridWidth)];
                    float topLeftValue = field[GetFieldIndex(mapX, mapY + 1, gridWidth)];

                    float bottomLeftHeight = vertexHeights[GetFieldIndex(mapX, mapY, gridWidth)];
                    float bottomRightHeight = vertexHeights[GetFieldIndex(mapX + 1, mapY, gridWidth)];
                    float topRightHeight = vertexHeights[GetFieldIndex(mapX + 1, mapY + 1, gridWidth)];
                    float topLeftHeight = vertexHeights[GetFieldIndex(mapX, mapY + 1, gridWidth)];

                    PolygonVertex bottomLeft = new PolygonVertex(new Vector3(xMin, bottomLeftHeight, zMin), new Vector2(uMin, vMin));
                    PolygonVertex bottomRight = new PolygonVertex(new Vector3(xMax, bottomRightHeight, zMin), new Vector2(uMax, vMin));
                    PolygonVertex topRight = new PolygonVertex(new Vector3(xMax, topRightHeight, zMax), new Vector2(uMax, vMax));
                    PolygonVertex topLeft = new PolygonVertex(new Vector3(xMin, topLeftHeight, zMax), new Vector2(uMin, vMax));
                    PolygonVertex edgeBottom = InterpolateVertex(bottomLeft, bottomRight, bottomLeftValue, bottomRightValue);
                    PolygonVertex edgeRight = InterpolateVertex(bottomRight, topRight, bottomRightValue, topRightValue);
                    PolygonVertex edgeTop = InterpolateVertex(topLeft, topRight, topLeftValue, topRightValue);
                    PolygonVertex edgeLeft = InterpolateVertex(bottomLeft, topLeft, bottomLeftValue, topLeftValue);

                    bool bottomLeftInside = bottomLeftValue >= MTBC1_minGroundCoverage;
                    bool bottomRightInside = bottomRightValue >= MTBC1_minGroundCoverage;
                    bool topRightInside = topRightValue >= MTBC1_minGroundCoverage;
                    bool topLeftInside = topLeftValue >= MTBC1_minGroundCoverage;
                    int caseIndex = (bottomLeftInside ? 1 : 0) |
                                    (bottomRightInside ? 2 : 0) |
                                    (topRightInside ? 4 : 0) |
                                    (topLeftInside ? 8 : 0);

                    if (caseIndex == 0)
                        continue;

                    float centerValue = (bottomLeftValue + bottomRightValue + topRightValue + topLeftValue) * 0.25f;
                    bool centerInside = centerValue >= MTBC1_minGroundCoverage;

                    polygon.Clear();
                    switch (caseIndex)
                    {
                        case 1:
                            polygon.Add(bottomLeft);
                            polygon.Add(edgeBottom);
                            polygon.Add(edgeLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeLeft, edgeBottom);
                            break;
                        case 2:
                            polygon.Add(edgeBottom);
                            polygon.Add(bottomRight);
                            polygon.Add(edgeRight);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeBottom, edgeRight);
                            break;
                        case 3:
                            polygon.Add(bottomLeft);
                            polygon.Add(bottomRight);
                            polygon.Add(edgeRight);
                            polygon.Add(edgeLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeLeft, edgeRight);
                            break;
                        case 4:
                            polygon.Add(edgeRight);
                            polygon.Add(topRight);
                            polygon.Add(edgeTop);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeRight, edgeTop);
                            break;
                        case 5:
                            if (centerInside)
                            {
                                polygon.Add(bottomLeft);
                                polygon.Add(edgeBottom);
                                polygon.Add(edgeRight);
                                polygon.Add(topRight);
                                polygon.Add(edgeTop);
                                polygon.Add(edgeLeft);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            }
                            else
                            {
                                polygon.Add(bottomLeft);
                                polygon.Add(edgeBottom);
                                polygon.Add(edgeLeft);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);

                                polygon.Clear();
                                polygon.Add(edgeRight);
                                polygon.Add(topRight);
                                polygon.Add(edgeTop);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            }

                            AddContourSegment(contourSegments, edgeLeft, edgeBottom);
                            AddContourSegment(contourSegments, edgeRight, edgeTop);
                            break;
                        case 6:
                            polygon.Add(edgeBottom);
                            polygon.Add(bottomRight);
                            polygon.Add(topRight);
                            polygon.Add(edgeTop);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeBottom, edgeTop);
                            break;
                        case 7:
                            polygon.Add(bottomLeft);
                            polygon.Add(bottomRight);
                            polygon.Add(topRight);
                            polygon.Add(edgeTop);
                            polygon.Add(edgeLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeLeft, edgeTop);
                            break;
                        case 8:
                            polygon.Add(edgeLeft);
                            polygon.Add(edgeTop);
                            polygon.Add(topLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeTop, edgeLeft);
                            break;
                        case 9:
                            polygon.Add(bottomLeft);
                            polygon.Add(edgeBottom);
                            polygon.Add(edgeTop);
                            polygon.Add(topLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeBottom, edgeTop);
                            break;
                        case 10:
                            if (centerInside)
                            {
                                polygon.Add(edgeBottom);
                                polygon.Add(bottomRight);
                                polygon.Add(edgeRight);
                                polygon.Add(edgeTop);
                                polygon.Add(topLeft);
                                polygon.Add(edgeLeft);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            }
                            else
                            {
                                polygon.Add(edgeBottom);
                                polygon.Add(bottomRight);
                                polygon.Add(edgeRight);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);

                                polygon.Clear();
                                polygon.Add(edgeLeft);
                                polygon.Add(edgeTop);
                                polygon.Add(topLeft);
                                AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            }

                            AddContourSegment(contourSegments, edgeBottom, edgeRight);
                            AddContourSegment(contourSegments, edgeLeft, edgeTop);
                            break;
                        case 11:
                            polygon.Add(bottomLeft);
                            polygon.Add(bottomRight);
                            polygon.Add(edgeRight);
                            polygon.Add(edgeTop);
                            polygon.Add(topLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeRight, edgeTop);
                            break;
                        case 12:
                            polygon.Add(edgeRight);
                            polygon.Add(topRight);
                            polygon.Add(topLeft);
                            polygon.Add(edgeLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeLeft, edgeRight);
                            break;
                        case 13:
                            polygon.Add(bottomLeft);
                            polygon.Add(edgeBottom);
                            polygon.Add(edgeRight);
                            polygon.Add(topRight);
                            polygon.Add(topLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeBottom, edgeRight);
                            break;
                        case 14:
                            polygon.Add(edgeBottom);
                            polygon.Add(bottomRight);
                            polygon.Add(topRight);
                            polygon.Add(topLeft);
                            polygon.Add(edgeLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            AddContourSegment(contourSegments, edgeLeft, edgeBottom);
                            break;
                        case 15:
                            polygon.Add(bottomLeft);
                            polygon.Add(bottomRight);
                            polygon.Add(topRight);
                            polygon.Add(topLeft);
                            AddPolygon(vertices, topTriangles, sideTriangles, uvs, polygon);
                            break;
                    }
                }
            }

            if (vertices.Count == 0)
            {
                Debug.LogWarning("[ChapterOneMaskTerrainBuilder] No ground pixels were found in the source mask with the current thresholds.", this);
                return null;
            }

            var mesh = new Mesh
            {
                name = gameObject.name + "_Chapter1MaskMesh"
            };

            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            if (MTBC1_edgeWallDepth > 0f)
            {
                for (int i = 0; i < contourSegments.Count; i++)
                {
                    AddWallSegment(vertices, sideTriangles, uvs, contourSegments[i]);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = sideTriangles.Count > 0 ? 2 : 1;
            mesh.SetTriangles(topTriangles, 0, true);
            if (sideTriangles.Count > 0)
                mesh.SetTriangles(sideTriangles, 1, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void FillEnclosedHoles(float[] field, int gridWidth, int gridHeight)
        {
            var groundCells = new bool[gridWidth * gridHeight];
            var visited = new bool[groundCells.Length];
            var queue = new Queue<int>();

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    groundCells[(y * gridWidth) + x] = GetCellCoverage(field, x, y, gridWidth) >= MTBC1_minGroundCoverage;
                }
            }

            for (int x = 0; x < gridWidth; x++)
            {
                EnqueueExteriorCell(queue, visited, groundCells, x, 0, gridWidth, gridHeight);
                EnqueueExteriorCell(queue, visited, groundCells, x, gridHeight - 1, gridWidth, gridHeight);
            }

            for (int y = 0; y < gridHeight; y++)
            {
                EnqueueExteriorCell(queue, visited, groundCells, 0, y, gridWidth, gridHeight);
                EnqueueExteriorCell(queue, visited, groundCells, gridWidth - 1, y, gridWidth, gridHeight);
            }

            while (queue.Count > 0)
            {
                int cellIndex = queue.Dequeue();
                int cellX = cellIndex % gridWidth;
                int cellY = cellIndex / gridWidth;

                EnqueueExteriorCell(queue, visited, groundCells, cellX - 1, cellY, gridWidth, gridHeight);
                EnqueueExteriorCell(queue, visited, groundCells, cellX + 1, cellY, gridWidth, gridHeight);
                EnqueueExteriorCell(queue, visited, groundCells, cellX, cellY - 1, gridWidth, gridHeight);
                EnqueueExteriorCell(queue, visited, groundCells, cellX, cellY + 1, gridWidth, gridHeight);
            }

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int cellIndex = (y * gridWidth) + x;
                    if (groundCells[cellIndex] || visited[cellIndex])
                        continue;

                    PromoteCellToGround(field, x, y, gridWidth);
                }
            }
        }

        private float[] BuildGroundField(int gridWidth, int gridHeight)
        {
            var field = new float[(gridWidth + 1) * (gridHeight + 1)];
            float sampleRadiusU = 0.5f / gridWidth;
            float sampleRadiusV = 0.5f / gridHeight;

            for (int y = 0; y <= gridHeight; y++)
            {
                float v = y / (float)gridHeight;
                for (int x = 0; x <= gridWidth; x++)
                {
                    float u = x / (float)gridWidth;
                    field[GetFieldIndex(x, y, gridWidth)] = SampleGroundField(u, v, sampleRadiusU, sampleRadiusV);
                }
            }

            return field;
        }

        private float[] BuildVertexHeights(float[] field, int gridWidth, int gridHeight)
        {
            int width = gridWidth + 1;
            int height = gridHeight + 1;
            int count = width * height;

            var inside = new bool[count];
            var distances = new float[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = GetFieldIndex(x, y, gridWidth);
                    bool isInside = field[index] >= MTBC1_minGroundCoverage;
                    inside[index] = isInside;
                    distances[index] = isInside ? DistanceFieldMax : 0f;
                }
            }

            RelaxDistanceField(distances, inside, width, height, gridWidth, true);
            RelaxDistanceField(distances, inside, width, height, gridWidth, false);

            float maxDistance = 0f;
            for (int i = 0; i < count; i++)
            {
                if (!inside[i] || distances[i] >= DistanceFieldMax)
                    continue;

                maxDistance = Mathf.Max(maxDistance, distances[i]);
            }

            if (maxDistance <= 0f)
                maxDistance = 1f;

            Vector2 noiseOffset = GetNoiseOffset();
            var heights = new float[count];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)gridHeight;
                for (int x = 0; x < width; x++)
                {
                    int index = GetFieldIndex(x, y, gridWidth);
                    if (!inside[index])
                    {
                        heights[index] = MTBC1_flatHeight;
                        continue;
                    }

                    float interior01 = Mathf.Clamp01(distances[index] / maxDistance);
                    float shapedInterior = Mathf.Pow(interior01, MTBC1_heightBlendExponent);
                    float heightOffset = Mathf.Lerp(MTBC1_edgeHeight, MTBC1_centerHeight, shapedInterior);
                    float noiseOffsetHeight = EvaluateNoiseHeight(x / (float)gridWidth, v, interior01, noiseOffset);
                    heights[index] = MTBC1_flatHeight + heightOffset + noiseOffsetHeight;
                }
            }

            return heights;
        }

        private void SmoothGroundField(float[] field, int gridWidth, int gridHeight)
        {
            int iterations = Mathf.Max(0, MTBC1_fieldSmoothIterations);
            float strength = Mathf.Clamp01(MTBC1_fieldSmoothStrength);
            if (iterations == 0 || strength <= 0f)
                return;

            int width = gridWidth + 1;
            int height = gridHeight + 1;
            var buffer = new float[field.Length];

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = GetFieldIndex(x, y, gridWidth);
                        float sum = 0f;
                        float weightSum = 0f;

                        for (int sampleY = -1; sampleY <= 1; sampleY++)
                        {
                            int neighborY = Mathf.Clamp(y + sampleY, 0, height - 1);
                            float yWeight = sampleY == 0 ? 2f : 1f;
                            for (int sampleX = -1; sampleX <= 1; sampleX++)
                            {
                                int neighborX = Mathf.Clamp(x + sampleX, 0, width - 1);
                                float xWeight = sampleX == 0 ? 2f : 1f;
                                float weight = xWeight * yWeight;
                                sum += field[GetFieldIndex(neighborX, neighborY, gridWidth)] * weight;
                                weightSum += weight;
                            }
                        }

                        float smoothedValue = weightSum > 0f ? sum / weightSum : field[index];
                        buffer[index] = Mathf.Lerp(field[index], smoothedValue, strength);
                    }
                }

                for (int i = 0; i < field.Length; i++)
                {
                    field[i] = buffer[i];
                }
            }
        }

        private void GetOutputGridSize(int sourceWidth, int sourceHeight, out int gridWidth, out int gridHeight)
        {
            int longestSide = Mathf.Max(16, MTBC1_meshResolutionLongestSide);
            if (sourceWidth >= sourceHeight)
            {
                gridWidth = longestSide;
                gridHeight = Mathf.Max(1, Mathf.RoundToInt((sourceHeight / (float)sourceWidth) * longestSide));
                return;
            }

            gridHeight = longestSide;
            gridWidth = Mathf.Max(1, Mathf.RoundToInt((sourceWidth / (float)sourceHeight) * longestSide));
        }

        private float SampleGroundField(float centerU, float centerV, float radiusU, float radiusV)
        {
            int samplesPerAxis = Mathf.Max(1, MTBC1_antiAliasSamplesPerAxis);
            int groundSamples = 0;
            int sampleCount = samplesPerAxis * samplesPerAxis;

            for (int sampleY = 0; sampleY < samplesPerAxis; sampleY++)
            {
                float sampleV = Mathf.Lerp(centerV - radiusV, centerV + radiusV, (sampleY + 0.5f) / samplesPerAxis);
                for (int sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                {
                    float sampleU = Mathf.Lerp(centerU - radiusU, centerU + radiusU, (sampleX + 0.5f) / samplesPerAxis);
                    if (GetSampleKind(SampleMaskColor(sampleU, sampleV)) == MaskSampleKind.Ground)
                        groundSamples++;
                }
            }

            return groundSamples / (float)sampleCount;
        }

        private float GetCellCoverage(float[] field, int x, int y, int gridWidth)
        {
            float bottomLeft = field[GetFieldIndex(x, y, gridWidth)];
            float bottomRight = field[GetFieldIndex(x + 1, y, gridWidth)];
            float topRight = field[GetFieldIndex(x + 1, y + 1, gridWidth)];
            float topLeft = field[GetFieldIndex(x, y + 1, gridWidth)];
            return (bottomLeft + bottomRight + topRight + topLeft) * 0.25f;
        }

        private void EnqueueExteriorCell(Queue<int> queue, bool[] visited, bool[] groundCells, int x, int y, int gridWidth, int gridHeight)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                return;

            int index = (y * gridWidth) + x;
            if (visited[index] || groundCells[index])
                return;

            visited[index] = true;
            queue.Enqueue(index);
        }

        private void PromoteCellToGround(float[] field, int x, int y, int gridWidth)
        {
            field[GetFieldIndex(x, y, gridWidth)] = Mathf.Max(field[GetFieldIndex(x, y, gridWidth)], MTBC1_minGroundCoverage);
            field[GetFieldIndex(x + 1, y, gridWidth)] = Mathf.Max(field[GetFieldIndex(x + 1, y, gridWidth)], MTBC1_minGroundCoverage);
            field[GetFieldIndex(x + 1, y + 1, gridWidth)] = Mathf.Max(field[GetFieldIndex(x + 1, y + 1, gridWidth)], MTBC1_minGroundCoverage);
            field[GetFieldIndex(x, y + 1, gridWidth)] = Mathf.Max(field[GetFieldIndex(x, y + 1, gridWidth)], MTBC1_minGroundCoverage);
        }

        private void RelaxDistanceField(float[] distances, bool[] inside, int width, int height, int gridWidth, bool forward)
        {
            int startY = forward ? 0 : height - 1;
            int endY = forward ? height : -1;
            int stepY = forward ? 1 : -1;
            int startX = forward ? 0 : width - 1;
            int endX = forward ? width : -1;
            int stepX = forward ? 1 : -1;

            for (int y = startY; y != endY; y += stepY)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int index = GetFieldIndex(x, y, gridWidth);
                    if (!inside[index])
                        continue;

                    float best = distances[index];
                    best = Mathf.Min(best, GetNeighborDistance(distances, x - stepX, y, width, height, gridWidth, 1f));
                    best = Mathf.Min(best, GetNeighborDistance(distances, x, y - stepY, width, height, gridWidth, 1f));
                    best = Mathf.Min(best, GetNeighborDistance(distances, x - stepX, y - stepY, width, height, gridWidth, 1.4142135f));
                    best = Mathf.Min(best, GetNeighborDistance(distances, x + stepX, y - stepY, width, height, gridWidth, 1.4142135f));
                    distances[index] = best;
                }
            }
        }

        private float GetNeighborDistance(float[] distances, int x, int y, int width, int height, int gridWidth, float stepCost)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return DistanceFieldMax;

            return distances[GetFieldIndex(x, y, gridWidth)] + stepCost;
        }

        private float EvaluateNoiseHeight(float u, float v, float interior01, Vector2 noiseOffset)
        {
            if (MTBC1_noiseHeight <= 0f || MTBC1_noiseScale <= 0f)
                return 0f;

            float sample = Mathf.PerlinNoise(noiseOffset.x + (u * MTBC1_noiseScale), noiseOffset.y + (v * MTBC1_noiseScale));
            float signedNoise = (sample * 2f) - 1f;
            float noiseBlend = Mathf.Lerp(1f - MTBC1_noiseEdgeFade, 1f, interior01);
            return signedNoise * MTBC1_noiseHeight * Mathf.Clamp01(noiseBlend);
        }

        private Vector2 GetNoiseOffset()
        {
            float offsetX = Mathf.Repeat((MTBC1_noiseSeed * 0.7548777f) + 0.25f, 1f) * 997f;
            float offsetY = Mathf.Repeat((MTBC1_noiseSeed * 0.5698403f) + 0.75f, 1f) * 991f;
            return new Vector2(offsetX, offsetY);
        }

        private static int GetFieldIndex(int x, int y, int gridWidth)
        {
            return (y * (gridWidth + 1)) + x;
        }

        private PolygonVertex InterpolateVertex(PolygonVertex from, PolygonVertex to, float fromValue, float toValue)
        {
            float delta = toValue - fromValue;
            float t = Mathf.Approximately(delta, 0f)
                ? 0.5f
                : Mathf.Clamp01((MTBC1_minGroundCoverage - fromValue) / delta);

            return new PolygonVertex(
                Vector3.Lerp(from.Position, to.Position, t),
                Vector2.Lerp(from.Uv, to.Uv, t));
        }

        private void AddPolygon(List<Vector3> vertices, List<int> topTriangles, List<int> sideTriangles, List<Vector2> uvs, List<PolygonVertex> polygon)
        {
            if (polygon.Count < 3)
                return;

            bool reverse = SignedAreaXZ(polygon) < 0f;
            int startIndex = AppendPolygonVertices(vertices, uvs, polygon, reverse, null);

            int vertexCount = polygon.Count;
            for (int i = 1; i < vertexCount - 1; i++)
            {
                topTriangles.Add(startIndex + 0);
                topTriangles.Add(startIndex + i + 1);
                topTriangles.Add(startIndex + i);
            }

            if (MTBC1_edgeWallDepth <= 0f)
                return;

            float bottomY = MTBC1_flatHeight - MTBC1_edgeWallDepth;
            int bottomStartIndex = AppendPolygonVertices(vertices, uvs, polygon, reverse, bottomY);
            for (int i = 1; i < vertexCount - 1; i++)
            {
                sideTriangles.Add(bottomStartIndex + 0);
                sideTriangles.Add(bottomStartIndex + i);
                sideTriangles.Add(bottomStartIndex + i + 1);
            }
        }

        private static int AppendPolygonVertices(List<Vector3> vertices, List<Vector2> uvs, List<PolygonVertex> polygon, bool reverse, float? overrideY)
        {
            int startIndex = vertices.Count;
            if (reverse)
            {
                for (int i = polygon.Count - 1; i >= 0; i--)
                {
                    AppendVertex(vertices, uvs, polygon[i], overrideY);
                }
            }
            else
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    AppendVertex(vertices, uvs, polygon[i], overrideY);
                }
            }

            return startIndex;
        }

        private static void AppendVertex(List<Vector3> vertices, List<Vector2> uvs, PolygonVertex vertex, float? overrideY)
        {
            Vector3 position = vertex.Position;
            if (overrideY.HasValue)
                position.y = overrideY.Value;

            vertices.Add(position);
            uvs.Add(vertex.Uv);
        }

        private static void AddContourSegment(List<ContourSegment> contourSegments, PolygonVertex start, PolygonVertex end)
        {
            if ((start.Position - end.Position).sqrMagnitude <= 0.000001f)
                return;

            contourSegments.Add(new ContourSegment(start, end));
        }

        private void AddWallSegment(List<Vector3> vertices, List<int> sideTriangles, List<Vector2> uvs, ContourSegment segment)
        {
            float bottomY = MTBC1_flatHeight - MTBC1_edgeWallDepth;
            Vector3 topA = segment.Start.Position;
            Vector3 topB = segment.End.Position;
            Vector3 bottomA = new Vector3(topA.x, bottomY, topA.z);
            Vector3 bottomB = new Vector3(topB.x, bottomY, topB.z);
            float wallLength = Vector3.Distance(topA, topB);
            float wallHeightA = Mathf.Abs(topA.y - bottomY);
            float wallHeightB = Mathf.Abs(topB.y - bottomY);

            int frontStart = vertices.Count;
            vertices.Add(topA);
            vertices.Add(topB);
            vertices.Add(bottomB);
            vertices.Add(bottomA);
            uvs.Add(new Vector2(0f, wallHeightA));
            uvs.Add(new Vector2(wallLength, wallHeightB));
            uvs.Add(new Vector2(wallLength, 0f));
            uvs.Add(new Vector2(0f, 0f));
            sideTriangles.Add(frontStart + 0);
            sideTriangles.Add(frontStart + 1);
            sideTriangles.Add(frontStart + 2);
            sideTriangles.Add(frontStart + 0);
            sideTriangles.Add(frontStart + 2);
            sideTriangles.Add(frontStart + 3);

            int backStart = vertices.Count;
            vertices.Add(topA);
            vertices.Add(bottomA);
            vertices.Add(bottomB);
            vertices.Add(topB);
            uvs.Add(new Vector2(0f, wallHeightA));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(wallLength, 0f));
            uvs.Add(new Vector2(wallLength, wallHeightB));
            sideTriangles.Add(backStart + 0);
            sideTriangles.Add(backStart + 1);
            sideTriangles.Add(backStart + 2);
            sideTriangles.Add(backStart + 0);
            sideTriangles.Add(backStart + 2);
            sideTriangles.Add(backStart + 3);
        }

        private static float SignedAreaXZ(List<PolygonVertex> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                PolygonVertex current = polygon[i];
                PolygonVertex next = polygon[(i + 1) % polygon.Count];
                area += (current.Position.x * next.Position.z) - (next.Position.x * current.Position.z);
            }

            return area * 0.5f;
        }

        private Color SampleMaskColor(float normalizedU, float normalizedV)
        {
            float sampleV = MTBC1_invertYAxis ? 1f - normalizedV : normalizedV;
            return MTBC1_sourceMask.GetPixelBilinear(Mathf.Clamp01(normalizedU), Mathf.Clamp01(sampleV));
        }

        public void ApplyMesh(Mesh mesh)
        {
            if (TargetMeshFilter != null)
                TargetMeshFilter.sharedMesh = mesh;

            ApplySurfaceMaterials(mesh);

            if (!MTBC1_buildMeshCollider)
            {
                if (TargetMeshCollider != null)
                    TargetMeshCollider.sharedMesh = null;
                return;
            }

            if (MTBC1_targetMeshCollider == null)
                MTBC1_targetMeshCollider = gameObject.AddComponent<MeshCollider>();

            TargetMeshCollider.sharedMesh = null;
            TargetMeshCollider.sharedMesh = mesh;
        }

        public void ClearAppliedMesh()
        {
            if (MTBC1_targetMeshFilter == null)
                MTBC1_targetMeshFilter = GetComponent<MeshFilter>();
            if (MTBC1_targetMeshFilter != null)
                MTBC1_targetMeshFilter.sharedMesh = null;

            if (MTBC1_targetMeshCollider == null)
                MTBC1_targetMeshCollider = GetComponent<MeshCollider>();
            if (MTBC1_targetMeshCollider != null)
                MTBC1_targetMeshCollider.sharedMesh = null;
        }

        private void ApplySurfaceMaterials(Mesh mesh)
        {
            MeshRenderer meshRenderer = TargetMeshRenderer;
            if (meshRenderer == null)
                return;

            Material topMaterial = BuildSurfaceMaterial(ref _generatedTopMaterial, MTBC1_topMaterialTemplate, DefaultTopMaterialName, MTBC1_topBaseTexture, MTBC1_topTint, MTBC1_topTextureTiling);
            bool needsWallMaterial = mesh != null && mesh.subMeshCount > 1;
            if (!needsWallMaterial)
            {
                if (topMaterial != null)
                    meshRenderer.sharedMaterials = new[] { topMaterial };
                return;
            }

            Material wallTemplate = MTBC1_wallMaterialTemplate != null ? MTBC1_wallMaterialTemplate : MTBC1_topMaterialTemplate;
            Texture2D wallTexture = MTBC1_wallBaseTexture != null ? MTBC1_wallBaseTexture : MTBC1_topBaseTexture;
            Material wallMaterial = BuildSurfaceMaterial(ref _generatedWallMaterial, wallTemplate, DefaultWallMaterialName, wallTexture, MTBC1_wallTint, MTBC1_wallTextureTiling);
            if (topMaterial != null && wallMaterial != null)
                meshRenderer.sharedMaterials = new[] { topMaterial, wallMaterial };
            else if (topMaterial != null)
                meshRenderer.sharedMaterials = new[] { topMaterial, topMaterial };
        }

        private Material BuildSurfaceMaterial(ref Material generatedMaterial, Material templateMaterial, string fallbackName, Texture2D baseTexture, Color tint, Vector2 tiling)
        {
            if (generatedMaterial == null)
            {
                if (templateMaterial != null)
                {
                    generatedMaterial = new Material(templateMaterial);
                }
                else
                {
                    Shader shader = ResolveLitShader();
                    if (shader == null)
                        return null;

                    generatedMaterial = new Material(shader);
                }

                generatedMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
            else if (templateMaterial != null)
            {
                if (generatedMaterial.shader != templateMaterial.shader)
                {
                    generatedMaterial = new Material(templateMaterial)
                    {
                        hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                    };
                }
                else
                {
                    generatedMaterial.CopyPropertiesFromMaterial(templateMaterial);
                }
            }

            generatedMaterial.name = gameObject.name + "_" + fallbackName;
            ApplyMaterialProperties(generatedMaterial, baseTexture, tint, tiling);
            return generatedMaterial;
        }

        private static void ApplyMaterialProperties(Material material, Texture2D baseTexture, Color tint, Vector2 tiling)
        {
            if (material == null)
                return;

            string mainTextureProperty = material.HasProperty("_BaseMap") ? "_BaseMap" : material.HasProperty("_MainTex") ? "_MainTex" : null;
            if (!string.IsNullOrEmpty(mainTextureProperty))
            {
                material.SetTexture(mainTextureProperty, baseTexture);
                material.SetTextureScale(mainTextureProperty, tiling);
            }

            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : null;
            if (!string.IsNullOrEmpty(colorProperty))
                material.SetColor(colorProperty, tint);
        }

        private static Shader ResolveLitShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;
                if (pipelineName.Contains("Universal"))
                    return Shader.Find("Universal Render Pipeline/Lit");

                if (pipelineName.Contains("HD"))
                    return Shader.Find("HDRP/Lit");
            }

            return Shader.Find("Standard");
        }

        public MaskSampleKind SampleWorldPosition(Vector3 worldPosition)
        {
            if (MTBC1_sourceMask == null || !MTBC1_sourceMask.isReadable)
                return MaskSampleKind.Empty;

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float normalizedX = Mathf.InverseLerp(-MTBC1_worldWidth * 0.5f, MTBC1_worldWidth * 0.5f, local.x);
            float normalizedZ = Mathf.InverseLerp(-MTBC1_worldDepth * 0.5f, MTBC1_worldDepth * 0.5f, local.z);
            if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                return MaskSampleKind.Empty;

            int pixelX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * MTBC1_sourceMask.width), 0, MTBC1_sourceMask.width - 1);
            int pixelY = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * MTBC1_sourceMask.height), 0, MTBC1_sourceMask.height - 1);
            int sampleY = MTBC1_invertYAxis ? MTBC1_sourceMask.height - 1 - pixelY : pixelY;
            return GetSampleKind(MTBC1_sourceMask.GetPixel(pixelX, sampleY));
        }

        public bool IsGroundAtWorldPosition(Vector3 worldPosition)
        {
            return SampleWorldPosition(worldPosition) == MaskSampleKind.Ground;
        }

        public bool IsWhiteAtWorldPosition(Vector3 worldPosition)
        {
            return SampleWorldPosition(worldPosition) == MaskSampleKind.White;
        }

        private MaskSampleKind GetSampleKind(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            bool isWhite = value >= MTBC1_whiteValueThreshold && saturation <= MTBC1_whiteSaturationThreshold;
            if (isWhite)
                return MaskSampleKind.White;

            float greenDominance = color.g - Mathf.Max(color.r, color.b);
            bool isGround = saturation >= MTBC1_minGroundSaturation && greenDominance >= MTBC1_minGroundGreenDominance;
            return isGround ? MaskSampleKind.Ground : MaskSampleKind.Empty;
        }
    }
}