using UnityEngine;

[AddComponentMenu("Player/Camera Shake")]
public class CameraShake : MonoBehaviour
{
    [Tooltip("Controlador del jugador. Si se deja vacío se busca automáticamente en los padres.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Rotación máxima de la sacudida (grados) cuando el 'trauma' está al máximo.")]
    [SerializeField, Min(0f)] private float maxShakeAngle = 6f;

    [Tooltip("Qué tan rápido oscila la sacudida.")]
    [SerializeField, Min(0f)] private float shakeFrequency = 22f;

    [Tooltip("Qué tan rápido se apaga la sacudida (por segundo).")]
    [SerializeField, Min(0f)] private float traumaDecay = 1.6f;

    private float trauma;
    private float seedX, seedY, seedZ;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerController>();

        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedZ = Random.Range(0f, 100f);
    }

    public void Shake(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (trauma > 0f)
            trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);

        float shakeAmount = trauma * trauma;
        if (shakeAmount <= 0.0001f)
        {
            player.CameraShakeOffset = Vector3.zero;
            return;
        }

        float t = Time.time * shakeFrequency;
        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(seedX, t) * 2f - 1f,
            Mathf.PerlinNoise(seedY, t) * 2f - 1f,
            Mathf.PerlinNoise(seedZ, t) * 2f - 1f) * (maxShakeAngle * shakeAmount);

        player.CameraShakeOffset = offset;
    }
}
