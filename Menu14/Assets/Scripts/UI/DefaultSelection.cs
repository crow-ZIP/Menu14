using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ponlo en un panel de menú. Al activarse selecciona su botón principal, y si en algún momento
/// no hay nada seleccionado (por ejemplo, tras hacer clic en el fondo), lo vuelve a seleccionar.
/// Así Enter siempre hace algo: en el menú principal, Enter = Jugar ("regla del Enter").
/// </summary>
public class DefaultSelection : MonoBehaviour
{
    [SerializeField] private Selectable firstSelected;

    private void OnEnable()
    {
        StartCoroutine(SelectWhenReady());
    }

    private IEnumerator SelectWhenReady()
    {
        // El EventSystem está en Persistent: si se hace Play en otra escena, tarda un fotograma en existir
        while (EventSystem.current == null) yield return null;
        Select();
    }

    private void Update()
    {
        EventSystem es = EventSystem.current;
        if (es == null || firstSelected == null) return;

        GameObject current = es.currentSelectedGameObject;
        if (current == null || !current.activeInHierarchy) Select();
    }

    public void Select()
    {
        if (firstSelected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }
}
