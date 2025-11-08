using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural animator that swings arms/legs around their attachment pivots (Torso/Pelvis)
/// while every limb rigidbody remains kinematic. Used only in BodyState.playing.
/// </summary>
public class BodyAnimator : MonoBehaviour
{
    [Header("Core Transforms")]
    [SerializeField] private Transform torso;
    [SerializeField] private Transform pelvis;
    [SerializeField] private Transform head;

    [Header("Upper Limbs")]
    [SerializeField] private Transform armLU;
    [SerializeField] private Transform armLD;
    [SerializeField] private Transform armRU;
    [SerializeField] private Transform armRD;

    [Header("Lower Limbs")]
    [SerializeField] private Transform legLU;
    [SerializeField] private Transform legLD;
    [SerializeField] private Transform legRU;
    [SerializeField] private Transform legRD;

    [Header("Walk Arc Settings")]
    [SerializeField] private float armAmplitude = 30f;
    [SerializeField] private float legAmplitude = 18f;
    [SerializeField] private float walkFrequency = 4f;
    [SerializeField] private float armYOffset = 0.15f;

    private readonly List<LimbArc> _limbArcs = new();
    private Quaternion _torsoBaseRot;
    private Quaternion _pelvisBaseRot;
    private Vector3 _headBasePos = new Vector3(0f, 1.4f, 0f);
    private Quaternion _headBaseRot;
    private Coroutine _currentRoutine;

    private void Awake()
    {
        CacheTransforms();
        _torsoBaseRot = torso != null ? torso.localRotation : Quaternion.identity;
        _pelvisBaseRot = pelvis != null ? pelvis.localRotation : Quaternion.identity;
        _headBasePos = head != null ? head.localPosition : new Vector3(0f, 1.4f, 0f);
        _headBaseRot = head != null ? head.localRotation : Quaternion.identity;
    }

    private void OnEnable()
    {
        ResetPose();
    }

    private void OnDisable()
    {
        StopCurrentRoutine();
        ResetPose();
    }

    private void CacheTransforms()
    {
        var all = GetComponentsInChildren<Transform>(true);
        torso ??= FindTransform(all, "Torso");
        pelvis ??= FindTransform(all, "Pelvis");
        head ??= FindTransform(all, "Head");
        armLU ??= FindTransform(all, "ArmLU");
        armRU ??= FindTransform(all, "ArmRU");
        legLU ??= FindTransform(all, "LegLU");
        legRU ??= FindTransform(all, "LegRU");

        _limbArcs.Clear();
        var armOffset = new Vector3(armYOffset, 0f, 0f);
        RegisterArcLimb(torso, armLU, armLD, armAmplitude, 0f, 90f, armOffset);
        RegisterArcLimb(torso, armRU, armRD, armAmplitude, Mathf.PI, -90f, -armOffset);
        RegisterArcLimb(pelvis, legLU, legLD, legAmplitude, Mathf.PI, 0f, Vector3.zero);
        RegisterArcLimb(pelvis, legRU, legRD, legAmplitude, 0f, 0f, Vector3.zero);
    }

    private Transform FindTransform(Transform[] all, string name)
    {
        foreach (var t in all)
        {
            if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private void ResetPose()
    {
        if (torso != null)
        {
            torso.localRotation = _torsoBaseRot;
        }

        if (pelvis != null)
        {
            pelvis.localRotation = _pelvisBaseRot;
        }

        if (head != null)
        {
            head.localPosition = _headBasePos;
            head.localRotation = _headBaseRot;
        }

        foreach (var arc in _limbArcs)
        {
            arc.Reset();
        }
    }

    public void StartAnimation(string animationName)
    {
        if (!enabled)
        {
            return;
        }

        StopCurrentRoutine();

        switch (animationName.ToLowerInvariant())
        {
            case "walk":
            case "walking":
            case "walkcycle":
            
                _currentRoutine = StartCoroutine(WalkRoutine());
                break;
            default:
                break;
        }
    }

    public void StopAnimation()
    {
        StopCurrentRoutine();
        ResetPose();
    }

    private void StopCurrentRoutine()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
        }
    }

    private IEnumerator WalkRoutine()
    {
        float time = 0f;

        while (true)
        {
            time += Time.deltaTime * walkFrequency;

            foreach (var arc in _limbArcs)
            {
                arc.Apply(time);
            }

            yield return null;
        }
    }

    private void RegisterArcLimb(
        Transform pivot,
        Transform primary,
        Transform secondary,
        float amplitudeDeg,
        float phase,
        float baseOffset = 0f,
        Vector3 localYOffset = default)
    {
        if (pivot == null || primary == null)
        {
            return;
        }

        var arc = new LimbArc(pivot, amplitudeDeg, phase, baseOffset);
        arc.AddSegment(primary, localYOffset);
        if (secondary != null)
        {
            arc.AddSegment(secondary, localYOffset);
        }

        _limbArcs.Add(arc);
    }

    [Serializable]
    private class LimbArc
    {
        private readonly Transform _pivot;
        private readonly float _amplitude;
        private readonly float _phase;
        private readonly Quaternion _baseOffset;
        private readonly List<SegmentData> _segments = new();

        public LimbArc(Transform pivot, float amplitude, float phase, float baseOffsetDeg)
        {
            _pivot = pivot;
            _amplitude = amplitude;
            _phase = phase;
            _baseOffset = Quaternion.Euler(0f, 0f, baseOffsetDeg);
        }

        public void AddSegment(Transform target, Vector3 localOffset)
        {
            if (target == null || _pivot == null)
            {
                return;
            }

            var pivotRot = _pivot.localRotation;
            var pivotPos = _pivot.localPosition;

            var relativePos = Quaternion.Inverse(pivotRot) * (target.localPosition - pivotPos) + localOffset;
            var relativeRot = Quaternion.Inverse(pivotRot) * target.localRotation;

            _segments.Add(new SegmentData(target, relativePos, relativeRot));
        }

        public void Reset()
        {
            if (_pivot == null)
            {
                return;
            }

            var pivotRot = _pivot.localRotation;
            var pivotPos = _pivot.localPosition;

            foreach (var segment in _segments)
            {
                if (segment.Target == null)
                {
                    continue;
                }

                segment.Target.localPosition = pivotPos + pivotRot * segment.RelPos;
                segment.Target.localRotation = pivotRot * segment.RelRot;
            }
        }

        public void Apply(float time)
        {
            if (_pivot == null)
            {
                return;
            }

            float angle = Mathf.Sin(time + _phase) * _amplitude;
            var arcRotation = _baseOffset * Quaternion.Euler(0f, 0f, angle);
            var pivotRot = _pivot.localRotation;
            var pivotPos = _pivot.localPosition;

            foreach (var segment in _segments)
            {
                if (segment.Target == null)
                {
                    continue;
                }

                var rotatedOffset = arcRotation * segment.RelPos;
                var rotatedRot = arcRotation * segment.RelRot;

                segment.Target.localPosition = pivotPos + pivotRot * rotatedOffset;
                segment.Target.localRotation = pivotRot * rotatedRot;
            }
        }

        private readonly struct SegmentData
        {
            public readonly Transform Target;
            public readonly Vector3 RelPos;
            public readonly Quaternion RelRot;

            public SegmentData(Transform target, Vector3 relPos, Quaternion relRot)
            {
                Target = target;
                RelPos = relPos;
                RelRot = relRot;
            }
        }
    }
}
