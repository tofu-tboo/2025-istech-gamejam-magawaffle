using UnityEngine;

public enum BodyState
{
    playing, // 현재 플레이어가 빙의 중
    undead,  // 플레이어가 떠난 '시체' 상태 (재빙의 가능)
    dead     // 완전히 버려진 '시체' 상태 (재빙의 불가능)
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Body : MonoBehaviour
{
    public BodyState state = BodyState.playing;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float acclerationForce = 100f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 20f;
    [SerializeField] private float gravityScale = 3f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;

    private Rigidbody2D rb;

    private Vector2 movingDirection;
    private bool jumpRequested;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = gravityScale; 
    }

    void FixedUpdate()
    {
        CheckGround();

        if (state == BodyState.playing)
        {
            rb.gravityScale = gravityScale;

            // 좌우 이동 (명령 실행)
            rb.AddForce(new Vector2(movingDirection.x * acclerationForce, 0f));
            float clampedXVelocity = Mathf.Clamp(rb.linearVelocity.x, -maxSpeed, maxSpeed);
            rb.linearVelocity = new Vector2(clampedXVelocity, rb.linearVelocity.y);

            // 점프 (명령 실행)
            if (jumpRequested && isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); 
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            }
        }
        // [수정됨] 'undead' 또는 'dead' 상태일 때
        else if (state == BodyState.undead || state == BodyState.dead)
        {
            // 두 상태 모두 조종은 불가능하지만,
            // 물리 법칙(중력, 밀림)은 그대로 적용됩니다.
            rb.gravityScale = gravityScale;
        }

        // 입력값 초기화
        movingDirection = Vector2.zero;
        jumpRequested = false;
    }

    void CheckGround()
    {
        // Raycast 결과를 바로 isGrounded에 대입합니다.
        // 유니티 설정(Layer Collision Matrix)에서 자신과의 충돌이 꺼져있어야 합니다.
        isGrounded = Physics2D.Raycast(
            groundCheck.position, 
            Vector2.down, 
            groundCheckDistance, 
            groundLayer
        );
        // Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }

    // --- '영혼(Character)'이 호출할 함수들 ---

    public void Move(Vector2 direction)
    {
        if (state == BodyState.playing)
        {
            movingDirection = direction;
        }
    }

    public void RequestJump()
    {
        if (state == BodyState.playing)
        {
            jumpRequested = true;
        }
    }
}