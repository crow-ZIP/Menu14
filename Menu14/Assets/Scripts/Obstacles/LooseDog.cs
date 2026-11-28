using UnityEngine;

public class LooseDog : ObstacleBehaviour
{
    [Tooltip("Las cuatro patas: se balancean al andar")]
    public Transform[] legs;
    [Tooltip("Cola: se mueve más rápido cuando persigue")]
    public Transform tail;
    [Tooltip("Grito del dueño: solo se ve mientras el perro persigue")]
    public GameObject ownerShout;
    [Tooltip("Grados que se balancean las patas")]
    public float legSwing = 35f;

    enum State { Wander, Chase, Return }

    State state = State.Wander;
    Vector3 home;
    Vector3 wanderTarget;
    float stateTime;
    float readyTime;
    float stepPhase;
    Quaternion[] legRest;
    Quaternion tailRest;

    public override void Setup(ObstacleConfig config, ObstacleSpot spot)
    {
        base.Setup(config, spot);
        home = Spot.ClampToArea(transform.position);
        wanderTarget = Spot.RandomPointInArea();
    }

    void Awake()
    {
        legRest = new Quaternion[legs.Length];
        for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i].localRotation;
        if (tail != null) tailRest = tail.localRotation;
        if (ownerShout != null) ownerShout.SetActive(false);
    }

    void Update()
    {
        if (Config == null) return;
        stateTime += Time.deltaTime;

        bool hasRider = TryGetRider(out ScooterController rider);
        float distance = hasRider ? DistanceToRider(rider, transform.position) : float.MaxValue;

        float speed = 0f;
        switch (state)
        {
            case State.Wander:
                if (hasRider && distance <= Config.dogDetectRadius && Time.time >= readyTime)
                {
                    SetState(State.Chase);
                    break;
                }
                if (Flat(transform.position - wanderTarget) < 0.2f || stateTime > 6f)
                {
                    wanderTarget = Spot.RandomPointInArea();
                    stateTime = 0f;
                }
                speed = MoveTowards(wanderTarget, Config.dogWanderSpeed);
                break;

            case State.Chase:
                if (!hasRider || stateTime > Config.dogGiveUpTime || distance > Config.dogDetectRadius * 1.5f)
                {
                    GiveUp();
                    break;
                }
                if (distance <= Config.dogBiteDistance)
                {
                    Vector3 away = rider.transform.position - transform.position;
                    away.y = 0f;
                    AudioManager.PlayAt(Sfx.DogBark, transform.position);
                    KnockOff(rider, CrashCause.Dog, away.sqrMagnitude > 0.01f ? away.normalized : rider.transform.forward);
                    GiveUp();
                    break;
                }
                speed = MoveTowards(Spot.ClampToArea(rider.transform.position), Config.dogChaseSpeed);
                break;

            case State.Return:
                speed = MoveTowards(home, Config.dogWanderSpeed * 1.5f);
                if (Flat(transform.position - home) < 0.2f) SetState(State.Wander);
                break;
        }

        Animate(speed);
    }

    void GiveUp()
    {
        readyTime = Time.time + Config.dogCooldown;
        SetState(State.Return);
    }

    void SetState(State next)
    {
        if (next == State.Chase && state != State.Chase) AudioManager.PlayAt(Sfx.DogBark, transform.position);
        state = next;
        stateTime = 0f;
        if (ownerShout != null) ownerShout.SetActive(next == State.Chase);
    }

    float MoveTowards(Vector3 target, float maxSpeed)
    {
        target.y = transform.position.y;
        Vector3 next = Vector3.MoveTowards(transform.position, target, maxSpeed * Time.deltaTime);
        Vector3 step = next - transform.position;
        transform.position = next;

        if (step.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(step), 540f * Time.deltaTime);
        return Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
    }

    void Animate(float speed)
    {
        stepPhase += speed * 5f * Time.deltaTime;
        float amount = Mathf.Clamp01(speed / 2f);
        for (int i = 0; i < legs.Length; i++)
        {
            float sign = (i == 0 || i == 3) ? 1f : -1f;
            legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(stepPhase) * legSwing * amount * sign, 0f, 0f);
        }

        if (tail != null)
        {
            float wag = state == State.Chase ? 18f : 6f;
            tail.localRotation = tailRest * Quaternion.Euler(0f, Mathf.Sin(Time.time * wag) * 30f, 0f);
        }
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
