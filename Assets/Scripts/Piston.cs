using UnityEngine;

public class Piston : MonoBehaviour
{
    // 피스톤의 4가지 상태
    private enum PistonState { Idle, Falling, Grounded, Rising }
    private PistonState currentState = PistonState.Idle;

    [Header("참조 (References)")]
    [Tooltip("실제로 움직일 자식 오브젝트 (PressSprite)")]
    [SerializeField] private GameObject pressObject;

    [Tooltip("상태에 따라 스프라이트가 바뀔 자석 부분")]
    [SerializeField] private SpriteRenderer magneticSpriteRenderer;

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
    private LayerMask combinedDetectionMask; // [추가]

    [Header("스프라이트 (Sprites)")]
    [SerializeField] private Sprite activeMagneticSprite;
    [SerializeField] private Sprite inactiveMagneticSprite;

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

        int bodyLayerIndex = LayerMask.NameToLayer("Body");
        if (bodyLayerIndex != -1)
        {
            // playerLayer에 "Body" 레이어를 추가
            combinedDetectionMask = playerLayer | (1 << bodyLayerIndex);
        }
        else
        {
            Debug.LogWarning("Piston: 'Body' layer not found. Piston will only detect 'Player' layer.");
            combinedDetectionMask = playerLayer;
        }

        // Rigidbody를 Kinematic으로 강제 설정
        pressRb.bodyType = RigidbodyType2D.Kinematic;

        // 시작 위치 저장 (월드 좌표 기준)
        originalPosition = pressRb.position;

        // 시작 시 콜라이더를 비활성화 (안전)
        pressCollider.enabled = false;

        if (magneticSpriteRenderer != null)
        {
            magneticSpriteRenderer.sprite = activeMagneticSprite;
        }
        else
        {
            Debug.LogWarning("Piston: magneticSpriteRenderer가 할당되지 않았습니다.", this);
        }
    }
    void Update()
    {
        if (CheckForPlayer())
        {
            currentState = PistonState.Falling;
        }
    }
    void FixedUpdate()
    {
        if (!isEnabled)
        {

            // [수정] 비활성화 시 Inactive 스프라이트로 고정
            if (magneticSpriteRenderer != null)
            {
                magneticSpriteRenderer.sprite = inactiveMagneticSprite;
            }
            // MovePosition을 호출하지 않으면 멈춥니다.
            return;
        }

        // 상태 머신(State Machine)
        switch (currentState)
        {
            case PistonState.Idle:
                pressCollider.enabled = false; // 대기 중엔 안전

                // [수정] 'inactive' 스프라이트
                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = inactiveMagneticSprite;
                }
                if (CheckForPlayer())
                {
                    currentState = PistonState.Falling;
                }
                break;

            case PistonState.Falling:
                // [핵심 수정] .velocity 대신 .MovePosition() 사용
                // 1. 현재 위치에서 (아래방향 * 속도 * 시간) 만큼 이동할 '다음 위치' 계산
                Vector2 newFallPos = pressRb.position + (Vector2.down * fallSpeed * Time.fixedDeltaTime);
                // 2. 물리 엔진을 통해 '다음 위치'로 이동
                // 바닥 감지
                if (CheckForGround())
                {
                    // [핵심 수정] MovePosition을 멈추고 상태만 변경
                    // (velocity = 0 코드가 필요 없어짐)
                    groundedTimer = groundedWaitTime;
                    currentState = PistonState.Grounded;
                }
                else
                    pressRb.MovePosition(newFallPos);

                pressCollider.enabled = true; // 낙하 중엔 위험

                // [수정] 'active' 스프라이트
                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = activeMagneticSprite;
                }

                break;

            case PistonState.Grounded:
                // MovePosition()을 호출하지 않으므로, 그 자리에 멈춰있습니다.
                pressCollider.enabled = true; // 바닥에 있을 때도 위험 (깔림)

                // [수정] 'active' 스프라이트
                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = activeMagneticSprite;
                }

                groundedTimer -= Time.fixedDeltaTime;
                if (groundedTimer <= 0)
                {
                    currentState = PistonState.Rising;
                }
                break;

            case PistonState.Rising:
                // [핵심 수정] .velocity 대신 .MovePosition() 사용
                Vector2 newRisePos = pressRb.position + (Vector2.up * riseSpeed * Time.fixedDeltaTime);
                pressRb.MovePosition(newRisePos);

                pressCollider.enabled = false; // 올라갈 땐 안전

                // [수정] 'inactive' 스프라이트
                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = inactiveMagneticSprite;
                }

                // 원래 위치로 복귀했는지 확인
                if (pressRb.position.y >= originalPosition.y)
                {
                    // [핵심 수정] 정확한 원래 위치로 스냅
                    pressRb.MovePosition(originalPosition);
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
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, detectionRange, combinedDetectionMask);

        // Debug.DrawRay(rayOrigin, Vector2.down * detectionRange, Color.red);

        return hit.collider != null;
    }

    // PressSprite가 바닥에 닿았는지 확인
    private bool CheckForGround()
    {
        if (pressCollider == null)
        {
            return false;
        }

        float bottomEdge = pressCollider.bounds.min.y;
        float centerX = pressCollider.bounds.center.x;
        Vector2 rayOrigin = new Vector2(centerX, bottomEdge + 0.1f);
        float rayDistance = 0.2f;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            rayDistance,
            groundLayer
        );

        // 씬(Scene) 뷰에 레이저를 그림
        Debug.DrawRay(rayOrigin, Vector2.down * rayDistance, hit.collider != null ? Color.green : Color.red);

        if (hit.collider != null)
        {
            return true;
        }
        else
        {
            return false;
        }
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

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Body"))
        {
            Destroy(collision.gameObject);
        }
    }

}