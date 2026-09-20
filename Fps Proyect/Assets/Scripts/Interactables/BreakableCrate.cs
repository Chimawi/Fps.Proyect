using System.Collections;
using UnityEngine;

[AddComponentMenu("Player/Breakable Crate")]
public class BreakableCrate : MonoBehaviour, IShootable
{
    [Header("REACCIÓN AL IMPACTO (ANTES DE ROMPERSE)")]
    [Tooltip("Cuánto dura la reacción antes de romperse del todo (segundos).")]
    [SerializeField, Min(0f)] private float preBreakDuration = 0.25f;

    [Tooltip("Color del destello de impacto (usa las propiedades de color/emisión del shader del material).")]
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.4f);

    [Tooltip("Intensidad del brillo emisivo del destello.")]
    [SerializeField, Min(0f)] private float flashIntensity = 3f;

    [Tooltip("Qué tan fuerte sacude el objeto al recibir el impacto.")]
    [SerializeField, Min(0f)] private float shakeAmount = 0.04f;

    [Tooltip("Velocidad de la sacudida.")]
    [SerializeField, Min(0f)] private float shakeSpeed = 45f;

    [Header("FRAGMENTOS AL ROMPERSE")]
    [SerializeField] private bool spawnFragments = true;

    [SerializeField, Range(1, 20)] private int fragmentCount = 8;

    [Tooltip("Tamaño de cada fragmento (cubos pequeños con el mismo material que la caja).")]
    [SerializeField, Min(0.01f)] private float fragmentSize = 0.22f;

    [Tooltip("Fuerza con la que salen disparados los fragmentos.")]
    [SerializeField, Min(0f)] private float fragmentForce = 3f;

    [Tooltip("Cuánto tardan los fragmentos en desaparecer (segundos).")]
    [SerializeField, Min(0.5f)] private float fragmentLifetime = 4f;

    [Header("SONIDO")]
    [Tooltip("Si se asigna, se reproduce este sonido en vez del generado automáticamente.")]
    [SerializeField] private AudioClip customBreakClip;

    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField, Range(0f, 0.3f)] private float pitchRandomness = 0.1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] renderers;
    private Color[] originalBaseColors;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 homeLocalPosition;
    private AudioSource audioSource;
    private AudioClip generatedClip;
    private bool broken;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        homeLocalPosition = transform.localPosition;

        originalBaseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            originalBaseColors[i] = (mat != null && mat.HasProperty(BaseColorId)) ? mat.GetColor(BaseColorId) : Color.white;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        if (customBreakClip == null)
            generatedClip = GenerateCrackClip();
    }

    public event System.Action<BreakableCrate> OnBroken;

    public void OnHitByProjectile(RaycastHit hit) => Break();

    public void Break()
    {
        if (broken) return;
        broken = true;
        OnBroken?.Invoke(this);
        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        PlaySound();

        float t = 0f;
        while (t < preBreakDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / Mathf.Max(0.0001f, preBreakDuration));
            float falloff = 1f - progress;

            SetFlash(falloff);

            Vector3 shakeOffset = new Vector3(
                Mathf.Sin(Time.time * shakeSpeed),
                Mathf.Sin(Time.time * shakeSpeed * 1.3f + 1f),
                Mathf.Sin(Time.time * shakeSpeed * 0.7f + 2f)) * (shakeAmount * falloff);
            transform.localPosition = homeLocalPosition + shakeOffset;

            yield return null;
        }

        transform.localPosition = homeLocalPosition;
        SetFlash(0f);

        if (spawnFragments)
            SpawnFragments();

        foreach (Renderer r in renderers)
            r.enabled = false;
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
            ownCollider.enabled = false;

        Destroy(gameObject, 1.5f);
    }

    private void SetFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(originalBaseColors[i], flashColor, amount));
            propertyBlock.SetColor(EmissionColorId, flashColor * (flashIntensity * amount));
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private void SpawnFragments()
    {
        Bounds bounds = GetBounds();

        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "CrateFragment";
            fragment.transform.position = bounds.center + Random.insideUnitSphere * (bounds.extents.magnitude * 0.3f);
            fragment.transform.rotation = Random.rotation;
            fragment.transform.localScale = Vector3.one * fragmentSize * Random.Range(0.7f, 1.3f);

            if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
                fragment.GetComponent<Renderer>().sharedMaterial = renderers[0].sharedMaterial;

            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            Vector3 outward = fragment.transform.position - bounds.center;
            if (outward.sqrMagnitude < 0.0001f) outward = Random.onUnitSphere;
            rb.AddForce((outward.normalized + Vector3.up * 0.6f) * fragmentForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * fragmentForce, ForceMode.Impulse);

            Destroy(fragment, fragmentLifetime);
        }
    }

    private Bounds GetBounds()
    {
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.5f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void PlaySound()
    {
        AudioClip clip = customBreakClip != null ? customBreakClip : generatedClip;
        if (clip == null || audioSource == null) return;

        audioSource.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateCrackClip()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        float[] samples = ProceduralAudio.AllocateSamples(0.35f, sampleRate);
        int sampleCount = samples.Length;

        float thumpPhase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;

            float noise = Random.value * 2f - 1f;
            float noiseEnvelope = Mathf.Pow(1f - t, 6f);

            float thumpFrequency = Mathf.Lerp(180f, 55f, t);
            thumpPhase += thumpFrequency * (1f / sampleRate) * Mathf.PI * 2f;
            float thumpEnvelope = Mathf.Pow(1f - t, 3f);
            float thump = Mathf.Sin(thumpPhase) * thumpEnvelope;

            samples[i] = noise * noiseEnvelope * 0.6f + thump * 0.6f;
        }

        return ProceduralAudio.CreateClip("CrateBreak (generado)", samples, sampleRate);
    }
}
