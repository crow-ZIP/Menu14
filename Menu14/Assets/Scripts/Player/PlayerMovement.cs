using UnityEngine;
using UnityEngine.InputSystem;

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

        Vector3 movement = new Vector3(input.x, 0f, input.y);
        movement = Vector3.ClampMagnitude(movement, 1f) * walkSpeed;
        movement = transform.TransformDirection(movement);

        // -2 en el suelo para que no flote en las bajadas
        if (charController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        else
            verticalSpeed += gravity * Time.deltaTime;

        movement.y = verticalSpeed;
        charController.Move(movement * Time.deltaTime);
    }
}
