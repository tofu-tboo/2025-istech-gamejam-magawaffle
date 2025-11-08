using System.Collections.Generic;
using UnityEngine;

public enum BodyState
{
    playing,
    undead,
    dead
}

/// <summary>
/// Switches between animated gameplay mode and physics-driven ragdoll mode.
/// </summary>
public class Body : MonoBehaviour
{
    public BodyState state = BodyState.playing;

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

    private void Reset()
    {
        CacheReferences();
        ApplyState(state, true);
    }

    private void Awake()
    {
        CacheReferences();
        ApplyState(state, true);
    }

    private void OnValidate()
    {
        CacheReferences();
        ApplyState(state, true);
    }

    private void Update()
    {
        ApplyState(state);
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

    private void ApplyState(BodyState nextState, bool force = false) // 이 함수를 통해서 애니메이션 및 Ragdoll 물리가 제어됨
    {
        if (!force && nextState == _appliedState) // No 중복
        {
            return;
        }

        _appliedState = nextState;
        var isPlaying = nextState == BodyState.playing;

        ToggleGameplaySystems(isPlaying);
        ToggleRagdoll(!isPlaying);
    }

    private void ToggleGameplaySystems(bool enableGameplay)
    {
        // Animator
        if (animator != null)
        {
            animator.enabled = enableGameplay;

            if (enableGameplay && autoPlayAnimatorState && !string.IsNullOrEmpty(locomotionStateName))
            {
                animator.Play(locomotionStateName, 0, 0f);
            }
        }

        // LocomotionBody
        if (locomotionBody != null)
        {
            // 이동 시에는 물리 적용 X
            locomotionBody.simulated = enableGameplay;
            if (!enableGameplay)
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
        //             behaviour.enabled = enableGameplay;
        //         }
        //     }
        // }
    }

    private void ToggleRagdoll(bool enable) // enable 여부에 따라서 흐느적거림 조정.
    {
        foreach (var body in _ragdollBodies)
        {
            if (enable) // Dynamic => 흐느적거림
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = ragdollGravityScale;
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
}
