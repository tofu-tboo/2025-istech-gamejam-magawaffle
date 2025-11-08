using System.Collections.Generic;
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
    [SerializeField] private BodyState _state = BodyState.playing;
    public BodyState state
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            ApplyState(value);
        }
    }

    [Header("Animation")]
    // [SerializeField] private Animator animator;
    [SerializeField] private BodyAnimator animator;
    [SerializeField] private string locomotionStateName = "walk";
    [SerializeField] private bool autoPlayAnimatorState = true;

    [SerializeField] private Rigidbody2D locomotionBody;
    // [SerializeField] private MonoBehaviour[] gameplayBehaviours;

    [Header("Ragdoll Physics Settings")]
    [SerializeField] private float ragdollGravityScale = 1f;
    [SerializeField] private float ragdollLinearDrag = 0f;
    [SerializeField] private float ragdollAngularDrag = 0.05f;

    private readonly List<Rigidbody2D> _ragdollBodies = new();
    private readonly List<Collider2D> _ragdollColliders = new();
    private readonly List<Joint2D> _ragdollJoints = new();
    private readonly HashSet<Rigidbody2D> _bodyLookup = new();
    private BodyState _appliedState = (BodyState)(-1);
    
    [Header("Movement Settings")] // (헤더 순서 변경)

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;
    private LayerMask combinedGroundCheckLayer;
    private bool isGrounded;
    private int playerLayerIndex;
    private int bodyLayerIndex;
    
    // [핵심 수정 1] Rb 속성이 'locomotionBody'를 반환하도록 수정
    public Rigidbody2D Rb => locomotionBody;


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

    private void Update()
    {
        if (Application.isPlaying) 
        {
            ApplyState(_state);
        }
    }

    // [핵심 수정 2] Body가 스스로 지면을 검사하도록 FixedUpdate 추가
    private void FixedUpdate()
    {
        // 'playing' 상태일 때만 지면 검사 (undead/dead는 래그돌)
        if (_appliedState == BodyState.playing)
        {
            CheckGround();
        }
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            // animator = GetComponentInChildren<Animator>(true);
            animator = GetComponentInChildren<BodyAnimator>(true);
        }

        if (locomotionBody == null)
        {
            locomotionBody = GetComponent<Rigidbody2D>();
        }

        _ragdollBodies.Clear();
        _ragdollColliders.Clear();
        _ragdollJoints.Clear();
        _bodyLookup.Clear();

        // Assign Rigidbodies
        var bodies = GetComponentsInChildren<Rigidbody2D>(true);
        foreach (var body in bodies)
        {
            if (locomotionBody != null && body == locomotionBody)
            {
                continue;
            }

            _ragdollBodies.Add(body);
            _bodyLookup.Add(body);
        }

        // Assign Colliders
        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.attachedRigidbody != null && _bodyLookup.Contains(collider.attachedRigidbody))
            {
                _ragdollColliders.Add(collider);
            }
        }

        // Assign Joints
        foreach (var joint in GetComponentsInChildren<Joint2D>(true))
        {
            if (joint.attachedRigidbody != null && _bodyLookup.Contains(joint.attachedRigidbody))
            {
                _ragdollJoints.Add(joint);
            }
        }
    }

    public void ApplyState(BodyState nextState, bool force = false) // 이 함수를 통해서 애니메이션 및 Ragdoll 물리가 제어됨
    {
        if (!force && nextState == _appliedState) // No 중복
        {
            return;
        }

        // [핵심 수정]
        // 상태가 'playing'이 되거나 'playing'에서 벗어날 때 (예: undead가 될 때),
        // 'isGrounded' 값을 'false'로 초기화합니다.
        // 이는 'isGrounded' 값이 갱신되지 않고(stale) 남아있어
        // 공중 점프가 되는 버그를 방지합니다.
        if (nextState == BodyState.playing || _appliedState == BodyState.playing)
        {
            isGrounded = false;
        }
        // --- 문제 1 수정 ---
        // 'undead' 상태의 Body가 'dead'가 되는 경우 (장애물 충돌 등)
        // 스스로 GameManager에 새 Body 스폰을 요청합니다.
        if (_appliedState == BodyState.undead && nextState == BodyState.dead)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpawnNewUndeadBody();
            }
        }

        _appliedState = nextState;
        var isPlaying = nextState == BodyState.playing;

        // --- 문제 2 수정 ---
        // 상태에 따라 레이어를 설정합니다.
        // (Character.cs가 아닌 Body가 직접 레이어를 관리)
        if (isPlaying)
        {
            SetLayerRecursively(playerLayerIndex);
        }
        else // 'undead' 또는 'dead' 상태
        {
            SetLayerRecursively(bodyLayerIndex);
        }

        ToggleSystems(isPlaying);
        ToggleRagdoll(!isPlaying);
    }

    private void ToggleSystems(bool enable)
    {
        if (animator != null)
        {
            animator.enabled = enable;

            if (!enable)
            {
                animator.StopAnimation();
            }
            else if (enable && autoPlayAnimatorState && !string.IsNullOrEmpty(locomotionStateName))
            {
                animator.StartAnimation(locomotionStateName);
            }
        }

        // LocomotionBody
        if (locomotionBody != null)
        {
            // [핵심 수정 3] 'playing' 상태(enable=true)일 때 Rigidbody를 활성화해야
            // Character가 제어할 수 있습니다.
            locomotionBody.simulated = enable;
        }

        // // Etc
        // (주석 처리된 코드 동일)
    }

    private void ToggleRagdoll(bool enable) // enable 여부에 따라서 흐느적거림 조정.
    {
        // [핵심 수정 1] 래그돌 자식이 없는 '단순 Body' (Square 등) 처리
        if (_ragdollBodies.Count == 0 && locomotionBody != null)
        {
            if (enable) // Dynamic (undead/dead) => 흐느적거림
            {
                locomotionBody.bodyType = RigidbodyType2D.Dynamic;
                // locomotionBody.gravityScale = ragdollGravityScale;
                locomotionBody.linearDamping = ragdollLinearDrag;
                locomotionBody.angularDamping = ragdollAngularDrag;

                // ToggleSystems(false)가 껐던 시뮬레이션을
                // 래그돌 물리(감지)를 위해 다시 켭니다.
                locomotionBody.simulated = true;
            }
            else // Kinematic (playing) => 애니메이션 제어
            {
                // 'playing' 상태로 돌아갈 때.
                // Character.cs가 PossessBody에서 Dynamic으로 바꿀 것이므로
                // 여기서는 Kinematic으로만 둡니다. (애니메이션 제어용)
                locomotionBody.bodyType = RigidbodyType2D.Kinematic;
                locomotionBody.linearVelocity = Vector2.zero;
                locomotionBody.angularVelocity = 0f;

                // 'simulated'는 ToggleSystems(true)가 true로 설정하므로
                // 여기서는 건드리지 않아도 됩니다.
            }
        }
    
        foreach (var body in _ragdollBodies)
        {
            if (body == null)
            {
                continue;
            }

            body.bodyType = enable ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            // body.gravityScale = enable ? ragdollGravityScale : 0f;
            body.linearDamping = enable ? ragdollLinearDrag : 0f;
            body.angularDamping = enable ? ragdollAngularDrag : 0f;
            body.simulated = true;
        }

        foreach (var collider in _ragdollColliders)
        {
            if (collider == null)
            {
                continue;
            }

            collider.enabled = enable;
            collider.isTrigger = !enable;
        }

        foreach (var joint in _ragdollJoints)
        {
            if (joint == null)
            {
                continue;
            }

            joint.enabled = enable;
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
        if (_appliedState == BodyState.dead)
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
    }




}