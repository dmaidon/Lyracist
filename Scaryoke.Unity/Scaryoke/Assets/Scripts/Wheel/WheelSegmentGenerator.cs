// Edited on Sep 23, 2026 @ 12:56:00 -> Fix Z coordinates so wheel front, pegs, labels, and hub face camera at -Z
using System;
using System.Collections.Generic;
using Scaryoke.Unity.Data;
using UnityEngine;

namespace Scaryoke.Unity.Wheel
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WheelSegmentGenerator : MonoBehaviour
    {
        // High-saturation, vivid arcade/carnival palette
        private static readonly Color[] PaletteColors = new[]
        {
            new Color(0.62f, 0.12f, 0.94f), // Electric Purple (#9E1EF0)
            new Color(1.00f, 0.45f, 0.00f), // Neon Spooky Orange (#FF7300)
            new Color(0.00f, 0.85f, 0.92f), // Electric Cyan (#00D9EA)
            new Color(1.00f, 0.10f, 0.55f), // Hot Neon Pink (#FF1A8C)
            new Color(0.18f, 0.38f, 1.00f), // Royal Blue (#2E61FF)
            new Color(1.00f, 0.60f, 0.00f), // Vivid Amber Orange (#FF9900)
            new Color(0.00f, 0.82f, 0.58f), // Radiant Mint Teal (#00D194)
            new Color(0.95f, 0.06f, 0.35f), // Crimson Rose (#F20F59)
            new Color(0.55f, 0.20f, 1.00f), // Bright Violet (#8C33FF)
            new Color(1.00f, 0.80f, 0.00f), // Golden Yellow (#FFCC00)
            new Color(0.00f, 0.72f, 0.88f)  // Ocean Blue (#00B8E0)
        };

        [Header("Wheel Dimensions")]
        public float Radius = 4.2f;
        public float Thickness = 0.5f;
        public int SegmentsPerWedge = 8;

        [Header("Peg Settings")]
        public GameObject? PegPrefab;
        public float PegRadius = 0.09f;
        public float PegHeight = 0.45f;

        [Header("State")]
        [SerializeField] private List<WheelSegmentData> _segments = new List<WheelSegmentData>();
        public IReadOnlyList<WheelSegmentData> Segments => _segments;

        private readonly List<GameObject> _spawnedPegs = new List<GameObject>();
        private readonly List<GameObject> _spawnedLabels = new List<GameObject>();
        private GameObject? _centerHub;

        public void RebuildWheel(ScaryokeSettingsModel? settings = null)
        {
            settings ??= ScaryokeDataBridge.LoadSettings();
            BuildSegmentData(settings.Categories);
            GenerateMeshAndMaterials();
            SpawnLabels();
            SpawnPegs();
            SpawnCenterHub();
        }

        private void BuildSegmentData(List<string> categories)
        {
            _segments.Clear();
            var list = new List<WheelSegmentData>();

            for (int i = 0; i < categories.Count; i++)
            {
                Color segColor = PaletteColors[i % PaletteColors.Length];
                list.Add(new WheelSegmentData
                {
                    Name = categories[i],
                    SegmentColor = segColor,
                    TextColor = GetContrastingTextColor(segColor),
                    Weight = 1.0f
                });
            }

            var singersChoice = new WheelSegmentData
            {
                Name = "Singer's Choice",
                SegmentColor = new Color(0.00f, 0.88f, 0.38f), // Brilliant Emerald Green (#00E061)
                TextColor = Color.white,
                Weight = 1.0f,
                IsSpecial = true
            };

            var leftSliver = new WheelSegmentData
            {
                Name = "DJ's Choice",
                SegmentColor = Color.black,
                TextColor = new Color(1.0f, 0.30f, 0.00f), // Fire Orange-Red
                Weight = 0.15f,
                IsDjChoice = true
            };

            var rightSliver = new WheelSegmentData
            {
                Name = "DJ's Choice",
                SegmentColor = Color.black,
                TextColor = new Color(1.0f, 0.30f, 0.00f),
                Weight = 0.15f,
                IsDjChoice = true
            };

            if (list.Count == 0)
            {
                list.Add(leftSliver);
                list.Add(singersChoice);
                list.Add(rightSliver);
            }
            else
            {
                int insertIndex = UnityEngine.Random.Range(0, list.Count + 1);
                list.Insert(insertIndex, leftSliver);
                list.Insert(insertIndex + 1, singersChoice);
                list.Insert(insertIndex + 2, rightSliver);
            }

            float totalWeight = (list.Count - 2) + (2f * 0.15f);
            float baseSweep = 360f / totalWeight;

            foreach (var seg in list)
            {
                seg.SweepAngle = seg.Weight * baseSweep;
                _segments.Add(seg);
            }
        }

        private void GenerateMeshAndMaterials()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            var mesh = new Mesh { name = "ProceduralWheelMesh" };

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            int submeshCount = _segments.Count;
            mesh.subMeshCount = submeshCount;

            var submeshTriangles = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                submeshTriangles[i] = new List<int>();
            }

            Vector3 centerFront = new Vector3(0, 0, -Thickness * 0.5f);
            Vector3 centerBack = new Vector3(0, 0, Thickness * 0.5f);

            float currentAngle = 0f;

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                float sweep = seg.SweepAngle;
                float step = sweep / SegmentsPerWedge;
                var currentTris = submeshTriangles[i];

                for (int s = 0; s < SegmentsPerWedge; s++)
                {
                    float a0 = (currentAngle + (s * step)) * Mathf.Deg2Rad;
                    float a1 = (currentAngle + ((s + 1) * step)) * Mathf.Deg2Rad;

                    Vector3 p0Front = new Vector3(Mathf.Sin(a0) * Radius, Mathf.Cos(a0) * Radius, -Thickness * 0.5f);
                    Vector3 p1Front = new Vector3(Mathf.Sin(a1) * Radius, Mathf.Cos(a1) * Radius, -Thickness * 0.5f);
                    Vector3 p0Back = new Vector3(Mathf.Sin(a0) * Radius, Mathf.Cos(a0) * Radius, Thickness * 0.5f);
                    Vector3 p1Back = new Vector3(Mathf.Sin(a1) * Radius, Mathf.Cos(a1) * Radius, Thickness * 0.5f);

                    // Front face triangle (facing -Z towards camera at -9.6)
                    int idx = vertices.Count;
                    vertices.Add(centerFront);
                    vertices.Add(p0Front);
                    vertices.Add(p1Front);

                    normals.Add(Vector3.back);
                    normals.Add(Vector3.back);
                    normals.Add(Vector3.back);

                    uvs.Add(new Vector2(0.5f, 0.5f));
                    uvs.Add(new Vector2(0.5f + (Mathf.Sin(a0) * 0.5f), 0.5f + (Mathf.Cos(a0) * 0.5f)));
                    uvs.Add(new Vector2(0.5f + (Mathf.Sin(a1) * 0.5f), 0.5f + (Mathf.Cos(a1) * 0.5f)));

                    currentTris.Add(idx);
                    currentTris.Add(idx + 1);
                    currentTris.Add(idx + 2);

                    // Back face triangle (facing +Z away from camera)
                    idx = vertices.Count;
                    vertices.Add(centerBack);
                    vertices.Add(p1Back);
                    vertices.Add(p0Back);

                    normals.Add(Vector3.forward);
                    normals.Add(Vector3.forward);
                    normals.Add(Vector3.forward);

                    uvs.Add(new Vector2(0.5f, 0.5f));
                    uvs.Add(new Vector2(0.5f + (Mathf.Sin(a1) * 0.5f), 0.5f + (Mathf.Cos(a1) * 0.5f)));
                    uvs.Add(new Vector2(0.5f + (Mathf.Sin(a0) * 0.5f), 0.5f + (Mathf.Cos(a0) * 0.5f)));

                    currentTris.Add(idx);
                    currentTris.Add(idx + 1);
                    currentTris.Add(idx + 2);

                    // Outer Rim Quad
                    idx = vertices.Count;
                    vertices.Add(p0Front);
                    vertices.Add(p1Front);
                    vertices.Add(p1Back);
                    vertices.Add(p0Back);

                    Vector3 rimNormal = (p0Front + p1Front).normalized;
                    normals.Add(rimNormal);
                    normals.Add(rimNormal);
                    normals.Add(rimNormal);
                    normals.Add(rimNormal);

                    uvs.Add(new Vector2(0, 1));
                    uvs.Add(new Vector2(1, 1));
                    uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(0, 0));

                    currentTris.Add(idx);
                    currentTris.Add(idx + 1);
                    currentTris.Add(idx + 2);
                    currentTris.Add(idx);
                    currentTris.Add(idx + 2);
                    currentTris.Add(idx + 3);
                }

                currentAngle += sweep;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            for (int i = 0; i < submeshCount; i++)
            {
                mesh.SetTriangles(submeshTriangles[i], i);
            }

            meshFilter.sharedMesh = mesh;

            // Prioritize Unlit shaders to guarantee 100% vibrant, radiant, un-dimmed colors
            Shader? shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Diffuse");

            var materials = new Material[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                var mat = new Material(shader ?? Shader.Find("Sprites/Default"));
                Color c = _segments[i].SegmentColor;
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                materials[i] = mat;
            }
            meshRenderer.sharedMaterials = materials;
        }

        private void SpawnLabels()
        {
            foreach (var lbl in _spawnedLabels)
            {
                if (lbl != null) DestroyImmediate(lbl);
            }
            _spawnedLabels.Clear();

            float currentAngle = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                float midAngle = currentAngle + (seg.SweepAngle * 0.5f);
                float midRad = midAngle * Mathf.Deg2Rad;

                // Position label along radial line on front face (-Z towards camera)
                float labelRadius = Radius * 0.65f;
                Vector3 pos = new Vector3(Mathf.Sin(midRad) * labelRadius, Mathf.Cos(midRad) * labelRadius, (-Thickness * 0.5f) - 0.05f);

                var labelObj = new GameObject($"Label_{i}_{seg.Name}");
                labelObj.transform.SetParent(transform, false);
                labelObj.transform.localPosition = pos;

                // Radial rotation pointing from center outward
                labelObj.transform.localRotation = Quaternion.Euler(0f, 0f, -midAngle + 90f);

                var tm = labelObj.AddComponent<TextMesh>();
                tm.text = seg.Name;
                tm.color = seg.TextColor;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;

                // Upsized bold text sizes
                if (seg.IsDjChoice)
                {
                    tm.fontSize = 26;
                    tm.characterSize = 0.042f;
                }
                else
                {
                    tm.fontSize = seg.Name.Length > 12 ? 34 : 42;
                    tm.characterSize = seg.Name.Length > 12 ? 0.052f : 0.062f;
                }

                _spawnedLabels.Add(labelObj);

                currentAngle += seg.SweepAngle;
            }
        }

        private void SpawnPegs()
        {
            foreach (var peg in _spawnedPegs)
            {
                if (peg != null) DestroyImmediate(peg);
            }
            _spawnedPegs.Clear();

            float currentAngle = 0f;
            Shader? pegShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            Color goldColor = new Color(1.0f, 0.88f, 0.20f); // Bright golden brass
            var pegMat = new Material(pegShader ?? Shader.Find("Sprites/Default"));
            pegMat.color = goldColor;
            if (pegMat.HasProperty("_BaseColor")) pegMat.SetColor("_BaseColor", goldColor);

            for (int i = 0; i < _segments.Count; i++)
            {
                float rad = currentAngle * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(rad) * (Radius * 0.95f), Mathf.Cos(rad) * (Radius * 0.95f), (-Thickness * 0.5f) - (PegHeight * 0.4f));

                GameObject pegObj;
                if (PegPrefab != null)
                {
                    pegObj = Instantiate(PegPrefab, transform);
                }
                else
                {
                    pegObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pegObj.name = $"Peg_{i}";
                    pegObj.transform.localScale = new Vector3(PegRadius * 2f, PegHeight * 0.5f, PegRadius * 2f);
                    pegObj.transform.SetParent(transform, false);

                    var rend = pegObj.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = pegMat;

                    var col = pegObj.GetComponent<Collider>();
                    if (col != null) col.isTrigger = false;
                }

                pegObj.transform.localPosition = pos;
                pegObj.transform.localRotation = Quaternion.Euler(90f, 0f, -currentAngle);
                _spawnedPegs.Add(pegObj);

                currentAngle += _segments[i].SweepAngle;
            }
        }

        private void SpawnCenterHub()
        {
            if (_centerHub != null)
            {
                DestroyImmediate(_centerHub);
            }

            _centerHub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _centerHub.name = "CenterHub";
            _centerHub.transform.SetParent(transform, false);
            _centerHub.transform.localPosition = new Vector3(0f, 0f, (-Thickness * 0.5f) - 0.08f);
            _centerHub.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _centerHub.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);

            var hubCol = _centerHub.GetComponent<Collider>();
            if (hubCol != null) DestroyImmediate(hubCol);

            Shader? hubShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            Color hubColor = new Color(0.10f, 0.06f, 0.18f); // Deep midnight purple
            var hubMat = new Material(hubShader ?? Shader.Find("Sprites/Default"));
            hubMat.color = hubColor;
            if (hubMat.HasProperty("_BaseColor")) hubMat.SetColor("_BaseColor", hubColor);

            var rend = _centerHub.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = hubMat;

            // Add center icon / text
            var iconObj = new GameObject("HubIcon");
            iconObj.transform.SetParent(_centerHub.transform, false);
            iconObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            iconObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var tm = iconObj.AddComponent<TextMesh>();
            tm.text = "🎃";
            tm.fontSize = 54;
            tm.characterSize = 0.22f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
        }

        private static Color GetContrastingTextColor(Color bg)
        {
            float luminance = (0.299f * bg.r) + (0.587f * bg.g) + (0.114f * bg.b);
            return luminance > 0.5f ? new Color(0.08f, 0.04f, 0.15f) : Color.white;
        }

        private void Start()
        {
            if (_segments.Count == 0)
            {
                RebuildWheel();
            }
        }
    }
}
