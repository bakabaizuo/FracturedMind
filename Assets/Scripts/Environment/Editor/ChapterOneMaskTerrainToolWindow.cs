using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FracturedStudios.Environment;

namespace FracturedStudios.EditorTools
{

    
    [ExecuteAlways]
    public sealed class ChapterOneMaskTerrainToolWindow : EditorWindow
    {
        private const float BrushStrengthMultiplier = 3f;
        private static ChapterOneMaskTerrainToolWindow _instance;
        private static ChapterOneMaskTerrainBuilder _activeBuilder;
        private static bool _toolEnabled;
        private static bool _isStrokeActive;

        [SerializeField] private GameObject targetObject;
        [SerializeField] private float brushRadius = 4f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float baseHeight;

        [MenuItem("FracturedStudios/Terrain/MTBC1 Tool")]
        public static void OpenWindow()
        {
            Open(_activeBuilder);
        }

        public static void Open(ChapterOneMaskTerrainBuilder builder)
        {
            _instance = GetWindow<ChapterOneMaskTerrainToolWindow>("MTBC1 Terrain Tool");
            _instance.minSize = new Vector2(300f, 210f);
            if (builder != null)
            {
                _activeBuilder = builder;
                _instance.targetObject = builder.gameObject;
                _instance.baseHeight = builder.BaseHeight;
            }
            _instance.Show();
        }

        private void OnEnable()
        {
            _instance = this;
            SceneView.duringSceneGui += OnSceneGUI;
            if (_activeBuilder != null)
            {
                targetObject = _activeBuilder.gameObject;
                baseHeight = _activeBuilder.BaseHeight;
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;

            SceneView.duringSceneGui -= OnSceneGUI;
            _toolEnabled = false;
            _isStrokeActive = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MTBC1 Mesh Terrain Tool", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                SyncActiveBuilderFromTarget();
            }

            using (new EditorGUI.DisabledScope(targetObject == null))
            {
                bool hasBuilder = GetBuilderOnTarget(targetObject) != null;
                bool builderEnabled = EditorGUILayout.ToggleLeft("Attach MTBC1 Builder", hasBuilder);
                if (builderEnabled != hasBuilder)
                {
                    SetBuilderAttachment(builderEnabled);
                    hasBuilder = builderEnabled;
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Builder", _activeBuilder, typeof(ChapterOneMaskTerrainBuilder), true);
            }

            using (new EditorGUI.DisabledScope(_activeBuilder == null))
            {
                bool newEnabled = EditorGUILayout.Toggle("Tool Enabled", _toolEnabled);
                if (newEnabled != _toolEnabled)
                {
                    _toolEnabled = newEnabled;
                    SceneView.RepaintAll();
                }

                EditorGUI.BeginChangeCheck();
                baseHeight = EditorGUILayout.FloatField("Base Height", baseHeight);
                if (EditorGUI.EndChangeCheck() && _activeBuilder != null)
                {
                    Undo.RecordObject(_activeBuilder, "MTBC1 Change Base Height");
                    _activeBuilder.BaseHeight = baseHeight;
                    EditorUtility.SetDirty(_activeBuilder);
                    EditorSceneManager.MarkSceneDirty(_activeBuilder.gameObject.scene);
                }

                brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.25f, 64f);
                brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.01f, 8f);

                EditorGUILayout.HelpBox(
                    "Shift + Left Click: raise terrain under the yellow brush.\n" +
                    "Shift + Ctrl + Left Click: lower terrain under the yellow brush.\n" +
                    "Uncheck 'Attach MTBC1 Builder' to remove the component entirely.\n" +
                    "Ctrl+Z will undo the brush stroke.",
                    MessageType.Info);
            }
        }

        private void SyncActiveBuilderFromTarget()
        {
            _activeBuilder = GetBuilderOnTarget(targetObject);
            if (_activeBuilder != null)
            {
                baseHeight = _activeBuilder.BaseHeight;
                return;
            }

            _toolEnabled = false;
            _isStrokeActive = false;
        }

        private void SetBuilderAttachment(bool shouldAttach)
        {
            if (targetObject == null)
                return;

            ChapterOneMaskTerrainBuilder existingBuilder = GetBuilderOnTarget(targetObject);
            if (shouldAttach)
            {
                if (existingBuilder == null)
                {
                    existingBuilder = Undo.AddComponent<ChapterOneMaskTerrainBuilder>(targetObject);
                    EditorUtility.SetDirty(targetObject);
                    EditorSceneManager.MarkSceneDirty(targetObject.scene);
                }

                _activeBuilder = existingBuilder;
                baseHeight = _activeBuilder.BaseHeight;
                return;
            }

            _toolEnabled = false;
            _isStrokeActive = false;
            if (existingBuilder != null)
            {
                RemoveBuilder(existingBuilder);
                EditorUtility.SetDirty(targetObject);
                EditorSceneManager.MarkSceneDirty(targetObject.scene);
            }

            _activeBuilder = null;
        }

        private static void RemoveBuilder(ChapterOneMaskTerrainBuilder builder)
        {
            if (builder == null)
                return;

            if (Application.isPlaying)
            {
                if (builder.enabled)
                    builder.enabled = false;

                if (builder != null)
                    Object.Destroy(builder);

                return;
            }

            Undo.DestroyObjectImmediate(builder);
        }

        private static ChapterOneMaskTerrainBuilder GetBuilderOnTarget(GameObject candidate)
        {
            if (candidate == null)
                return null;

            return candidate.GetComponent<ChapterOneMaskTerrainBuilder>();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_toolEnabled || _activeBuilder == null)
                return;

            if (_activeBuilder.EditableMesh == null)
                return;

            Event evt = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (!TryGetBrushHit(ray, out Vector3 hitPoint))
                return;

            DrawBrushGizmo(hitPoint);

            bool isShiftPrimary = evt.shift && !evt.control && !evt.command;
            bool isShiftCtrl = evt.shift && (evt.control || evt.command);

            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && (isShiftPrimary || isShiftCtrl))
            {
                if (!_isStrokeActive)
                {
                    BeginUndoStroke();
                    _isStrokeActive = true;
                }

                float signedStrength = (isShiftCtrl ? -_instance.brushStrength : _instance.brushStrength) * BrushStrengthMultiplier;
                ApplyBrush(hitPoint, signedStrength);
                evt.Use();
            }

            if ((evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp) && evt.button == 0)
            {
                _isStrokeActive = false;
            }

            if (isShiftPrimary || isShiftCtrl)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
        }

        private static void DrawBrushGizmo(Vector3 hitPoint)
        {
            Color yellow = new Color(1f, 0.92f, 0.16f, 0.95f);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = yellow;
            Handles.DrawWireDisc(hitPoint, _activeBuilder.SurfaceNormal, _instance != null ? _instance.brushRadius : 4f, 2f);

            Color solid = yellow;
            solid.a = 0.08f;
            Handles.color = solid;
            Handles.DrawSolidDisc(hitPoint, _activeBuilder.SurfaceNormal, _instance != null ? _instance.brushRadius : 4f);
        }

        private static bool TryGetBrushHit(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = default;
            MeshCollider collider = _activeBuilder.TargetMeshCollider;
            if (collider != null && collider.sharedMesh != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
            {
                hitPoint = hit.point;
                return true;
            }

            Plane plane = new Plane(_activeBuilder.SurfaceNormal, _activeBuilder.transform.TransformPoint(new Vector3(0f, _activeBuilder.BaseHeight, 0f)));
            if (!plane.Raycast(ray, out float enter))
                return false;

            hitPoint = ray.GetPoint(enter);
            return true;
        }

        private static void BeginUndoStroke()
        {
            Mesh mesh = _activeBuilder.EditableMesh;
            if (mesh != null)
                Undo.RegisterCompleteObjectUndo(mesh, "MTBC1 Terrain Brush");

            Undo.RecordObject(_activeBuilder, "MTBC1 Terrain Brush");

            MeshCollider collider = _activeBuilder.TargetMeshCollider;
            if (collider != null)
                Undo.RecordObject(collider, "MTBC1 Terrain Brush");

            MeshFilter filter = _activeBuilder.TargetMeshFilter;
            if (filter != null)
                Undo.RecordObject(filter, "MTBC1 Terrain Brush");
        }

        private static void ApplyBrush(Vector3 worldCenter, float signedStrength)
        {
            Mesh mesh = _activeBuilder.EditableMesh;
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            bool changed = false;
            float radius = _instance != null ? _instance.brushRadius : 4f;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldVertex = _activeBuilder.transform.TransformPoint(vertices[i]);
                Vector2 worldXZ = new Vector2(worldVertex.x, worldVertex.z);
                Vector2 centerXZ = new Vector2(worldCenter.x, worldCenter.z);
                float distance = Vector2.Distance(worldXZ, centerXZ);
                if (distance > radius)
                    continue;

                float falloff = 1f - Mathf.Clamp01(distance / radius);
                vertices[i].y += signedStrength * falloff;
                changed = true;
            }

            if (!changed)
                return;

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshCollider collider = _activeBuilder.TargetMeshCollider;
            if (collider != null)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                EditorUtility.SetDirty(collider);
            }

            EditorUtility.SetDirty(mesh);
            EditorUtility.SetDirty(_activeBuilder);
            EditorSceneManager.MarkSceneDirty(_activeBuilder.gameObject.scene);
            SceneView.RepaintAll();
        }
    }
}