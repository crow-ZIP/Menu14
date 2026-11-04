using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento a pie del jugador en primera persona.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Tooltip("Velocidad al andar, en metros por segundo")]
    public float walkSpeed = 3.5f;

    [Tooltip("Aceleración de la gravedad (negativa)")]
    public float gravity = -9.8f;

    private CharacterController charController;
    private InputAction moveAction;
    private float verticalSpeed;

    void Start()
    {
        charController = GetComponent<CharacterController>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Movimiento horizontal en el espacio local del jugador (hacia donde mira).
        Vector3 movement = new Vector3(input.x, 0f, input.y);
        movement = Vector3.ClampMagnitude(movement, 1f) * walkSpeed;
        movement = transform.TransformDirection(movement);

        // Gravedad: en el suelo se mantiene un pequeño empuje hacia abajo para no "flotar" en las bajadas.
        if (charController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        else
            verticalSpeed += gravity * Time.deltaTime;

        movement.y = verticalSpeed;
        charController.Move(movement * Time.deltaTime); // Move calcula las colisiones
    }
}
