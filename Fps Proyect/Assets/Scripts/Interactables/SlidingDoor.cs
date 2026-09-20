using System.Collections;
using UnityEngine;

[AddComponentMenu("Player/Sliding Door")]
public class SlidingDoor : MonoBehaviour
{
    [Tooltip("Cuánto se mueve la puerta (en su propio espacio local) al abrirse, respecto a su posición inicial. " +
             "Probá los valores en Play Mode hasta que deslice en la dirección y distancia correctas.")]
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 9f, 0f);

    [Tooltip("Cuánto tarda en abrirse (segundos).")]
    [SerializeField, Min(0.05f)] private float openDuration = 1.2f;

    [Tooltip("Curva de suavizado del movimiento (por defecto: arranca rápido y frena suave, como una puerta automática).")]
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Si se desactiva un Collider en este mismo objeto, se apaga al abrir (para no dejar la puerta 'trabada' aunque ya esté abierta visualmente).")]
    [SerializeField] private bool disableOwnColliderOnOpen = true;

    [Tooltip("Otros colliders a desactivar al abrir (por ejemplo, el collider grueso de la pared/marco que " +
             "en algunos prefabs de puerta vive en un objeto HERMANO separado del panel que se desliza -- " +
             "ese collider no se mueve solo, así que hay que apagarlo a mano acá).")]
    [SerializeField] private Collider[] collidersToDisable;

    [Tooltip("Si está activo, una vez que termina de subir/deslizarse se destruye el objeto (para que no quede visible asomando en la escena).")]
    [SerializeField] private bool destroyAfterOpening = false;

    private Vector3 closedLocalPosition;
    private bool isOpen;
    private Coroutine moveRoutine;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        closedLocalPosition = transform.localPosition;
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        if (disableOwnColliderOnOpen)
        {
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = false;
        }

        if (collidersToDisable != null)
        {
            foreach (Collider extra in collidersToDisable)
            {
                if (extra != null) extra.enabled = false;
            }
        }

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(closedLocalPosition + openLocalOffset));
    }

    private IEnumerator MoveRoutine(Vector3 targetLocalPosition)
    {
        Vector3 start = transform.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;
            float eased = easing.Evaluate(Mathf.Clamp01(t));
            transform.localPosition = Vector3.LerpUnclamped(start, targetLocalPosition, eased);
            yield return null;
        }

        transform.localPosition = targetLocalPosition;

        if (destroyAfterOpening)
            Destroy(gameObject);
    }
}
