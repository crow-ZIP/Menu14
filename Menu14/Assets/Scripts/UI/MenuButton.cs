using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Efecto de los botones de menú: el botón seleccionado crece un poco.
/// Pasar el ratón por encima también lo selecciona, para que ratón y teclado (Enter) vayan a la par.
/// Usa tiempo sin escalar, así también funciona en el menú de pausa.
/// </summary>
public class MenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float speed = 12f;

    private bool selected;

    private void OnDisable()
    {
        selected = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float target = selected ? selectedScale : 1f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
