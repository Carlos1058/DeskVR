using UnityEngine;

namespace DeskVR.OS
{
    /// <summary>
    /// Manages ambient room sounds for DeskVR's cozy study environment.
    /// Features a primary ambient loop (subtle room tone / AC hum) plus
    /// an intermittent secondary sound (clock ticking or other environmental detail).
    /// All volumes are intentionally very soft to remain immersive without distraction.
    /// </summary>
    public class DeskVRAmbientSoundManager : MonoBehaviour
    {
        [Header("Ambient Background")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioClip ambientLoop;
        [SerializeField] private float ambientVolume = 0.08f; // Very soft background

        [Header("Procedural Room Tone (Fallback)")]
        [Tooltip("If no ambient loop is assigned, generate a subtle procedural room tone")]
        [SerializeField] private bool useProceduralTone = true;
        [SerializeField] private float proceduralToneVolume = 0.04f;

        [Header("Clock Tick")]
        [SerializeField] private AudioSource clockSource;
        [SerializeField] private float clockInterval = 1.0f; // Every second
        [SerializeField] private float clockVolume = 0.06f;

        private AudioClip proceduralRoomToneClip;
        private AudioClip proceduralTickClip;
        private float clockTimer;

        private void Awake()
        {
            // Setup ambient source
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
            }
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f; // 2D ambient (fills the whole room)
            ambientSource.priority = 200; // Low priority, background

            // Setup clock source
            if (clockSource == null)
            {
                GameObject clockGO = new GameObject("ClockTickSource");
                clockGO.transform.SetParent(transform, false);
                clockSource = clockGO.AddComponent<AudioSource>();
            }
            clockSource.playOnAwake = false;
            clockSource.loop = false;
            clockSource.spatialBlend = 0.4f; // Slightly spatial for realism
            clockSource.priority = 220;
        }

        private void Start()
        {
            // Generate procedural sounds
            GenerateProceduralRoomTone();
            GenerateProceduralClockTick();

            // Start ambient
            if (ambientLoop != null)
            {
                ambientSource.clip = ambientLoop;
                ambientSource.volume = ambientVolume;
            }
            else if (useProceduralTone && proceduralRoomToneClip != null)
            {
                ambientSource.clip = proceduralRoomToneClip;
                ambientSource.volume = proceduralToneVolume;
            }

            if (ambientSource.clip != null)
            {
                ambientSource.Play();
            }

            clockTimer = clockInterval;
        }

        private void Update()
        {
            // Intermittent clock tick
            clockTimer -= Time.deltaTime;
            if (clockTimer <= 0f)
            {
                clockTimer = clockInterval;
                PlayClockTick();
            }
        }

        private void PlayClockTick()
        {
            if (clockSource == null) return;

            AudioClip tickClip = proceduralTickClip;
            if (tickClip != null)
            {
                clockSource.pitch = Random.Range(0.96f, 1.04f); // Slight variation
                clockSource.PlayOneShot(tickClip, clockVolume);
            }
        }

        /// <summary>
        /// Generates a 2-second loopable subtle room tone (low-frequency AC hum).
        /// Very soft, barely perceptible — adds subliminal "lived-in" feeling.
        /// </summary>
        private void GenerateProceduralRoomTone()
        {
            int sampleRate = 44100;
            float duration = 2.0f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // Very low frequency AC hum (50/60 Hz harmonics)
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.15f;
                hum += Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.08f;
                hum += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.03f;

                // Add very subtle brownian noise
                float noise = (Random.value * 2f - 1f) * 0.02f;

                samples[i] = Mathf.Clamp((hum + noise) * 0.3f, -1f, 1f);
            }

            proceduralRoomToneClip = AudioClip.Create("ProceduralRoomTone", sampleCount, 1, sampleRate, false);
            proceduralRoomToneClip.SetData(samples, 0);
        }

        /// <summary>
        /// Generates a short procedural clock tick sound (~15ms).
        /// Crisp, dry, subtle.
        /// </summary>
        private void GenerateProceduralClockTick()
        {
            int sampleRate = 44100;
            float duration = 0.015f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float normalizedT = (float)i / sampleCount;

                // Sharp transient with rapid decay
                float envelope = Mathf.Exp(-normalizedT * 15f);

                // Click frequency (wood-on-wood character)
                float tick = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.5f;
                tick += Mathf.Sin(2f * Mathf.PI * 1100f * t) * 0.3f;

                // Brief noise burst at attack
                if (t < 0.002f)
                {
                    tick += (Random.value * 2f - 1f) * 0.4f * (1f - t / 0.002f);
                }

                samples[i] = Mathf.Clamp(tick * envelope * 0.6f, -1f, 1f);
            }

            proceduralTickClip = AudioClip.Create("ProceduralClockTick", sampleCount, 1, sampleRate, false);
            proceduralTickClip.SetData(samples, 0);
        }
    }
}
