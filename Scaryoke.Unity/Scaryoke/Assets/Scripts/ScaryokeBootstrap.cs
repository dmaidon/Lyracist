// Edited on Sep 23, 2026 @ 12:56:00 -> Position flapper needle on front face at Z = -0.42 matching front-facing wheel
using System;
using Scaryoke.Unity.Display;
using Scaryoke.Unity.Wheel;
using UnityEngine;

namespace Scaryoke.Unity
{
    public class ScaryokeBootstrap : MonoBehaviour
    {
        [Header("Runtime State")]
        public WheelController? Wheel;
        public FlapperNeedle? Flapper;
        public WheelAudioController? AudioCtrl;
        public DisplayManager? DisplayMgr;
        public CastingBridge? CastBridge;

        private string _announcement = "Press SPIN or hit SPACEBAR!";
        private string _winnerBanner = string.Empty;
        private GUIStyle? _titleStyle;
        private GUIStyle? _buttonStyle;
        private GUIStyle? _bannerStyle;
        private GUIStyle? _exitButtonStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindObjectOfType<ScaryokeBootstrap>() == null)
            {
                var go = new GameObject("ScaryokeBootstrapManager");
                go.AddComponent<ScaryokeBootstrap>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            // Default to Windowed 1280x720 so it never traps the screen in exclusive fullscreen
            Screen.fullScreenMode = FullScreenMode.Windowed;

            SetupSceneComponents();
        }

        private void Start()
        {
            if (Wheel != null)
            {
                Wheel.OnSpinStarted += HandleSpinStarted;
                Wheel.OnSpinStopped += HandleSpinStopped;
            }
        }

        private void Update()
        {
            // Escape key exits immediately
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                TriggerSpin();
            }
        }

        public void TriggerSpin()
        {
            if (Wheel != null && !Wheel.IsSpinning)
            {
                Wheel.Spin();
            }
        }

        private void HandleSpinStarted()
        {
            _announcement = "Spinning...";
            _winnerBanner = string.Empty;
        }

        private void HandleSpinStopped(WheelSegmentData winner)
        {
            _announcement = "Landed on:";
            _winnerBanner = winner.Name;
        }

        private void SetupSceneComponents()
        {
            // 1. Ensure Camera exists with spooky dark background and frame large wheel
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            mainCam.transform.position = new Vector3(0f, 0.1f, -9.6f);
            mainCam.transform.LookAt(new Vector3(0f, 0.1f, 0f));
            mainCam.backgroundColor = new Color(0.03f, 0.04f, 0.08f); // #070B14
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // 2. Ensure Light exists
            if (FindObjectOfType<Light>() == null)
            {
                var lightObj = new GameObject("Directional Light");
                var light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.intensity = 1.1f;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 3. Audio Controller
            if (AudioCtrl == null)
            {
                var audioObj = new GameObject("AudioController");
                audioObj.transform.SetParent(transform);
                AudioCtrl = audioObj.AddComponent<WheelAudioController>();
            }

            // 4. Wheel & Generator
            if (Wheel == null)
            {
                var wheelObj = new GameObject("ScaryokeWheel");
                wheelObj.transform.SetParent(transform);
                wheelObj.transform.position = Vector3.zero;

                var generator = wheelObj.AddComponent<WheelSegmentGenerator>();
                Wheel = wheelObj.AddComponent<WheelController>();

                generator.RebuildWheel();
            }

            // 5. Pointed Flapper Needle (positioned for 4.2 radius wheel)
            if (Flapper == null)
            {
                var flapperObj = GameObject.Find("FlapperNeedle");
                if (flapperObj == null)
                {
                    flapperObj = new GameObject("FlapperNeedle");
                    flapperObj.transform.SetParent(transform);
                }

                flapperObj.transform.position = new Vector3(0f, 4.60f, -0.42f);
                flapperObj.transform.localScale = Vector3.one;

                Flapper = flapperObj.GetComponent<FlapperNeedle>();
                if (Flapper == null)
                {
                    Flapper = flapperObj.AddComponent<FlapperNeedle>();
                }
                Flapper.AudioController = AudioCtrl;
                Flapper.BuildPointedNeedle();
            }
            else
            {
                Flapper.gameObject.transform.position = new Vector3(0f, 4.60f, -0.42f);
                Flapper.gameObject.transform.localScale = Vector3.one;
                Flapper.BuildPointedNeedle();
            }

            if (Wheel != null)
            {
                Wheel.Flapper = Flapper;
                Wheel.AudioController = AudioCtrl;
            }

            // 6. Display & Casting
            if (DisplayMgr == null)
            {
                DisplayMgr = gameObject.AddComponent<DisplayManager>();
            }
            if (CastBridge == null)
            {
                CastBridge = gameObject.AddComponent<CastingBridge>();
                CastBridge.Controller = Wheel;
            }
        }

        private void OnGUI()
        {
            InitStyles();

            // Top Left Close / Exit Button
            if (GUI.Button(new Rect(16, 16, 110, 38), "✕ Exit (Esc)", _exitButtonStyle))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            // Top Header Card
            float screenWidth = Screen.width;
            GUI.Box(new Rect(screenWidth * 0.5f - 240, 16, 480, 75), string.Empty);
            GUI.Label(new Rect(screenWidth * 0.5f - 240, 20, 480, 30), "🎃 SCARYOKE WHEEL 3D", _titleStyle);
            GUI.Label(new Rect(screenWidth * 0.5f - 240, 52, 480, 32),
                string.IsNullOrEmpty(_winnerBanner) ? _announcement : $"{_announcement} {_winnerBanner}",
                string.IsNullOrEmpty(_winnerBanner) ? _titleStyle : _bannerStyle);

            // Bottom Center Spin Button
            bool spinning = Wheel != null && Wheel.IsSpinning;
            string buttonText = spinning ? "SPINNING..." : "SPIN (SPACE)";
            GUI.enabled = !spinning;
            if (GUI.Button(new Rect(screenWidth * 0.5f - 120, Screen.height - 85, 240, 56), buttonText, _buttonStyle))
            {
                TriggerSpin();
            }
            GUI.enabled = true;

            // Top Right Quick Casting Controls
            if (GUI.Button(new Rect(screenWidth - 170, 16, 154, 38), "📺 Project to TV"))
            {
                CastBridge?.ConnectToMiracastTv();
            }
        }

        private void InitStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                _titleStyle.normal.textColor = new Color(0.65f, 0.55f, 0.98f); // Violet
            }

            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(_titleStyle)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };
                _bannerStyle.normal.textColor = new Color(1.0f, 0.43f, 0.0f); // Spooky Orange
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
            }

            if (_exitButtonStyle == null)
            {
                _exitButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                _exitButtonStyle.normal.textColor = new Color(1.0f, 0.4f, 0.4f); // Light Red
            }
        }
    }
}
