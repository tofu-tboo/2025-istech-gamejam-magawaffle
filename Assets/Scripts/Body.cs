using UnityEngine;

public enum BodyState
{
    playing, 
    undead,  
    dead     
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Body : MonoBehaviour
{
    public BodyState state = BodyState.playing;

    [Header("Movement Settings")]
    public float maxSpeed = 3f;
    public float acclerationForce = 100f; 

    [Header("Jump Settings")]
    public float jumpForce = 20f;
    [SerializeField] private float gravityScale = 3f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;

    private Rigidbody2D rb;
    private bool isGrounded;
    
    public Rigidbody2D Rb => rb; // Character가 Rigidbody에 접근 가능

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = gravityScale; 
    }

    void FixedUpdate()
    {
        // Body는 오직 Raycast만 실행하고, 자신의 중력 설정만 유지합니다.
        CheckGround();
        
        if (state == BodyState.playing)
        {
            rb.gravityScale = gravityScale;
        }
        else if (state == BodyState.undead || state == BodyState.dead)
        {
            rb.gravityScale = gravityScale;
        }
    }

    /// <summary>
    /// Raycast를 실행하고 지면 체크 결과를 내부 변수에 저장합니다.
    /// </summary>
    void CheckGround()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position, 
            Vector2.down, 
            groundCheckDistance, 
            groundLayer
        );
        
        if (hit.collider != null)
        {
            // 감지된 오브젝트가 Body 자기 자신이 아니라면 (무한 점프 방지)
            isGrounded = hit.collider.gameObject != gameObject;
        }
        else
        {
            isGrounded = false;
        }
        // Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }

    /// <summary>
    /// Character가 Body의 지면 상태를 외부에서 확인할 수 있도록 합니다.
    /// </summary>
    public bool IsGrounded()
    {
        return isGrounded;
    }
}