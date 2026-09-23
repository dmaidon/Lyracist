// Created on Sep 23, 2026 @ 11:47:00 -> Dynamic audio controller with procedural sound generation fallback
using System;
using UnityEngine;

namespace Scaryoke.Unity.Wheel
{
    [RequireComponent(typeof(AudioSource))]
    public class WheelAudioController : MonoBehaviour
    {
        [Header("Clips (Optional - Procedural fallback used if null)")]
        public AudioClip? TickClip;
        public AudioClip? EvilLaughClip;
        public AudioClip? CheerClip;

        [Header("Audio Properties")]
        public float BasePitch = 1.0f;
        public float MaxPitch = 1.6f;

        private AudioSource _audioSource = null!;
        private AudioClip _procTickClip = null!;
        private AudioClip _procLaughClip = null!;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D clean stereo

            _procTickClip = GenerateProceduralTick();
            _procLaughClip = GenerateProceduralLaugh();
        }

        public void PlayTick(float speed = 100f)
        {
            float pitchFactor = Mathf.Clamp01(speed / 500f);
            _audioSource.pitch = Mathf.Lerp(BasePitch, MaxPitch, pitchFactor);

            AudioClip clip = TickClip != null ? TickClip : _procTickClip;
            _audioSource.PlayOneShot(clip, 0.75f);
        }

        public void PlayEvilLaugh()
        {
            _audioSource.pitch = 1.0f;
            AudioClip clip = EvilLaughClip != null ? EvilLaughClip : _procLaughClip;
            _audioSource.PlayOneShot(clip, 1.0f);
        }

        public void PlayCelebration()
        {
            _audioSource.pitch = 1.1f;
            AudioClip clip = CheerClip != null ? CheerClip : _procTickClip;
            _audioSource.PlayOneShot(clip, 1.0f);
        }

        private static AudioClip GenerateProceduralTick()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.035f); // 35 ms snappy click
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 220f);
                float wave = Mathf.Sin(2f * Mathf.PI * 1800f * t);
                data[i] = wave * decay * 0.8f;
            }

            var clip = AudioClip.Create("ProceduralTick", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateProceduralLaugh()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 1.5f); // 1.5 sec eerie tremolo laugh
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.Clamp01(t / 1.5f) * Mathf.PI);
                float fm = 160f + (Mathf.Sin(2f * Mathf.PI * 6f * t) * 60f); // cackle wobble
                float wave = Mathf.Sin(2f * Mathf.PI * fm * t);
                data[i] = wave * envelope * 0.7f;
            }

            var clip = AudioClip.Create("ProceduralLaugh", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
