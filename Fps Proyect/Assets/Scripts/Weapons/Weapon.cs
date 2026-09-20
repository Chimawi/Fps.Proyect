using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("Player/Weapon")]
public class Weapon : MonoBehaviour
{
    [Header("REFERENCIAS")]
    [Tooltip("Controlador del jugador. Si se deja vacío se busca automáticamente en los padres.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Cámara del jugador. Si se deja vacío se busca automáticamente en los padres.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Punto desde donde nacen los disparos, en el mundo. IMPORTANTE: para que se vea bien " +
             "incluso girando rápido, este punto debe ser fijo respecto a la cámara (no un hijo del " +
             "arma que se balancea) — así el proyectil nunca nace 'flotando' lejos del cañón. " +
             "Si se deja vacío, se usa este mismo objeto (el arma), que sí se balancea.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("Prefab del proyectil a disparar. Debe tener el componente LaserProjectile.")]
    [SerializeField] private LaserProjectile projectilePrefab;

    [Header("PUNTO DE MIRA (RECOMENDADO)")]
    [Tooltip("Transform HIJO DEL ARMA ubicado exactamente sobre la mira/muesca del modelo 3D " +
             "(la parte que el jugador debería ver centrada en pantalla al apuntar). " +
             "Si se asigna, el arma se alinea SOLA para que ese punto quede centrado — así no hay " +
             "que adivinar números de posición a mano. Para ubicarlo: creá un hijo vacío del arma, " +
             "movelo en la Scene view hasta que quede justo sobre la mira del modelo, y arrastralo acá.")]
    [SerializeField] private Transform ironSight;

    [Tooltip("A qué distancia de la cámara (eje adelante) queda la mira una vez centrada.")]
    [SerializeField, Min(0.01f)] private float ironSightDistance = 0.25f;

    [Header("POSE DE APUNTADO (MANUAL)")]
    [Tooltip("Se usa solo si 'Iron Sight' está vacío. Posición local del arma (relativa a la cámara) mientras se apunta.")]
    [SerializeField] private Vector3 aimLocalPosition = new Vector3(0f, -0.05f, 0.28f);

    [Tooltip("Rotación local del arma (en grados) mientras se apunta.")]
    [SerializeField] private Vector3 aimLocalRotationEuler = Vector3.zero;

    [Tooltip("Qué tan rápido pasa de la pose normal a la de apuntado, y viceversa.")]
    [SerializeField, Min(0.1f)] private float aimTransitionSpeed = 10f;

    [Header("ESTABILIDAD AL APUNTAR")]
    [Tooltip("Multiplicador del balanceo por mirar (mouse) mientras se apunta. 1 = igual que sin apuntar, 0 = totalmente estable.")]
    [SerializeField, Range(0f, 1f)] private float aimSwayMultiplier = 0.25f;

    [Tooltip("Multiplicador del rebote al caminar/correr mientras se apunta.")]
    [SerializeField, Range(0f, 1f)] private float aimBobMultiplier = 0.4f;

    [Header("ZOOM (FOV)")]
    [Tooltip("Si está activo, la cámara reduce su campo de visión al apuntar (efecto de zoom).")]
    [SerializeField] private bool enableZoom = true;

    [Tooltip("Campo de visión (FOV) mientras se apunta. El valor normal se toma automáticamente de la cámara al iniciar.")]
    [SerializeField, Range(1f, 100f)] private float aimFieldOfView = 45f;

    [Header("SENSIBILIDAD AL APUNTAR")]
    [Tooltip("Multiplicador de la sensibilidad del mouse/stick al apuntar. 1 = igual que sin apuntar, valores bajos = cámara más lenta y precisa.")]
    [SerializeField, Range(0.05f, 1f)] private float aimSensitivityMultiplier = 0.5f;

    [Header("MIRILLA")]
    [Tooltip("Objeto de UI (la cruz/punto en pantalla) que se activa al apuntar.")]
    [SerializeField] private GameObject crosshairObject;

    [Tooltip("Si está activo, la mirilla solo se ve mientras se apunta. Si se desactiva, queda siempre visible.")]
    [SerializeField] private bool showCrosshairOnlyWhileAiming = true;

    [Header("DISPARO")]
    [Tooltip("A qué distancia de la cámara (hacia adelante) nace el disparo mientras se está apuntando " +
             "-- así sale desde el centro de la mirilla en vez de desde el cañón.")]
    [SerializeField, Min(0f)] private float aimSpawnDistance = 0.8f;

    [Tooltip("Ajuste vertical del punto de nacimiento al apuntar (negativo = más abajo). " +
             "La cámara está a la altura de los ojos, pero el cañón del arma queda más abajo -- " +
             "esto corrige que el disparo se vea saliendo 'desde arriba'.")]
    [SerializeField] private float aimSpawnVerticalOffset = -0.15f;

    [Tooltip("Tiempo mínimo entre disparos, en segundos.")]
    [SerializeField, Min(0.05f)] private float fireInterval = 0.7f;

    [Tooltip("Si está activo, mantener apretado el botón sigue disparando (respetando 'Fire Interval'); si no, hay que soltar y volver a apretar para cada disparo.")]
    [SerializeField] private bool automatic = true;

    [Header("POSE DE REPOSO (BALANCEO)")]
    [Tooltip("Si está activo, usa la posición/rotación local que el arma tiene ahora mismo en la escena como pose base.")]
    [SerializeField] private bool useCurrentTransformAsHome = true;

    [Tooltip("Posición local base (relativa a la cámara) si 'useCurrentTransformAsHome' está desactivado.")]
    [SerializeField] private Vector3 homePositionOverride;

    [Tooltip("Rotación local base en grados si 'useCurrentTransformAsHome' está desactivado.")]
    [SerializeField] private Vector3 homeRotationOverride;

    [Header("BALANCEO POR MIRAR (SWAY)")]
    [Tooltip("El arma se retrasa un poco al mover el mouse, como si tuviera peso en la mano.")]
    [SerializeField] private bool enableLookSway = true;

    [Tooltip("Cuánto se desplaza el arma por cada pixel de movimiento del mouse.")]
    [SerializeField, Min(0f)] private float lookSwayPositionAmount = 0.0015f;

    [Tooltip("Desplazamiento máximo permitido (metros).")]
    [SerializeField, Min(0f)] private float maxLookSwayPosition = 0.05f;

    [Tooltip("Cuánto rota el arma por cada pixel de movimiento del mouse (grados).")]
    [SerializeField, Min(0f)] private float lookSwayRotationAmount = 0.06f;

    [Tooltip("Rotación máxima permitida (grados).")]
    [SerializeField, Min(0f)] private float maxLookSwayRotation = 8f;

    [Tooltip("Qué tan rápido el arma 'alcanza' al movimiento del mouse. Más alto = más rígida/rápida.")]
    [SerializeField, Min(0.1f)] private float lookSwaySmoothing = 8f;

    [Tooltip("Invertir la dirección del balanceo (gusto personal).")]
    [SerializeField] private bool invertLookSway = false;

    [Header("BALANCEO AL CAMINAR (BOB)")]
    [SerializeField] private bool enableWalkBob = true;

    [Tooltip("Velocidad del ciclo de rebote en función de la velocidad del jugador.")]
    [SerializeField, Min(0f)] private float bobFrequency = 1.6f;

    [Tooltip("Desplazamiento del rebote (metros).")]
    [SerializeField, Min(0f)] private float bobPositionAmount = 0.015f;

    [Tooltip("Rotación (inclinación) del rebote (grados).")]
    [SerializeField, Min(0f)] private float bobRotationAmount = 1.5f;

    [Tooltip("Suavizado de entrada/salida del rebote al empezar o dejar de moverse.")]
    [SerializeField, Min(0.1f)] private float bobSmoothing = 10f;

    [Tooltip("Multiplicador del rebote mientras se corre.")]
    [SerializeField, Min(0f)] private float sprintBobMultiplier = 1.6f;

    [Tooltip("Multiplicador del rebote mientras se está agachado.")]
    [SerializeField, Min(0f)] private float crouchBobMultiplier = 0.5f;

    [Header("RESPIRACIÓN (IDLE)")]
    [Tooltip("Pequeño movimiento continuo cuando el jugador está quieto, para que el arma no se sienta 'muerta'.")]
    [SerializeField] private bool enableIdleSway = true;

    [SerializeField, Min(0f)] private float idleSwaySpeed = 1f;
    [SerializeField, Min(0f)] private float idleSwayAmount = 0.008f;

    [Header("RETROCESO AL DISPARAR (RECOIL)")]
    [Tooltip("Si está activo, cada disparo empuja levemente el arma.")]
    [SerializeField] private bool enableRecoil = true;

    [Tooltip("Cuánto se mueve el arma por disparo (metros). Z negativo = hacia atrás.")]
    [SerializeField] private Vector3 recoilPositionKick = new Vector3(0f, 0.01f, -0.04f);

    [Tooltip("Cuánto rota el arma por disparo (grados). X negativo = la punta sube.")]
    [SerializeField] private Vector3 recoilRotationKick = new Vector3(-3f, 0f, 0f);

    [Tooltip("Qué tan rápido vuelve el arma a su posición después del retroceso. Más alto = recupera más rápido.")]
    [SerializeField, Min(0.1f)] private float recoilRecoverySpeed = 10f;

    [Tooltip("Variación aleatoria del retroceso por disparo (0 = siempre igual, 1 = muy variable).")]
    [SerializeField, Range(0f, 1f)] private float recoilRandomness = 0.2f;

    [Header("SONIDO DE DISPARO")]
    [Tooltip("Si se asigna, se reproduce este sonido en vez del generado automáticamente.")]
    [SerializeField] private AudioClip customClip;

    [Tooltip("Duración del sonido generado, en segundos.")]
    [SerializeField, Range(0.02f, 1f)] private float soundDuration = 0.1f;

    [Tooltip("Frecuencia inicial del tono (Hz).")]
    [SerializeField, Min(20f)] private float startFrequency = 1800f;

    [Tooltip("Frecuencia final del tono (Hz). Menor que la inicial = sonido descendente típico de láser.")]
    [SerializeField, Min(20f)] private float endFrequency = 200f;

    [Tooltip("Qué tan rápido cae la frecuencia: con la curva exponencial (recomendado) el tono " +
             "se desploma en el primer instante y luego se estabiliza, dando el clásico 'pew' de láser.")]
    [SerializeField] private bool exponentialSweep = true;

    [Tooltip("Mezcla de onda cuadrada sobre la onda senoidal: le agrega un 'zumbido' áspero. 0 = limpio, 1 = muy áspero.")]
    [SerializeField, Range(0f, 1f)] private float harshness = 0.25f;

    [Tooltip("Velocidad del vibrato (Hz) que se le agrega al tono, para que 'tiemble' como un rayo de energía.")]
    [SerializeField, Range(0f, 150f)] private float vibratoSpeed = 55f;

    [Tooltip("Profundidad del vibrato (Hz). 0 = sin vibrato.")]
    [SerializeField, Range(0f, 300f)] private float vibratoDepth = 60f;

    [Tooltip("Qué tan rápido decae el volumen del disparo. Más alto = 'pew' más corto y percusivo.")]
    [SerializeField, Range(0.5f, 6f)] private float envelopeSharpness = 3f;

    [Tooltip("Volumen del disparo.")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.6f;

    [Tooltip("Variación aleatoria de tono por disparo, para que no suenen todos idénticos.")]
    [SerializeField, Range(0f, 0.5f)] private float pitchRandomness = 0.05f;

    [Header("ENTRADA (INPUT SYSTEM)")]
    [Tooltip("Asset de acciones a usar. Si se deja vacío se usan las acciones del proyecto.")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActions;
#else
    [SerializeField] private Object inputActions;
#endif

    [SerializeField] private string actionMap = "Player";
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string aimActionName = "Aim";

    public bool IsAiming { get; private set; }
    public float AimAmount { get; private set; }

    private float defaultFieldOfView = 60f;
    private float nextFireTime;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 lookSwayPos;
    private Vector3 lookSwayRot;
    private Vector3 bobPos;
    private Vector3 bobRot;
    private Vector3 idlePos;
    private Vector3 recoilPosOffset;
    private Vector3 recoilRotOffset;
    private float bobCycle;

    private AudioSource audioSource;
    private AudioClip generatedClip;

#if ENABLE_INPUT_SYSTEM
    private InputAction aimAction;
    private InputAction attackAction;
#endif

    private Vector3 AimLocalPosition
    {
        get
        {
            if (ironSight == null) return aimLocalPosition;

            Vector3 desiredSightPosition = new Vector3(0f, 0f, ironSightDistance);
            return desiredSightPosition - ironSight.localPosition;
        }
    }

    private Quaternion AimLocalRotation => Quaternion.Euler(aimLocalRotationEuler);

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerController>();
        if (playerCamera == null)
            playerCamera = GetComponentInParent<Camera>();
        if (playerCamera != null)
            defaultFieldOfView = playerCamera.fieldOfView;

        if (crosshairObject != null)
            crosshairObject.SetActive(!showCrosshairOnlyWhileAiming);

        homePosition = useCurrentTransformAsHome ? transform.localPosition : homePositionOverride;
        homeRotation = useCurrentTransformAsHome ? transform.localRotation : Quaternion.Euler(homeRotationOverride);

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (customClip == null)
            generatedClip = GenerateLaserClip();

        ResolveInputActions();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        aimAction?.Enable();
        attackAction?.Enable();
#endif
    }

    private void OnDisable()
    {
        if (playerCamera != null)
            playerCamera.fieldOfView = defaultFieldOfView;
        if (player != null)
            player.SensitivityMultiplier = 1f;
    }

    private void Update()
    {
        UpdateAim();

        if (Time.time >= nextFireTime && WantsToFire())
        {
            Fire();
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void UpdateAim()
    {
        IsAiming = ReadAimHeld();

        float target = IsAiming ? 1f : 0f;
        AimAmount = Mathf.MoveTowards(AimAmount, target, aimTransitionSpeed * Time.deltaTime);

        if (crosshairObject != null && showCrosshairOnlyWhileAiming)
            crosshairObject.SetActive(IsAiming || AimAmount > 0.01f);

        if (enableZoom && playerCamera != null)
            playerCamera.fieldOfView = Mathf.Lerp(defaultFieldOfView, aimFieldOfView, AimAmount);

        if (player != null)
            player.SensitivityMultiplier = Mathf.Lerp(1f, aimSensitivityMultiplier, AimAmount);
    }

    private void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Weapon] No hay un prefab de proyectil asignado.", this);
            return;
        }

        bool guidedByReticle = IsAiming && playerCamera != null;

        Vector3 spawnPosition;
        Quaternion shotRotation;
        if (guidedByReticle)
        {
            Transform cameraTransform = playerCamera.transform;
            spawnPosition = cameraTransform.position
                + cameraTransform.forward * aimSpawnDistance
                + cameraTransform.up * aimSpawnVerticalOffset;
            shotRotation = Quaternion.LookRotation(cameraTransform.forward);
        }
        else
        {
            spawnPosition = muzzlePoint != null ? muzzlePoint.position : transform.position;
            shotRotation = transform.rotation;
        }

        LaserProjectile bolt = Instantiate(projectilePrefab, spawnPosition, shotRotation);
        bolt.Launch(player != null ? player.transform : null);

        Kick();
        PlayFireSound();
    }

    private void LateUpdate()
    {
        UpdateLookSway();
        UpdateWalkBob();
        UpdateIdleSway();
        UpdateRecoil();

        Vector3 basePosition = Vector3.Lerp(homePosition, AimLocalPosition, AimAmount);
        Quaternion baseRotation = Quaternion.Slerp(homeRotation, AimLocalRotation, AimAmount);
        float swayMultiplier = Mathf.Lerp(1f, aimSwayMultiplier, AimAmount);
        float bobMultiplier = Mathf.Lerp(1f, aimBobMultiplier, AimAmount);

        transform.localPosition = basePosition + lookSwayPos * swayMultiplier + bobPos * bobMultiplier + idlePos * swayMultiplier + recoilPosOffset;
        transform.localRotation = baseRotation * Quaternion.Euler(lookSwayRot * swayMultiplier + bobRot * bobMultiplier + recoilRotOffset);
    }

    private void Kick()
    {
        if (!enableRecoil) return;

        float randomFactor = 1f + Random.Range(-recoilRandomness, recoilRandomness);
        recoilPosOffset += recoilPositionKick * randomFactor;
        recoilRotOffset += recoilRotationKick * randomFactor;
    }

    private void UpdateRecoil()
    {
        recoilPosOffset = Vector3.Lerp(recoilPosOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
        recoilRotOffset = Vector3.Lerp(recoilRotOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
    }

    private void UpdateLookSway()
    {
        Vector3 targetPos = Vector3.zero;
        Vector3 targetRot = Vector3.zero;

        if (enableLookSway && player != null)
        {
            Vector2 look = player.LookDelta;
            float sign = invertLookSway ? 1f : -1f;

            targetPos = new Vector3(sign * look.x, sign * look.y, 0f) * lookSwayPositionAmount;
            targetPos = Vector3.ClampMagnitude(targetPos, maxLookSwayPosition);

            targetRot = new Vector3(-sign * look.y, sign * look.x, sign * look.x) * lookSwayRotationAmount;
            targetRot = Vector3.ClampMagnitude(targetRot, maxLookSwayRotation);
        }

        lookSwayPos = Vector3.Lerp(lookSwayPos, targetPos, lookSwaySmoothing * Time.deltaTime);
        lookSwayRot = Vector3.Lerp(lookSwayRot, targetRot, lookSwaySmoothing * Time.deltaTime);
    }

    private void UpdateWalkBob()
    {
        Vector3 targetPos = Vector3.zero;
        Vector3 targetRot = Vector3.zero;

        if (enableWalkBob && player != null && player.IsGrounded)
        {
            Vector3 flatVelocity = player.Velocity;
            flatVelocity.y = 0f;
            float speed = flatVelocity.magnitude;

            if (speed > 0.1f)
            {
                float multiplier = player.IsCrouching ? crouchBobMultiplier : (player.IsSprinting ? sprintBobMultiplier : 1f);
                bobCycle += speed * bobFrequency * Time.deltaTime;

                targetPos = new Vector3(
                    Mathf.Cos(bobCycle) * bobPositionAmount * multiplier,
                    Mathf.Abs(Mathf.Sin(bobCycle)) * bobPositionAmount * multiplier,
                    0f);

                targetRot = new Vector3(0f, 0f, Mathf.Cos(bobCycle) * bobRotationAmount * multiplier);
            }
        }

        bobPos = Vector3.Lerp(bobPos, targetPos, bobSmoothing * Time.deltaTime);
        bobRot = Vector3.Lerp(bobRot, targetRot, bobSmoothing * Time.deltaTime);
    }

    private void UpdateIdleSway()
    {
        if (!enableIdleSway)
        {
            idlePos = Vector3.zero;
            return;
        }

        float t = Time.time * idleSwaySpeed;
        idlePos = new Vector3(Mathf.Sin(t), Mathf.Sin(t * 0.6f), 0f) * idleSwayAmount;
    }

    private void PlayFireSound()
    {
        AudioClip clip = customClip != null ? customClip : generatedClip;
        if (clip == null || audioSource == null) return;

        audioSource.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
        audioSource.PlayOneShot(clip, soundVolume);
    }

    private AudioClip GenerateLaserClip()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        float[] samples = ProceduralAudio.AllocateSamples(soundDuration, sampleRate);
        int sampleCount = samples.Length;

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;

            float baseFrequency = exponentialSweep
                ? startFrequency * Mathf.Pow(endFrequency / startFrequency, t)
                : Mathf.Lerp(startFrequency, endFrequency, t);

            float vibrato = Mathf.Sin(2f * Mathf.PI * vibratoSpeed * (i / (float)sampleRate)) * vibratoDepth;

            float frequency = Mathf.Max(20f, baseFrequency + vibrato);
            phase += frequency * (1f / sampleRate) * Mathf.PI * 2f;

            float sine = Mathf.Sin(phase);
            float square = Mathf.Sign(sine);
            float waveform = Mathf.Lerp(sine, square, harshness);

            float envelope = Mathf.Pow(1f - t, envelopeSharpness);

            samples[i] = waveform * envelope;
        }

        return ProceduralAudio.CreateClip("LaserShot (generado)", samples, sampleRate);
    }

    private void ResolveInputActions()
    {
#if ENABLE_INPUT_SYSTEM
        aimAction = InputActionResolver.Resolve(inputActions, actionMap, aimActionName, this, "Weapon");
        attackAction = InputActionResolver.Resolve(inputActions, actionMap, attackActionName, this, "Weapon");
#endif
    }

    private bool ReadAimHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return aimAction != null && aimAction.IsPressed();
#else
        return false;
#endif
    }

    private bool WantsToFire()
    {
#if ENABLE_INPUT_SYSTEM
        if (attackAction == null) return false;
        return automatic ? attackAction.IsPressed() : attackAction.WasPressedThisFrame();
#else
        return false;
#endif
    }
}
