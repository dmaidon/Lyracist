// Edited on Sep 23, 2026 @ 12:51:00 -> Fix Unity GetComponent fake null handling and add RequireComponent for MeshFilter
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaryoke.Unity.Wheel
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class FlapperNeedle : MonoBehaviour
    {
        [Header("Spring Physics")]
        public float MaxDeflectionAngle = 28.0f;
        public float SpringReturnSpeed = 35.0f;
        public float Damping = 6.0f;

        [Header("Audio Reference")]
        public WheelAudioController? AudioController;

        private float _currentAngle;
        private float _velocity;
        private Quaternion _baseRotation;

        public event Action? OnPegHit;

        private void Awake()
        {
            _baseRotation = transform.localRotation;
            BuildPointedNeedle();
        }

        public void BuildPointedNeedle()
        {
            _baseRotation = transform.localRotation;

            var filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            var rend = GetComponent<MeshRenderer>();
            if (rend == null)
            {
                rend = gameObject.AddComponent<MeshRenderer>();
            }

            var boxCol = GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = gameObject.AddComponent<BoxCollider>();
            }

            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, -0.33f, 0f);
            boxCol.size = new Vector3(0.32f, 0.90f, 0.20f);

            var mesh = new Mesh { name = "PointedNeedleMesh" };

            // 6 perimeter vertices at z = 0
            Vector3 vTop = new Vector3(0.0f, 0.12f, 0.0f);
            Vector3 vTopL = new Vector3(-0.10f, 0.08f, 0.0f);
            Vector3 vMidL = new Vector3(-0.15f, -0.10f, 0.0f);
            Vector3 vTip = new Vector3(0.0f, -0.78f, 0.0f); // Sharp pointed tip pointing down into the wheel
            Vector3 vMidR = new Vector3(0.15f, -0.10f, 0.0f);
            Vector3 vTopR = new Vector3(0.10f, 0.08f, 0.0f);

            // Front center ridge (towards camera, z = -0.055f)
            Vector3 csFront = new Vector3(0.0f, -0.10f, -0.055f);

            // Back center ridge (away from camera, z = +0.055f)
            Vector3 csBack = new Vector3(0.0f, -0.10f, 0.055f);

            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            Color baseColor = new Color(1.0f, 0.30f, 0.0f); // Radiant Neon Orange
            Color highlightColor = new Color(1.0f, 0.50f, 0.15f); // Facet highlight
            Color shadeColor = new Color(0.85f, 0.20f, 0.0f); // Facet shade
            Color backColor = new Color(0.70f, 0.16f, 0.0f); // Back shadow

            void AddTri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int start = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                colors.Add(col);
                colors.Add(col);
                colors.Add(col);
                tris.Add(start);
                tris.Add(start + 1);
                tris.Add(start + 2);
            }

            // Front 6 facets (winding clockwise towards camera)
            AddTri(vTop, csFront, vTopL, highlightColor);
            AddTri(vTopL, csFront, vMidL, highlightColor);
            AddTri(vMidL, csFront, vTip, highlightColor);

            AddTri(vTip, csFront, vMidR, shadeColor);
            AddTri(vMidR, csFront, vTopR, shadeColor);
            AddTri(vTopR, csFront, vTop, shadeColor);

            // Back 6 facets (winding clockwise towards rear)
            AddTri(vTopL, csBack, vTop, backColor);
            AddTri(vMidL, csBack, vTopL, backColor);
            AddTri(vTip, csBack, vMidL, backColor);

            AddTri(vMidR, csBack, vTip, backColor);
            AddTri(vTopR, csBack, vMidR, backColor);
            AddTri(vTop, csBack, vTopR, backColor);

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;

            // Needle Material
            Shader? shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = baseColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                rend.sharedMaterial = mat;
            }

            // Add polished brass pivot bolt at rotation axis
            var existingPin = transform.Find("PivotPin");
            if (existingPin == null)
            {
                var pinObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pinObj.name = "PivotPin";
                pinObj.transform.SetParent(transform, false);
                pinObj.transform.localPosition = Vector3.zero;
                pinObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pinObj.transform.localScale = new Vector3(0.13f, 0.07f, 0.13f);

                var pCol = pinObj.GetComponent<Collider>();
                if (pCol != null)
                {
                    if (Application.isEditor && !Application.isPlaying)
                        DestroyImmediate(pCol);
                    else
                        Destroy(pCol);
                }

                var pRend = pinObj.GetComponent<Renderer>();
                if (pRend != null && shader != null)
                {
                    var pMat = new Material(shader);
                    Color gold = new Color(1.0f, 0.82f, 0.2f);
                    pMat.color = gold;
                    if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", gold);
                    pRend.sharedMaterial = pMat;
                }
            }
        }

        private void Update()
        {
            // Simple damped harmonic oscillator back to base rotation
            float displacement = 0f - _currentAngle;
            float force = displacement * SpringReturnSpeed;
            _velocity += force * Time.deltaTime;
            _velocity -= _velocity * (Damping * Time.deltaTime);
            _currentAngle += _velocity * Time.deltaTime;

            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _currentAngle);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.name.StartsWith("Peg_", StringComparison.Ordinal))
            {
                // Deflect needle in direction of motion
                _currentAngle = MaxDeflectionAngle;
                _velocity = -10f;

                AudioController?.PlayTick();
                OnPegHit?.Invoke();
            }
        }

        public void TriggerStrike(float spinSpeed)
        {
            float direction = Mathf.Sign(spinSpeed);
            _currentAngle = direction * MaxDeflectionAngle;
            _velocity = -direction * 15f;

            AudioController?.PlayTick(Mathf.Abs(spinSpeed));
            OnPegHit?.Invoke();
        }
    }
}
