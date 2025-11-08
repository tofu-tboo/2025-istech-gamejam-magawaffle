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

    private Vector2 movingDirection;
    private bool jumpRequested; 
    
    public CharacterState state = CharacterState.ghost; 

    // 레이어 인덱스 변수들
    private int playerLayerIndex;
    private int ghostLayerIndex;
    private int bodyLayerIndex;
    
    private Rigidbody2D bodyRb;
    private bool isWalking;

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
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;

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
            
            if (Input.GetKeyDown(dieKey)) AttemptRePossession();
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
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SpawnNewUndeadBody();
                }

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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnNewUndeadBody();
        }
    }
    
    private void BecomeGhost()
    {
        state = CharacterState.ghost;
        currentBody = null;
        bodyRb = null; 

        // [수정] 영혼 시각 요소 활성화
        if (spriteRenderer != null) spriteRenderer.enabled = true;
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

        col.enabled = false; 

        // [수정] 영혼 시각 요소 비활성화 및 애니메이터 리셋
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (animator != null) 
        {
            animator.enabled = false;
            animator.SetFloat("MoveX", 0f); // 'PlayerIdle' 상태로 리셋
        }

        gameObject.layer = playerLayerIndex;
        jumpRequested = false;

        bodyRb = currentBody.Rb; 

        if (bodyRb == null)
        {
            Debug.LogError("PossessBody: bodyRb(locomotionBody)가 null입니다! Body.cs의 Rb 속성을 확인하세요.");
            BecomeGhost(); 
            return;
        }
        
        bodyRb.bodyType = RigidbodyType2D.Dynamic; 
        bodyRb.gravityScale = gravityScale;

        currentBody.state = BodyState.idle; 
    }
}