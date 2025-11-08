using UnityEngine;
using System.Collections.Generic;

public enum CharacterState
{
    moving, // '육체'에 빙의 중 (Body가 Player 레이어)
    ghost   // '유령' 상태 (Character가 Ghost 레이어)
}

[RequireComponent(typeof(Rigidbody2D))] // 유령 모드용 Rigidbody
[RequireComponent(typeof(Collider2D))]  // 유령 모드용 Collider
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
    
    public CharacterState state = CharacterState.ghost; 

    // 레이어 인덱스 변수들
    private int playerLayerIndex;
    private int ghostLayerIndex;
    private int bodyLayerIndex; 

    // 'undead' 상태의 재빙의 가능 Body 리스트
    private List<Body> nearbyBodies = new List<Body>();
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        col.isTrigger = true; 
        
        DontDestroyOnLoad(gameObject);
        
        // 유니티 에디터에서 "Player", "Body", "Ghost" 레이어를 만들어야 합니다.
        playerLayerIndex = LayerMask.NameToLayer("Player");
        bodyLayerIndex = LayerMask.NameToLayer("Body"); 
        ghostLayerIndex = LayerMask.NameToLayer("Ghost");
        
        BecomeGhost();
    }

    void Update()
    {
        // --- 입력 감지 ---
        movingDirection = Vector2.zero;

        if (state == CharacterState.moving) // '빙의' 상태일 때
        {
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;

            // 점프 (Body에게 명령)
            if (Input.GetKeyDown(jumpKey))
            {
                if (currentBody != null) currentBody.RequestJump();
            }

            // '빙의 해제' (Q) -> undead (재빙의 가능)
            if (Input.GetKeyDown(dieKey))
            {
                ReleaseBody(); 
            }
            
            // '육체 버리기' (E) -> dead (재빙의 불가능 + 새 Body 스폰)
            if (Input.GetKeyDown(killKey))
            {
                KillCurrentBody();
            }
        }
        else if (state == CharacterState.ghost) // '유령' 상태일 때
        {
            if (Input.GetKey(leftKey)) movingDirection += Vector2.left;
            if (Input.GetKey(rightKey)) movingDirection += Vector2.right;
            if (Input.GetKey(upKey)) movingDirection += Vector2.up;
            if (Input.GetKey(downKey)) movingDirection += Vector2.down;

            // '재빙의' 시도 (Q)
            if (Input.GetKeyDown(dieKey))
            {
                AttemptRePossession();
            }
        }
    }

    void FixedUpdate()
    {
        if (state == CharacterState.moving)
        {
            if (currentBody != null)
            {
                // Body에게 이동 명령 전달
                currentBody.Move(movingDirection);
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
    /// GameManager가 호출: 지정된 'Body'에 빙의합니다. Body를 'Player' 레이어로 전환합니다.
    /// </summary>
    public void PossessBody(Body bodyToPossess)
    {
        currentBody = bodyToPossess;
        currentBody.state = BodyState.playing;
        
        // [레이어 전환] Body를 Player 레이어로 변경 (무한 점프 방지)
        currentBody.gameObject.layer = playerLayerIndex; 
        
        state = CharacterState.moving;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        col.enabled = false; 
        gameObject.layer = playerLayerIndex;
        transform.position = currentBody.transform.position;
        nearbyBodies.Clear();
    }

    /// <summary>
    /// Q키: Body를 'undead' 상태로 만들고, 'Body' 레이어로 전환합니다.
    /// </summary>
    public void ReleaseBody()
    {
        if (currentBody != null)
        {
            currentBody.state = BodyState.undead; 
            // [레이어 전환] Body를 Body 레이어로 변경 (밟을 수 있는 발판화)
            currentBody.gameObject.layer = bodyLayerIndex; 
            currentBody = null;
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
            // [레이어 전환] Body를 Body 레이어로 변경 (밟을 수 있는 발판화)
            currentBody.gameObject.layer = bodyLayerIndex; 
            currentBody = null;
        }
        BecomeGhost();

        // GameManager에게 새 Body를 리스폰하라고 알림 (단 1회)
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