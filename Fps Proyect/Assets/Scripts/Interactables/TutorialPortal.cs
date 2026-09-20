using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
[AddComponentMenu("Player/Tutorial Portal")]
public class TutorialPortal : MonoBehaviour
{
    [Tooltip("Controlador del jugador, para congelarlo mientras se muestra la pantalla. Si se deja vacío se busca automáticamente.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("El objeto raíz del arma. Se apaga al mostrar la pantalla para que no se pueda seguir disparando " +
             "(ni escuchar el sonido de disparo) con el juego 'terminado'. Si se deja vacío se busca automáticamente.")]
    [SerializeField] private GameObject weaponObject;

    [Tooltip("Panel de fondo negro con el mensaje y los botones (se activa al tocar el portal).")]
    [SerializeField] private GameObject completionPanel;

    [Tooltip("Botón 'Salir' -- cierra el juego.")]
    [SerializeField] private Button exitButton;

    [Tooltip("Botón 'Reintentar' -- recarga la escena actual desde el principio.")]
    [SerializeField] private Button retryButton;

    private bool triggered;

    private void Awake()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (weaponObject == null)
        {
            Weapon weapon = FindFirstObjectByType<Weapon>();
            if (weapon != null) weaponObject = weapon.gameObject;
        }

        if (completionPanel != null)
            completionPanel.SetActive(false);

        if (exitButton != null)
            exitButton.onClick.AddListener(Exit);
        if (retryButton != null)
            retryButton.onClick.AddListener(Retry);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TutorialPortal] OnTriggerEnter con '{other.name}'.", this);

        if (triggered) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        triggered = true;
        ShowCompletionScreen();
    }

    private void ShowCompletionScreen()
    {
        if (completionPanel != null)
            completionPanel.SetActive(true);

        if (playerController != null)
            playerController.MovementLocked = true;

        if (weaponObject != null)
            weaponObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }

    public void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
