// Created on Sep 23, 2026 @ 11:43:00 -> Wheel segment data representation
using System;
using UnityEngine;

namespace Scaryoke.Unity.Wheel
{
    [Serializable]
    public class WheelSegmentData
    {
        public string Name = string.Empty;
        public Color SegmentColor = new Color(1f, 0.43f, 0f);
        public Color TextColor = Color.white;
        public float SweepAngle;
        public float Weight = 1.0f;
        public bool IsSpecial;
        public bool IsDjChoice;
    }
}
