using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Móvil desplegable. La acción "Player/Phone" (Tab o Select del mando) lo saca o lo guarda.
/// El juego sigue mientras está abierto: solo muestra información, así que el ratón sigue bloqueado.
/// Ponlo en el panel Phone y colócalo en el editor donde quieras verlo ABIERTO: al empezar se esconde solo.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PhoneUI : MonoBehaviour
{
    [Tooltip("Cuánto baja el móvil (en unidades del Canvas) para quedar fuera de la pantalla. Que sea más que su alto")]
    [SerializeField] private float hiddenOffset = 900f;
    [Tooltip("Segundos que tarda en subir o bajar")]
    [SerializeField] private float slideTime = 0.25f;
    [SerializeField] private bool startOpen = false;

    [Tooltip("Opcional: el aviso \"Tab – Móvil\" de la pantalla principal. Solo se ve con el móvil cerrado")]
    [SerializeField] private GameObject closedHint;

    public bool IsOpen { get; private set; }

    private RectTransform rect;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private InputAction phoneAction;
    private float openAmount;   // 0 = guardado, 1 = sacado

    private void Awake()
    {
        rect = (RectTransform)transform;
        shownPosition = rect.anchoredPosition;                       // donde lo pusiste en el editor
        hiddenPosition = shownPosition + Vector2.down * hiddenOffset;
    }

    private void Start()
    {
        phoneAction = InputSystem.actions.FindAction("Player/Phone");
        if (phoneAction == null) Debug.LogWarning("PhoneUI: falta la acción Player/Phone en InputSystem_Actions");

        SetOpen(startOpen, instant: true);
    }

    private void Update()
    {
        if (phoneAction != null && phoneAction.WasPressedThisFrame()) Toggle();

        // Avanza poco a poco hacia abierto o cerrado, con un suavizado al principio y al final
        float speed = slideTime > 0f ? 1f / slideTime : 1000f;
        openAmount = Mathf.MoveTowards(openAmount, IsOpen ? 1f : 0f, speed * Time.deltaTime);
        rect.anchoredPosition = Vector2.Lerp(hiddenPosition, shownPosition, Mathf.SmoothStep(0f, 1f, openAmount));
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open, bool instant = false)
    {
        IsOpen = open;
        if (instant) openAmount = open ? 1f : 0f;
        if (closedHint != null) closedHint.SetActive(!open);
    }
}
