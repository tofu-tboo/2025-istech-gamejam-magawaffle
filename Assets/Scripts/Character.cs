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

    // [삭제] Character는 groundCheck가 필요 없음
    // [Header("Layer Settings")]
    // [SerializeField] private LayerMask groundLayer;
    // [SerializeField] private Transform groundCheck;
    // [SerializeField] private float groundCheckDistance = 0.1f;


    public Body currentBody { get; private set; }
    private Rigidbody2D rb;  // Character(영혼)의 Rigidbody
    private Collider2D col; // Character(영혼)의 Collider

    private Vector2 movingDirection;
    private bool jumpRequested; 
    
    public CharacterState state = CharacterState.ghost; 

    // 레이어 인덱스 변수들
    private int playerLayerIndex;
    private int ghostLayerIndex;
    private int bodyLayerIndex;
    
    // [추가] 제어할 Body의 Rigidbody
    private Rigidbody2D bodyRb;
    private bool isWalking;

    // [삭제] Character는 isGrounded 변수가 필요 없음

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        
        // [수정] Character의 콜라이더는 항상 Trigger여야 합니다. (Body와 충돌 방지)
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
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;

            // [수정] Body의 IsGrounded()를 사용
            if (Input.GetKeyDown(jumpKey) && currentBody != null && currentBody.IsGrounded())
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
            // [수정]
            // Q(ReleaseBody)로 인해 currentBody가 null이 되거나
            // 장애물 충돌로 bodyRb가 null이 될 수 있음
            if (currentBody == null || bodyRb == null) 
            {
                BecomeGhost(); // 안전장치
                return;
            }

            // [핵심 추가]
            // 장애물 충돌 등으로 Body가 'dead' 상태가 되었는지 확인
            if (currentBody.state == BodyState.dead)
            {
                // 2. 고스트로 전환
                BecomeGhost();
                return; // 물리 제어를 중단합니다.
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
            // 유령 모드는 'rb' (자신)를 제어
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
        }
    }

    // [추가] Character(뇌)가 Body(육체)의 위치를 따라가도록 함
    void LateUpdate()
    {
        if (state == CharacterState.moving && currentBody != null)
        {
            transform.position = currentBody.transform.position;
        }
    }

    // [삭제] Character의 CheckGround() 함수 삭제

    /// <summary>
    /// Q키: Body를 'undead' 상태로 만들고, 'Body' 레이어로 전환합니다.
    /// </summary>
    public void ReleaseBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.undead; // 레이어 자동 변경
            currentBody = null;
        }
        BecomeGhost();
    }

    /// <summary>
    /// E키: Body를 'dead' 상태로 만들고...
    /// </summary>
    private void KillCurrentBody()
    {
        if (currentBody != null)
        {
            // [수정] 부모-자식 관계가 아니므로 SetParent 필요 없음
            currentBody.state = BodyState.dead; 
            currentBody = null;
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
        bodyRb = null; // Body 제어 해제

        // Character(자신)의 Rigidbody를 유령 모드로 활성화
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic; // 혹시 모르니 Dynamic으로 명시
        rb.gravityScale = 0f; 

        // Character의 Collider는 항상 Trigger (Awake에서 설정)
        col.enabled = true; 
        
        gameObject.layer = ghostLayerIndex;
    }

    /// <summary>
    /// Q키를 눌렀을 때 'undead' Body에 재빙의를 시도합니다.
    /// </summary>
    private void AttemptRePossession()
    {
        // GameManager.GetOverlapped는 이제 [Body_오브젝트] (count: 1) 또는 [] (count: 0)을 반환합니다.
        List<Body> nearbyBodies = GameManager.Instance.GetOverlapped(rb.position, findrange, true);
        
        if (nearbyBodies.Count == 0)
        {
            Debug.Log("[Character] No bodies found.");
            return;
        }

        Body bodyToPossess = null;
        float closestDist = float.MaxValue;

        // [디버그 1] 찾은 모든 Body의 상태를 확인합니다.
        foreach (Body body in nearbyBodies)
        {
            if (body == null) continue; 

            // [핵심 디버그]
            // Q키를 눌렀을 때 이 로그가 "State is: undead"로 나와야 합니다.
            // 만약 "dead" 또는 "playing"으로 나온다면, 상태 관리 로직에 문제가 있는 것입니다.
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
            // [디버그 2] 이 로그가 나온다면, 찾은 Body가 'undead' 상태가 아니라는 뜻입니다.
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

        // 1. Character(자신)의 Rigidbody 비활성화
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;

        // 2. Character의 Collider 비활성화 (Trigger지만 꺼두는 것이 안전)
        col.enabled = false;

        gameObject.layer = playerLayerIndex;
        jumpRequested = false;

        // 3. [핵심] Body의 Rigidbody를 제어 대상으로 설정
        bodyRb = currentBody.Rb; // (Body.cs 버그 수정으로 인해 locomotionBody가 할당됨)

        if (bodyRb == null)
        {
            Debug.LogError("PossessBody: bodyRb(locomotionBody)가 null입니다! Body.cs의 Rb 속성을 확인하세요.");
            BecomeGhost(); // 빙의 실패
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
}
