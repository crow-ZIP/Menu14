using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ScooterPrompt : MonoBehaviour
{
    public ScooterMount mount;
    public ScooterController scooter;
    public TMP_Text label;
    [Tooltip("Image con Image Type = Filled que se llena mientras te recuperas de una caída")]
    public Image ring;
    [Tooltip("Fondo del anillo; se ve y se esconde junto con él")]
    public GameObject ringBackground;

    [Tooltip("Metros a los que tienes que estar del patinete para ver el aviso")]
    public float showDistance = 4f;
    [Tooltip("Altura (m) del aviso sobre el suelo")]
    public float height = 1.4f;

    Canvas canvas;
    InputAction interactAction;

    void Start()
    {
        canvas = GetComponent<Canvas>();
        interactAction = InputSystem.actions.FindAction("Player/Interact");
    }

    void LateUpdate()
    {
        GameObject player = mount != null ? mount.player : null;
        bool show = scooter != null && !scooter.IsRidden && player != null && player.activeInHierarchy;
        if (show)
        {
            Vector3 offset = player.transform.position - scooter.transform.position;
            offset.y = 0f;
            show = offset.magnitude <= showDistance || mount.RemountTimeLeft > 0f;
        }

        canvas.enabled = show;
        if (!show) return;

        Collider body = scooter.GetComponent<Collider>();
        float ground = body != null ? body.bounds.min.y : scooter.transform.position.y;
        transform.position = new Vector3(scooter.transform.position.x, ground + height, scooter.transform.position.z);

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        float left = mount.RemountTimeLeft;
        bool recovering = left > 0f;
        ring.gameObject.SetActive(recovering);
        if (ringBackground != null) ringBackground.SetActive(recovering);

        if (recovering)
        {
            ring.fillAmount = mount.RemountDuration > 0f ? 1f - left / mount.RemountDuration : 1f;
            label.text = $"Recuperándote… {Mathf.CeilToInt(left)}";
        }
        else
        {
            label.text = mount.CanMountNow ? $"{KeyName()} · Montar" : "Acércate para montar";
        }
    }

    string KeyName()
    {
        if (interactAction == null) return "E";
        string group = Gamepad.current != null ? "Gamepad" : "Keyboard&Mouse";
        string key = interactAction.GetBindingDisplayString(InputBinding.MaskByGroup(group),
                                                           InputBinding.DisplayStringOptions.DontIncludeInteractions);
        return string.IsNullOrEmpty(key) ? "E" : key;
    }
}
