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
    [SerializeField] private float fallSpeed = 20f;
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

        string layers = "";
        for (int i = 0; i < 32; i++)
        {
            if ((combinedDetectionMask.value & (1 << i)) != 0)
            {
                layers += LayerMask.LayerToName(i) + " | ";
            }
        }
        Debug.Log($"[Piston Awake: {gameObject.name}] 감지할 레이어: {layers}");

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
    // void Update()
    // {
    //     if (CheckForPlayer())
    //     {
    //         currentState = PistonState.Falling;
    //     }
    // }
    void FixedUpdate()
    {
        if (!isEnabled)
        {

            Debug.LogWarning($"[Piston FixedUpdate: {gameObject.name}] Piston is NOT ENABLED.");

            if (magneticSpriteRenderer != null)
            {
                magneticSpriteRenderer.sprite = inactiveMagneticSprite;
            }

            if (currentState != PistonState.Idle)
            {
                GoToRisingState();
            }
            return;
        }

        // 상태 머신(State Machine)
        switch (currentState)
        {
            case PistonState.Idle:
                pressCollider.enabled = false;

                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = inactiveMagneticSprite;
                }

                if (CheckForPlayer())
                {
                    Debug.Log($"[Piston: {gameObject.name}] PLAYER DETECTED! State -> Falling.");
                    currentState = PistonState.Falling;
                }
                break;

            case PistonState.Falling:
                pressCollider.enabled = true;

                // [수정 1] 바닥을 *먼저* 확인합니다.
                if (CheckForGround())
                {
                    Debug.Log($"[Piston: {gameObject.name}] GROUND DETECTED! State -> Grounded.");
                    groundedTimer = groundedWaitTime;
                    currentState = PistonState.Grounded;

                    // 바닥에 닿았으므로 여기서 MovePosition을 멈춥니다.
                    break;
                }

                // [수정 1] 바닥에 닿지 않았을 때만 이동합니다.
                Vector2 newFallPos = pressRb.position + (Vector2.down * fallSpeed * Time.fixedDeltaTime);
                pressRb.MovePosition(newFallPos);

                // Body 파괴 로직 (BoxCastAll - 저번 턴에 수정한 내용 유지)
                if (pressCollider.enabled)
                {
                    Bounds bounds = pressCollider.bounds;
                    RaycastHit2D[] hits = Physics2D.BoxCastAll(bounds.center, bounds.size, 0f, Vector2.zero, 0f, combinedDetectionMask);

                    foreach (RaycastHit2D hit in hits)
                    {
                        if (hit.collider != null && hit.collider.TryGetComponent<Body>(out Body body))
                        {
                            Debug.LogWarning($"[Piston: {gameObject.name}] Falling 중 감지! {body.gameObject.name} (Layer: {LayerMask.LayerToName(body.gameObject.layer)}) 파괴 시도.");
                            body.HandlePistonCrush();
                        }
                    }
                }

                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = activeMagneticSprite;
                }
                break;

            case PistonState.Grounded:
                pressCollider.enabled = true;

                if (magneticSpriteRenderer != null)
                {
                    magneticSpriteRenderer.sprite = activeMagneticSprite;
                }

                groundedTimer -= Time.fixedDeltaTime;
                if (groundedTimer <= 0)
                {
                    Debug.Log($"[Piston: {gameObject.name}] Grounded timer finished. State -> Rising.");
                    currentState = PistonState.Rising;
                }
                break;

            case PistonState.Rising:
                // [수정 2] 올라가는 도중 플레이어 감지
                if (CheckForPlayer())
                {
                    Debug.Log($"[Piston: {gameObject.name}] PLAYER DETECTED while rising! State -> Falling.");
                    currentState = PistonState.Falling;
                }
                else
                {
                    // 플레이어가 없으면 계속 올라감
                    GoToRisingState();
                }
                break;
        }
    }
    
    // [신규] 복귀 로직 함수
    private void GoToRisingState()
    {
        Vector2 newRisePos = pressRb.position + (Vector2.up * riseSpeed * Time.fixedDeltaTime);
        pressRb.MovePosition(newRisePos);

        pressCollider.enabled = false; 

        if (magneticSpriteRenderer != null)
        {
            magneticSpriteRenderer.sprite = inactiveMagneticSprite;
        }

        if (pressRb.position.y >= originalPosition.y)
        {
            // [DEBUG]
            Debug.Log($"[Piston: {gameObject.name}] Reached original position. State -> Idle.");
            pressRb.MovePosition(originalPosition);
            currentState = PistonState.Idle;
        }
    }


  // PressSprite 바로 아래에 플레이어가 있는지 확인
    private bool CheckForPlayer()
    {
        if (pressCollider == null) return false;

        Vector2 rayOrigin = new Vector2(pressCollider.bounds.center.x, pressCollider.bounds.min.y);
        
        // [DEBUG] Scene 뷰에 플레이어 감지 레이저 그림 (빨간색)
        Debug.DrawRay(rayOrigin, Vector2.down * detectionRange, Color.red);
        
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, detectionRange, combinedDetectionMask);

        if (hit.collider != null)
        {
            // [DEBUG]
            Debug.Log($"[Piston CheckForPlayer: {gameObject.name}] Raycast HIT: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            return true;
        }
        return false;
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

        // [DEBUG] Scene 뷰에 바닥 감지 레이저 그림 (초록색/빨간색)
        Debug.DrawRay(rayOrigin, Vector2.down * rayDistance, hit.collider != null ? Color.green : Color.red);

        if (hit.collider != null)
        {
            // [DEBUG]
            Debug.Log($"[Piston CheckForGround: {gameObject.name}] Raycast HIT Ground: {hit.collider.name}");
            return true;
        }
        else
        {
            return false;
        }
    }


    public void Activate()
    {
        isEnabled = true;
    }

    public void Deactivate()
    {
        isEnabled = false;
        // [수정] 비활성화 시 복귀하도록 강제
        if (currentState != PistonState.Rising && currentState != PistonState.Idle)
        {
            currentState = PistonState.Rising; 
        }
    }

    // [수정] Body.cs의 HandlePistonCrush()로 로직이 이동했으므로 이 함수는 삭제
    // void OnCollisionStay2D(Collision2D collision)
    // {
    //    ...
    // }
}