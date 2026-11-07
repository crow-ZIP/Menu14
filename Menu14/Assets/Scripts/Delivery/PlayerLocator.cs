using UnityEngine;


public static class PlayerLocator
{
    static PlayerMovement player;
    static ScooterController scooter;

    /// <summary>Collider del jugador ahora mismo: el del patinete si va montado, el del Player si va a pie.</summary>
    public static Collider Current
    {
        get
        {
            FindReferences();
            if (scooter != null && scooter.IsRidden) return scooter.GetComponent<Collider>();
            if (player != null && player.gameObject.activeInHierarchy) return player.GetComponent<Collider>();
            return null;
        }
    }

    /// <summary>¿Este collider es el jugador? El Player a pie, o el patinete solo si alguien va montado.</summary>
    public static bool IsPlayer(Collider c)
    {
        if (c == null) return false;
        if (c.GetComponentInParent<PlayerMovement>() != null) return true;

        var s = c.GetComponentInParent<ScooterController>();
        return s != null && s.IsRidden;   // un patinete aparcado no es el jugador
    }

    static void FindReferences()
    {
        // Include: el Player puede estar desactivado (cuando vas montado) y aun así hay que encontrarlo.
        // Si se cambia de escena, las referencias a objetos destruidos pasan a valer null y se vuelven a buscar.
        if (player == null) player = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (scooter == null) scooter = Object.FindFirstObjectByType<ScooterController>();
    }
}
