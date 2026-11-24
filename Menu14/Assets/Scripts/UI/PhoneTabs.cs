using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pestañas del móvil. Con el móvil abierto, las acciones Player/Previous y Player/Next
/// (teclas 1 y 2, o la cruceta del mando izquierda / derecha) cambian de pestaña.
/// Solo se ve una página a la vez. Ponlo en el objeto Phone, junto a PhoneUI.
/// </summary>
public class PhoneTabs : MonoBehaviour
{
    [Tooltip("Las páginas del móvil, en orden: Pantalla (Info), PantallaMapa...")]
    [SerializeField] private GameObject[] pages;
    [Tooltip("Opcional: los textos de las pestañas de arriba, en el mismo orden que las páginas")]
    [SerializeField] private TMP_Text[] tabLabels;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.35f);

    public int Current { get; private set; }

    private PhoneUI phone;
    private InputAction previousAction;
    private InputAction nextAction;

    private void Start()
    {
        phone = GetComponent<PhoneUI>();
        previousAction = InputSystem.actions.FindAction("Player/Previous");
        nextAction = InputSystem.actions.FindAction("Player/Next");
        Show(0);
    }

    private void Update()
    {
        if (phone != null && !phone.IsOpen) return;   // con el móvil guardado, 1 y 2 no hacen nada

        if (previousAction != null && previousAction.WasPressedThisFrame()) Show(Current - 1);
        if (nextAction != null && nextAction.WasPressedThisFrame()) Show(Current + 1);
    }

    public void Show(int index)
    {
        if (pages == null || pages.Length == 0) return;
        Current = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == Current);

        if (tabLabels == null) return;
        for (int i = 0; i < tabLabels.Length; i++)
            if (tabLabels[i] != null) tabLabels[i].color = i == Current ? activeColor : inactiveColor;
    }
}
