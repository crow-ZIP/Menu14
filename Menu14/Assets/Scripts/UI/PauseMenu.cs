using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa. Vive en la escena Persistent (en su propio Canvas), pero solo se puede abrir en City.
/// Esc (o Start en el mando) pausa y reanuda. Mientras está en pausa: el tiempo se para,
/// se desactiva el input de juego (mapa "Player") y se suelta el ratón.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Panel con la interfaz de pausa (hijo de este objeto)")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;

    public static bool IsPaused { get; private set; }

    private InputActionMap playerMap;
    private CursorLockMode savedLockState;
    private bool savedCursorVisible;

    private void Awake()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (menuButton != null) menuButton.onClick.AddListener(QuitToMenu);
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        playerMap = InputSystem.actions.FindActionMap("Player");
    }

    private void Update()
    {
        if (!PausePressed()) return;

        if (IsPaused) Resume();
        else if (CanPause()) Pause();
    }

    private static bool PausePressed()
    {
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        return esc || start;
    }

    private static bool CanPause()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsLoading || gm.IsGameOver) return false;
        if (SceneManager.GetActiveScene().name != GameManager.CityScene) return false;

        // Con la pantalla del PC abierta, Esc la cierra a ella (y no abre la pausa en el mismo fotograma)
        if (OrderTerminal.AnyOpen || OrderTerminal.LastClosedFrame == Time.frameCount) return false;
        return true;
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (playerMap != null) playerMap.Disable();

        savedLockState = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (panel != null) panel.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (playerMap != null) playerMap.Enable();

        Cursor.lockState = savedLockState;
        Cursor.visible = savedCursorVisible;

        if (panel != null) panel.SetActive(false);
    }

    // Sale al menú principal. La partida en curso se pierde.
    private void QuitToMenu()
    {
        Resume();
        if (GameManager.Instance != null) GameManager.Instance.BackToMenu();
    }
}
