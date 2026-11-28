using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NoticeKind { Info, Good, Bad, Warning }

/// <summary>
/// Avisos en pantalla: píldoras que aparecen arriba en el centro, se apilan y se desvanecen solas.
/// Vive en la escena Persistent. Desde cualquier script: Notifications.Show("+150 €", NoticeKind.Good);
/// Usa el tiempo del juego, así que en pausa se quedan quietos.
/// </summary>
public class Notifications : MonoBehaviour
{
    [Tooltip("Aviso de ejemplo (desactivado): una Image con un TMP_Text hijo y un CanvasGroup. Se copia uno por aviso")]
    [SerializeField] private RectTransform template;
    [SerializeField] private float duration = 2.5f;
    [SerializeField] private float fadeTime = 0.4f;
    [Tooltip("Distancia vertical entre avisos apilados")]
    [SerializeField] private float spacing = 70f;
    [SerializeField] private int maxVisible = 4;

    [Header("Colores del texto")]
    [SerializeField] private Color infoColor = Color.white;
    [SerializeField] private Color goodColor = new Color(0.45f, 0.85f, 0.45f);
    [SerializeField] private Color badColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.34f);

    private class Toast
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float age;
    }

    private static Notifications instance;
    private readonly List<Toast> toasts = new List<Toast>();

    public static void Show(string message, NoticeKind kind = NoticeKind.Info)
    {
        Debug.Log(message);
        if (instance != null) instance.Add(message, kind);
    }

    private void Awake()
    {
        instance = this;
        if (template != null) template.gameObject.SetActive(false);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    // Al cambiar de escena (fin de noche, menú...) se quitan los avisos que quedaran
    private void OnSceneUnloaded(Scene scene) => Clear();

    private void Add(string message, NoticeKind kind)
    {
        if (template == null) return;

        RectTransform rect = Instantiate(template, template.parent);
        rect.gameObject.SetActive(true);

        TMP_Text text = rect.GetComponentInChildren<TMP_Text>();
        text.text = message;
        text.color = ColorFor(kind);
        float width = text.GetPreferredValues(message).x + 70f;
        rect.sizeDelta = new Vector2(Mathf.Max(width, 220f), rect.sizeDelta.y);
        rect.anchoredPosition = template.anchoredPosition + Vector2.up * spacing * 0.5f;   // entra desde arriba

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;

        toasts.Insert(0, new Toast { rect = rect, group = group });
        while (toasts.Count > maxVisible) Remove(toasts.Count - 1);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            Toast t = toasts[i];
            t.age += dt;
            if (t.age >= duration + fadeTime) { Remove(i); continue; }

            Vector2 target = template.anchoredPosition + Vector2.down * spacing * i;
            t.rect.anchoredPosition = Vector2.Lerp(t.rect.anchoredPosition, target, 1f - Mathf.Exp(-14f * dt));

            if (t.group != null)
                t.group.alpha = t.age < 0.15f ? t.age / 0.15f
                              : t.age > duration ? 1f - (t.age - duration) / fadeTime
                              : 1f;
        }
    }

    private void Remove(int index)
    {
        Destroy(toasts[index].rect.gameObject);
        toasts.RemoveAt(index);
    }

    private void Clear()
    {
        for (int i = toasts.Count - 1; i >= 0; i--) Remove(i);
    }

    private Color ColorFor(NoticeKind kind) => kind switch
    {
        NoticeKind.Good => goodColor,
        NoticeKind.Bad => badColor,
        NoticeKind.Warning => warningColor,
        _ => infoColor,
    };
}
