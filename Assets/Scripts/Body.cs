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
    [SerializeField] private Animator animator;
    [SerializeField] private string locomotionStateName = "Walk";
    [SerializeField] private bool autoPlayAnimatorState = true;

    [Header("Gameplay References")]
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

    private Rigidbody2D rb; // 이 변수는 초기화되지 않아 사용되지 않음
    private bool isGrounded;
    
    // [핵심 수정 1] Rb 속성이 'locomotionBody'를 반환하도록 수정
    public Rigidbody2D Rb => locomotionBody;


    private void Awake()
    {
        CacheReferences();
        ApplyState(_state, true);
    }

    private void OnValidate()
    {
        CacheReferences();
        ApplyState(_state, true);
    }

    private void Update()
    {
        ApplyState(_state);
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
            animator = GetComponentInChildren<Animator>(true);
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
            
            // Character.cs가 jumpRequested를 true로 갖고 있을 수 있으므로,
            // Character.cs의 PossessBody()에서 jumpRequested = false;는 필수입니다.
            // (현재 코드에 이미 구현되어 있음)
        }

        _appliedState = nextState;
        var isPlaying = nextState == BodyState.playing;

        ToggleSystems(isPlaying);
        ToggleRagdoll(!isPlaying);
    }

    private void ToggleSystems(bool enable)
    {
        if (animator != null)
        {
            animator.enabled = enable;

            if (enable && autoPlayAnimatorState && !string.IsNullOrEmpty(locomotionStateName))
            {
                Debug.Log(animator);
                
                // [수정] 게임이 실행 중(Playing)일 때만 애니메이터를 Play 합니다.
                if (Application.isPlaying && animator.gameObject.activeInHierarchy)
                {
                    animator.Play(locomotionStateName, 0, 0f);
                }
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

    // --- 기존 래그돌(복합 Body) 로직 ---
    // (이 코드는 _ragdollBodies.Count > 0 일 때만 의미가 있습니다)
    foreach (var body in _ragdollBodies)
    {
        if (enable) // Dynamic => 흐느적거림
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            // body.gravityScale = ragdollGravityScale;
            body.linearDamping = ragdollLinearDrag;
            body.angularDamping = ragdollAngularDrag;
            body.simulated = true;
        }
        else // Kinematic => Animation으로만 제어
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = false;
        }
    }

    foreach (var joint in _ragdollJoints)
    {
        joint.enabled = enable;
    }

    foreach (var collider in _ragdollColliders)
    {
        collider.enabled = enable;
    }
}

    void CheckGround()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position, 
            Vector2.down, 
            groundCheckDistance, 
            groundLayer
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
}