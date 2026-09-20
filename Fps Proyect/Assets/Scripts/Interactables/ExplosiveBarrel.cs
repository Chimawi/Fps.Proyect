using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Player/Explosive Barrel")]
public class ExplosiveBarrel : MonoBehaviour, IShootable
{
    [Header("DAÑO POR IMPACTO")]
    [Tooltip("Cuántos disparos hacen falta para que explote.")]
    [SerializeField, Min(1)] private int hitsToExplode = 3;

    [Tooltip("Color hacia el que vira el barril con cada impacto (usa las propiedades de color/emisión del shader).")]
    [SerializeField] private Color damagedColor = new Color(1f, 0.1f, 0.05f);

    [Tooltip("Cuánto crece de tamaño (permanentemente) por cada disparo recibido, como si se hinchara.")]
    [SerializeField, Range(0f, 0.3f)] private float swellPerHit = 0.07f;

    [Tooltip("Qué tan rápido es el rebote elástico de la 'hinchazón' al recibir el golpe.")]
    [SerializeField, Min(0.5f)] private float swellPunchSpeed = 10f;

    [Header("EXPLOSIÓN")]
    [Tooltip("Cantidad de partículas en la explosión.")]
    [SerializeField, Range(1, 100)] private int particleCount = 45;

    [Tooltip("Cuánta sacudida de cámara produce la explosión (0-1, ver CameraShake).")]
    [SerializeField, Range(0f, 1f)] private float cameraShakeAmount = 0.8f;

    [Tooltip("Radio de referencia para la luz de la explosión.")]
    [SerializeField, Min(0f)] private float lightRange = 9f;

    [Tooltip("Cuánto tarda en desaparecer del todo el barril reventado.")]
    [SerializeField, Min(0.5f)] private float destroyDelay = 2f;

    [Tooltip("Radio dentro del cual la explosión rompe las cajas (BreakableCrate) que encuentre alrededor.")]
    [SerializeField, Min(0f)] private float crateBreakRadius = 4f;

    [Header("SONIDO")]
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.9f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] renderers;
    private Color[] originalColors;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 baseScale;
    private int hitsTaken;
    private bool exploded;
    private AudioSource audioSource;
    private CameraShake cameraShake;
    private Coroutine swellRoutine;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        baseScale = transform.localScale;

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            originalColors[i] = (mat != null && mat.HasProperty(BaseColorId)) ? mat.GetColor(BaseColorId) : Color.white;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        cameraShake = FindFirstObjectByType<CameraShake>();
    }

    public bool HasExploded => exploded;

    public event System.Action OnExploded;

    public void OnHitByProjectile(RaycastHit hit) => RegisterHit();

    public void RegisterHit()
    {
        if (exploded) return;

        hitsTaken++;
        float damageRatio = Mathf.Clamp01((float)hitsTaken / hitsToExplode);
        ApplyDamageTint(damageRatio);
        PlayGeneratedClip(GenerateHitClip(), hitVolume, 1f);

        if (swellRoutine != null) StopCoroutine(swellRoutine);
        swellRoutine = StartCoroutine(SwellPunchRoutine());

        if (hitsTaken >= hitsToExplode)
            Explode();
    }

    private void ApplyDamageTint(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(originalColors[i], damagedColor, amount));
            propertyBlock.SetColor(EmissionColorId, damagedColor * (amount * 1.5f));
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private IEnumerator SwellPunchRoutine()
    {
        Vector3 grownScale = baseScale * (1f + swellPerHit * hitsTaken);
        Vector3 punchScale = grownScale * 1.18f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * swellPunchSpeed;
            transform.localScale = Vector3.Lerp(punchScale, grownScale, t);
            yield return null;
        }
        transform.localScale = grownScale;
    }

    private void Explode()
    {
        exploded = true;
        if (swellRoutine != null) StopCoroutine(swellRoutine);

        OnExploded?.Invoke();

        Vector3 center = GetCenter();

        PlayGeneratedClip(GenerateExplosionClip(), explosionVolume, 1f);
        SpawnExplosionParticles(center);
        StartCoroutine(SpawnExplosionLight(center));
        BreakNearbyCrates(center);

        if (cameraShake != null)
            cameraShake.Shake(cameraShakeAmount);

        foreach (Renderer r in renderers) r.enabled = false;
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null) ownCollider.enabled = false;

        Destroy(gameObject, destroyDelay);
    }

    private void BreakNearbyCrates(Vector3 center)
    {
        if (crateBreakRadius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(center, crateBreakRadius);
        var broken = new HashSet<BreakableCrate>();
        foreach (Collider hit in hits)
        {
            BreakableCrate crate = hit.GetComponentInParent<BreakableCrate>();
            if (crate != null && broken.Add(crate))
                crate.Break();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (crateBreakRadius <= 0f) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        Gizmos.DrawWireSphere(GetCenterSafe(), crateBreakRadius);
    }

    private Vector3 GetCenterSafe()
    {
        if (renderers != null && renderers.Length > 0)
            return GetCenter();
        return transform.position;
    }

    private void SpawnExplosionParticles(Vector3 center)
    {
        GameObject fx = new GameObject("BarrelExplosionFX");
        fx.transform.position = center;

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        float lifetime = 0.7f;

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.25f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.6f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.5f, 0.12f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader != null)
        {
            Material mat = new Material(particleShader);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, Color.white);
            ps.GetComponent<ParticleSystemRenderer>().material = mat;
        }

        ps.Play();
        Destroy(fx, main.duration + lifetime + 0.5f);
    }

    private IEnumerator SpawnExplosionLight(Vector3 center)
    {
        GameObject lightObj = new GameObject("BarrelExplosionLight");
        lightObj.transform.position = center;

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.range = lightRange;

        float duration = 0.4f;
        float startIntensity = 8f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            light.intensity = Mathf.Lerp(startIntensity, 0f, t / duration);
            yield return null;
        }

        Destroy(lightObj);
    }

    private Vector3 GetCenter()
    {
        if (renderers.Length == 0) return transform.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private void PlayGeneratedClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateHitClip()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        float[] samples = ProceduralAudio.AllocateSamples(0.12f, sampleRate);
        int sampleCount = samples.Length;

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float frequency = Mathf.Lerp(700f, 250f, t);
            phase += frequency * (1f / sampleRate) * Mathf.PI * 2f;
            float envelope = Mathf.Pow(1f - t, 4f);
            samples[i] = Mathf.Sin(phase) * envelope;
        }

        return ProceduralAudio.CreateClip("BarrelHit (generado)", samples, sampleRate);
    }

    private AudioClip GenerateExplosionClip()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        float[] samples = ProceduralAudio.AllocateSamples(1f, sampleRate);
        int sampleCount = samples.Length;

        float rumblePhase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;

            float noise = Random.value * 2f - 1f;
            float noiseEnvelope = Mathf.Pow(1f - t, 8f);

            float rumbleFrequency = Mathf.Lerp(90f, 35f, t);
            rumblePhase += rumbleFrequency * (1f / sampleRate) * Mathf.PI * 2f;
            float rumbleEnvelope = Mathf.Pow(1f - t, 1.5f);
            float rumble = Mathf.Sin(rumblePhase) * rumbleEnvelope;

            samples[i] = Mathf.Clamp(noise * noiseEnvelope * 0.8f + rumble * 0.7f, -1f, 1f);
        }

        return ProceduralAudio.CreateClip("BarrelExplosion (generado)", samples, sampleRate);
    }
}
