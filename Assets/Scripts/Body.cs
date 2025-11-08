using UnityEngine;

public enum BodyState
{
    playing,
    undead,
    dead
}

public class Body : MonoBehaviour
{
    public BodyState state = BodyState.playing;

    [Header("Ragdoll Link")]
    [SerializeField] private ProceduralRagdoll2D ragdoll;
    [SerializeField] private Transform centerOverride;

    public Transform Center => centerOverride != null
        ? centerOverride
        : ragdoll != null ? ragdoll.CenterTransform : null;

    public Rigidbody2D CenterRigidbody => ragdoll != null ? ragdoll.PrimaryRigidbody : null;

    private void Reset()
    {
        AutoAssignRagdoll();
    }

    private void Awake()
    {
        AutoAssignRagdoll();
        EnsureCenterOverride();
    }

    private void OnValidate()
    {
        AutoAssignRagdoll();
        EnsureCenterOverride();
    }

    private void AutoAssignRagdoll()
    {
        if (ragdoll == null)
        {
            ragdoll = GetComponent<ProceduralRagdoll2D>();
        }

        if (ragdoll == null)
        {
            ragdoll = GetComponentInChildren<ProceduralRagdoll2D>();
        }
    }

    private void EnsureCenterOverride()
    {
        if (centerOverride == null && ragdoll != null)
        {
            centerOverride = ragdoll.CenterTransform;
        }
    }
}
