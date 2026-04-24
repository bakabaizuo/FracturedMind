using System.Collections.Generic;
using UnityEngine;

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
        private float _lastSampleTotal;
        public  const string ObjectHolder = "_System" + nameof(AiLightProcessor);
        
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
                    light.Active = false;///this is our limit mark for sampling a light.
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