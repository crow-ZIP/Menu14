using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú principal: abre y cierra la ventana de créditos.
/// Jugar y Salir llaman a SceneButtons (StartNewGame y QuitGame) desde el OnClick de sus botones.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button creditsBackButton;

    private InputAction cancelAction;

    private void Awake()
    {
        if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
        if (creditsBackButton != null) creditsBackButton.onClick.AddListener(CloseCredits);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    private void Start()
    {
        cancelAction = InputSystem.actions.FindAction("UI/Cancel");
    }

    private void Update()
    {
        // Esc (o B en el mando) cierra los créditos
        if (creditsPanel != null && creditsPanel.activeSelf && cancelAction != null && cancelAction.WasPressedThisFrame())
            CloseCredits();
    }

    public void OpenCredits()
    {
        creditsPanel.SetActive(true);
        Select(creditsBackButton);
    }

    public void CloseCredits()
    {
        creditsPanel.SetActive(false);
        Select(creditsButton);
    }

    private static void Select(Selectable s)
    {
        if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
