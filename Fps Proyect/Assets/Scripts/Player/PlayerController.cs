using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Player/Player Controller (First Person)")]
public class PlayerController : MonoBehaviour
{
    [Header("REFERENCIAS")]
    [Tooltip("Transform de la cámara (el hijo que rota en vertical al mirar). " +
             "Si se deja vacío se busca automáticamente una Camera hija.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Punto usado para comprobar el suelo. Colócalo a la altura de los pies. " +
             "Si se deja vacío se usa la base del CharacterController.")]
    [SerializeField] private Transform groundCheck;

    [Header("MOVIMIENTO")]
    [Tooltip("Velocidad al caminar (m/s).")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;

    [Tooltip("Aceleración con la que se alcanza la velocidad objetivo en el suelo.")]
    [SerializeField, Min(0f)] private float acceleration = 14f;

    [Tooltip("Frenado cuando no hay input en el suelo.")]
    [SerializeField, Min(0f)] private float deceleration = 18f;

    [Tooltip("Control en el aire: 0 = ninguno, 1 = igual que en el suelo.")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.4f;

    [Header("SPRINT")]
    [SerializeField] private bool enableSprint = true;

    [Tooltip("Velocidad al correr (m/s).")]
    [SerializeField, Min(0f)] private float sprintSpeed = 8.5f;

    [Tooltip("Si está activo se corre manteniendo la tecla; si no, funciona como interruptor.")]
    [SerializeField] private bool sprintIsHold = true;

    [Header("AGACHARSE (CROUCH)")]
    [SerializeField] private bool enableCrouch = true;

    [Tooltip("Velocidad al ir agachado (m/s).")]
    [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;

    [Tooltip("Altura del CharacterController de pie.")]
    [SerializeField, Min(0.2f)] private float standingHeight = 1.8f;

    [Tooltip("Altura del CharacterController agachado.")]
    [SerializeField, Min(0.2f)] private float crouchHeight = 1f;

    [Tooltip("Suavizado de la transición de altura al agacharse/levantarse.")]
    [SerializeField, Min(0.1f)] private float crouchLerpSpeed = 12f;

    [Tooltip("Mantener pulsado para agacharse; si se desactiva funciona como interruptor.")]
    [SerializeField] private bool crouchIsHold = true;

    [Tooltip("Si está activo, la cámara baja al agacharse (para que se note desde la vista del jugador).")]
    [SerializeField] private bool lowerCameraOnCrouch = true;

    [Header("SALTO Y GRAVEDAD")]
    [SerializeField] private bool enableJump = true;

    [Tooltip("Altura máxima del salto en metros.")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.4f;

    [Tooltip("Gravedad aplicada al jugador (negativa).")]
    [SerializeField] private float gravity = -22f;

    [Tooltip("Saltos máximos seguidos (2 = doble salto).")]
    [SerializeField, Min(1)] private int maxJumps = 1;

    [Tooltip("Margen tras dejar el suelo en el que todavía se puede saltar (segundos).")]
    [SerializeField, Range(0f, 0.5f)] private float coyoteTime = 0.12f;

    [Tooltip("Tiempo que se 'recuerda' la pulsación de salto antes de tocar suelo (segundos).")]
    [SerializeField, Range(0f, 0.5f)] private float jumpBuffer = 0.1f;

    [Tooltip("Velocidad vertical de caída máxima (terminal).")]
    [SerializeField, Min(1f)] private float terminalVelocity = 55f;

    [Header("CÁMARA (MIRAR)")]
    [SerializeField] private bool enableLook = true;

    [Tooltip("Sensibilidad del ratón / stick.")]
    [SerializeField, Min(0f)] private float lookSensitivity = 0.1f;

    [Tooltip("Multiplicador extra cuando se usa mando (stick derecho).")]
    [SerializeField, Min(0f)] private float gamepadLookMultiplier = 12f;

    [Tooltip("Invertir el eje vertical de la cámara.")]
    [SerializeField] private bool invertY = false;

    [Tooltip("Límite de ángulo vertical de la cámara (grados).")]
    [SerializeField, Range(0f, 90f)] private float maxPitch = 88f;

    [Tooltip("Bloquear y ocultar el cursor al iniciar.")]
    [SerializeField] private bool lockCursor = true;

    [Tooltip("Suavizado (en segundos, aprox. lo que tarda en asentarse) del giro de cámara cuando algo " +
             "externo (como un diálogo con un NPC) toma el control de la mirada con 'LookOverrideTarget'. " +
             "Más alto = giro más lento y suave; más bajo = más rápido/directo.")]
    [SerializeField, Min(0.01f)] private float lookOverrideSmoothTime = 0.25f;

    [Header("DETECCIÓN DE SUELO")]
    [Tooltip("Radio de la esfera de comprobación de suelo.")]
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.3f;

    [Tooltip("Distancia hacia abajo desde el punto de comprobación.")]
    [SerializeField, Min(0f)] private float groundCheckOffset = 0.08f;

    [Tooltip("Capas consideradas 'suelo' para la comprobación por esfera (extra al CharacterController). " +
             "Déjalo en 'Nothing' para usar solo el CharacterController.")]
    [SerializeField] private LayerMask groundMask = 0;

    [Header("ENTRADA (INPUT SYSTEM)")]
    [Tooltip("Asset de acciones a usar. Si se deja vacío se usan las acciones del proyecto.")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActions;
#else
    [SerializeField] private Object inputActions;
#endif

    [Tooltip("Nombre del mapa de acciones.")]
    [SerializeField] private string actionMap = "Player";

    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string crouchActionName = "Crouch";

    [Header("ESTADO (solo lectura)")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isCrouching;
    [SerializeField] private bool isSprinting;
    [SerializeField] private Vector3 currentVelocity;

    public bool IsGrounded => isGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsSprinting => isSprinting;
    public Vector3 Velocity => currentVelocity;
    public Vector2 LookDelta { get; private set; }

    public float SensitivityMultiplier { get; set; } = 1f;

    public Vector3 CameraShakeOffset { get; set; }

    public Vector3? LookOverrideTarget { get; set; }

    public bool MovementLocked { get; set; }

    private CharacterController controller;
    private float pitch;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private int jumpsRemaining;
    private float targetHeight;
    private float lookOverrideYawVelocity;
    private float lookOverridePitchVelocity;
    private float defaultCenterY;
    private float eyeHeightRatio;
    private bool sprintToggled;
    private bool crouchToggled;

#if ENABLE_INPUT_SYSTEM
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
#endif

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        standingHeight = Mathf.Max(standingHeight, controller.radius * 2f);
        crouchHeight = Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight);
        targetHeight = standingHeight;
        defaultCenterY = controller.center.y;

        eyeHeightRatio = (cameraTransform != null && standingHeight > 0f)
            ? cameraTransform.localPosition.y / standingHeight
            : 0.5f;

        controller.height = standingHeight;
        jumpsRemaining = maxJumps;

        ResolveInputActions();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        crouchAction?.Enable();
#endif
    }

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (cameraTransform != null)
            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
    }

    private void Update()
    {
        ReadGround();
        HandleLook();
        HandleCrouch();
        HandleMovement();
        HandleJumpAndGravity();

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        currentVelocity = velocity;
    }

    private void ReadGround()
    {
        Vector3 origin = groundCheck != null
            ? groundCheck.position
            : transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);

        origin += Vector3.down * groundCheckOffset;

        bool sphereHit = groundMask.value != 0 &&
                         Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        isGrounded = controller.isGrounded || sphereHit;

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            jumpsRemaining = maxJumps;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }
    }

    private void HandleLook()
    {
        if (!enableLook || cameraTransform == null)
        {
            LookDelta = Vector2.zero;
            return;
        }

        if (LookOverrideTarget.HasValue)
        {
            LookDelta = Vector2.zero;
            AimLookAt(LookOverrideTarget.Value);
            return;
        }

        Vector2 look = ReadLook();
        LookDelta = look;

        if (look.sqrMagnitude > 0f)
        {
            float effectiveSensitivity = lookSensitivity * SensitivityMultiplier;
            float yaw = look.x * effectiveSensitivity;
            float pitchDelta = look.y * effectiveSensitivity * (invertY ? 1f : -1f);

            transform.Rotate(Vector3.up * yaw, Space.Self);
            pitch = Mathf.Clamp(pitch + pitchDelta, -maxPitch, maxPitch);
        }

        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(CameraShakeOffset);
    }

    private void AimLookAt(Vector3 worldPoint)
    {
        Vector3 toTarget = worldPoint - cameraTransform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        Vector3 desiredEuler = desiredRotation.eulerAngles;

        float desiredYaw = desiredEuler.y;
        float desiredPitch = Mathf.Clamp(NormalizeAngle(desiredEuler.x), -maxPitch, maxPitch);

        float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, desiredYaw, ref lookOverrideYawVelocity, lookOverrideSmoothTime);
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

        pitch = Mathf.SmoothDampAngle(pitch, desiredPitch, ref lookOverridePitchVelocity, lookOverrideSmoothTime);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(CameraShakeOffset);
    }

    private void HandleCrouch()
    {
        if (enableCrouch && !MovementLocked)
        {
            bool wantsCrouch = ReadCrouch();

            if (!wantsCrouch && isCrouching && !CanStandUp())
                wantsCrouch = true;

            isCrouching = wantsCrouch;
        }
        else if (!enableCrouch)
        {
            isCrouching = false;
        }

        targetHeight = isCrouching ? crouchHeight : standingHeight;

        float newHeight = Mathf.MoveTowards(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
        if (!Mathf.Approximately(newHeight, controller.height))
        {
            controller.height = newHeight;
            controller.center = new Vector3(
                controller.center.x,
                defaultCenterY + (newHeight - standingHeight) * 0.5f,
                controller.center.z);
        }

        if (lowerCameraOnCrouch && cameraTransform != null)
        {
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = eyeHeightRatio * controller.height;
            cameraTransform.localPosition = camPos;
        }
    }

    private bool CanStandUp()
    {
        float radius = controller.radius * 0.95f;
        Vector3 start = transform.position + controller.center + Vector3.up * (controller.height * 0.5f - radius);
        float castDistance = (standingHeight - controller.height) + 0.05f;
        return !Physics.SphereCast(start, radius, Vector3.up, out _, castDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void HandleMovement()
    {
        Vector2 move = MovementLocked ? Vector2.zero : ReadMove();
        Vector3 wishDir = (transform.right * move.x + transform.forward * move.y);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        isSprinting = enableSprint && !isCrouching && ReadSprint() && move.y > 0.1f;

        float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        Vector3 targetVelocity = wishDir * targetSpeed;

        bool hasInput = move.sqrMagnitude > 0.01f;
        float rate = hasInput ? acceleration : deceleration;
        if (!isGrounded) rate *= airControl;

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);
    }

    private void HandleJumpAndGravity()
    {
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (enableJump && !MovementLocked)
        {
            if (ReadJumpPressed())
                jumpBufferCounter = jumpBuffer;
            else
                jumpBufferCounter -= Time.deltaTime;

            bool canGroundJump = coyoteCounter > 0f;
            bool canAirJump = !canGroundJump && jumpsRemaining > 0 && maxJumps > 1;

            if (jumpBufferCounter > 0f && (canGroundJump || canAirJump))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
                jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        verticalVelocity = Mathf.Max(verticalVelocity, -terminalVelocity);
    }

    private void ResolveInputActions()
    {
#if ENABLE_INPUT_SYSTEM
        InputActionAsset asset = inputActions != null ? inputActions : InputSystem.actions;
        if (asset == null)
        {
            Debug.LogError("[PlayerController] No hay un InputActionAsset asignado ni acciones de proyecto configuradas.", this);
            return;
        }

        InputActionMap map = asset.FindActionMap(actionMap, false);
        moveAction = FindAction(asset, map, moveActionName);
        lookAction = FindAction(asset, map, lookActionName);
        jumpAction = FindAction(asset, map, jumpActionName);
        sprintAction = FindAction(asset, map, sprintActionName);
        crouchAction = FindAction(asset, map, crouchActionName);
#else
        Debug.LogError("[PlayerController] El paquete Input System no está disponible.", this);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static InputAction FindAction(InputActionAsset asset, InputActionMap map, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        InputAction action = map != null ? map.FindAction(name, false) : null;
        return action ?? asset.FindAction(name, false);
    }

    private Vector2 ReadMove() => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

    private Vector2 ReadLook()
    {
        if (lookAction == null) return Vector2.zero;
        Vector2 value = lookAction.ReadValue<Vector2>();

        bool fromGamepad = lookAction.activeControl != null &&
                           lookAction.activeControl.device is Gamepad;
        if (fromGamepad)
            value *= gamepadLookMultiplier * Time.deltaTime * 100f;

        return value;
    }

    private bool ReadJumpPressed() => jumpAction != null && jumpAction.WasPressedThisFrame();

    private bool ReadSprint()
    {
        if (sprintAction == null) return false;
        if (sprintIsHold) return sprintAction.IsPressed();
        if (sprintAction.WasPressedThisFrame()) sprintToggled = !sprintToggled;
        return sprintToggled;
    }

    private bool ReadCrouch()
    {
        if (crouchAction == null) return false;
        if (crouchIsHold) return crouchAction.IsPressed();
        if (crouchAction.WasPressedThisFrame()) crouchToggled = !crouchToggled;
        return crouchToggled;
    }
#else
    private Vector2 ReadMove() => Vector2.zero;
    private Vector2 ReadLook() => Vector2.zero;
    private bool ReadJumpPressed() => false;
    private bool ReadSprint() => false;
    private bool ReadCrouch() => false;
#endif

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    private void OnValidate()
    {
        if (crouchHeight > standingHeight) crouchHeight = standingHeight;
        if (sprintSpeed < walkSpeed) sprintSpeed = walkSpeed;
        if (maxJumps < 1) maxJumps = 1;
    }

    private void OnDrawGizmosSelected()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null) return;

        Vector3 origin = groundCheck != null
            ? groundCheck.position
            : transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
        origin += Vector3.down * groundCheckOffset;

        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
    }
}
