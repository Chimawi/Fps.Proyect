using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[AddComponentMenu("Player/NPC Dialogue")]
public class NpcDialogue : MonoBehaviour
{
    [Header("REFERENCIAS")]
    [Tooltip("Transform del jugador. Si se deja vacío se busca automáticamente en la escena.")]
    [SerializeField] private Transform player;

    [Tooltip("Controlador del jugador (para fijar la cámara en el NPC). Si se deja vacío se busca automáticamente.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Cámara del jugador (para el zoom). Si se deja vacío se busca automáticamente.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("El objeto raíz del arma. Se oculta mientras dura el diálogo (así no molesta en el zoom y no se puede disparar). Si se deja vacío se busca automáticamente.")]
    [SerializeField] private GameObject weaponObject;

    [Tooltip("Panel del cuadro de diálogo (el contenedor que se activa/desactiva).")]
    [SerializeField] private GameObject dialogueBox;

    [Tooltip("Texto dentro del cuadro de diálogo.")]
    [SerializeField] private Text dialogueText;

    [Tooltip("Cartel de 'Presioná E para hablar' (opcional). Se muestra al estar cerca, entre interacciones.")]
    [SerializeField] private GameObject interactPrompt;

    [Header("CÁMARA FIJA EN EL NPC (DURANTE EL DIÁLOGO)")]
    [Tooltip("Si está activo, mientras el cuadro de diálogo esté abierto (presentación o charla con E) " +
             "la cámara deja de responder al mouse y queda fija mirando al NPC, y el jugador no se puede mover.")]
    [SerializeField] private bool lockCameraOnNpc = true;

    [Tooltip("Transform exacto al que apunta la cámara (por ejemplo el hueso 'Head' del robot, para mirarlo " +
             "a la cara con precisión). Si se asigna, tiene prioridad sobre el offset de abajo.")]
    [SerializeField] private Transform lookAtPoint;

    [Tooltip("Se usa solo si 'Look At Point' está vacío: punto a mirar, relativo a este objeto.")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);

    [Header("EL NPC GIRA HACIA EL JUGADOR")]
    [Tooltip("Si está activo, el NPC gira (en el eje Y) para quedar de frente al jugador mientras dura el " +
             "diálogo, y vuelve solo a su rotación original al terminar.")]
    [SerializeField] private bool faceTowardsPlayer = true;

    [Tooltip("Suavizado del giro del NPC (segundos aprox. para asentarse).")]
    [SerializeField, Min(0.01f)] private float faceTurnSmoothTime = 0.3f;

    [Header("DETECCIÓN")]
    [Tooltip("Radio dentro del cual el NPC reacciona a la presencia del jugador.")]
    [SerializeField, Min(0f)] private float detectionRadius = 4f;

    [Tooltip("Cada cuánto se revisa la distancia al jugador (segundos, no hace falta que sea cada frame).")]
    [SerializeField, Min(0.05f)] private float checkInterval = 0.2f;

    [Header("PRESENTACIÓN (SOLO LA PRIMERA VEZ)")]
    [Tooltip("Texto que aparece automáticamente la primera vez que te acercás. Totalmente personalizable.")]
    [TextArea(2, 5)]
    [SerializeField]
    private string introText = "Hola, soy Kyle. No te había visto por acá.";

    [Tooltip("Cuánto tiempo queda abierto el cuadro de presentación antes de cerrarse solo.")]
    [SerializeField, Min(0.5f)] private float introDisplayDuration = 4f;

    [Header("ZOOM DE CÁMARA (SOLO LA PRIMERA VEZ)")]
    [SerializeField] private bool enableIntroZoom = true;

    [Tooltip("Campo de visión de la cámara durante el zoom de presentación.")]
    [SerializeField, Range(1f, 100f)] private float zoomFieldOfView = 40f;

    [Tooltip("Qué tan rápido entra y sale del zoom.")]
    [SerializeField, Min(0.1f)] private float zoomSpeed = 4f;

    [Header("DIÁLOGO (APRETANDO E)")]
    [Tooltip("Líneas de la primera conversación. Cada pulsación de E avanza a la siguiente; al llegar al final, se cierra. Personalizable.")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] dialogueLines =
    {
        "Este sector todavía está en construcción.",
        "Si escuchás disparos, probablemente sea yo probando el escáner.",
        "¡Suerte ahí afuera!"
    };

    [Header("SEGUNDA CONVERSACIÓN (DESPUÉS DE DESTRUIR LAS CAJAS)")]
    [Tooltip("Líneas que se muestran en vez de las de arriba una vez que se destruyen todas las cajas.")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] questCompleteDialogueLines =
    {
        "¡Buen trabajo destruyendo esas cajas!",
        "Con eso alcanza por ahora."
    };

    [Header("TERCERA CONVERSACIÓN (DESPUÉS DE EXPLOTAR EL BARRIL)")]
    [Tooltip("Líneas que se muestran una vez que explota el barril de recompensa.")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] masteryDialogueLines =
    {
        "Ya sabés lo básico."
    };

    [Header("MISIÓN: DESTRUIR CAJAS")]
    [Tooltip("Transform que marca dónde aparecen las cajas (por ejemplo, detrás de una baranda). " +
             "Las cajas se acomodan en cuadrícula relativa a su posición y rotación -- movelo/rotalo " +
             "en la Scene view si no caen bien ubicadas.")]
    [SerializeField] private Transform spawnArea;

    [Tooltip("Prefab de la caja a instanciar (debe tener BreakableCrate). Usá 'Crate Short'.")]
    [SerializeField] private BreakableCrate cratePrefab;

    [Tooltip("Panel de misiones en pantalla (arriba a la izquierda). Si se deja vacío, no se muestra ningún texto.")]
    [SerializeField] private QuestHUD questHud;

    [Tooltip("Texto del objetivo que se muestra en el panel de misiones (el contador \"(x/total)\" se agrega solo).")]
    [SerializeField] private string questLabel = "Destruye Las Cajas";

    [SerializeField, Range(1, 30)] private int crateCount = 10;

    [Tooltip("Cuántas cajas entran por fila antes de pasar a la siguiente.")]
    [SerializeField, Min(1)] private int columns = 5;

    [Tooltip("Separación entre cajas (metros).")]
    [SerializeField, Min(0.1f)] private float spacing = 1.2f;

    [Tooltip("Variación aleatoria de posición, para que no queden en una cuadrícula perfecta.")]
    [SerializeField, Range(0f, 1f)] private float positionJitter = 0.15f;

    [Header("MISIÓN: EXPLOTAR EL BARRIL (RECOMPENSA)")]
    [Tooltip("Barril explosivo que aparece la segunda vez que terminás de hablar con el robot (con la misión ya cumplida).")]
    [SerializeField] private ExplosiveBarrel barrelPrefab;

    [Tooltip("Cuántas cajas aparecen alrededor del barril de recompensa.")]
    [SerializeField, Range(0, 12)] private int rewardCrateCount = 4;

    [Tooltip("Distancia entre el barril y cada caja de alrededor.")]
    [SerializeField, Min(0.3f)] private float rewardCrateRadius = 1.3f;

    [Tooltip("Lo que dice el robot si le disparás a una de estas cajas en vez de al barril.")]
    [TextArea(2, 3)]
    [SerializeField] private string wrongTargetMessage = "¡No, dije que le dispararas al barril!";

    [Tooltip("Panel de misión (arriba a la derecha) para el objetivo de esta recompensa. Si se deja vacío, no se muestra ningún texto.")]
    [SerializeField] private QuestHUD barrelQuestHud;

    [Tooltip("Texto del objetivo del barril que se muestra en ese panel.")]
    [SerializeField] private string barrelQuestLabel = "Explota el barril";

    [Tooltip("Puerta (por ejemplo, LP_Bay_Door_snaps) que se abre al completar esta misión.")]
    [SerializeField] private SlidingDoor bayDoor;

    [Header("ENTRADA (INPUT SYSTEM)")]
    [Tooltip("Asset de acciones a usar. Si se deja vacío se usan las acciones del proyecto.")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActions;
#else
    [SerializeField] private Object inputActions;
#endif

    [SerializeField] private string actionMap = "Player";
    [SerializeField] private string interactActionName = "Interact";

    private bool hasIntroduced;
    private bool inRange;
    private bool showingDialogue;
    private bool isInterrupting;
    private int dialogueIndex;
    private int dialogueStage;
    private float introTimer;
    private float checkTimer;
    private float defaultFieldOfView = 60f;
    private float homeYaw;
    private float faceYawVelocity;

    private bool questStarted;
    private bool questCompleted;
    private bool rewardSpawned;
    private int cratesRemaining;
    private int cratesDestroyed;
    private ExplosiveBarrel rewardBarrelInstance;
    private readonly List<BreakableCrate> rewardCrates = new List<BreakableCrate>();

    public int CratesRemaining => cratesRemaining;
    public bool QuestCompleted => questCompleted;

#if ENABLE_INPUT_SYSTEM
    private InputAction interactAction;
#endif

    public void AdvanceToNextStage()
    {
        dialogueStage = Mathf.Min(dialogueStage + 1, 2);
    }

    public void Interrupt(string message, float duration = 2.5f)
    {
        if (dialogueBox != null && dialogueBox.activeSelf) return;

        showingDialogue = false;
        isInterrupting = true;
        introTimer = duration;
        SetText(message);
        if (dialogueBox != null) dialogueBox.SetActive(true);
        EnterDialogueState();
    }

    private void Awake()
    {
        homeYaw = transform.eulerAngles.y;

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (player == null && playerController != null)
            player = playerController.transform;

        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>();

        if (playerCamera != null)
            defaultFieldOfView = playerCamera.fieldOfView;

        if (weaponObject == null && player != null)
        {
            Weapon weapon = player.GetComponentInChildren<Weapon>(true);
            if (weapon != null) weaponObject = weapon.gameObject;
        }

        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (interactPrompt != null) interactPrompt.SetActive(false);

        if (bayDoor == null)
            bayDoor = FindFirstObjectByType<SlidingDoor>();

        ResolveInputAction();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        interactAction?.Enable();
#endif
    }

    private void OnDisable()
    {
        if (playerCamera != null)
            playerCamera.fieldOfView = defaultFieldOfView;
        if (playerController != null)
        {
            playerController.LookOverrideTarget = null;
            playerController.MovementLocked = false;
        }
        if (weaponObject != null)
            weaponObject.SetActive(true);
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = checkInterval;
            inRange = player != null && Vector3.Distance(transform.position, player.position) <= detectionRadius;
        }

        if (!hasIntroduced && inRange)
            StartIntro();

        if (dialogueBox != null && dialogueBox.activeSelf && !showingDialogue)
        {
            introTimer -= Time.deltaTime;
            bool leftRange = !isInterrupting && !inRange;
            if (introTimer <= 0f || leftRange)
                CloseBox();
        }

        bool interactPressed = ReadInteractPressed();

        if (hasIntroduced && inRange && !showingDialogue && !(dialogueBox != null && dialogueBox.activeSelf) && interactPressed)
            StartDialogue();
        else if (showingDialogue && interactPressed)
            AdvanceDialogue();

        if (showingDialogue && !inRange)
            CloseBox();

        UpdateCameraLock();
        UpdateFacing();
        UpdateZoom();
        UpdatePrompt();
    }

    private void UpdateCameraLock()
    {
        if (!lockCameraOnNpc || playerController == null) return;

        bool dialogueActive = dialogueBox != null && dialogueBox.activeSelf;
        playerController.LookOverrideTarget = dialogueActive ? GetLookAtWorldPosition() : (Vector3?)null;
    }

    private void UpdateFacing()
    {
        if (!faceTowardsPlayer) return;

        bool dialogueActive = dialogueBox != null && dialogueBox.activeSelf;
        float targetYaw = homeYaw;

        if (dialogueActive && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                targetYaw = Quaternion.LookRotation(toPlayer.normalized, Vector3.up).eulerAngles.y;
        }

        float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref faceYawVelocity, faceTurnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }

    private Vector3 GetLookAtWorldPosition()
    {
        return lookAtPoint != null ? lookAtPoint.position : transform.position + lookAtOffset;
    }

    private void StartIntro()
    {
        hasIntroduced = true;
        showingDialogue = false;
        introTimer = introDisplayDuration;
        SetText(introText);
        if (dialogueBox != null) dialogueBox.SetActive(true);
        EnterDialogueState();
    }

    private void StartDialogue()
    {
        showingDialogue = true;
        dialogueIndex = 0;

        string[] activeLines = GetActiveDialogueLines();
        SetText(activeLines != null && activeLines.Length > 0 ? activeLines[0] : string.Empty);

        if (dialogueBox != null) dialogueBox.SetActive(true);
        EnterDialogueState();
    }

    private string[] GetActiveDialogueLines()
    {
        if (dialogueStage >= 2) return masteryDialogueLines;
        return dialogueStage >= 1 ? questCompleteDialogueLines : dialogueLines;
    }

    private void EnterDialogueState()
    {
        if (playerController != null)
            playerController.MovementLocked = true;

        if (weaponObject != null)
            weaponObject.SetActive(false);
    }

    private void ExitDialogueState()
    {
        if (playerController != null)
            playerController.MovementLocked = false;

        if (weaponObject != null)
            weaponObject.SetActive(true);
    }

    private void AdvanceDialogue()
    {
        dialogueIndex++;
        string[] activeLines = GetActiveDialogueLines();
        if (activeLines == null || dialogueIndex >= activeLines.Length)
        {
            CloseBox();
            return;
        }
        SetText(activeLines[dialogueIndex]);
    }

    private void CloseBox()
    {
        bool wasRealConversation = showingDialogue;

        showingDialogue = false;
        isInterrupting = false;
        if (dialogueBox != null) dialogueBox.SetActive(false);
        ExitDialogueState();

        if (wasRealConversation)
            HandleConversationEnded();
    }

    private void SetText(string text)
    {
        if (dialogueText != null) dialogueText.text = text;
    }

    private void UpdateZoom()
    {
        if (playerCamera == null) return;

        bool zooming = enableIntroZoom && dialogueBox != null && dialogueBox.activeSelf && !showingDialogue;
        float target = zooming ? zoomFieldOfView : defaultFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, zoomSpeed * Time.deltaTime);
    }

    private void UpdatePrompt()
    {
        if (interactPrompt == null) return;

        bool boxOpen = dialogueBox != null && dialogueBox.activeSelf;
        interactPrompt.SetActive(hasIntroduced && inRange && !boxOpen);
    }

    private void ResolveInputAction()
    {
#if ENABLE_INPUT_SYSTEM
        interactAction = InputActionResolver.Resolve(inputActions, actionMap, interactActionName, this, "NpcDialogue");
#endif
    }

    private bool ReadInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return interactAction != null && interactAction.WasPressedThisFrame();
#else
        return false;
#endif
    }

    private void HandleConversationEnded()
    {
        if (!questStarted)
        {
            questStarted = true;
            SpawnCrates();

            if (questHud != null)
                questHud.ShowQuest(questLabel, crateCount);
            return;
        }

        if (questCompleted && !rewardSpawned)
        {
            rewardSpawned = true;
            SpawnRewardBarrel();
        }
    }

    private void SpawnCrates()
    {
        if (cratePrefab == null)
        {
            Debug.LogWarning("[NpcDialogue] No hay un prefab de caja asignado.", this);
            return;
        }
        if (spawnArea == null)
        {
            Debug.LogWarning("[NpcDialogue] No hay un 'Spawn Area' asignado.", this);
            return;
        }

        cratesRemaining = crateCount;
        cratesDestroyed = 0;

        for (int i = 0; i < crateCount; i++)
        {
            int row = i / columns;
            int col = i % columns;

            float centeredCol = col - (columns - 1) * 0.5f;
            Vector3 jitter = new Vector3(
                Random.Range(-positionJitter, positionJitter),
                0f,
                Random.Range(-positionJitter, positionJitter));

            Vector3 localOffset = new Vector3(centeredCol * spacing, 0f, row * spacing) + jitter;
            Vector3 worldPosition = spawnArea.TransformPoint(localOffset);

            BreakableCrate crate = Instantiate(cratePrefab, worldPosition, spawnArea.rotation);
            crate.OnBroken += HandleCrateBroken;
        }
    }

    private void HandleCrateBroken(BreakableCrate crate)
    {
        crate.OnBroken -= HandleCrateBroken;
        cratesRemaining = Mathf.Max(0, cratesRemaining - 1);
        cratesDestroyed = Mathf.Min(crateCount, cratesDestroyed + 1);

        if (questHud != null)
            questHud.UpdateProgress(cratesDestroyed);

        if (cratesRemaining == 0)
            CompleteCrateQuest();
    }

    private void SpawnRewardBarrel()
    {
        if (spawnArea == null) return;

        Vector3 center = spawnArea.position;

        if (barrelPrefab != null)
        {
            rewardBarrelInstance = Instantiate(barrelPrefab, center, Quaternion.identity);
            rewardBarrelInstance.OnExploded += HandleBarrelExploded;
        }

        rewardCrates.Clear();

        if (cratePrefab != null && rewardCrateCount > 0)
        {
            for (int i = 0; i < rewardCrateCount; i++)
            {
                float angle = (360f / rewardCrateCount) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rewardCrateRadius;
                BreakableCrate crate = Instantiate(cratePrefab, center + offset, Quaternion.identity);
                crate.OnBroken += HandleRewardCrateBroken;
                rewardCrates.Add(crate);
            }
        }

        if (barrelQuestHud != null)
            barrelQuestHud.ShowQuest(barrelQuestLabel, 1);
    }

    private void HandleRewardCrateBroken(BreakableCrate crate)
    {
        crate.OnBroken -= HandleRewardCrateBroken;
        rewardCrates.Remove(crate);

        bool barrelAlreadyExploded = rewardBarrelInstance != null && rewardBarrelInstance.HasExploded;
        if (barrelAlreadyExploded) return;

        Interrupt(wrongTargetMessage);
        ResetRewardSetup();
    }

    private void HandleBarrelExploded()
    {
        if (barrelQuestHud != null)
        {
            barrelQuestHud.UpdateProgress(1);
            barrelQuestHud.CompleteQuest();
        }

        AdvanceToNextStage();

        if (bayDoor != null)
            bayDoor.Open();
    }

    private void ResetRewardSetup()
    {
        foreach (BreakableCrate remaining in rewardCrates)
        {
            if (remaining == null) continue;
            remaining.OnBroken -= HandleRewardCrateBroken;
            Destroy(remaining.gameObject);
        }
        rewardCrates.Clear();

        if (rewardBarrelInstance != null)
        {
            rewardBarrelInstance.OnExploded -= HandleBarrelExploded;
            Destroy(rewardBarrelInstance.gameObject);
            rewardBarrelInstance = null;
        }

        SpawnRewardBarrel();
    }

    private void CompleteCrateQuest()
    {
        questCompleted = true;
        AdvanceToNextStage();

        if (questHud != null)
            questHud.CompleteQuest();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (spawnArea == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < crateCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float centeredCol = col - (columns - 1) * 0.5f;
            Vector3 localOffset = new Vector3(centeredCol * spacing, 0f, row * spacing);
            Gizmos.DrawWireCube(spawnArea.TransformPoint(localOffset), Vector3.one * 0.5f);
        }
    }
}
