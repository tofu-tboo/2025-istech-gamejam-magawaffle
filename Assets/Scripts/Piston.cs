using UnityEngine;

public class Piston : MonoBehaviour
{
    // 피스톤의 4가지 상태
    private enum PistonState { Idle, Falling, Grounded, Rising }
    private PistonState currentState = PistonState.Idle;

    [Header("참조 (References)")]
    [Tooltip("실제로 움직일 자식 오브젝트 (PressSprite)")]
    [SerializeField] private GameObject pressObject;
    
    [Header("설정 (Settings)")]
    [Tooltip("플레이어를 감지할 거리 (PressSprite 아래쪽)")]
    [SerializeField] private float detectionRange = 5f;
    [Tooltip("빠르게 떨어지는 속도")]
    [SerializeField] private float fallSpeed = 10f;
    [Tooltip("천천히 올라가는 속도")]
    [SerializeField] private float riseSpeed = 1f;
    [Tooltip("바닥에 닿은 후 멈춰있는 시간")]
    [SerializeField] private float groundedWaitTime = 1f;

    [Header("레이어 (Layers)")]
    [Tooltip("감지할 플레이어 레이어 (Body의 Player 레이어)")]
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("바닥으로 인식할 레이어")]
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D pressRb;
    private Collider2D pressCollider;
    private Vector2 originalPosition; // PressSprite의 원래 시작 위치
    private float groundedTimer;

    // 피스톤 전체를 켜고 끄는 변수
    private bool isEnabled = true;

    void Awake()
    {
        if (pressObject == null)
        {
            Debug.LogError("Piston: 'pressObject'가 할당되지 않았습니다!", this);
            return;
        }

        // PressSprite의 컴포넌트 가져오기
        pressRb = pressObject.GetComponent<Rigidbody2D>();
        pressCollider = pressObject.GetComponent<Collider2D>();

        if (pressRb == null || pressCollider == null)
        {
            Debug.LogError("Piston: 'pressObject'에 Rigidbody2D 또는 Collider2D가 없습니다.", this);
            return;
        }

        // Rigidbody를 Kinematic으로 강제 설정
        pressRb.bodyType = RigidbodyType2D.Kinematic;
        
        // 시작 위치 저장 (월드 좌표 기준)
        originalPosition = pressRb.position;

        // 시작 시 콜라이더를 비활성화 (안전)
        pressCollider.enabled = false;
    }

    void FixedUpdate()
    {
        if (!isEnabled)
        {
            // 비활성화 시 속도를 0으로 고정
            pressRb.linearVelocity = Vector2.zero;
            return;
        }

        // 상태 머신(State Machine)
        switch (currentState)
        {
            case PistonState.Idle:
                pressRb.linearVelocity = Vector2.zero;
                pressCollider.enabled = false; // 대기 중엔 안전
                
                // 플레이어 감지
                if (CheckForPlayer())
                {
                    currentState = PistonState.Falling;
                }
                break;

            case PistonState.Falling:
                pressRb.linearVelocity = new Vector2(0, -fallSpeed);
                pressCollider.enabled = true; // 낙하 중엔 위험
                
                // 바닥 감지
                if (CheckForGround())
                {
                    pressRb.linearVelocity = Vector2.zero;
                    groundedTimer = groundedWaitTime;
                    currentState = PistonState.Grounded;
                    // (선택적) 바닥에 부딪히는 사운드 재생
                }
                break;

            case PistonState.Grounded:
                pressCollider.enabled = true; // 바닥에 있을 때도 위험 (깔림)
                
                groundedTimer -= Time.fixedDeltaTime;
                if (groundedTimer <= 0)
                {
                    currentState = PistonState.Rising;
                }
                break;

            case PistonState.Rising:
                pressRb.linearVelocity = new Vector2(0, riseSpeed);
                pressCollider.enabled = false; // 올라갈 땐 안전
                
                // 원래 위치로 복귀했는지 확인
                if (pressRb.position.y >= originalPosition.y)
                {
                    pressRb.position = originalPosition; // 정확한 위치로 스냅
                    pressRb.linearVelocity = Vector2.zero;
                    currentState = PistonState.Idle;
                }
                break;
        }
    }

    // PressSprite 바로 아래에 플레이어가 있는지 확인
    private bool CheckForPlayer()
    {
        if (pressCollider == null) return false;
        
        // 콜라이더의 바닥 중앙 지점 계산
        Vector2 rayOrigin = new Vector2(pressCollider.bounds.center.x, pressCollider.bounds.min.y);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, detectionRange, playerLayer);

        // Debug.DrawRay(rayOrigin, Vector2.down * detectionRange, Color.red);
        
        return hit.collider != null;
    }

    // PressSprite가 바닥에 닿았는지 확인
    private bool CheckForGround()
    {
        if (pressCollider == null) return false;

        // 콜라이더의 바닥 중앙 지점 계산
        Vector2 rayOrigin = new Vector2(pressCollider.bounds.center.x, pressCollider.bounds.min.y);
        // 매우 짧은 거리(0.1f)로 바닥을 감지
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 0.1f, groundLayer);

        // Debug.DrawRay(rayOrigin, Vector2.down * 0.1f, Color.blue);

        return hit.collider != null;
    }


    /// <summary>
    /// 피스톤(쿵쿵이)의 작동을 완전히 켭니다.
    /// </summary>
    public void Activate()
    {
        isEnabled = true;
    }

    /// <summary>
    /// 피스톤(쿵쿵이)의 작동을 완전히 끕니다.
    /// (안전을 위해 천천히 올라가는 상태로 강제 변경)
    /// </summary>
    public void Deactivate()
    {
        isEnabled = false;
        currentState = PistonState.Rising; // 비활성화 시 안전하게 복귀
    }
}