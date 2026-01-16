using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class BucketTopCharacterController2D : MonoBehaviour
{
    [Header("Movement")] 
    public float moveSpeed = 6f;

    [Header("Standing rail")]
    public Transform topRail;
    public float yOffset = 0f;

    [Header("Bucket bounds")]
    public Collider2D bucketBoundsCollider;
    public float xMargin = 0.2f;
    
    [Header("Visual / Rig")]
    public Transform visualRoot;
    public bool faceRightIsPositiveScale = true;
    private float baseVisualScaleX;
    
    [Header("Animation")]
    public Animator animator;
    public string runBool = "IsRunning";
    
    [Header("Character collider for clamping")]
    public Collider2D clampingCollider;
    
    private Rigidbody2D rigidBody;
    private float inputX;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        rigidBody.bodyType = RigidbodyType2D.Kinematic;
        rigidBody.gravityScale = 0f;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (clampingCollider == null)
        {
            clampingCollider = GetComponent<Collider2D>();
        }

        baseVisualScaleX = Mathf.Abs(visualRoot.localScale.x);
    }

    // Update is called once per frame
    void Update()
    {
        var keyBoard = Keyboard.current;
        inputX = 0f;

        if (keyBoard.aKey.isPressed || keyBoard.leftArrowKey.isPressed)
        {
            inputX = -1f;
        }

        if (keyBoard.dKey.isPressed || keyBoard.rightArrowKey.isPressed)
        {
            inputX = 1f;
        }

        
        if (animator != null)
        {
            animator.SetBool(runBool, Mathf.Abs(inputX) > 0.01f);
        }
        
        
        // Flip the whole rig visually
        if (Mathf.Abs(inputX) > 0.01f)
        {
            bool facingRight = inputX > 0f;
            
            float sign = facingRight ? 1f : -1f;
            if (!faceRightIsPositiveScale)
            {
                sign *= -1f;
            }

            Vector3 scale = visualRoot.localScale;
            scale.x = sign * baseVisualScaleX;
            visualRoot.localScale = scale;
        }
    }

    private void FixedUpdate()
    {
        Vector2 pos = rigidBody.position;
        
        // Lock Y
        if (topRail != null)
        {
            pos.y = topRail.position.y + yOffset;
        }
        
        // Move X
        pos.x += inputX * moveSpeed * Time.fixedDeltaTime;
        
        // Clamp X
        if (bucketBoundsCollider != null)
        {
            Bounds boundsBucket = bucketBoundsCollider.bounds;

            float halfWidth = 0f;
            if (clampingCollider != null)
            {
                halfWidth = clampingCollider.bounds.extents.x;
            }
            
            float minX = boundsBucket.min.x + xMargin + halfWidth;
            float maxX = boundsBucket.max.x - xMargin - halfWidth;
            
            // Safety if margins or extents are too large
            if (minX > maxX)
            {
                float mid = (boundsBucket.min.x + boundsBucket.max.x) / 2f;
                minX = maxX = mid;
            }
            
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }
        
        rigidBody.MovePosition(pos);
    }
}
