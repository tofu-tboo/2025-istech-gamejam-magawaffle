using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public enum BodyState
{
    idle,
    walking,
    catching,
    throwing,
    undead,
    dead
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Body : MonoBehaviour
{
    [SerializeField] private BodyState _state = BodyState.undead;
    public BodyState state
    {
        get => _state;
        set
        {
            if (_state == value) return;
            else if (_state == BodyState.dead) return; // 아예 죽은 시체는 이용 불가.

            ApplyState(value);
        }
    }

    [Header("Animation")]
    [SerializeField] private BodyAnimator animator;

    [SerializeField] private Rigidbody2D locomotionBody;
    // [SerializeField] private MonoBehaviour[] gameplayBehaviours;

    [Header("Hand Settings")] // 들고 던지기를 위한 hand 위치 저장
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform righthand;
    
    [Header("Ragdoll Physics Settings")]
    [SerializeField] private float ragdollGravityScale = 1f;
    [SerializeField] private float ragdollLinearDrag = 0f;
    [SerializeField] private float ragdollAngularDrag = 0.05f;

    [Header("Movement Settings")] // (헤더 순서 변경)

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [Header("Interaction Settings")]
    [Tooltip("스프링과 충돌 시 받을 수직 방향의 힘")]
    [SerializeField] private float springLaunchForce = 25f;
    private LayerMask combinedGroundCheckLayer;
    private bool isGrounded = false;
    private int playerLayerIndex;
    private int bodyLayerIndex;

    // [핵심 수정 1] Rb 속성이 'locomotionBody'를 반환하도록 수정
    public Rigidbody2D Rb => locomotionBody;

    // 플레이어 빙의 상태 확인을 위한 플래그
    private bool isPlaying = false;
    public bool IsPlaying() => isPlaying;

    private bool istechtasercollide = false;
    public bool IsTechTaserCollide => istechtasercollide;


    private void Awake()
    {
        CacheReferences();// [수정] ApplyState보다 먼저 레이어를 캐시해야 합니다.
        // [수정] ApplyState보다 먼저 레이어를 캐시해야 합니다.
        playerLayerIndex = LayerMask.NameToLayer("Player");
        
        // [수정] "int"를 삭제합니다.
        // int bodyLayerIndex = LayerMask.NameToLayer("Body"); // <- (오류 원인)
        bodyLayerIndex = LayerMask.NameToLayer("Body");     // <- (수정된 코드)

        // [수정] groundLayer와 'Body' 레이어 결합
        if (bodyLayerIndex != -1)
        {
            combinedGroundCheckLayer = groundLayer | (1 << bodyLayerIndex);
        }
        else
        {
            Debug.LogWarning("Body.cs: 'Body' layer not found...");
            combinedGroundCheckLayer = groundLayer;
        }

        // ApplyState는 레이어 캐시 *이후*에 호출
        ApplyState(_state, true);
    }

    // private void Update()
    // {
    //     if (Application.isPlaying) 
    //     {
    //         ApplyState(_state);
    //     }
    // }

    // [핵심 수정 2] Body가 스스로 지면을 검사하도록 FixedUpdate 추가
    private void FixedUpdate()
    {
        // 'playing' 상태일 때만 지면 검사 (undead/dead는 래그돌)
        if (isPlaying)
        {
            CheckGround();
        }
    }

    private void CacheReferences()
    {
        // Inspector에서 설정 안 했으면 추가
        if (animator == null)
        {
            // animator = GetComponentInChildren<Animator>(true);
            animator = GetComponentInChildren<BodyAnimator>(true);
        }

        if (locomotionBody == null)
        {
            locomotionBody = GetComponent<Rigidbody2D>();
        }
    }

    private void ApplyChildrenDim(float factor)
    {
        foreach (Transform childTransform in transform)
        {
            // 루프 변수 'childTransform'은 각 자식 오브젝트의 Transform 컴포넌트입니다.
            GameObject childObject = childTransform.gameObject;
            
            // 예: 자식의 이름 출력
            Debug.Log($"자식 이름: {childObject.name}");

            // 예: 자식에게 특정 스크립트 메서드 호출
            Dimmed dimmer = childObject.GetComponent<Dimmed>();
            if (dimmer != null)
            {
                dimmer.AdjustBrightness(factor);
            }
        }
    }

    public void ApplyState(BodyState nextState, bool force = false) // 이 함수를 통해서 애니메이션 및 Ragdoll 물리가 제어됨
    {

        if (isPlaying) isGrounded = false; // 공중 점프 방지

        switch (nextState)
        {
            case BodyState.idle:
                isPlaying = true;
                animator?.StartAnimation("idle");
                isGrounded = false; // 공중 점프 방지

                ApplyChildrenDim(1.0f);
                break;
            case BodyState.walking:
                animator?.StartAnimation("walk");
                break;
            case BodyState.undead:
                isPlaying = false;
                animator?.StartAnimation("free");
                ApplyChildrenDim(0.8f);
                break;
            case BodyState.dead:
                if (_state != BodyState.undead && _state!= BodyState.dead) // 새 Body 요청
                //{
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.SpawnNewUndeadBody();
                    }
                //}
                isPlaying = false;
                animator?.StartAnimation("free");
                ApplyChildrenDim(0.4f);

                break;
            case BodyState.catching:
                //TODO: catch 로직
                animator?.StartAnimation("catch");
                break;
            case BodyState.throwing:
                //TODO: throw 로직
                animator?.StartAnimation("throw");
                break;
        }
        _state = nextState;

        // --- 문제 2 수정 ---
        // 상태에 따라 레이어를 설정합니다.
        // (Character.cs가 아닌 Body가 직접 레이어를 관리)

        if (isPlaying) // isPlaying = true로 바뀌었을 때
        {
            SetLayerRecursively(playerLayerIndex);
        }
        else // 'undead' 또는 'dead' 상태
        {
            SetLayerRecursively(bodyLayerIndex);
        }

        ToggleBodyPhysics(!isPlaying);
    }

    private void ToggleBodyPhysics(bool enable) // enable 여부에 따라서 흐느적거림 조정.
    {
        if (locomotionBody != null)
        {
            if (enable) // Dynamic (undead/dead) => 흐느적거림
            {
                // locomotionBody.bodyType = RigidbodyType2D.Dynamic;
                // locomotionBody.gravityScale = ragdollGravityScale;
                locomotionBody.linearDamping = ragdollLinearDrag;
                locomotionBody.angularDamping = ragdollAngularDrag;

                locomotionBody.GetComponent<BoxCollider2D>().isTrigger = false;
                locomotionBody.simulated = true;

                // ToggleSystems(false)가 껐던 시뮬레이션을
                // 래그돌 물리(감지)를 위해 다시 켭니다.
            }
            else // Kinematic (playing) => 애니메이션 제어
            {
                // 'playing' 상태로 돌아갈 때.
                // Character.cs가 PossessBody에서 Dynamic으로 바꿀 것이므로
                // 여기서는 Kinematic으로만 둡니다. (애니메이션 제어용)
                // locomotionBody.bodyType = RigidbodyType2D.Kinematic;
                locomotionBody.linearVelocity = Vector2.zero;
                locomotionBody.angularVelocity = 0f;

                locomotionBody.GetComponent<BoxCollider2D>().isTrigger = false;
                locomotionBody.simulated = true;
            }

        }

    }


    void CheckGround()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position, 
            Vector2.down, 
            groundCheckDistance, 
            combinedGroundCheckLayer // [수정] groundLayer 대신 combinedGroundCheckLayer 사용
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

    /// <summary>
    /// 이 게임오브젝트와 모든 자식 오브젝트의 레이어를 재귀적으로 설정합니다.
    /// </summary>
    /// <param name="newLayer">새 레이어 인덱스</param>
    public void SetLayerRecursively(int newLayer)
    {
        // 모든 자식 트랜스폼(자기 자신 포함)을 가져옵니다.
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            child.gameObject.layer = newLayer;
        }
    }
    
    /// <summary>
    /// 이 Body의 콜라이더가 다른 콜라이더와 부딪혔을 때 호출됩니다.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 이미 dead일 경우만 무시
        if (_state == BodyState.dead)
        {
            return;
        }

        // 충돌한 오브젝트가 "Obstacle" 태그를 가졌는지 확인
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            Debug.Log("Body가 Obstacle과 충돌했습니다. 'dead' 상태가 됩니다.");

            // 'E' 키를 누른 것처럼 'dead' 상태로 변경
            // 이 상태 변경은 즉시 ApplyState(dead)를 호출하여 래그돌로 만들고
            // 레이어를 'Body'로 변경합니다.
            this.state = BodyState.dead;
        }
        if (collision.gameObject.CompareTag("PistonPress"))
        {
            if (_state != BodyState.dead) // 살아있는 모든 상태
            {
                Debug.Log("Body가 PistonPress와 충돌. '소멸'합니다.");

                // 1. Character(영혼)를 즉시 Ghost로 만듦
                if (GameManager.Instance != null && GameManager.Instance.playerSoul != null)
                {
                    GameManager.Instance.playerSoul.HandleBodyDestruction();
                }

                // 2. 새 'undead' Body를 리스폰
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SpawnNewUndeadBody();
                }

                // 3. 이 Body 오브젝트 파괴
                Destroy(gameObject);
            }
            return;
        }
                if (collision.gameObject.CompareTag("Spring"))
        {
            // 'playing' (빙의) 상태이고, locomotionBody가 할당되었을 때만 작동
            if (isPlaying && locomotionBody != null)
            {
                Debug.Log("Body가 Spring과 충돌! 위로 쏩니다.");

                // '뿅' 하고 튀어 오르는 효과를 위해 Impulse(충격량) 모드로 힘을 가합니다.
                // 기존 속도를 무시하고 즉시 힘을 적용하기 위해 y 속도를 0으로 리셋 (선택 사항)
                locomotionBody.linearVelocity = new Vector2(locomotionBody.linearVelocity.x, 0f);
                
                // 위쪽으로 힘 적용
                locomotionBody.AddForce(Vector2.up * springLaunchForce, ForceMode2D.Impulse);
            }
            return; // 충돌 처리 완료
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 충돌한 Trigger가 "Techtaser" 태그를 가졌는지 확인
        if (other.gameObject.CompareTag("Techtaser"))
        {
            istechtasercollide = true;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
    // 빠져나온 Trigger가 "Techtaser" 태그를 가졌는지 확인
    if (other.gameObject.CompareTag("Techtaser"))
        {
        istechtasercollide = false;
        }
    }



}
