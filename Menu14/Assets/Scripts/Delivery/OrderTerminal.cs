using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class OrderTerminal : MonoBehaviour
{
    [Tooltip("Panel con la interfaz de pedidos. Se activa al usar la pantalla y se desactiva al cerrarla. " +
             "Si se deja vacío, usar la pantalla acepta directamente el primer encargo pendiente")]
    public GameObject ordersScreen;

    [Tooltip("Punto que tiene que mirar el jugador (el centro de la pantalla). Si se deja vacío, este objeto")]
    public Transform screenCenter;
    [Tooltip("Metros máximos entre el jugador y la pantalla para poder usarla")]
    public float useDistance = 2f;
    [Tooltip("Grados máximos entre hacia dónde mira la cámara y la pantalla")]
    public float maxLookAngle = 35f;

    [Header("Aviso")]
    public Canvas prompt;
    public TMP_Text promptLabel;

    [Header("Eventos")]
    public UnityEvent opened;
    public UnityEvent closed;

    public bool IsOpen { get; private set; }

    GameObject player;
    PlayerMovement movement;
    PlayerLook look;
    InputAction interactAction;
    InputAction cancelAction;

    void Start()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact");
        cancelAction = InputSystem.actions.FindAction("UI/Cancel");
        if (screenCenter == null) screenCenter = transform;
        if (ordersScreen != null) ordersScreen.SetActive(false);
    }

    void Update()
    {
        FindPlayer();

        if (IsOpen)
        {
            if (Pressed(interactAction) || Pressed(cancelAction)) Close();
        }
        else if (CanUse() && Pressed(interactAction))
        {
            Open();
        }

        UpdatePrompt();
    }

    public void Open()
    {
        if (IsOpen) return;

        if (ordersScreen == null)
        {
            AcceptFirstPending();
            return;
        }

        IsOpen = true;
        ordersScreen.SetActive(true);
        SetPlayerControl(false);
        opened.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        ordersScreen.SetActive(false);
        SetPlayerControl(true);
        closed.Invoke();
    }

    void AcceptFirstPending()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int index = gm.FirstPendingOrder();
        if (index >= 0 && gm.AcceptOrder(index))
            Debug.Log($"Encargo aceptado: {gm.AcceptedOrder.customerName}. Recógelo en el mostrador");
    }

    bool CanUse()
    {
        if (player == null || !player.activeInHierarchy) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 toScreen = screenCenter.position - cam.transform.position;
        if (toScreen.magnitude > useDistance) return false;
        return Vector3.Angle(cam.transform.forward, toScreen) <= maxLookAngle;
    }

    void SetPlayerControl(bool enabled)
    {
        if (movement != null) movement.enabled = enabled;
        if (look != null) look.enabled = enabled;
    }

    void UpdatePrompt()
    {
        if (prompt == null) return;

        bool show = !IsOpen && CanUse();
        prompt.enabled = show;
        if (!show) return;

        Camera cam = Camera.main;
        prompt.transform.rotation = Quaternion.LookRotation(prompt.transform.position - cam.transform.position);
        if (promptLabel != null) promptLabel.text = PromptText();
    }

    string PromptText()
    {
        var gm = GameManager.Instance;
        if (ordersScreen != null || gm == null) return $"{KeyName()} · Ver encargos";

        if (gm.HasActiveOrder) return "Ya llevas un encargo";
        if (gm.HasAcceptedOrder) return "Recoge el encargo en el estante";
        if (gm.FirstPendingOrder() < 0) return "No quedan encargos esta noche";
        return $"{KeyName()} · Aceptar encargo";
    }

    void FindPlayer()
    {
        if (player != null) return;

        player = GameObject.FindWithTag("Player");
        if (player == null) return;
        movement = player.GetComponent<PlayerMovement>();
        look = player.GetComponent<PlayerLook>();
    }

    static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();

    string KeyName() => InputHint.KeyName(interactAction);
}
