using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class OrderPickup : MonoBehaviour
{
    [Tooltip("Opcional: la bolsa del encargo. Solo se ve mientras hay un encargo aceptado esperando a que lo recojas")]
    public GameObject bag;

    [Header("Aviso")]
    public Canvas prompt;
    public TMP_Text promptLabel;

    bool playerInside;
    InputAction interactAction;

    void Start()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact");
        if (prompt != null) prompt.enabled = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool waiting = gm != null && gm.HasAcceptedOrder;
        if (bag != null && bag.activeSelf != waiting) bag.SetActive(waiting);

        bool canPickUp = playerInside && waiting;
        UpdatePrompt(canPickUp);
        if (!canPickUp || interactAction == null || !interactAction.WasPressedThisFrame()) return;

        string customer = gm.AcceptedOrder.customerName;
        if (gm.PickUpAcceptedOrder())
        {
            AudioManager.Play(Sfx.OrderPickedUp);
            Notifications.Show($"Encargo de {customer} recogido. ¡En marcha!");
        }
    }

    void UpdatePrompt(bool show)
    {
        if (prompt == null) return;

        prompt.enabled = show;
        if (!show) return;

        Camera cam = Camera.main;
        if (cam != null) prompt.transform.rotation = Quaternion.LookRotation(prompt.transform.position - cam.transform.position);
        if (promptLabel != null) promptLabel.text = $"{InputHint.KeyName(interactAction)} · Recoger encargo";
    }

    void OnTriggerEnter(Collider other)
    {
        if (PlayerLocator.IsPlayer(other)) playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (PlayerLocator.IsPlayer(other)) playerInside = false;
    }
}
