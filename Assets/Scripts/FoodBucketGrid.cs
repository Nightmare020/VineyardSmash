using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class FoodBucketGrid : MonoBehaviour
{
    [Header("Bounds for slot layout")]
    public Collider2D bucketBoundsCollider;

    // Inset from bucket bounds so items don't intersect walls
    public Vector2 inset = new Vector2(0.2f, 0.2f);

    [Header("Grid Size")]
    [Min(1)] public int columns = 4;
    [Min(1)] public int rows = 4;
    
    [Header("Food Prefabs")]
    public GameObject[] foodPrefabs;
    
    // Parent spawned food under this transform
    public Transform spawnedParent;
    
    [Header("Character used to pick column")]
    public Transform characterRoot;

    [Header("UI preview images")] 
    public Image[] nextColumnImages;
    public Texture2D[] nextTextureBank;

    [Header("Clamp column pick withing X margins")]
    public float xMargin = 0f;

    private GameObject[,] grid;
    private Vector2[,] slots;
    private string[] nextId;
    
    // ID to prefab
    private Dictionary<string, GameObject> prefabById = new Dictionary<string, GameObject>();
    // ID to texture
    private Dictionary<string, Texture2D> textureById = new Dictionary<string, Texture2D>();
    // ID to sprite
    private Dictionary<string, Sprite> spriteById = new Dictionary<string, Sprite>();
    // List of IDs for random pick
    private List<string> ids = new List<string>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (bucketBoundsCollider == null || foodPrefabs == null || foodPrefabs.Length == 0)
        {
            Debug.LogError("No bucket bound or food prefabs found");
            enabled = false;
            return;
        }

        BuildPrefabIndex();
        BuildTextureIndex();
        BuildSlots();
        InitGrid();
        InitNextQueueAndUI();
    }
    
    // Update is called once per frame
    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
        {
            StepColumnUnderCharacter();
        }
    }

    private void BuildTextureIndex()
    {
        textureById.Clear();

        if (nextTextureBank == null || nextTextureBank.Length == 0)
        {
            Debug.LogWarning("No texture bank found cause it's empty");
            return;
        }

        foreach (var text in nextTextureBank)
        {
            if (text == null)
            {
                continue;
            }

            string id = NormalizeId(text.name);
            if (!textureById.ContainsKey(id))
            {
                textureById[id] = text;
            }
        }
    }

    private void BuildPrefabIndex()
    {
        prefabById.Clear();
        spriteById.Clear();
        ids.Clear();

        foreach (var prefab in foodPrefabs)
        {
            if (prefab == null)
            {
                continue;
            }
            
            // Better explicit food id if present
            string id = null;
            var foodId = prefab.GetComponent<FoodId>();
            if (foodId != null && !string.IsNullOrWhiteSpace(foodId.id))
            {
                id = NormalizeId(foodId.id);
            }
            else
            {
                id = NormalizeId(prefab.name);
            }

            if (prefabById.ContainsKey(id))
            {
                Debug.LogWarning("Duplicate food id");
                continue;
            }

            prefabById[id] = prefab;
            
            var sprite = ExtractSprite(prefab);
            if (sprite)
            {
                spriteById[id] = sprite;
            }
            
            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            Debug.LogError("No food id found");
            enabled = false;
        }
    }

    private void BuildSlots()
    {
        grid = new GameObject[rows, columns];
        slots = new Vector2[rows, columns];
        
        Bounds bucketBounds = bucketBoundsCollider.bounds;

        float minX = bucketBounds.min.x + inset.x + xMargin;
        float maxX = bucketBounds.max.x - inset.x - xMargin;
        float minY = bucketBounds.min.y + inset.y;
        float maxY = bucketBounds.max.y - inset.y;

        float width = Mathf.Max(0.001f, maxX - minX);
        float height = Mathf.Max(0.001f, maxY - minY);

        float cellW = width / columns;
        float cellH = height / rows;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                slots[r, c] = new Vector2(
                    minX + (c + 0.5f) * cellW,
                    minY + (r + 0.5f) * cellH
                    );
            }
        }
    }

    private void InitGrid()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                grid[r, c] = SpawnFood(RandomId(), slots[r, c]);
            }
        }
    }

    private void InitNextQueueAndUI()
    {
        nextId = new String[columns];

        for (int c = 0; c < columns; c++)
        {
            // Sets next id for the column and updates ingredient image
            RollNextForColumn(c);
        }
    }

    private void RollNextForColumn(int column)
    {
        // Pick a random texture from the bank of textures
        if (nextTextureBank == null || nextTextureBank.Length == 0)
        {
            // Use random prefab id if no textures exist
            nextId[column] = RandomId();
            UpdateNextUI(column, nextId[column]);
            return;
        }
        
        Texture2D texture = nextTextureBank[Random.Range(0, nextTextureBank.Length)];
        if (texture == null)
        {
            nextId[column] = RandomId();
            UpdateNextUI(column, nextId[column]);
            return;
        }
        
        // This id will be spawned next in that column
        nextId[column] = NormalizeId(texture.name);
        
        // Push the texture to the UI image
        if (nextColumnImages != null && column >= 0 &&
            column < nextColumnImages.Length && nextColumnImages[column] != null)
        {
            nextColumnImages[column].sprite = TextureToSprite(texture);
            nextColumnImages[column].enabled = true;
        }
    }

    private void StepColumnUnderCharacter()
    {
        if (characterRoot == null)
        {
            Debug.LogWarning("No character root found");
            return;
        }
        
        int col = GetColumnFromX(characterRoot.position.x);
        
        int topRow = rows - 1;
        
        // Destroy top cell in that column
        if (grid[topRow, col])
        {
            Destroy(grid[topRow, col]);
        }
        
        // Shift everything up, so row - 1 is row now
        for (int r = topRow; r >= 1; r--)
        {
            grid[r, col] = grid[r - 1, col];
            if (grid[r, col])
            {
                grid[r, col].transform.position = slots[r, col];
            }
        }

        // Insert the next prefab at the bottom
        grid[0, col] = SpawnFood(nextId[col], slots[0, col]);
            
        // Roll next prefab and update UI
        RollNextForColumn(col);
    }

    private int GetColumnFromX(float worldX)
    {
        Bounds bucketBounds = bucketBoundsCollider.bounds;
        
        float minX = bucketBounds.min.x + inset.x + xMargin;
        float maxX = bucketBounds.max.x - inset.x - xMargin;
        float width = Mathf.Max(0.001f, maxX - minX);
        
        float t = Mathf.Clamp01((worldX - minX) / width);
        int col = Mathf.FloorToInt(t * columns);

        if (col >= columns)
        {
            col = columns - 1;
        }

        if (col < 0)
        {
            col = 0;
        }
        
        return col;
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private GameObject SpawnFood(string id, Vector2 pos)
    {
        id = NormalizeId(id);
        
        if (!prefabById.TryGetValue(id, out var prefab)|| prefab == null)
        {
            Debug.LogError("Prefab not found");
            return null;
        }
        
        var gameObject = Instantiate(prefab, pos, Quaternion.identity, spawnedParent);
        
        // Remove rigid body and joints
        var targetJoint = gameObject.GetComponent<TargetJoint2D>();
        if (targetJoint)
        {
            Destroy(targetJoint);
        }

        var rigidBody = gameObject.GetComponent<Rigidbody2D>();
        if (rigidBody)
        {
            Destroy(rigidBody);
        }
        
        return gameObject;
    }

    private string RandomId()
    {
        return ids[Random.Range(0, ids.Count)];
    }

    private void UpdateNextUI(int col, string id)
    {
        if (nextColumnImages == null)
        {
            return;
        }

        if (col < 0 || col >= nextColumnImages.Length)
        {
            return;
        }

        if (nextColumnImages[col] == null)
        {
            return;
        }

        id = NormalizeId(id);
        
        // If texture for this exist, show it
        if (textureById.TryGetValue(id, out var tex) && tex != null)
        {
            nextColumnImages[col].sprite = TextureToSprite(tex);
            nextColumnImages[col].enabled = true;
            return;
        }

        // Otherwise try prefab sprite fallback
        spriteById.TryGetValue(id, out var sprite);
        nextColumnImages[col].sprite = sprite;
        nextColumnImages[col].enabled = (sprite != null);
        
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private Sprite ExtractSprite(GameObject prefab)
    {
        var spriteRenderer = prefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = prefab.gameObject.GetComponentInChildren<SpriteRenderer>();
        }
        
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    private string NormalizeId(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return "";
        }

        s = s.Trim().ToLowerInvariant();
        s = s.Replace(" ", "");
        s = s.Replace("_", "");
        s = s.Replace("-", "");
        return s;
    }

    private Sprite TextureToSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }
        
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), 
            new Vector2(0.5f, 0.5f), 100f);
    }
}
