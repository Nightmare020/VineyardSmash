using System.Collections;
using UnityEngine;

public class FoodBucketFiller : MonoBehaviour
{
    [Header("Bucket containment")]
    public Collider2D bucketBoundsCollider;

    [Header("Food Prefabs")]
    public GameObject[] foodPrefabs;

    [Header("Grid")]
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows = 5;

    // Inset from bucket bounds so items don't intersect walls
    public Vector2 inset = new Vector2(0.2f, 0.2f);

    // Extra spacing factor inside each cell
    [Range(0f, 0.4f)] public float cellShrink = 0.15f;

    [Header("Spawn")]
    // Spawn height above each slot
    public float spawnHeight = 3.0f;

    // Small random horizontal offset at spawn
    public float spawnJitterX = 0.05f;

    [Header("Settle to slot")]
    // How strongly items are pulled to their assigned slot
    public float jointMaxForce = 200f;

    // Frequency relation with snappier or floatier pull
    public float jointFrequency = 6f;

    [Range(0f, 1f)] public float jointDampingRatio = 0.85f;

    [Header("Freeze after settling")]
    public bool freezeWhenSettled = true;
    public float settleVelocityThreshold = 0.05f;
    public float settleAngularThreshold = 2f;
    public float settleTimeRequired = 0.4f;

    [Header("Snap + Freeze after settling")]
    // How close before snapping
    public float snapDistance = 0.03f;

    // How close in rotation before snapping
    public float snapAngleDeg = 3f;

    public bool snapRotationUpright = true;

    // Parent spawned food under this transform
    public Transform spawnedParent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SpawnGridOrdered();
    }

    [ContextMenu("Fill Bucket With Food Based on Grid")]
    public void SpawnGridOrdered()
    {
        if (bucketBoundsCollider == null)
        {
            Debug.LogError("Bucket Bounds Collider is not assigned");
            return;
        }

        if (foodPrefabs == null || foodPrefabs.Length == 0)
        {
            Debug.LogError("No Food Prefabs assigned");
            return;
        }

        Bounds bucketBounds = bucketBoundsCollider.bounds;

        // Compute interior rect
        float minX = bucketBounds.min.x + inset.x;
        float maxX = bucketBounds.max.x - inset.x;
        float minY = bucketBounds.min.y + inset.y;
        float maxY = bucketBounds.max.x - inset.y;

        float width = Mathf.Max(0.001f, maxX - minX);
        float height = Mathf.Max(0.001f, maxY - minY);

        float cellW = width / columns;
        float cellH = height / rows;

        // Shrink inside each cel so colliders don't flight much
        float usableW = cellW * (1f - cellShrink);
        float usableH = cellH * (1f - cellShrink);

        // Bottom row is i = 0, and top row is i = rows - 1
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                Vector2 slotPos = new Vector2(
                    minX + (j + 0.5f) * cellW,
                    minY + (i + 0.5f) * cellH
                );

                // Spawn above its own slot, with tiny jitter
                Vector2 spawnPos = slotPos + Vector2.up * spawnHeight;
                spawnPos.x += Random.Range(-spawnJitterX, spawnJitterX);

                GameObject prefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
                GameObject gamePrefab = Instantiate(prefab, spawnPos, Quaternion.identity, spawnedParent);

                Rigidbody2D rigidBody = gamePrefab.GetComponent<Rigidbody2D>();
                if (rigidBody == null)
                {
                    rigidBody = gamePrefab.AddComponent<Rigidbody2D>();
                }
                rigidBody.bodyType = RigidbodyType2D.Dynamic;
                rigidBody.gravityScale = Mathf.Max(0.01f, rigidBody.gravityScale);

                // Pull to exact assigned slot
                var targetJoint = gamePrefab.GetComponent<TargetJoint2D>();

                if (targetJoint == null)
                {
                    targetJoint = gamePrefab.AddComponent<TargetJoint2D>();
                }
                targetJoint.autoConfigureTarget = false;
                targetJoint.target = slotPos;
                targetJoint.maxForce = jointMaxForce;
                targetJoint.frequency = jointFrequency;
                targetJoint.dampingRatio = jointDampingRatio;

                // Clamp the joint's anchor a bit toward the center for stability
                targetJoint.anchor = Vector2.zero;

                if (freezeWhenSettled)
                {
                    StartCoroutine(FreezeAfterSettled(rigidBody, targetJoint));
                }
            }
        }
    }

    private IEnumerator FreezeAfterSettled(Rigidbody2D rigidBody, TargetJoint2D targetJoint)
    {
        float stableTime = 0f;

        while (rigidBody != null)
        {
            // Distance/angle to target
            float dist = targetJoint != null
                ? Vector2.Distance(rigidBody.position, targetJoint.target)
                : 0f;

            float angle = Mathf.Abs(Mathf.DeltaAngle(rigidBody.rotation, 0f));
            
            bool stable =
                rigidBody.linearVelocity.magnitude < settleVelocityThreshold &&
                Mathf.Abs(rigidBody.angularVelocity) < settleAngularThreshold &&
                dist < snapDistance &&
                (!snapRotationUpright || angle < snapAngleDeg);

            stableTime = stable ? stableTime + Time.deltaTime : 0f;

            if (stableTime >= settleTimeRequired)
            {
                // Snap exactly into place
                if (targetJoint != null)
                {
                    rigidBody.position = targetJoint.target;
                }

                if (snapRotationUpright)
                {
                    rigidBody.rotation = 0f;
                }
                
                // Lock in place and stop motion
                rigidBody.linearVelocity = Vector2.zero;
                rigidBody.angularVelocity = 0f;
                
                // freeze completely
                rigidBody.bodyType = RigidbodyType2D.Static;

                // Remove this joint so it doesn't keep applying forces
                if (targetJoint != null)
                {
                    Destroy(targetJoint);
                }

                yield break;
            }

            yield return null;
        }
    }
}
