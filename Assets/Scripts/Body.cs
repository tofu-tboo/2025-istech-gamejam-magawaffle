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
    [Header("Movement Settings")]

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;

    private Rigidbody2D rb;
    private bool isGrounded;
    
    public Rigidbody2D Rb => rb; // Character가 Rigidbody에 접근 가능


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

        _appliedState = nextState;
        var isPlaying = nextState == BodyState.playing;

        ToggleSystems(isPlaying);
        ToggleRagdoll(!isPlaying);
    }

    private void ToggleSystems(bool enable)
    {
        // Animator
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
            // 이동 시에는 물리 적용 X
            locomotionBody.simulated = enable;
            if (!enable)
            {
                locomotionBody.linearVelocity = Vector2.zero;
                locomotionBody.angularVelocity = 0f;
            }
        }

        // // Etc
        // if (gameplayBehaviours != null)
        // {
        //     foreach (var behaviour in gameplayBehaviours)
        //     {
        //         if (behaviour != null)
        //         {
        //             behaviour.enabled = enable;
        //         }
        //     }
        // }
    }

    private void ToggleRagdoll(bool enable)
    {
        foreach (var body in _ragdollBodies)
        {
            if (body == null)
            {
                continue;
            }

            body.bodyType = enable ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            body.gravityScale = enable ? ragdollGravityScale : 0f;
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
}
