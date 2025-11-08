using UnityEngine;
using System.Collections.Generic;

public enum CharacterState
{
    moving, 
    ghost   
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

    [Header("Ghost Mode Settings")]
    [SerializeField] private float maxGhostSpeed = 5f;

    public Body currentBody { get; private set; }
    private Rigidbody2D rb; 
    private Collider2D col; 

    private Vector2 movingDirection;
    private bool jumpRequested; // 점프 요청 변수를 Character가 가집니다.
    
    public CharacterState state = CharacterState.ghost; 

    // 레이어 인덱스 변수들
    private int playerLayerIndex;
    private int ghostLayerIndex;
    private int bodyLayerIndex; 
    
    // [핵심] Body의 Rigidbody와 Movement 설정값을 Character가 캐시합니다.
    private Rigidbody2D bodyRb;
    private float maxSpeedCache;
    private float acclerationForceCache;
    private float jumpForceCache;

    private List<Body> nearbyBodies = new List<Body>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        col.isTrigger = true; 
        
        DontDestroyOnLoad(gameObject);
        
        playerLayerIndex = LayerMask.NameToLayer("Player");
        bodyLayerIndex = LayerMask.NameToLayer("Body"); 
        ghostLayerIndex = LayerMask.NameToLayer("Ghost");
        
        BecomeGhost();
    }

    void Update()
    {
        // --- 입력 감지 ---
        movingDirection = Vector2.zero;
        // Update에서 점프 요청을 받고 FixedUpdate에서 실행하므로, 매 프레임 초기화
        // FixedUpdate에서 실행될 때까지 이 값을 유지해야 하므로, 점프 키 입력시에만 true로 설정합니다.
        
        if (state == CharacterState.moving)
        {
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;

            // [NEW] 점프 입력 시 요청 변수를 true로 설정 (FixedUpdate에서 실행)
            if (Input.GetKeyDown(jumpKey))
            {
                jumpRequested = true;
            }

            if (Input.GetKeyDown(dieKey)) ReleaseBody(); 
            if (Input.GetKeyDown(killKey)) KillCurrentBody();
        }
        else if (state == CharacterState.ghost)
        {
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;
            if (Input.GetKey(upKey)) movingDirection += Vector2.up;
            if (Input.GetKey(downKey)) movingDirection += Vector2.down;
            
            if (Input.GetKeyDown(dieKey)) AttemptRePossession();
        }
    }

    void FixedUpdate()
    {
        if (state == CharacterState.moving)
        {
            if (currentBody != null && bodyRb != null)
            {
                // [핵심] Body의 Rigidbody를 직접 조작합니다.
                
                // 1. 좌우 이동
                bodyRb.AddForce(new Vector2(movingDirection.x * acclerationForceCache, 0f));
                float clampedXVelocity = Mathf.Clamp(bodyRb.linearVelocity.x, -maxSpeedCache, maxSpeedCache);
                bodyRb.linearVelocity = new Vector2(clampedXVelocity, bodyRb.linearVelocity.y);
                
                // 2. 점프 (지면 체크는 Body에게 요청)
                if (jumpRequested && currentBody.IsGrounded())
                {
                    bodyRb.linearVelocity = new Vector2(bodyRb.linearVelocity.x, 0f); 
                    bodyRb.AddForce(Vector2.up * jumpForceCache, ForceMode2D.Impulse);
                    jumpRequested = false; // 점프 실행 후 요청 초기화
                }
                else if (jumpRequested && !currentBody.IsGrounded())
                {
                    // 공중에서는 점프가 불가능하므로 요청만 해제
                    jumpRequested = false;
                }

                // 영혼의 위치를 Body에 동기화
                rb.MovePosition(currentBody.transform.position);
            }
        }
        else if (state == CharacterState.ghost)
        {
            // 유령 관성 없이 이동
            rb.linearVelocity = movingDirection.normalized * maxGhostSpeed;
        }
    }

    // --- 상태 변경 함수들 ---

    /// <summary>
    /// 빙의합니다. Body의 Rigidbody와 설정을 캐시하고, Body를 'Player' 레이어로 전환합니다.
    /// </summary>
    public void PossessBody(Body bodyToPossess)
    {
        currentBody = bodyToPossess;
        currentBody.state = BodyState.playing;
        
        // [핵심] Body의 Rigidbody와 설정값 캐시
        bodyRb = currentBody.Rb; 
        maxSpeedCache = currentBody.maxSpeed;
        acclerationForceCache = currentBody.acclerationForce;
        jumpForceCache = currentBody.jumpForce;
        
        // [레이어 전환] Body를 Player 레이어로 변경 (무한 점프 방지)
        currentBody.gameObject.layer = playerLayerIndex; 
        
        state = CharacterState.moving;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        col.enabled = false; 
        gameObject.layer = playerLayerIndex;
        transform.position = currentBody.transform.position;
        nearbyBodies.Clear();
        jumpRequested = false; // 새 몸에 빙의 시 점프 요청 초기화
    }

    /// <summary>
    /// Q키: Body를 'undead' 상태로 만들고, 'Body' 레이어로 전환합니다.
    /// </summary>
    public void ReleaseBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.undead; 
            currentBody.gameObject.layer = bodyLayerIndex; 
            currentBody = null;
            bodyRb = null; // 캐시 해제
        }
        BecomeGhost();
    }

    /// <summary>
    /// E키: Body를 'dead' 상태로 만들고, 'Body' 레이어로 전환한 후, GameManager에게 새 Body 스폰을 알립니다.
    /// </summary>
    private void KillCurrentBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.dead; 
            currentBody.gameObject.layer = bodyLayerIndex; 
            currentBody = null;
            bodyRb = null; // 캐시 해제
        }
        BecomeGhost();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnNewUndeadBody();
        }
    }

    /// <summary>
    /// 유령 상태로 전환합니다.
    /// </summary>
    private void BecomeGhost()
    {
        state = CharacterState.ghost;
        currentBody = null;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f; 
        col.enabled = true; 
        gameObject.layer = ghostLayerIndex;
        nearbyBodies.Clear();
    }
    
    /// <summary>
    /// Q키를 눌렀을 때 'undead' Body에 재빙의를 시도합니다.
    /// </summary>
    private void AttemptRePossession()
    {
        if (nearbyBodies.Count == 0) return;

        Body targetBody = null;
        foreach (Body body in nearbyBodies)
        {
            // 'undead' 상태인 Body만 재빙의 대상으로 찾습니다.
            if (body.state == BodyState.undead)
            {
                targetBody = body;
                break; 
            }
        }

        if (targetBody != null)
        {
            PossessBody(targetBody);
        }
    }

    // --- 트리거 감지 로직 ---

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != CharacterState.ghost) return;
        if (!other.CompareTag("Body")) return;
        
        Body body = other.GetComponent<Body>();
        if (body == null) return;

        // 'undead' 상태의 Body만 재빙의 후보 리스트에 추가합니다.
        if (body.state == BodyState.undead)
        {
            if (!nearbyBodies.Contains(body))
            {
                nearbyBodies.Add(body);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (state != CharacterState.ghost) return;
        if (!other.CompareTag("Body")) return;
        
        Body body = other.GetComponent<Body>();
        if (body == null) return;

        // 'undead' Body가 멀어질 때만 리스트에서 제거합니다.
        if (body.state == BodyState.undead)
        {
            if (nearbyBodies.Contains(body))
            {
                nearbyBodies.Remove(body);
            }
        }
    }
    
    /// <summary>
    /// GameManager가 퍼즐을 리셋할 때 호출 (내부 리스트 정리)
    /// </summary>
    public void ResetGhostState()
    {
        nearbyBodies.Clear();
    }
}