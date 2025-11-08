using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally spawns a simple 2D ragdoll made of rectangular sprites, joints,
/// and a static floor, then nudges the ragdoll with a random impulse on Start.
/// </summary>
public class ProceduralRagdoll2D : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Color bodyColor = new(0.9f, 0.8f, 0.7f, 1f);
    [SerializeField] private Color floorColor = new(0.5f, 0.5f, 0.5f, 1f);

    [Header("Impulse")]
    [SerializeField] private Vector2 impulseMagnitudeRange = new(1.5f, 3.5f);
    [SerializeField] private Vector2 torqueImpulseRange = new(-2f, 2f);

    [Header("Floor")]
    [SerializeField] private Vector2 floorSize = new(10f, 1f);
    [SerializeField] private float floorYOffset = -3.5f;

    [Header("Build Options")]
    [SerializeField] private bool autoBuildOnStart;

    private readonly Dictionary<string, RagdollPart> _parts = new();
    private Sprite _cubeSprite;

    private static readonly PartDefinition[] BodyDefinitions =
    {
        new("Head", new Vector2(0.45f, 0.45f), new Vector2(0f, 1.6f)),
        new("UpperTorso", new Vector2(0.6f, 0.9f), new Vector2(0f, 0.9f)),
        new("LowerTorso", new Vector2(0.6f, 0.7f), new Vector2(0f, 0.3f)),
        new("LeftUpperArm", new Vector2(0.5f, 0.2f), new Vector2(-0.65f, 1f)),
        new("LeftLowerArm", new Vector2(0.45f, 0.18f), new Vector2(-1.0f, 0.95f)),
        new("LeftHand", new Vector2(0.2f, 0.15f), new Vector2(-1.35f, 0.9f)),
        new("RightUpperArm", new Vector2(0.5f, 0.2f), new Vector2(0.65f, 1f)),
        new("RightLowerArm", new Vector2(0.45f, 0.18f), new Vector2(1.0f, 0.95f)),
        new("RightHand", new Vector2(0.2f, 0.15f), new Vector2(1.35f, 0.9f)),
        new("LeftUpperLeg", new Vector2(0.35f, 0.6f), new Vector2(-0.2f, -0.2f)),
        new("LeftLowerLeg", new Vector2(0.3f, 0.6f), new Vector2(-0.2f, -0.9f)),
        new("LeftFoot", new Vector2(0.4f, 0.18f), new Vector2(-0.2f, -1.3f)),
        new("RightUpperLeg", new Vector2(0.35f, 0.6f), new Vector2(0.2f, -0.2f)),
        new("RightLowerLeg", new Vector2(0.3f, 0.6f), new Vector2(0.2f, -0.9f)),
        new("RightFoot", new Vector2(0.4f, 0.18f), new Vector2(0.2f, -1.3f))
    };

    private void Awake()
    {
        _cubeSprite = Resources.GetBuiltinResource<Sprite>("Sprites/Square.psd");
        if (_cubeSprite == null)
        {
            Debug.LogWarning("Square sprite not found; using procedural texture.");
            _cubeSprite = CreateFallbackSprite();
        }
    }

    private void Start()
    {
        if (autoBuildOnStart || !HasExistingParts())
        {
            RebuildRagdollHierarchy();
        }
        else
        {
            HydrateExistingParts();
            IgnoreSelfCollisions();
        }

        ApplyRandomImpulse();
    }

    [ContextMenu("Rebuild Ragdoll Hierarchy")]
    private void RebuildRagdollHierarchy()
    {
        ClearExistingChildren();
        BuildRagdoll();
        CreateFloor();
        IgnoreSelfCollisions();
    }

    private bool HasExistingParts()
    {
        foreach (var definition in BodyDefinitions)
        {
            if (transform.Find(definition.Name) != null)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildRagdoll()
    {
        _parts.Clear();
        foreach (var definition in BodyDefinitions)
        {
            CreatePart(definition);
        }

        Connect("Head", Edge.Bottom, "UpperTorso", Edge.Top, -10f, 10f);
        Connect("UpperTorso", Edge.Bottom, "LowerTorso", Edge.Top, -5f, 5f);

        Connect("LeftUpperArm", Edge.Right, "UpperTorso", Edge.Left, -60f, 60f);
        Connect("LeftLowerArm", Edge.Right, "LeftUpperArm", Edge.Left, -70f, 30f);
        Connect("LeftHand", Edge.Right, "LeftLowerArm", Edge.Left, -40f, 40f);

        Connect("RightUpperArm", Edge.Left, "UpperTorso", Edge.Right, -60f, 60f);
        Connect("RightLowerArm", Edge.Left, "RightUpperArm", Edge.Right, -70f, 30f);
        Connect("RightHand", Edge.Left, "RightLowerArm", Edge.Right, -40f, 40f);

        Connect("LeftUpperLeg", Edge.Top, "LowerTorso", Edge.Bottom, -30f, 30f);
        Connect("LeftLowerLeg", Edge.Top, "LeftUpperLeg", Edge.Bottom, -10f, 90f);
        Connect("LeftFoot", Edge.Top, "LeftLowerLeg", Edge.Bottom, -25f, 25f);

        Connect("RightUpperLeg", Edge.Top, "LowerTorso", Edge.Bottom, -30f, 30f);
        Connect("RightLowerLeg", Edge.Top, "RightUpperLeg", Edge.Bottom, -10f, 90f);
        Connect("RightFoot", Edge.Top, "RightLowerLeg", Edge.Bottom, -25f, 25f);
    }

    private void CreateFloor()
    {
        var existing = transform.Find("Floor");
        if (existing != null)
        {
            DestroyChild(existing.gameObject);
        }

        var floor = new GameObject("Floor");
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(0f, floorYOffset, 0f);
        floor.transform.localScale = new Vector3(floorSize.x, floorSize.y, 1f);

        var sr = floor.AddComponent<SpriteRenderer>();
        sr.sprite = _cubeSprite;
        sr.color = floorColor;
        sr.sortingOrder = -1;

        var body = floor.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        var collider = floor.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private void ApplyRandomImpulse()
    {
        if (_parts.Count == 0)
        {
            HydrateExistingParts();
        }

        if (_parts.Count == 0)
        {
            return;
        }

        var impulseDir = Random.insideUnitCircle.normalized;
        var impulseMag = Random.Range(impulseMagnitudeRange.x, impulseMagnitudeRange.y);
        var impulse = impulseDir * impulseMag;

        foreach (var part in _parts.Values)
        {
            var torque = Random.Range(torqueImpulseRange.x, torqueImpulseRange.y);
            part.Rigidbody.AddForce(impulse, ForceMode2D.Impulse);
            part.Rigidbody.AddTorque(torque, ForceMode2D.Impulse);
        }
    }

    private void CreatePart(PartDefinition def)
    {
        var go = new GameObject(def.Name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = def.Position;
        go.transform.localScale = new Vector3(def.Size.x, def.Size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _cubeSprite;
        sr.color = bodyColor;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        _parts[def.Name] = new RagdollPart(def.Name, go, rb, collider, def.Size);
    }

    private void Connect(string partName, Edge partEdge, string targetName, Edge targetEdge, float minLimit, float maxLimit)
    {
        if (!_parts.TryGetValue(partName, out var part) || !_parts.TryGetValue(targetName, out var target))
        {
            Debug.LogWarning($"Cannot connect {partName} to {targetName}: missing part.");
            return;
        }

        var joint = part.GameObject.AddComponent<HingeJoint2D>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = target.Rigidbody;
        joint.anchor = GetAnchor(part.Size, partEdge);
        joint.connectedAnchor = GetAnchor(target.Size, targetEdge);
        joint.useLimits = true;
        joint.limits = new JointAngleLimits2D { min = minLimit, max = maxLimit };
    }

    private static Vector2 GetAnchor(Vector2 size, Edge edge)
    {
        return edge switch
        {
            Edge.Top => new Vector2(0f, size.y * 0.5f),
            Edge.Bottom => new Vector2(0f, -size.y * 0.5f),
            Edge.Left => new Vector2(-size.x * 0.5f, 0f),
            Edge.Right => new Vector2(size.x * 0.5f, 0f),
            _ => Vector2.zero
        };
    }

    private readonly struct PartDefinition
    {
        public string Name { get; }
        public Vector2 Size { get; }
        public Vector2 Position { get; }

        public PartDefinition(string name, Vector2 size, Vector2 position)
        {
            Name = name;
            Size = size;
            Position = position;
        }
    }

    private readonly struct RagdollPart
    {
        public string Name { get; }
        public GameObject GameObject { get; }
        public Rigidbody2D Rigidbody { get; }
        public Collider2D Collider { get; }
        public Vector2 Size { get; }

        public RagdollPart(string name, GameObject gameObject, Rigidbody2D rigidbody, Collider2D collider, Vector2 size)
        {
            Name = name;
            GameObject = gameObject;
            Rigidbody = rigidbody;
            Collider = collider;
            Size = size;
        }
    }

    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    private void HydrateExistingParts()
    {
        _parts.Clear();
        foreach (var definition in BodyDefinitions)
        {
            var child = transform.Find(definition.Name);
            if (child == null)
            {
                continue;
            }

            var rb = child.GetComponent<Rigidbody2D>();
            var collider = child.GetComponent<Collider2D>();
            if (rb == null || collider == null)
            {
                continue;
            }

            var size = GetColliderSize(collider);
            _parts[definition.Name] = new RagdollPart(definition.Name, child.gameObject, rb, collider, size);
        }
    }

    private static Vector2 GetColliderSize(Collider2D collider)
    {
        return collider switch
        {
            BoxCollider2D box => box.size,
            CapsuleCollider2D capsule => capsule.size,
            _ => collider.bounds.size
        };
    }

    private void IgnoreSelfCollisions()
    {
        if (_parts.Count == 0)
        {
            HydrateExistingParts();
        }

        var cache = new List<RagdollPart>(_parts.Values);

        for (var i = 0; i < cache.Count; i++)
        {
            for (var j = i + 1; j < cache.Count; j++)
            {
                var first = cache[i].Collider;
                var second = cache[j].Collider;
                if (first != null && second != null)
                {
                    Physics2D.IgnoreCollision(first, second, true);
                }
            }
        }

        cache.Clear();
    }

    private void ClearExistingChildren()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            DestroyChild(child);
        }

        _parts.Clear();
    }

    private void DestroyChild(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static Sprite CreateFallbackSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ProceduralRagdollSquare",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };

        var pixels = new Color[size * size];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
