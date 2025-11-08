using System;
using Unity.Burst.Intrinsics;
using UnityEditor.UI;
using UnityEngine;

public enum CharacterState
{
    moving,
    ghost
}

public class Character : MonoBehaviour
{

    [Header("References")]

    [Header("Key Settings")]
    public KeyCode upKey = KeyCode.W;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode downKey = KeyCode.S;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode jumpKey = KeyCode.Space;

    [Header("Game Settings")]
    public CharacterState state = CharacterState.moving;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float acclerationForce = 100f;
    [SerializeField] private float maxGhostSpeed = 5f;
    [SerializeField] private float acclerationGhostForce = 100f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 20f;
    [SerializeField] private float gravityScale = 3f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;

    private Rigidbody2D rb;
    private Vector2 movingDirection;
    private bool isGrounded;
    private bool jumpRequested;
    private int playerLayerIndex;
    private int ghostLayerIndex;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerLayerIndex = LayerMask.NameToLayer("Player");
        ghostLayerIndex = LayerMask.NameToLayer("Ghost");
    }

    void Start()
    {

    }

    void Update()
    {
        movingDirection = Vector2.zero;
        if (state == CharacterState.moving)
        {
            if (Input.GetKey(leftKey))
            {
                movingDirection += Vector2.left;
            }
            if (Input.GetKey(rightKey))
            {
                movingDirection += Vector2.right;
            }

            if (Input.GetKeyDown(jumpKey) && isGrounded)
            {
                jumpRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                ChangeState(CharacterState.ghost);
            }
        }
        else if (state == CharacterState.ghost)
        {
            if (Input.GetKey(leftKey))
            {
                movingDirection += Vector2.left;
            }
            if (Input.GetKey(upKey))
            {
                movingDirection += Vector2.up;
            }
            if (Input.GetKey(downKey))
            {
                movingDirection += Vector2.down;
            }
            if (Input.GetKey(rightKey))
            {
                movingDirection += Vector2.right;
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                ChangeState(CharacterState.moving);
            }
        }
    }

    void FixedUpdate()
    {
        CheckGround();

        if (state == CharacterState.moving)
        {
            rb.gravityScale = gravityScale;
            rb.AddForce(new Vector2(movingDirection.x * acclerationForce, 0f));
            float clampedXVelocity = Mathf.Clamp(rb.linearVelocity.x, -maxSpeed, maxSpeed);
            rb.linearVelocity = new Vector2(clampedXVelocity, rb.linearVelocity.y);

            if (jumpRequested)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                jumpRequested = false;
            }
        }
        else if (state == CharacterState.ghost)
        {
            rb.gravityScale = 0f;
            rb.AddForce(movingDirection.normalized * acclerationGhostForce);
            if (rb.linearVelocity.magnitude > maxGhostSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxGhostSpeed;
            }
        }
    }

    void CheckGround()
    {
        isGrounded = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance, Color.red);
    }

    void ChangeState(CharacterState newState)
    {
        state = newState;
        if (state == CharacterState.moving)
        {
            gameObject.layer = playerLayerIndex;
        }
        else if (state == CharacterState.ghost)
        {
            gameObject.layer = ghostLayerIndex;
        }
    }
}
