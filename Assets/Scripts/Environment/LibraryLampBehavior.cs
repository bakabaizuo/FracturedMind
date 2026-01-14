using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LibraryLampBehavior : MonoBehaviour
{
    [Header("Lamp References")]
    [SerializeField] private Light lampLight;
    [SerializeField] private Renderer lampRenderer;
    [SerializeField] private ParticleSystem explosionEffect;
    [SerializeField] private Transform lookAnchor;

    [Header("Lamp Settings")]
    [SerializeField, Min(1)] private int looksBeforeExplosion = 3;
    [SerializeField, Min(0.1f)] private float flickerDuration = 1.25f;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Events")]
    [SerializeField] private UnityEvent onFlicker;
    [SerializeField] private UnityEvent onExplode;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private static readonly List<LibraryLampBehavior> activeLamps = new List<LibraryLampBehavior>();

    private int lookCounter;
    private bool hasExploded;
    private Coroutine flickerRoutine;

    public static IReadOnlyList<LibraryLampBehavior> ActiveLamps => activeLamps;
    public Transform LookAnchor => lookAnchor == null ? transform : lookAnchor;
    public bool HasExploded => hasExploded;

    private void OnEnable()
    {
        if (!activeLamps.Contains(this))
            activeLamps.Add(this);
    }

    private void OnDisable()
    {
        activeLamps.Remove(this);
    }

    private void Reset()
    {
        if (lampLight == null)
            lampLight = GetComponentInChildren<Light>();
        if (lampRenderer == null)
            lampRenderer = GetComponentInChildren<Renderer>();
        if (lookAnchor == null)
            lookAnchor = transform;
    }

    public void RegisterLook(LampVisionSensor sensor)
    {
        if (hasExploded)
            return;

        lookCounter++;
        if (logDebug)
            Debug.Log($"[LibraryLamp] Looks: {lookCounter}/{looksBeforeExplosion}");

        if (flickerRoutine != null)
            StopCoroutine(flickerRoutine);

        flickerRoutine = StartCoroutine(FlickerSequence());
    }

    private IEnumerator FlickerSequence()
    {
        onFlicker?.Invoke();

        float elapsed = 0f;
        float originalIntensity = lampLight != null ? lampLight.intensity : 1f;
        float duration = Mathf.Max(0.1f, flickerDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (lampLight != null)
                lampLight.intensity = originalIntensity * intensityCurve.Evaluate(t);
            yield return null;
        }

        if (lampLight != null)
            lampLight.intensity = originalIntensity;

        if (lookCounter >= looksBeforeExplosion)
            TriggerExplosion();
    }

    public void TriggerExplosion()
    {
        if (hasExploded)
            return;

        hasExploded = true;
        onExplode?.Invoke();

        if (explosionEffect != null)
            explosionEffect.Play();

        if (lampLight != null)
            lampLight.enabled = false;

        if (lampRenderer != null)
            lampRenderer.enabled = false;

        if (logDebug)
            Debug.Log("[LibraryLamp] Explosion triggered");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (looksBeforeExplosion < 1)
            looksBeforeExplosion = 1;
        if (flickerDuration < 0.1f)
            flickerDuration = 0.1f;
    }
#endif
}
