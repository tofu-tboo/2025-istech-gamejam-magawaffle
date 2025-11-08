using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight procedural animator that lerps registered rigidbodies toward caller-defined target poses.
/// - Each body part stores its origin (local position/rotation) captured on Awake.
/// - Call <see cref="SetTargetPose"/> to define the desired local pose for a part.
/// - Invoke <see cref="Play(float)"/> to smoothly MovePosition/MoveRotation every part toward its target.
/// - Use <see cref="ResetTargetsToOrigins"/> and <see cref="SnapToTargets"/> as needed.
/// - <see cref="StartAnimation"/>/<see cref="StopAnimation"/> provide the same interface as the previous version.
/// </summary>
public class BodyAnimator : MonoBehaviour
{
    [Serializable]
    public class BodyPart
    {
        public string name;
        public Rigidbody2D body;

        [HideInInspector] public Vector3 originLocalPosition;
        [HideInInspector] public Quaternion originLocalRotation;
        [HideInInspector] public Vector3 targetLocalPosition;
        [HideInInspector] public Quaternion targetLocalRotation;

        public Transform Transform => body != null ? body.transform : null;

        public void CaptureOrigin()
        {
            if (Transform == null)
            {
                return;
            }

            Debug.Log("test: " + body.name + " / " + Transform.localPosition);

            originLocalPosition = Transform.localPosition;
            originLocalRotation = Transform.localRotation;
            targetLocalPosition = originLocalPosition;
            targetLocalRotation = originLocalRotation;
        }
    }

    [SerializeField] private List<BodyPart> parts = new();
    [SerializeField] private float defaultDuration = 0.35f;
    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly Dictionary<string, BodyPart> _lookup = new(StringComparer.OrdinalIgnoreCase);
    private Coroutine _lerpRoutine;

    private void Awake()
    {
        BuildLookup();
        CaptureAllOrigins();
    }

    private void BuildLookup()
    {
        _lookup.Clear();
        foreach (var part in parts)
        {
            if (part == null || part.body == null || part.Transform == null)
            {
                continue;
            }

            if (!_lookup.ContainsKey(part.name))
            {
                _lookup.Add(part.name, part);
            }
        }
    }

    public void CaptureAllOrigins()
    {
        foreach (var part in parts)
        {
            part?.CaptureOrigin();
        }
    }

    // 삭제 금지: 추후 사용될 수도 있음.
    // public void ResetTargetsToOrigins()
    // {
    //     foreach (var part in parts)
    //     {
    //         if (part == null)
    //         {
    //             continue;
    //         }

    //         part.targetLocalPosition = part.originLocalPosition;
    //         part.targetLocalRotation = part.originLocalRotation;
    //     }
    // }

    public void SetTargetPose(string partName, Vector3 localPosition, Quaternion localRotation)
    {
        if (!_lookup.TryGetValue(partName, out var part) || part == null)
        {
            Debug.LogWarning($"BodyAnimator: part '{partName}' not found.");
            return;
        }

        part.targetLocalPosition = localPosition;
        part.targetLocalRotation = localRotation;
    }

    public void SnapToTargets() // 즉시 transform 적용.
    {
        foreach (var part in parts)
        {
            if (part?.Transform == null)
            {
                continue;
            }

            ApplyLocalPose(part, part.targetLocalPosition, part.targetLocalRotation);
        }
    }

    public void StartAnimation(string animationName, float duration = -1f)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        switch (animationName?.ToLowerInvariant())
        {
            case "idle":
            case "standing":
            case "wait":
                foreach (var partName in new string[] { "Head", "Torso", "Pelvis", "LegLU", "LegRU", "LegLD", "LegRD" })
                {
                    var part = _lookup[partName];
                    part.body.bodyType = RigidbodyType2D.Kinematic;
                    SetTargetPose(partName, part.originLocalPosition, part.originLocalRotation);
                    part.body.gravityScale = 0.0f;

                }
                foreach (var part in parts)
                {
                    part.body.GetComponent<BoxCollider2D>().isTrigger = true;
                }
                break;

            case "walk":
            case "walking":
            case "walkcycle":
                break;

            case "free":
            case "freemove":
            case "explore":
                StopAnimation();
                foreach (var part in parts)
                {
                    part.body.GetComponent<BoxCollider2D>().isTrigger = false;
                    part.body.bodyType = RigidbodyType2D.Dynamic; // freebody
                    part.body.gravityScale = 1.0f;
                }
                break;
            
            case "catch":
            case "catching":
            case "receive":
                break;

            case "throw":
            case "throwing":
            case "toss":
                break;
            default:
                break;
        }

        Play(duration);
    }

    public void StopAnimation()
    {
        if (_lerpRoutine != null)
        {
            StopCoroutine(_lerpRoutine);
            _lerpRoutine = null;
        }
    }

    public void Play(float duration = -1f)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (duration <= 0f)
        {
            duration = defaultDuration;
        }

        if (_lerpRoutine != null)
        {
            StopCoroutine(_lerpRoutine);
        }

        _lerpRoutine = StartCoroutine(LerpToTargets(duration));
    }

    private IEnumerator LerpToTargets(float duration)
    {
        var startPos = new Dictionary<BodyPart, Vector3>(parts.Count);
        var startRot = new Dictionary<BodyPart, Quaternion>(parts.Count);

        foreach (var part in parts)
        {
            if (part?.Transform == null)
            {
                continue;
            }

            startPos[part] = part.Transform.localPosition;
            startRot[part] = part.Transform.localRotation;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased = easingCurve.Evaluate(t);

            foreach (var part in parts)
            {
                if (part == null || part.Transform == null || !startPos.ContainsKey(part))
                {
                    continue;
                }

                Vector3 pos = Vector3.Lerp(startPos[part], part.targetLocalPosition, eased);
                Quaternion rot = Quaternion.Slerp(startRot[part], part.targetLocalRotation, eased);
                ApplyLocalPose(part, pos, rot);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (var part in parts)
        {
            if (part == null || part.Transform == null)
            {
                continue;
            }

            ApplyLocalPose(part, part.targetLocalPosition, part.targetLocalRotation);
        }

        _lerpRoutine = null;
    }

    private void ApplyLocalPose(BodyPart part, Vector3 localPosition, Quaternion localRotation)
    {
        if (part.body == null || part.Transform == null)
        {
            return;
        }

        var parent = part.Transform.parent;
        if (parent == null)
        {
            part.body.MovePosition(localPosition);
            part.body.MoveRotation(localRotation.eulerAngles.z);
            part.Transform.localPosition = localPosition;
            part.Transform.localRotation = localRotation;
            return;
        }

        Vector3 worldPosition = parent.TransformPoint(localPosition);
        Quaternion worldRotation = parent.rotation * localRotation;

        part.body.MovePosition(worldPosition);
        part.body.MoveRotation(worldRotation.eulerAngles.z);

        part.Transform.localPosition = localPosition;
        part.Transform.localRotation = localRotation;
    }
}
