using UnityEngine;
using System.Collections.Generic;

public enum CharacterState
{
    moving,
    ghost
}

public enum MovementType
{
    smooth,
    instant
}

[RequireComponent(typeof(Rigidbody2D))] 
[RequireComponent(typeof(Collider2D))]  
public class Character : MonoBehaviour
{
    [Header("Key Settings")]
    public KeyCode upKey = KeyCode.W;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode downKey = KeyCode.S;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dieKey = KeyCode.Q;     // '빙의 해제/재빙의' (undead, Body를 Body 레이어로 전환)
    public KeyCode killKey = KeyCode.E;    // '육체 버리기' (dead, Body를 Body 레이어로 전환 및 새 Body 스폰 요청)

    [Header("Game Settings")]
    [SerializeField] private float findrange = 10f;

    [Header("Movement Settings")]
    [SerializeField] private MovementType movetype = MovementType.instant;
    public float maxSpeed = 3f;
    [SerializeField] private float acclerationForce = 100f;
    public float instantSpeed = 3f;
    public float gravityScale = 3f;
    public float jumpForce = 20f;

    [Header("Ghost Mode Settings")]
    [SerializeField] private MovementType ghostMoveType = MovementType.smooth;
    public float ghostMaxSpeed = 4f;
    [SerializeField] private float ghostAcclerationForce = 100f;
    public float ghostInstantSpeed = 4f;

    [Header("Soul Visuals")] // [추가] 영혼 시각적 요소를 위한 헤더
    [SerializeField] private SpriteRenderer spriteRenderer; // [추가] 영혼 스프라이트
    [SerializeField] private Animator animator;               // [추가] 영혼 애니메이터

    public Body currentBody { get; private set; }
    private Rigidbody2D rb;  // Character(영혼)의 Rigidbody
    private Collider2D col; // Character(영혼)의 Collider
    private Transform currentCapsule = null;

    private Vector2 movingDirection;
    private bool jumpRequested; 
    
    public CharacterState state = CharacterState.ghost; 

    // 레이어 인덱스 변수들
    private int playerLayerIndex;
    private int ghostLayerIndex;
    private int bodyLayerIndex;

    private Rigidbody2D bodyRb;

    private bool _isWalking = false;
    private bool isWalking
    {
        get => _isWalking;
        set
        {
            if (_isWalking != value) // Toggle
            {
                _isWalking = value;

                if (value)
                {
                    currentBody.state = BodyState.walking;
                }
                else
                {
                    currentBody.state = BodyState.idle;
                }
            }
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        
        // [추가] 시각적 요소 컴포넌트 가져오기
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        
        col.isTrigger = true; 
        
        DontDestroyOnLoad(gameObject);
        
        playerLayerIndex = LayerMask.NameToLayer("Player");
        bodyLayerIndex = LayerMask.NameToLayer("Body"); 
        ghostLayerIndex = LayerMask.NameToLayer("Ghost");
        
        BecomeGhost();
    }

    void Update()
    {
        movingDirection = Vector2.zero;
        
        if (state == CharacterState.moving)
        {
            if (!Input.GetKey(leftKey) && !Input.GetKey(rightKey)) // isWalking toggle
                isWalking = false;

            if (Input.GetKey(leftKey))
            {
                movingDirection += Vector2.left;
                isWalking = true; // isWalking toggle
            }
            if (Input.GetKey(rightKey))
            {
                movingDirection += Vector2.right;
                isWalking = true; // isWalking toggle
            }

            if (Input.GetKeyDown(jumpKey) && currentBody != null && currentBody.IsGrounded())
            {
                jumpRequested = true;
            }

            if (Input.GetKeyDown(dieKey) && currentBody != null && currentBody.IsTechTaserCollide)
            {
                ReleaseBody(); 
            }
            if (Input.GetKeyDown(killKey)) KillCurrentBody();
        }
        else if (state == CharacterState.ghost)
        {
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;
            if (Input.GetKey(upKey)) movingDirection += Vector2.up;
            if (Input.GetKey(downKey)) movingDirection += Vector2.down;
            
            if (Input.GetKeyDown(dieKey))
            {
                AttemptRePossession();
                if (currentCapsule != null)
                {
                    GameManager.Instance.SpawnAndPossessBody(currentCapsule.position);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (state == CharacterState.moving)
        {
            if (currentBody == null || bodyRb == null) 
            {
                BecomeGhost(); 
                return;
            }

            if (currentBody.state == BodyState.dead)
            {
                // 2. 고스트로 전환
                BecomeGhost();
                return; 
            }
            
            if (movetype == MovementType.instant)
            {
                bodyRb.linearVelocity = new Vector2(movingDirection.normalized.x * instantSpeed, bodyRb.linearVelocity.y);
            }
            else if (movetype == MovementType.smooth)
            {
                bodyRb.AddForce(new Vector2(movingDirection.x * acclerationForce, 0f));
                float clampedVelocity = Mathf.Clamp(bodyRb.linearVelocity.x, -maxSpeed, maxSpeed);
                bodyRb.linearVelocity = new Vector2(clampedVelocity, bodyRb.linearVelocity.y);
            }

            if (jumpRequested)
            {
                bodyRb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                jumpRequested = false;
            }
        }
        else if (state == CharacterState.ghost)
        {
            // --- 유령 물리 이동 로직 ---
            if (ghostMoveType == MovementType.instant)
            {
                rb.linearVelocity = movingDirection.normalized * ghostInstantSpeed;
            }
            else if (ghostMoveType == MovementType.smooth)
            {
                rb.AddForce(movingDirection.normalized * ghostAcclerationForce);
                if (rb.linearVelocity.magnitude > ghostMaxSpeed)
                {
                    rb.linearVelocity = rb.linearVelocity.normalized * ghostMaxSpeed;
                }
            }
            
            // --- [핵심 수정] 애니메이터 제어 로직 ---
            // Animator에 현재 수평 입력(-1 ~ 1) 값을 전달합니다.
            // Animator Controller가 이 'MoveX' 값을 보고 
            // 'PlayerIdle', 'PlayerSoulLeft', 'PlayerSoulRight' 상태를 자동으로 전환합니다.
            if (animator != null)
            {
                animator.SetFloat("MoveX", movingDirection.x);
            }
        }
    }

    void LateUpdate()
    {
        if (state == CharacterState.moving && currentBody != null)
        {
            transform.position = currentBody.transform.position;
        }
    }
    
    public void ReleaseBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.undead; 
            currentBody.SetLayerRecursively(bodyLayerIndex); 
            currentBody = null;
        }
        BecomeGhost();
    }
    
    private void KillCurrentBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.dead; 
            currentBody = null;
        }
        BecomeGhost();

       /* if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnNewUndeadBody();
        }*/
    }
    
    private void BecomeGhost()
    {
        state = CharacterState.ghost;
        currentBody = null;
        bodyRb = null;

        // [수정] 영혼 시각 요소 활성화
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = 1;
        }
        if (animator != null) animator.enabled = true; 

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic; 
        rb.gravityScale = 0f; 

        col.enabled = true; 
        
        gameObject.layer = ghostLayerIndex;
    }
    
    private void AttemptRePossession()
    {
        List<Body> nearbyBodies = GameManager.Instance.GetOverlapped(rb.position, findrange, true);
        
        if (nearbyBodies.Count == 0)
        {
            Debug.Log("[Character] No bodies found.");
            return;
        }

        Body bodyToPossess = null;
        float closestDist = float.MaxValue;
        
        foreach (Body body in nearbyBodies)
        {
            if (body == null) continue; 

            Debug.Log($"[Character] Checking body '{body.gameObject.name}'. State is: {body.state}");

            if (body.state == BodyState.undead)
            {
                Debug.Log($"[Character] Found valid 'undead' body: {body.gameObject.name}");
                float dist = (body.transform.position - transform.position).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bodyToPossess = body;
                }
            }
        }

        if (bodyToPossess != null)
        {
            Debug.Log($"[Character] Possessing: {bodyToPossess.gameObject.name}");
            PossessBody(bodyToPossess);
        }
        else
        {
            Debug.Log("[Character] Failed to find any 'undead' bodies in the list.");
        }
    }

    public void PossessBody(Body newBody)
    {
        if (newBody == null)
        {
            Debug.LogError("newBody is null.");
            return;
        }

        currentBody = newBody;
        state = CharacterState.moving;

        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;

        if (spriteRenderer != null) 
        {
            spriteRenderer.enabled = false; // 빙의 시 영혼 스프라이트 비활성화
            spriteRenderer.sortingOrder = -2; // [추가] 빙의 시 Order in Layer를 -2로 설정
        }
        // 2. Character의 Collider 비활성화 (Trigger지만 꺼두는 것이 안전)
        col.enabled = false;

        gameObject.layer = playerLayerIndex;
        jumpRequested = false;

        bodyRb = currentBody.Rb; 

        if (bodyRb == null)
        {
            Debug.LogError("PossessBody: bodyRb(locomotionBody)가 null입니다! Body.cs의 Rb 속성을 확인하세요.");
            BecomeGhost(); 
            return;
        }

        // 4. Body의 Rigidbody에 플레이어 설정 적용
        // [핵심 수정 2] Body가 Kinematic(래그돌 해제) 상태일 수 있으므로
        // 물리 제어를 위해 반드시 Dynamic으로 설정합니다
        bodyRb.bodyType = RigidbodyType2D.Dynamic;
        bodyRb.gravityScale = gravityScale;

        // 5. Body 상태 및 레이어 설정
        // (GameManager가 이미 playing으로 설정했더라도, 재빙의 시 필요)
        currentBody.state = BodyState.idle; // 자동으로 하위 오브젝트들의 레이어 설정.

        // 6. Character 위치를 Body 위치로 즉시 동기화
        transform.position = currentBody.transform.position;

        // [삭제] SetParent 제거
        // currentBody.transform.SetParent(this.transform, true);
        // currentBody.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Body가 '소멸'될 때(예: 피스톤) 호출됩니다.
    /// currentBody에 접근하지 않고 즉시 고스트 상태로 전환합니다.
    /// </summary>
    public void HandleBodyDestruction()
    {
        Debug.Log("Body가 소멸되어 강제로 고스트가 됩니다.");

        // private인 BecomeGhost() 함수를 호출하여
        // currentBody = null, bodyRb = null 등을 처리하고
        // 고스트 물리 상태로 전환합니다.
        BecomeGhost();
    }
        private void OnTriggerEnter2D(Collider2D other)
    {
        // 진입한 Trigger가 "Capsule" 태그인지 확인
        if (other.gameObject.CompareTag("Capsule"))
        {
            Debug.Log("Character(Ghost)가 배양기(Capsule)에 진입.");
            currentCapsule = other.transform; // 배양기 위치 저장
        }
    }

    /// <summary>
    /// [추가] Character(영혼)의 Trigger가 다른 Collider에서 빠져나왔을 때
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        // 이탈한 Trigger가 "Capsule" 태그인지 확인
        if (other.gameObject.CompareTag("Capsule"))
        {
            Debug.Log("Character(Ghost)가 배양기(Capsule)에서 이탈.");
            currentCapsule = null; // 배양기 위치 정보 삭제
        }
    }
}
