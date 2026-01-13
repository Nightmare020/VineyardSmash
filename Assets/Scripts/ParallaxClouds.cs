using UnityEngine;

public class ParallaxClouds : MonoBehaviour
{
    // World units/sec
    // X scrolls horizontally, while Y vertically
    public float speed = 0.5f;

    // Move direction
    public Vector2 direction = new Vector2(-1f, 0f);

    public Camera targetCamera;

    private Transform layer1;
    private Transform layer2;

    private SpriteRenderer spriteRendererLayer1;
    private SpriteRenderer spriteRendererLayer2;

    private float width;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (transform.childCount < 2)
        {
            Debug.LogError("ParallaxClouds requires at least two child objects for scrolling.");
            enabled = false;
            return;
        }

        layer1 = transform.GetChild(0);
        layer2 = transform.GetChild(1);

        spriteRendererLayer1 = layer1.GetComponent<SpriteRenderer>();
        spriteRendererLayer2 = layer2.GetComponent<SpriteRenderer>();

        if (spriteRendererLayer1 == null || spriteRendererLayer2 == null)
        {
            Debug.LogError("Child objects must have a SpriteRenderer component.");
            enabled = false;
            return;
        }

        // Use visible width of sprite of layer 1 in world units
        width = spriteRendererLayer1.bounds.size.x;

        // Ensure both cloud layers start adjacent to each other
        layer2.position = new Vector3(layer1.position.x + width, layer2.position.y, layer2.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 delta = (Vector3)(direction.normalized * speed * Time.deltaTime);
        layer1.position += delta;
        layer2.position += delta;

        // Camera horizontal bounds in world space
        float cameraLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;
        float cameraRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;

        // Wrap logic based on which direction we're moving
        if (direction.x < 0f) // moving left
        {
            // If layer 1 is completely left of the camera, move it to the right of layer 2
            if (spriteRendererLayer1.bounds.max.x < cameraLeft)
            {
                layer1.position = new Vector3(layer2.position.x + width, layer1.position.y, layer1.position.z);
            }

            // If layer 2 is completely left of the camera, move it to the right of layer 1
            if (spriteRendererLayer2.bounds.max.x < cameraLeft)
            {
                layer2.position = new Vector3(layer1.position.x + width, layer2.position.y, layer2.position.z);
            }
        }
        else if (direction.x > 0f)
        {
            // If layer 1 is completely right of the camera, move it to the left of layer 2
            if (spriteRendererLayer1.bounds.min.x > cameraRight)
            {
                layer1.position = new Vector3(layer2.position.x - width, layer1.position.y, layer1.position.z);
            }
            // If layer 2 is completely right of the camera, move it to the left ob layer 1
            if (spriteRendererLayer2.bounds.min.x > cameraRight)
            {
                layer2.position = new Vector3(layer1.position.x - width, layer2.position.y, layer2.position.z);
            }
        }
    }
}
