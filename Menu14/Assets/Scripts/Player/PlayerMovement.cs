using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Tooltip("Velocidad al andar, en metros por segundo")]
    public float walkSpeed = 3.5f;

    [Tooltip("Aceleración de la gravedad (negativa)")]
    public float gravity = -9.8f;

    [Tooltip("Frenado (m/s²) del empujón de una caída cuando tocas el suelo")]
    public float launchGroundDrag = 14f;

    [Tooltip("Frenado (m/s²) del empujón de una caída mientras vuelas")]
    public float launchAirDrag = 2f;

    private CharacterController charController;
    private InputAction moveAction;
    private float verticalSpeed;
    private Vector3 launchVelocity;

    void Start()
    {
        charController = GetComponent<CharacterController>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    public void Launch(Vector3 velocity)
    {
        launchVelocity = new Vector3(velocity.x, 0f, velocity.z);
        verticalSpeed = velocity.y;
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 movement = new Vector3(input.x, 0f, input.y);
        movement = Vector3.ClampMagnitude(movement, 1f) * walkSpeed;
        movement = transform.TransformDirection(movement);

        // -2 en el suelo para que no flote en las bajadas
        if (charController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        else
            verticalSpeed += gravity * Time.deltaTime;

        float drag = charController.isGrounded ? launchGroundDrag : launchAirDrag;
        launchVelocity = Vector3.MoveTowards(launchVelocity, Vector3.zero, drag * Time.deltaTime);

        movement += launchVelocity;
        movement.y = verticalSpeed;
        charController.Move(movement * Time.deltaTime);
    }
}
