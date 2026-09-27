using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference moveAction;

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 3f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    public Vector2 MoveInput
    {
        get
        {
            return moveInput;
        }
    }

    public bool IsMoving
    {
        get
        {
            return moveInput.sqrMagnitude > 0.01f;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
    }

    private void Update()
    {
        moveInput =moveAction.action.ReadValue<Vector2>();
        // 防止斜向速度更快
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void FixedUpdate()
    {
        Vector2 targetPosition =rb.position+ moveInput*moveSpeed*Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }
}