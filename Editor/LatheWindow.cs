using UnityEditor;
using UnityEngine;
using ProfileLathe;

namespace ProfileLathe.EditorTools
{
    /// <summary>
    /// Tools ▸ Profile Lathe. Draw a cross-section on the left, watch the
    /// revolved solid update live on the right, then save it as a mesh asset
    /// (optionally with a baked relief normal map and a prefab).
    /// </summary>
    public class LatheWindow : EditorWindow
    {
        LatheProfile _profile = new LatheProfile();
        readonly ProfileEditorControl _editor = new ProfileEditorControl();

        // preview (drawn with PreviewRenderUtility — we own the camera + lights
        // so the mesh always shows and orbit/zoom persists across edits)
        PreviewRenderUtility _preview;
        Mesh _previewMesh;
        Material _previewMat;
        Texture2D _normalMap;
        bool _dirty = true;

        // preview camera state
        Vector2 _previewDir = new Vector2(120f, -20f); // yaw, pitch (degrees)
        float _previewZoom = 1.6f;

        // save settings
        string _savePath = "Assets/LatheMeshes";
        string _assetName = "NewLathe";

        // ui state
        Vector2 _scroll;
        Color _matColor = new Color(0.79f, 0.63f, 0.29f);
        float _metallic = 0.8f;
        float _roughness = 0.3f;
        bool _doubleSided = true;

        const float LEFT_WIDTH = 320f;
        const float EDITOR_HEIGHT = 280f;

        [MenuItem("Tools/Profile Lathe")]
        static void Open()
        {
            var win = GetWindow<LatheWindow>("Profile Lathe");
            win.minSize = new Vector2(720, 520);
        }

        void OnEnable() => EditorApplication.delayCall += RebuildPreview;
        void OnDisable() => Cleanup();

        void Cleanup()
        {
            if (_preview != null) { _preview.Cleanup(); _preview = null; }
            if (_previewMesh != null) { DestroyImmediate(_previewMesh); _previewMesh = null; }
            if (_previewMat != null) { DestroyImmediate(_previewMat); _previewMat = null; }
            if (_normalMap != null) { DestroyImmediate(_normalMap); _normalMap = null; }
        }

        // ── GUI ──
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // left column
            EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_WIDTH), GUILayout.ExpandHeight(true));
            DrawLeftPanel();
            EditorGUILayout.EndVertical();

            // divider
            var div = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(div, new Color(0, 0, 0, 0.3f));

            // right column (preview)
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (_dirty)
            {
                RebuildPreview();
                _dirty = false;
                Repaint();
            }
        }

        void DrawLeftPanel()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Profile Lathe", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Draw a cross-section · revolve into a solid", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // ── profile editor canvas ──
            Rect canvas = GUILayoutUtility.GetRect(LEFT_WIDTH - 16f, EDITOR_HEIGHT);
            canvas = new RectOffset(2, 2, 2, 2).Remove(canvas);
            if (_editor.Draw(canvas, _profile)) _dirty = true;

            EditorGUILayout.LabelField(
                "Drag points · click curve to add · right-click to delete",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // presets
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Vase")) LoadPreset(LatheProfile.LathePreset.Vase);
            if (GUILayout.Button("Goblet")) LoadPreset(LatheProfile.LathePreset.Goblet);
            if (GUILayout.Button("Bottle")) LoadPreset(LatheProfile.LathePreset.Bottle);
            if (GUILayout.Button("Pawn")) LoadPreset(LatheProfile.LathePreset.Pawn);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();

            // ── revolve ──
            EditorGUILayout.LabelField("Revolve", EditorStyles.boldLabel);
            _profile.interpolation = (ProfileInterpolation)EditorGUILayout.EnumPopup(
                new GUIContent("Curve", "Linear segments or a smooth Catmull-Rom spline through the points."),
                _profile.interpolation);
            if (_profile.interpolation == ProfileInterpolation.CatmullRom)
                _profile.curveSamples = EditorGUILayout.IntSlider(
                    new GUIContent("Curve Samples", "Points sampled between control points."),
                    _profile.curveSamples, 2, 64);
            _profile.segments = EditorGUILayout.IntSlider(
                new GUIContent("Segments", "Angular subdivisions around the revolve."),
                _profile.segments, 3, 256);
            _profile.sweepDegrees = EditorGUILayout.Slider(
                new GUIContent("Sweep°", "Full 360 for a closed solid, less for a partial revolve."),
                _profile.sweepDegrees, 1f, 360f);
            _profile.capEnds = EditorGUILayout.Toggle(
                new GUIContent("Cap Ends", "Seal the open top and bottom with triangle fans."),
                _profile.capEnds);
            _profile.smoothShading = EditorGUILayout.Toggle("Smooth Shading", _profile.smoothShading);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("World Scale", EditorStyles.boldLabel);
            _profile.worldRadius = EditorGUILayout.FloatField("Radius", _profile.worldRadius);
            _profile.worldHeight = EditorGUILayout.FloatField("Height", _profile.worldHeight);

            // ── surface relief ──
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Surface Relief", EditorStyles.boldLabel);
            _profile.enableFlutes = EditorGUILayout.Toggle(
                new GUIContent("Vertical Flutes", "Bake a relief normal map of vertical flutes — detail without geometry."),
                _profile.enableFlutes);
            using (new EditorGUI.DisabledScope(!_profile.enableFlutes))
            {
                _profile.fluteCount = EditorGUILayout.IntSlider("Flute Count", _profile.fluteCount, 2, 64);
                _profile.fluteDepth = EditorGUILayout.Slider("Depth", _profile.fluteDepth, 0f, 1f);
                _profile.normalMapResolution = EditorGUILayout.IntPopup("Map Size",
                    _profile.normalMapResolution,
                    new[] { "128", "256", "512", "1024" },
                    new[] { 128, 256, 512, 1024 });
            }

            // ── material ──
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);
            _matColor = EditorGUILayout.ColorField("Color", _matColor);
            _metallic = EditorGUILayout.Slider("Metallic", _metallic, 0f, 1f);
            _roughness = EditorGUILayout.Slider("Roughness", _roughness, 0f, 1f);
            _doubleSided = EditorGUILayout.Toggle(
                new GUIContent("Double-Sided", "Draw both faces. Needed for hollow forms like a goblet, where the far inner wall would otherwise be culled and look like a hole."),
                _doubleSided);

            if (EditorGUI.EndChangeCheck()) _dirty = true;

            // ── stats ──
            EditorGUILayout.Space(6);
            if (_previewMesh != null)
                EditorGUILayout.LabelField(
                    $"Verts {_previewMesh.vertexCount:N0}   Tris {_previewMesh.triangles.Length / 3:N0}",
                    EditorStyles.miniLabel);

            // ── export ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            _savePath = EditorGUILayout.TextField("Folder", _savePath);
            _assetName = EditorGUILayout.TextField("Name", _assetName);
            if (GUILayout.Button("Save Mesh + Prefab", GUILayout.Height(26)))
                SaveAssets();

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        void DrawPreviewPanel()
        {
            if (_previewMesh == null) RebuildPreview();
            EnsurePreviewUtility();

            Rect r = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            HandlePreviewInput(r);

            if (Event.current.type == EventType.Repaint && _previewMesh != null)
            {
                _preview.BeginPreview(r, GUIStyle.none);
                RenderPreviewScene();
                _preview.EndAndDrawPreview(r);
            }

            GUI.Label(new Rect(r.x + 10, r.yMax - 22, 260, 18),
                "drag to orbit · scroll to zoom", EditorStyles.miniLabel);
        }

        void EnsurePreviewUtility()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 100f;
            _preview.lights[0].intensity = 1.4f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _preview.lights[1].intensity = 0.6f;
        }

        void HandlePreviewInput(Rect r)
        {
            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _previewDir.x += e.delta.x;
                _previewDir.y += e.delta.y;
                _previewDir.y = Mathf.Clamp(_previewDir.y, -89f, 89f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _previewZoom = Mathf.Clamp(_previewZoom + e.delta.y * 0.05f, 0.4f, 8f);
                e.Use();
                Repaint();
            }
        }

        void RenderPreviewScene()
        {
            _previewMesh.RecalculateBounds();
            Bounds b = _previewMesh.bounds;
            float radius = b.extents.magnitude;
            if (radius < 1e-4f) radius = 1f;

            // orbit camera around the mesh centre
            Quaternion rot = Quaternion.Euler(_previewDir.y, _previewDir.x, 0f);
            float dist = radius * 3.6f * _previewZoom;
            Vector3 camPos = b.center + rot * (Vector3.back * dist);
            _preview.camera.transform.position = camPos;
            _preview.camera.transform.rotation = rot;
            _preview.camera.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
            _preview.camera.farClipPlane = dist + radius * 4f;

            _preview.DrawMesh(_previewMesh, Matrix4x4.identity, _previewMat, 0);
            _preview.camera.Render();
        }

        // ── build / preview ──
        void RebuildPreview()
        {
            if (_previewMesh != null) DestroyImmediate(_previewMesh);
            _previewMesh = LatheMeshBuilder.Build(_profile, "LathePreview");

            if (_previewMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _previewMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            ApplyMaterial(_previewMat);
        }

        void ApplyMaterial(Material mat)
        {
            mat.color = _matColor;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", _metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - _roughness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1f - _roughness);

            if (_profile.enableFlutes)
            {
                if (_normalMap != null) DestroyImmediate(_normalMap);
                _normalMap = SurfaceBaker.BakeFlutes(
                    _profile.normalMapResolution, _profile.fluteCount, _profile.fluteDepth);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", _normalMap);
                if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", _normalMap);
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            }

            // Double-sided: a lathe shell is a single surface, so the far inner
            // wall of a hollow form (goblet cup) is back-facing and gets culled,
            // reading as a hole. URP/Lit exposes _Cull (0 = Off) to draw both
            // sides. Legacy Standard has no runtime cull control, so on Built-in
            // this toggle has no effect — that pipeline needs a two-sided shader.
            if (mat.HasProperty("_Cull"))
            {
                mat.SetInt("_Cull",
                    _doubleSided ? (int)UnityEngine.Rendering.CullMode.Off
                                 : (int)UnityEngine.Rendering.CullMode.Back);
            }
            mat.doubleSidedGI = _doubleSided;
        }

        void LoadPreset(LatheProfile.LathePreset preset)
        {
            _profile.LoadPreset(preset);
            _dirty = true;
        }

        // ── export ──
        void SaveAssets()
        {
            EnsureFolder(_savePath);

            Mesh mesh = LatheMeshBuilder.Build(_profile, _assetName + "_Mesh");
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{_savePath}/{_assetName}_Mesh.asset");
            AssetDatabase.CreateAsset(mesh, meshPath);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = _assetName + "_Mat" };
            ApplyMaterial(mat);

            if (_profile.enableFlutes && _normalMap != null)
            {
                var savedNormal = Instantiate(_normalMap);
                string normalPath = AssetDatabase.GenerateUniqueAssetPath($"{_savePath}/{_assetName}_Normal.asset");
                AssetDatabase.CreateAsset(savedNormal, normalPath);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", savedNormal);
            }

            string matPath = AssetDatabase.GenerateUniqueAssetPath($"{_savePath}/{_assetName}_Mat.mat");
            AssetDatabase.CreateAsset(mat, matPath);

            var go = new GameObject(_assetName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{_savePath}/{_assetName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            Debug.Log($"Profile Lathe: saved {meshPath}");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
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