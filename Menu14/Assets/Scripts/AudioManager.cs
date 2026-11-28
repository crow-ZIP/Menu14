using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum Sfx
{
    Click, Select,
    OrderAccepted, OrderPickedUp, Delivered, OrderLost, InterestPaid, WrongDestination, Hurry,
    Crash, Caught, Detected, DogBark, Firecracker, Firework,
    NightComplete, Victory, Defeat,
}

/// <summary>
/// Todo el sonido del juego. Vive en la escena Persistent.
/// - Música: la de menús fuera de la ciudad y la de juego en City, con fundido.
/// - Ambiente de ciudad y motor del patinete (más volumen y tono cuanto más rápido).
/// - Efectos: AudioManager.Play(Sfx.Delivered) o, con posición en el mundo, AudioManager.PlayAt(Sfx.DogBark, pos).
/// Si un clip está vacío, ese sonido no suena (sin errores).
/// Hay un único AudioListener (el de este objeto), que sigue a la cámara activa: así no se corta
/// ningún sonido al cambiar de escena.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Volumen")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float motorVolume = 0.5f;

    [Header("Música y ambiente (en bucle)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip cityAmbience;
    [SerializeField] private AudioClip scooterMotor;

    [Header("Interfaz")]
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip select;

    [Header("Encargos")]
    [SerializeField] private AudioClip orderAccepted;
    [SerializeField] private AudioClip orderPickedUp;
    [SerializeField] private AudioClip delivered;
    [SerializeField] private AudioClip orderLost;
    [SerializeField] private AudioClip interestPaid;
    [SerializeField] private AudioClip wrongDestination;
    [SerializeField] private AudioClip hurry;

    [Header("Peligros")]
    [Tooltip("Se elige uno al azar en cada caída")]
    [SerializeField] private AudioClip[] crash;
    [SerializeField] private AudioClip caught;
    [SerializeField] private AudioClip detected;
    [SerializeField] private AudioClip dogBark;
    [SerializeField] private AudioClip firecracker;
    [Tooltip("Fuegos artificiales de Nochevieja. Si está vacío, se usa el del petardo")]
    [SerializeField] private AudioClip firework;

    [Header("Fin de noche y de partida")]
    [SerializeField] private AudioClip nightComplete;
    [SerializeField] private AudioClip victory;
    [SerializeField] private AudioClip defeat;

    private static AudioManager instance;

    private AudioSource musicSource, ambienceSource, motorSource, uiSource, sfxSource;
    private AudioListener listener;
    private Coroutine musicFade;
    private ScooterController scooter;
    private float nextScooterSearch;

    public static void Play(Sfx sfx, float volume = 1f)
    {
        if (instance == null) return;
        AudioClip clip = instance.ClipFor(sfx);
        if (clip == null) return;

        bool ui = sfx == Sfx.Click || sfx == Sfx.Select;
        (ui ? instance.uiSource : instance.sfxSource).PlayOneShot(clip, volume * instance.sfxVolume * instance.masterVolume);
    }

    // Sonido con posición en el mundo: se oye más flojo cuanto más lejos está
    public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f)
    {
        if (instance == null) return;
        AudioClip clip = instance.ClipFor(sfx);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume * instance.sfxVolume * instance.masterVolume);
    }

    private void Awake()
    {
        if (instance != null) { Destroy(this); return; }
        instance = this;

        musicSource = NewSource(loop: true, ignorePause: true);
        ambienceSource = NewSource(loop: true, ignorePause: false);
        motorSource = NewSource(loop: true, ignorePause: false);
        uiSource = NewSource(loop: false, ignorePause: true);    // los clics suenan también en pausa
        sfxSource = NewSource(loop: false, ignorePause: false);

        listener = GetComponent<AudioListener>();
        if (listener == null) listener = gameObject.AddComponent<AudioListener>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ScooterCrash.Crashed += OnCrash;

        // Si se pulsa Play en otra escena, esa ya está cargada antes que Persistent
        DisableOtherListeners();
        OnActiveSceneChanged(default, SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        ScooterCrash.Crashed -= OnCrash;
    }

    private AudioSource NewSource(bool loop, bool ignorePause)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = ignorePause;
        return s;
    }

    private void OnCrash(CrashCause cause) => Play(Sfx.Crash);

    // Cada escena trae su AudioListener en la cámara: se apagan para que solo quede el de aquí
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DisableOtherListeners();

    private void DisableOtherListeners()
    {
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l != listener) l.enabled = false;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        bool city = next.name == GameManager.CityScene;
        SetMusic(city ? gameMusic : menuMusic);

        ambienceSource.clip = city ? cityAmbience : null;
        ambienceSource.volume = ambienceVolume * masterVolume;
        if (ambienceSource.clip != null) ambienceSource.Play(); else ambienceSource.Stop();

        scooter = null;
    }

    private void SetMusic(AudioClip clip)
    {
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeTo(clip));
    }

    private IEnumerator FadeTo(AudioClip clip)
    {
        const float fadeTime = 0.6f;
        float start = musicSource.volume;
        for (float t = 0f; t < fadeTime && musicSource.isPlaying; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(start, 0f, t / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = clip;
        if (clip == null) yield break;

        musicSource.Play();
        float target = musicVolume * masterVolume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, target, t / fadeTime);
            yield return null;
        }
        musicSource.volume = target;
    }

    private void Update()
    {
        UpdateMotor();
    }

    private void LateUpdate()
    {
        // El oído va donde esté la cámara que se ve (a pie o en el patinete)
        Camera cam = Camera.main;
        if (cam == null || !cam.isActiveAndEnabled) cam = FindActiveCamera();
        if (cam != null) transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
    }

    private static Camera FindActiveCamera()
    {
        foreach (Camera c in Camera.allCameras)
            if (c.isActiveAndEnabled) return c;
        return null;
    }

    private void UpdateMotor()
    {
        if (scooterMotor == null) return;

        if (scooter == null && Time.unscaledTime >= nextScooterSearch)
        {
            nextScooterSearch = Time.unscaledTime + 1f;
            scooter = FindFirstObjectByType<ScooterController>();
        }

        float speed01 = 0f;
        bool riding = scooter != null && scooter.IsRidden && !scooter.IsFallen;
        if (riding) speed01 = Mathf.Clamp01(Mathf.Abs(scooter.Speed) / Mathf.Max(0.1f, scooter.maxSpeed));

        float targetVolume = riding ? Mathf.Lerp(0.2f, 1f, speed01) * motorVolume * masterVolume : 0f;
        motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVolume, Time.deltaTime * 2f);
        motorSource.pitch = Mathf.Lerp(0.8f, 1.5f, speed01);

        if (motorSource.volume > 0.001f && !motorSource.isPlaying)
        {
            motorSource.clip = scooterMotor;
            motorSource.Play();
        }
        else if (motorSource.volume <= 0.001f && motorSource.isPlaying)
        {
            motorSource.Stop();
        }
    }

    private AudioClip ClipFor(Sfx sfx) => sfx switch
    {
        Sfx.Click => click,
        Sfx.Select => select,
        Sfx.OrderAccepted => orderAccepted,
        Sfx.OrderPickedUp => orderPickedUp,
        Sfx.Delivered => delivered,
        Sfx.OrderLost => orderLost,
        Sfx.InterestPaid => interestPaid,
        Sfx.WrongDestination => wrongDestination,
        Sfx.Hurry => hurry,
        Sfx.Crash => crash != null && crash.Length > 0 ? crash[Random.Range(0, crash.Length)] : null,
        Sfx.Caught => caught,
        Sfx.Detected => detected,
        Sfx.DogBark => dogBark,
        Sfx.Firecracker => firecracker,
        Sfx.Firework => firework != null ? firework : firecracker,
        Sfx.NightComplete => nightComplete,
        Sfx.Victory => victory,
        Sfx.Defeat => defeat,
        _ => null,
    };
}
