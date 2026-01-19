using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class FoodBucketGrid : MonoBehaviour
{
    [Header("Bounds for slot layout")]
    public Collider2D bucketBoundsCollider;

    // Inset from bucket bounds so items don't intersect walls
    public Vector2 inset = new Vector2(0.2f, 0.2f);

    [Header("Grid Size")]
    [Min(1)] private int columns = 5;
    [Min(1)] private int rows = 5;
    
    // One extra row for visual overflow
    private int overflowRows = 1;
    
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

    [Header("Input")] 
    public bool handleInputInternally = false;

    // Playable capacity
    public int CapacityRows => rows;
    public int TotalRows => rows + overflowRows;
    
    public int Rows => TotalRows;
    public int Columns => columns;
    
    // ID to prefab
    private Dictionary<string, GameObject> prefabById = new Dictionary<string, GameObject>();
    // ID to texture
    private Dictionary<string, Texture2D> textureById = new Dictionary<string, Texture2D>();
    // ID to sprite
    private Dictionary<string, Sprite> spriteById = new Dictionary<string, Sprite>();
    // List of IDs for random pick
    private List<string> ids = new List<string>();

    public float CellHeight
    {
        get
        {
            Bounds bounds = bucketBoundsCollider.bounds;
            float minY = bounds.min.y + inset.y;
            float maxY = bounds.max.y - inset.y;
            float height = Mathf.Max(0.001f, maxY - minY);
            return height / CapacityRows;
        }
    }

    public int CurrentHeight
    {
        get
        {
            if (grid == null)
            {
                return -1;
            }
            
            int height = 0;
            for (int r = 0; r < Rows; r++)
            {
                bool any = false;
                for (int c = 0; c < Columns; c++)
                {
                    if (grid[r, c])
                    {
                        any = true;
                        break;
                    }
                }

                if (any)
                {
                    height = r + 1;
                }
            }

            return height;
        }
    }
    
    public bool IsInitialized => grid != null;

    private void Awake()
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }
    
    public GameObject Cell(int r, int c) => grid[r, c];

    public int GetColumnUnderX(float x) => GetColumnFromX(x);

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
        grid = new GameObject[TotalRows, columns];
        slots = new Vector2[TotalRows, columns];
        
        Bounds bucketBounds = bucketBoundsCollider.bounds;

        float minX = bucketBounds.min.x + inset.x + xMargin;
        float maxX = bucketBounds.max.x - inset.x - xMargin;
        float minY = bucketBounds.min.y + inset.y;
        float maxY = bucketBounds.max.y - inset.y;

        float width = Mathf.Max(0.001f, maxX - minX);
        float height = Mathf.Max(0.001f, maxY - minY);

        float cellW = width / columns;
        float cellH = height / CapacityRows;

        // Playable rows fill the bucket area
        for (int r = 0; r < CapacityRows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                slots[r, c] = new Vector2(
                    minX + (c + 0.5f) * cellW,
                    minY + (r + 0.5f) * cellH
                    );
            }
        }
        
        // Overflow rows go above the bucket area
        for (int r = CapacityRows; r < TotalRows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                slots[r, c] = new Vector2(
                    minX + (c + 0.5f) * cellW,
                    maxY + ((r - CapacityRows) + 0.5f) * cellH
                );
            }
        }
    }

    private void InitGrid()
    {
        for (int r = 0; r < CapacityRows; r++)
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

    public void StepColumnShiftAndSpawn(int column)
    {
        int height = CurrentHeight;

        if (height <= 0)
        {
            return;
        }
        
        int topRow = height - 1;
        
        // Destroy top cell in that column
        if (grid[topRow, column])
        {
            Destroy(grid[topRow, column]);
        }
        
        // Shift everything up, so row - 1 is row now
        for (int r = topRow; r >= 1; r--)
        {
            grid[r, column] = grid[r - 1, column];
            if (grid[r, column])
            {
                grid[r, column].transform.position = slots[r, column];
            }
        }

        // Insert the next prefab at the bottom
        grid[0, column] = SpawnFood(nextId[column], slots[0, column]);
            
        // Roll next prefab and update UI
        RollNextForColumn(column);
    }
    
    // Remove only top cell
    public bool ChallengeRemoveTopCell(int column)
    {
        int height = CurrentHeight;
        if (height <= 0)
        {
            return false;
        }
        
        int topRow = height - 1;
        if (grid[topRow, column] == null)
        {
            return false;
        }
        
        Destroy(grid[topRow, column]);
        grid[topRow, column] = null;
        return true;
    }

    public int GetColumnFromX(float worldX)
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
        foreach (var targetJoint in gameObject.GetComponentsInChildren<TargetJoint2D>())
        {
            Destroy(targetJoint);
        }

        foreach (var rigidBody in gameObject.GetComponentsInChildren<Rigidbody2D>())
        {
            Destroy(rigidBody);
        }
        
        return gameObject;
    }
    
    // Add a full new row from the UI at the bottom
    public bool TryAddNewRowFromBottom()
    {
        int height = CurrentHeight;
        
        // If already full, adding another row would exceed the bucket, which means game over
        if (height >= TotalRows)
        {
            return false;
        }

        bool overflow = (height >= CapacityRows);
        
        // When full, shift all rows up by 1 inside the fixed grid
        int shiftFromRow = Mathf.Min(height, TotalRows - 1);
        
        // Shift existing rows up by 1
        for (int r = shiftFromRow; r >= 1; r--)
        {
            for (int c = 0; c < Columns; c++)
            {
                grid[r, c] = grid[r - 1, c];
                if (grid[r, c])
                {
                    grid[r, c].transform.position = slots[r, c];
                }
            }
        }
        
        // Insert new bottom row from UI preview for each column
        for (int c = 0; c < columns; c++)
        {
            grid[0, c] = SpawnFood(nextId[c], slots[0, c]);
            RollNextForColumn(c);
        }

        // returns false only to signal overflow and game over, after visual update
        return !overflow;
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
        s = s.Replace("(clone)", "");
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

    public void ResetGridAndUI()
    {
        // Destroy all spawned objects
        if (grid != null)
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (grid[r, c])
                    {
                        Destroy(grid[r, c]);
                        grid[r, c] = null;
                    }
                }
            }
        }
        
        // Re-roll the next queue and refill the playable area
        InitNextQueueAndUI();
        InitGrid();
    }

    public Sprite GetSpriteForId(string id)
    {
        id = NormalizeId(id);

        if (spriteById != null && spriteById.TryGetValue(id, out var s))
        {
            return s;
        }
        
        // If texture exists for that id, make a sprite from it
        if (textureById != null && textureById.TryGetValue(id, out var tex) && tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        
        return null;
    }
}
