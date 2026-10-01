using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 读取新版 Input System 的移动输入，并通过 Rigidbody2D 移动角色。
/// 数据流：PlayerInput.inputactions 中的 Move 动作 -> moveAction -> moveInput-> Rigidbody2D.MovePosition。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("引用 PlayerInput.inputactions 中 Player/Move 动作；需要在 Inspector 中赋值。")]
    [SerializeField]
    private InputActionReference moveAction;

    [Header("Movement")]
    [Min(0f)]
    [SerializeField]
    private float moveSpeed = 3f;

    private Rigidbody2D rb;
    private Vector2 moveInput;   // 当前帧输入

    /// <summary>供动画控制器读取当前移动方向。</summary>
    public Vector2 MoveInput
    {
        get
        {
            return moveInput;
        }
    }

    /// <summary>
    /// 输入长度大于一个很小的阈值时视为正在移动。
    /// 使用 sqrMagnitude 可以省去开平方计算，也能过滤摇杆的轻微漂移。
    /// </summary>
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
        if (moveAction == null || moveAction.action == null)
        {
            Debug.LogError("PlayerMovement requires a valid Move InputActionReference.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        // 新输入系统的 Action 必须启用后才会读取设备输入。
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        // 组件停用时同步停用 Action，防止无效监听或重复启用。
        moveInput = Vector2.zero;
        if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
    }

    private void Update()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && root.InputModes.CurrentMode != GameInputMode.Gameplay)
        {
            moveInput = Vector2.zero;
            return;
        }

        // Move 是 Value/Vector2 类型。WASD 或方向键的 2D Vector Composite
        // 会被 Input System 合成为一个 Vector2，而不是分别读取四个按键。
        moveInput = moveAction.action.ReadValue<Vector2>();

        // 同时按两个方向时向量长度约为 1.414；归一化后可防止斜向速度更快。
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void FixedUpdate()
    {
        if (moveInput.sqrMagnitude <= 0f) return;
        Vector2 targetPosition = rb.position + moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }
}
