using System.Collections;
using UnityEngine;

public class FireworksShow : MonoBehaviour
{
    [Tooltip("Material con emisión para las chispas (se copia y se tiñe en cada cohete)")]
    public Material sparkMaterial;
    [Tooltip("Zona del cielo donde explotan: centro y tamaño en metros")]
    public Vector3 areaCenter = new Vector3(0f, 45f, 4f);
    public Vector3 areaSize = new Vector3(150f, 20f, 160f);
    [Tooltip("Segundos entre cohete y cohete (mínimo y máximo)")]
    public Vector2 interval = new Vector2(0.4f, 1.2f);
    [Tooltip("Radio (m) de la explosión")]
    public float radius = 14f;
    [Tooltip("Tamaño (m) de cada chispa al empezar")]
    public float sparkSize = 2.4f;
    [Tooltip("Chispas de cada cohete")]
    public int sparks = 26;
    public Color[] colors = { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.4f), new Color(0.4f, 0.6f, 1f), new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 1f) };

    float next;

    void Update()
    {
        if (sparkMaterial == null || Time.time < next) return;
        next = Time.time + Random.Range(interval.x, interval.y);
        Vector3 offset = Vector3.Scale(new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f), areaSize);
        StartCoroutine(Burst(areaCenter + offset, colors[Random.Range(0, colors.Length)]));
    }

    IEnumerator Burst(Vector3 center, Color color)
    {
        var mat = new Material(sparkMaterial);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_EmissionColor", color * 1.6f);

        AudioManager.PlayAt(Sfx.Firework, center, 0.6f);
        var root = new GameObject("Cohete").transform;
        root.SetParent(transform, false);
        root.position = center;
        var light = root.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 90f;
        light.shadows = LightShadows.None;

        var pieces = new Transform[sparks];
        var dirs = new Vector3[sparks];
        for (int i = 0; i < sparks; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * sparkSize;
            pieces[i] = go.transform;
            dirs[i] = Random.onUnitSphere;
        }

        const float duration = 1.4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float r = Mathf.Sqrt(k) * radius;
            for (int i = 0; i < sparks; i++)
            {
                pieces[i].localPosition = dirs[i] * r + Vector3.down * k * k * 6f;
                pieces[i].localScale = Vector3.one * Mathf.Lerp(sparkSize, sparkSize * 0.15f, k);
            }
            light.intensity = Mathf.Lerp(8f, 0f, k);
            yield return null;
        }

        Destroy(root.gameObject);
        Destroy(mat);
    }
}
