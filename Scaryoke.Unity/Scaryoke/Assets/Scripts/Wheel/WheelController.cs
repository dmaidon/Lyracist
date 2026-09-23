// Created on Sep 23, 2026 @ 11:48:00 -> Wheel physics rotation and outcome manager
using System;
using UnityEngine;

namespace Scaryoke.Unity.Wheel
{
    [RequireComponent(typeof(WheelSegmentGenerator))]
    public class WheelController : MonoBehaviour
    {
        [Header("Physics & Motion")]
        public float MinSpinSpeed = 650.0f;
        public float MaxSpinSpeed = 1200.0f;
        public float AngularDrag = 140.0f;

        [Header("References")]
        public FlapperNeedle? Flapper;
        public WheelAudioController? AudioController;

        [Header("State")]
        [SerializeField] private bool _isSpinning;
        [SerializeField] private float _currentSpeed;
        [SerializeField] private float _currentRotation;

        public bool IsSpinning => _isSpinning;
        public float CurrentRotation => _currentRotation;

        public event Action? OnSpinStarted;
        public event Action<WheelSegmentData>? OnSpinStopped;

        private WheelSegmentGenerator _generator = null!;
        private float _lastTickAngle;

        private void Awake()
        {
            _generator = GetComponent<WheelSegmentGenerator>();
        }

        private void Update()
        {
            if (!_isSpinning) return;

            // Apply rotation
            float deltaAngle = _currentSpeed * Time.deltaTime;
            _currentRotation = (_currentRotation + deltaAngle) % 360f;
            transform.localRotation = Quaternion.Euler(0f, 0f, -_currentRotation);

            // Check peg strikes against flapper
            CheckPegPasses(deltaAngle);

            // Apply realistic deceleration
            float dragFactor = Mathf.Max(AngularDrag, _currentSpeed * 0.18f);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, dragFactor * Time.deltaTime);

            if (_currentSpeed <= 0.05f)
            {
                StopSpin();
            }
        }

        public void Spin()
        {
            if (_isSpinning) return;

            _isSpinning = true;
            _currentSpeed = UnityEngine.Random.Range(MinSpinSpeed, MaxSpinSpeed);
            _lastTickAngle = _currentRotation;

            OnSpinStarted?.Invoke();
        }

        private void StopSpin()
        {
            _isSpinning = false;
            _currentSpeed = 0f;

            WheelSegmentData? winner = GetWinningSegment();
            if (winner != null)
            {
                if (winner.IsDjChoice)
                {
                    AudioController?.PlayEvilLaugh();
                }
                else if (winner.IsSpecial)
                {
                    AudioController?.PlayCelebration();
                }

                OnSpinStopped?.Invoke(winner);
            }
        }

        public WheelSegmentData? GetWinningSegment()
        {
            var segments = _generator.Segments;
            if (segments == null || segments.Count == 0) return null;

            // The needle is at 12 o'clock (0 degrees).
            // Rotation rotates the wheel clockwise.
            float normalizedAngle = (360f - (_currentRotation % 360f)) % 360f;

            float accumulated = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                accumulated += segments[i].SweepAngle;
                if (normalizedAngle <= accumulated)
                {
                    return segments[i];
                }
            }

            return segments[segments.Count - 1];
        }

        private void CheckPegPasses(float delta)
        {
            var segments = _generator.Segments;
            if (segments == null || segments.Count == 0) return;

            // Check if we crossed a slice boundary
            float prev = _lastTickAngle;
            float current = _currentRotation;
            _lastTickAngle = current;

            float accumulated = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                accumulated += segments[i].SweepAngle;
                // If boundary lies between prev and current
                if (IsAngleBetween(accumulated, prev, current))
                {
                    Flapper?.TriggerStrike(_currentSpeed);
                    break;
                }
            }
        }

        private static bool IsAngleBetween(float target, float a, float b)
        {
            float targetNorm = target % 360f;
            float aNorm = a % 360f;
            float bNorm = b % 360f;

            if (aNorm <= bNorm)
            {
                return targetNorm >= aNorm && targetNorm < bNorm;
            }
            return targetNorm >= aNorm || targetNorm < bNorm;
        }
    }
}
