using UnityEngine;

[RequireComponent(typeof(Renderer))]
[AddComponentMenu("Player/Portal Visual")]
public class PortalVisual : MonoBehaviour
{
    [Tooltip("Color principal del portal.")]
    [SerializeField] private Color portalColor = new Color(0.15f, 0.6f, 1f);

    [Tooltip("Segundo color, con el que alterna el pulso de emisión.")]
    [SerializeField] private Color pulseColor = new Color(0.7f, 0.15f, 1f);

    [Tooltip("Intensidad del brillo emisivo.")]
    [SerializeField, Min(0f)] private float emissionIntensity = 2.5f;

    [Tooltip("Velocidad del pulso de color.")]
    [SerializeField, Min(0f)] private float pulseSpeed = 1.5f;

    [Tooltip("Velocidad con la que se desliza la textura de energía sobre la superficie.")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.3f, 0.6f);

    [Tooltip("Resolución de la textura generada (más alto = más detalle, pero más memoria).")]
    [SerializeField, Range(32, 256)] private int textureResolution = 128;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer portalRenderer;
    private Material portalMaterial;

    private void Awake()
    {
        portalRenderer = GetComponent<Renderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        portalMaterial = new Material(shader);
        portalMaterial.EnableKeyword("_EMISSION");
        portalMaterial.mainTexture = GenerateRingTexture(textureResolution);

        portalRenderer.material = portalMaterial;
    }

    private void Update()
    {
        if (portalMaterial == null) return;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Color mixed = Color.Lerp(portalColor, pulseColor, pulse);

        portalMaterial.SetColor(BaseColorId, mixed);
        portalMaterial.SetColor(EmissionColorId, mixed * emissionIntensity);
        portalMaterial.mainTextureOffset = scrollSpeed * Time.time;
    }

    private void OnDestroy()
    {
        if (portalMaterial != null)
            Destroy(portalMaterial);
    }

    private Texture2D GenerateRingTexture(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;

        Vector2 center = new Vector2(resolution * 0.5f, resolution * 0.5f);
        float maxDist = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 pos = new Vector2(x, y) - center;
                float dist = pos.magnitude / maxDist;
                float angle = Mathf.Atan2(pos.y, pos.x);

                float spiral = Mathf.Sin(dist * 18f - angle * 3f) * 0.5f + 0.5f;
                float falloff = Mathf.Clamp01(1f - dist);
                float value = spiral * falloff;

                tex.SetPixel(x, y, new Color(value, value, value, 1f));
            }
        }

        tex.Apply();
        return tex;
    }
}
