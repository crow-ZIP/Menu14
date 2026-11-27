using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pantalla de presentación. Aparece al arrancar el juego y, pasados unos segundos,
/// va sola al menú principal. Si se pulsa cualquier tecla (o botón), va al menú antes.
/// </summary>
public class SplashScreen : MonoBehaviour
{
    [Tooltip("Segundos hasta pasar al menú automáticamente")]
    [SerializeField] private float duration = 4f;
    [Tooltip("Segundos del fundido de entrada")]
    [SerializeField] private float fadeInTime = 0.8f;
    [Tooltip("No se puede saltar antes de esto (para no saltarla sin querer)")]
    [SerializeField] private float minTimeBeforeSkip = 0.4f;

    [Tooltip("Contenido que aparece con fundido (CanvasGroup)")]
    [SerializeField] private CanvasGroup content;
    [Tooltip("Opcional: el texto \"Pulsa cualquier tecla\", que parpadea")]
    [SerializeField] private TMP_Text hint;

    private float elapsed;
    private bool leaving;

    private void Start()
    {
        if (content != null) content.alpha = 0f;
    }

    private void Update()
    {
        if (leaving) return;

        elapsed += Time.unscaledDeltaTime;
        if (content != null) content.alpha = Mathf.Clamp01(elapsed / fadeInTime);
        if (hint != null) hint.alpha = 0.45f + 0.45f * Mathf.Sin(elapsed * 4f);

        bool skip = elapsed >= minTimeBeforeSkip && AnyInputPressed();
        if (elapsed >= duration || skip) GoToMenu();
    }

    private static bool AnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;

        Gamepad pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame
                               || pad.buttonEast.wasPressedThisFrame);
    }

    private void GoToMenu()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;   // si se hace Play en esta escena, espera a que se cargue Persistent

        leaving = true;
        gm.LoadScene(GameManager.MainMenuScene);
    }
}
