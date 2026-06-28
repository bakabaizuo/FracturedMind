using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // Required for SphericalHarmonicsL2

namespace FracturedMind.AI
{
    /// <summary>
    /// Runtime registry for light influence used by AI perception.
    /// Register lights from scene setup code, then sample a normalized light level from any position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiLightProcessor : MonoBehaviour
    {
        private sealed class LightProxy
        {
            public Light Source;
            public Vector3 Position;
            public float Range;
            public float Intensity;
            public bool Active;
        }

        private readonly List<LightProxy> registeredLights = new List<LightProxy>();

        [SerializeField, Min(0.01f)] private float aiMaxSampleDistance = 300f;
        [SerializeField, Min(0.01f)] private float aiResumeSampleDistance = 280f;
        [SerializeField, Range(0f, 1f)] private float aiMinimumExposureThreshold = 0.02f;
        [SerializeField, Range(0f, 1f)] private float aiSampleSmoothing = 0.2f;

        [Header("Light Probes Fallback Configuration")]
        [SerializeField] private bool useLightProbesFallback = true;
        [SerializeField, Range(0.1f, 5f)] private float lightProbeIntensityScalar = 1.0f;

        private float _lastSampleTotal;
        public  const string ObjectHolder = "_System" + nameof(AiLightProcessor);
        
        // Cache structures for Spherical Harmonics evaluation to avoid runtime allocations
        private readonly Vector3[] shEvaluationDirections = new Vector3[] { Vector3.up };
        private readonly Color[] shEvaluationResults = new Color[1];

        public void Register(Light lightSource)
        {
            if (lightSource == null)
                return;

            Register(lightSource, lightSource.range, lightSource.intensity);
        }

        public void Register(Light lightSource, float range, float intensity)
        {
            if (lightSource == null)
                return;

            Unregister(lightSource);
            registeredLights.Add(new LightProxy
            {
                Source = lightSource,
                Position = lightSource.transform.position,
                Range = Mathf.Max(0.01f, range),
                Intensity = Mathf.Max(0f, intensity),
                Active = true,
            });
        }

        public void Unregister(Light lightSource)
        {
            if (lightSource == null)
                return;

            for (int i = registeredLights.Count - 1; i >= 0; i--)
            {
                if (registeredLights[i].Source == lightSource)
                    registeredLights.RemoveAt(i);
            }
        }

        public void Clear()
        {
            registeredLights.Clear();
        }

        /// <summary>
        /// Samples the baked Spherical Harmonics coefficients from surrounding Light Probes at a given position.
        /// </summary>
        private float SampleBakedLightProbes(Vector3 worldPosition)
        {
            // Query closest probe arrays and interpolate coefficients safely into an L2 struct
            LightProbes.GetInterpolatedProbe(worldPosition, null, out SphericalHarmonicsL2 sh);

            // Project coefficients against our upward evaluation vector (simulating overhead environmental lighting)
            sh.Evaluate(shEvaluationDirections, shEvaluationResults);

            // Extract the grayscale value from the resulting color profile channel
            float ambientIntensity = shEvaluationResults[0].grayscale * lightProbeIntensityScalar;

            return ambientIntensity;
        }

        private void UpdateLightCache()
        {
            for (int i = 0; i < registeredLights.Count; i++)
            {
                var l = registeredLights[i];

                if (l.Source == null)
                {
                    registeredLights.RemoveAt(i);
                    i--;
                    continue;
                }

                l.Position = l.Source.transform.position;
                l.Intensity = l.Source.intensity;
                l.Range = l.Source.range;
            }
        }

        public float SampleLightLevel(Vector3 samplePosition, Vector3 forward)
        {
            UpdateLightCache();

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            float total = 0f;

            for (int i = registeredLights.Count - 1; i >= 0; i--)
            {
                LightProxy light = registeredLights[i];

                if (light.Source == null)
                {
                    registeredLights.RemoveAt(i);
                    continue;
                }

                Vector3 toLight = light.Position - samplePosition;
                float dist = toLight.magnitude;

                if (!light.Active)
                {
                    if (dist > aiResumeSampleDistance)
                    {
                        registeredLights[i] = light;
                        continue;
                    }

                    light.Active = true;
                }
              
                if (dist > aiMaxSampleDistance)
                {
                    light.Active = false; // Limit mark for sampling a light.
                    registeredLights[i] = light;
                    continue;
                }

                if (dist <= 0.001f)
                {
                    registeredLights[i] = light;
                    continue;
                }

                float range = Mathf.Max(0.01f, light.Range);

                if (dist > range)
                {
                    registeredLights[i] = light;
                    continue;
                }

                Vector3 dir = toLight / dist;
                float dot = Mathf.Max(0f, Vector3.Dot(dir, forward));

                if (dot < 0.05f)
                {
                    registeredLights[i] = light;
                    continue;
                }

                float normalizedRange = Mathf.Clamp01(1f - (dist / range));
                float falloff = Mathf.Pow(normalizedRange, 2f);
                float visionWeight = Mathf.Pow(dot, 2f);

                total += visionWeight * falloff * light.Intensity;

                registeredLights[i] = light;
            }

            // Fallback: If no real-time dynamic light balances are hitting this spot, blend the baked light probes
            if (total < 0.01f && useLightProbesFallback)
            {
                total = SampleBakedLightProbes(samplePosition);
            }

            if (float.IsNaN(total) || float.IsInfinity(total))
                total = 0f;

            total = Mathf.Lerp(_lastSampleTotal, total, aiSampleSmoothing);
            _lastSampleTotal = total;

            if (total < aiMinimumExposureThreshold)
                total = 0f;

            return Mathf.Clamp01(total);
        }

        public float SampleDarkness(Vector3 samplePosition, Vector3 forward)
        {
            return 1f - SampleLightLevel(samplePosition, forward);
        }

        public float SampleHalfDarkness(Vector3 samplePosition, Vector3 forward)
        {
            return Mathf.Clamp01(SampleDarkness(samplePosition, forward) * 0.5f);
        }
    }
}