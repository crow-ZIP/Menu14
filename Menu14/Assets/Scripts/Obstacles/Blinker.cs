using UnityEngine;

public class Blinker : MonoBehaviour
{
    [Tooltip("Objetos que se encienden y se apagan a la vez (los intermitentes)")]
    public GameObject[] lights;
    [Tooltip("Segundos encendido y segundos apagado")]
    public float interval = 0.5f;

    void Update()
    {
        bool on = Mathf.Repeat(Time.time, interval * 2f) < interval;
        foreach (GameObject l in lights)
            if (l != null && l.activeSelf != on) l.SetActive(on);
    }
}
