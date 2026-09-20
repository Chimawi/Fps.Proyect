using UnityEngine;

[AddComponentMenu("Player/Laser Projectile")]
public class LaserProjectile : MonoBehaviour
{
    [Header("MOVIMIENTO")]
    [Tooltip("Velocidad de viaje del proyectil (m/s).")]
    [SerializeField, Min(0f)] private float speed = 40f;

    [Header("ALCANCE / DURACIÓN")]
    [Tooltip("Distancia máxima al jugador antes de autodestruirse.")]
    [SerializeField, Min(0f)] private float maxDistanceFromPlayer = 60f;

    [Tooltip("Tiempo de vida máximo por seguridad, aunque no se cumplan las otras condiciones.")]
    [SerializeField, Min(0f)] private float maxLifetime = 5f;

    [Header("IMPACTO")]
    [Tooltip("Si está activo, el proyectil solo se destruye al chocar contra un BoxCollider. " +
             "Cualquier otro tipo de colisionador lo deja pasar de largo.")]
    [SerializeField] private bool onlyDestroyOnBoxCollider = true;

    [Tooltip("Capas que el proyectil puede llegar a impactar.")]
    [SerializeField] private LayerMask hitMask = ~0;

    private Transform player;
    private Vector3 lastPosition;
    private float spawnTime;

    public void Launch(Transform playerTransform)
    {
        player = playerTransform;
    }

    private void Start()
    {
        lastPosition = transform.position;
        spawnTime = Time.time;

        if (player == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
    }

    private void Update()
    {
        Vector3 step = transform.forward * (speed * Time.deltaTime);

        if (step.sqrMagnitude > 0f &&
            Physics.Raycast(lastPosition, step.normalized, out RaycastHit hit, step.magnitude, hitMask, QueryTriggerInteraction.Ignore))
        {
            bool hitSelf = player != null && hit.transform.IsChildOf(player);
            bool hitIsBox = hit.collider is BoxCollider;
            IShootable shootable = hit.collider.GetComponentInParent<IShootable>();

            if (!hitSelf && (!onlyDestroyOnBoxCollider || hitIsBox || shootable != null))
            {
                shootable?.OnHitByProjectile(hit);

                transform.position = hit.point;
                Destroy(gameObject);
                return;
            }
        }

        Vector3 newPosition = transform.position + step;
        transform.position = newPosition;
        lastPosition = newPosition;

        if (player != null && Vector3.Distance(newPosition, player.position) > maxDistanceFromPlayer)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time - spawnTime > maxLifetime)
            Destroy(gameObject);
    }
}
