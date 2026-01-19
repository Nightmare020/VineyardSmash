using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class BucketGameLogic : MonoBehaviour
{
    [Header("Core")]
    public FoodBucketGrid grid;
    public RepeatableTimerCountdown timer;
    public ScorePointsMarker score;
    
    [Header("Character & Rail")]
    public Transform characterRoot;
    public Transform topRail;
    
    [Header("IDs")] 
    public string eggId = "egg";
    
    [Header("State")]
    public bool isGameOver { get; private set; }
    public bool isWin { get; private set; }
    
    // UI panels for win/loose
    public GameObject gameOverPanel;
    public GameObject winPanel;

    private enum Mode
    {
        Normal,
        Challenge
    }
    
    private Mode mode = Mode.Normal;
    
    // Challenge bookkeeping
    private bool[] clearedColumn;
    private bool challengeIsCake;
    private int challengePointsPerItem;

    [Header("Timer UI Label")] 
    public TextMeshProUGUI timerLabelText;

    public string normalLabel = "Next Row In:";
    public string challengeLabel = "Step Before:";
    
    [Header("Run Timer")]
    public TimerCounter timerCounter;

    private Vector3 startCharacterPos;
    private Vector3 startRailPos;
    
    [Header("Restart Button")]
    public Button restartButton;

    [Header("Audio")] 
    public StepFoodAudio audioCtrl;
    
    [Header("Points Popup UI")]
    public TMPro.TextMeshProUGUI pointsGainedText;
    public float pointsGainedShowSeconds = 1f;

    [Header("Recipe Popup UI")] public GameObject recipePanel;
    public Image recipeIconImage;
    public TextMeshProUGUI recipeText;

    public Sprite cakeSprite;

    [Header("Reward Sequence")] 
    // How long input will be disabled
    public float rewardLockSeconds = 10.5f;
    // Duration of fill animation
    public float pourDurationSeconds = 10f;
    
    [Header("Pour and Fill Visuals")]
    public GameObject pourStreamGameObject;
    public SpriteRenderer pourStreamSpriteRenderer;
    public SpriteRenderer smallBucketFillUISpriteRenderer;

    private bool rewardActive;
    private float rewardStartTime;
    private float rewardEndTime;

    private string challengeRowId;
    private string cakeFlavorId;
    
    private Color rewardColor = Color.white;
    private bool lockSteppingInput;

    private Vector3 smallBucketFillFullScale;

    [Header("Small Bucket Reset")] 
    public float smallBucketResetDelaySeconds = 20f;

    private bool pendingSmallBucketReset;
    private float smallBucketResetAt;

    private void OnDestroy()
    {
        if (timer != null)
        {
            timer.TimerExpired -= OnTimerExpired;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (grid == null || timer == null || score == null || characterRoot == null || topRail == null)
        {
            Debug.LogError("One of the Game logic Elements missing");
            enabled = false;
            return;
        }

        if (smallBucketFillUISpriteRenderer)
        {
            smallBucketFillFullScale = smallBucketFillUISpriteRenderer.transform.localScale;
        }

        startCharacterPos = characterRoot.position;
        startRailPos = topRail.position;
        
        clearedColumn = new bool[grid.Columns];

        timer.TimerExpired += OnTimerExpired;
        
        // Normal mode, new row timer repeats
        EnterNormalMode(restartTimer: true);
        CheckWinCondition();
        
        // In case, we start with a match at teh start
        CheckStartChallengeFromTopRow();
    }

    // Update is called once per frame
    void Update()
    {
        if (isGameOver || isWin)
        {
            return;
        }
        
        if (pendingSmallBucketReset && Time.time >= smallBucketResetAt)
        {
            ResetFillVisual();
            
            // Reset liquid tint to default
            ApplyPourColor(Color.white);
            
            pendingSmallBucketReset = false;
        }
        
        if (rewardActive)
        {
            UpdateRewardSequence();
            return;
        }
        
        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }
        
        if (!(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return;
        }
        
        int col = grid.GetColumnFromX(characterRoot.position.x);

        if (mode == Mode.Normal)
        {
            grid.StepColumnShiftAndSpawn(col);
            if (audioCtrl)
            {
                audioCtrl.PlayStep();
            }
            CheckStartChallengeFromTopRow();
            CheckWinCondition();
        }
        else // Challenge mode
        {
            if (clearedColumn[col])
            {
                return;
            }

            bool removed = grid.ChallengeRemoveTopCell(col);
            if (!removed)
            {
                return;
            }
            
            if (audioCtrl)
            {
                audioCtrl.PlayStep();
            }
            
            clearedColumn[col] = true;

            if (AllColumnsCleared())
            {
                // Success
                BeginRewardSequence();
            }
        }
    }

    private void OnTimerExpired()
    {
        if (isGameOver || isWin)
        {
            return;
        }

        if (mode == Mode.Normal)
        {
            // Add a new row
            // If bucket is already full, game over
            bool added = grid.TryAddNewRowFromBottom();
            
            // Adding a new row pushes stack up, so rail goes up one row
            MoveRail(+1);
            
            if (!added)
            {
                TriggerGameOver();
                return;
            }

            CheckStartChallengeFromTopRow();
            CheckWinCondition();
        }
        else
        {
            // Challenge failed, so delete row with no points
            EndChallengeAndDeleteTopRow();
        }
    }
    
    /* -------------------------------------------------------
                        Challenge Detection
     ----------------------------------------------------------*/
    private void CheckStartChallengeFromTopRow()
    {
        int height = grid.CurrentHeight;
        if (height <= 0)
        {
            return;
        }

        int topRow = height - 1;

        for (int c = 0; c < grid.Columns; c++)
        {
            if (grid.Cell(topRow, c) == null)
            {
                return;
            }
        }
        
        // Read Ids and evaluate patterns
        string firstId = NormalizeIdFromCell(grid.Cell(topRow, 0));
        bool uniform = true;

        int eggs = 0;
        int nonEggs = 0;

        for (int c = 0; c < grid.Columns; c++)
        {
            var cell = grid.Cell(topRow, c);
            string id = NormalizeIdFromCell(cell);

            if (id != firstId)
            {
                uniform = false;
            }

            if (id == Normalize(eggId))
            {
                eggs++;
            }
            else
            {
                nonEggs++;
            }
        }

        bool cake = (eggs == 4 && nonEggs == 1);

        if (!uniform && !cake)
        {
            return;
        }
        
        StartChallenge(topRow, cake);
    }

    private void StartChallenge(int topRow, bool isCake)
    {
        if (mode == Mode.Challenge)
        {
            return;
        }

        mode = Mode.Challenge;
        challengeIsCake = isCake;
        
        // Cache the recipe ingredient
        if (isCake)
        {
            cakeFlavorId = "";
            for (int c = 0; c < grid.Columns; c++)
            {
                string id = NormalizeIdFromCell(grid.Cell(topRow, c));
                if (id != Normalize(eggId))
                {
                    cakeFlavorId = id;
                    break;
                }
            }
            
            // Use flavor for color and text
            challengeRowId = cakeFlavorId;
        }
        else
        {
            challengeRowId = NormalizeIdFromCell(grid.Cell(topRow, 0));
            cakeFlavorId = "";
        }
        
        Array.Clear(clearedColumn, 0, clearedColumn.Length);
        
        // Cache scoring now
        if (challengeIsCake)
        {
            challengePointsPerItem = 0;
        }
        else
        {
            // Use tag-based scoring
            var anyCell = grid.Cell(topRow, 0);
            challengePointsPerItem = score.GetPointsForTag(anyCell ? anyCell.tag : "");
        }
        
        if (timerLabelText)
        {
            timerLabelText.text = challengeLabel;
        }
        
        // Switch timer to step countdown and stop auto restarts
        timer.autoRestart = false;
        timer.UseStepTimerMode(true, restartNow: true);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void EndChallengeAndDeleteTopRow()
    {
        // As row is gone, go down one row
        MoveRail(-1);
        
        // Back to normal mode
        EnterNormalMode(restartTimer: true);
        
        CheckWinCondition();
        CheckStartChallengeFromTopRow();
    }

    private void EnterNormalMode(bool restartTimer)
    {
        mode = Mode.Normal;
        challengeIsCake = false;
        challengePointsPerItem = 0;

        if (timerLabelText)
        {
            timerLabelText.text = normalLabel;
        }
        
        timer.autoRestart = true;
        timer.UseStepTimerMode(false, restartNow: restartTimer);
    }

    private void AwardChallengePoints()
    {
        if (challengeIsCake)
        {
            int gained = score.cakePoints;
            score.AddCake();
            
            ShowPointsGained(gained);
            
            if (audioCtrl)
            {
                audioCtrl.PlayCakeReady();
            }
            
            return;
        }
        
        // 5 items in the row
        int gainedNormal = challengePointsPerItem * grid.Columns;
        score.Add(gainedNormal);
        
        ShowPointsGained(gainedNormal);
        
        if (audioCtrl)
        {
            audioCtrl.PlayIngredientClear();
        }
    }

    private bool AllColumnsCleared()
    {
        for (int i = 0; i < clearedColumn.Length; i++)
        {
            if (!clearedColumn[i])
            {
                return false;
            }
        }
        
        return true;
    }
    
    /* -------------------------------------------------------
                        Win or Loose Condition
     ----------------------------------------------------------*/

    private void CheckWinCondition()
    {
        int height = grid.CurrentHeight;
        
        // Grid not initialized yet
        if (height == -1)
        {
            return;
        }
        
        // Win rule is when the bucket is empty
        if (height == 0)
        {
            TriggerWin();
        }
    }

    private void TriggerGameOver()
    {
        isGameOver = true;
        timer.enabled = false;

        if (timerCounter)
        {
            timerCounter.StopTimer();
        }

        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
        }

        if (restartButton)
        {
            restartButton.gameObject.SetActive(true);
        }
    }

    private void TriggerWin()
    {
        isWin = true;
        timer.enabled = false;
        
        if (timerCounter)
        {
            timerCounter.StopTimer();
        }

        if (winPanel)
        {
            winPanel.SetActive(true);
        }
        
        if (restartButton)
        {
            restartButton.gameObject.SetActive(true);
        }
    }

    private void MoveRail(int rowDelta)
    {
        float deltaY = grid.CellHeight * rowDelta;
        Vector3 delta = Vector3.up * deltaY;

        if (topRail)
        {
            topRail.position += delta;
        }

        if (characterRoot)
        {
            characterRoot.position += delta;
        }
    }

    private void BeginRewardSequence()
    {
        if (rewardActive)
        {
            return;
        }
        
        // Award points and sounds
        AwardChallengePoints();
        
        // Show recipe UI
        ShowRecipeUI();
        
        // Start pour and fill visuals
        rewardColor = GetColorForIngredient(challengeRowId);
        ApplyPourColor(rewardColor);
        ResetFillVisual();
        if (pourStreamGameObject)
        {
            pourStreamGameObject.SetActive(true);
            RestartPourAnimation();
        }
        
        // Lock stepping input for a moment
        lockSteppingInput = true;
        rewardActive = true;
        rewardStartTime = Time.time;
        rewardEndTime = Time.time + Mathf.Max(0.1f, rewardLockSeconds);
        
        // Stop the countdown during reward
        if (timer)
        {
            timer.enabled = false;
        }
    }

    private void UpdateRewardSequence()
    {
        if (!rewardActive)
        {
            return;
        }

        float t = 0f;

        if (pourDurationSeconds > 0.01f)
        {
            t = Mathf.Clamp01((Time.time - rewardStartTime) / pourDurationSeconds);
        }
        else
        {
            t = 1f;
        }

        SetFillAmount(t);

        if (Time.time >= rewardEndTime)
        {
            // End visuals
            if (pourStreamGameObject)
            {
                pourStreamGameObject.SetActive(false);
            }
            
            // Hide recipe UI
            if (recipePanel)
            {
                recipePanel.SetActive(false);
            }

            rewardActive = false;
            lockSteppingInput = false;
            
            // Remove row and continue
            EndChallengeAndDeleteTopRow();
            
            // Schedule instant reset later
            pendingSmallBucketReset = true;
            smallBucketResetAt = Time.time + Mathf.Max(0f, smallBucketResetDelaySeconds);
        }
    }
    
    /* -------------------------------------------------------
                        Id Helpers
     ----------------------------------------------------------*/

    private string NormalizeIdFromCell(GameObject cell)
    {
        if (cell == null)
        {
            return "";
        }

        var fid = cell.GetComponent<FoodId>();
        if (fid != null && !string.IsNullOrWhiteSpace(fid.id))
        {
            return Normalize(fid.id);
        }
        
        return Normalize(cell.name);
    }

    private string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return "";
        }

        s = s.Trim().ToLowerInvariant();
        s = s.Replace("(clone)", "");
        s = s.Replace(" ", "").Replace("_", "").Replace("-", "");
        return s;
    }

    public void RestartGame()
    {
        // Hide UI
        if (gameOverPanel)
        {
            gameOverPanel.SetActive(false);
        }

        if (winPanel)
        {
            winPanel.SetActive(false);
        }
        
        if (restartButton)
        {
            restartButton.gameObject.SetActive(false);
        }
        
        // Reset state
        isGameOver = false;
        isWin = false;
        
        // Reset score
        if (score)
        {
            score.ResetScore();
        }
        
        // Reset rail and character
        if (topRail)
        {
            topRail.position = startRailPos;
        }

        if (characterRoot)
        {
            characterRoot.position = startCharacterPos;
        }
        
        // Reset grid
        if (grid)
        {
            grid.ResetGridAndUI();
        }
        
        // Reset bookkeeping
        clearedColumn = new bool[grid.Columns];
        mode = Mode.Normal;
        
        // Reset timers
        if (timer)
        {
            timer.enabled = true;
            timer.autoRestart = true;
            timer.UseStepTimerMode(false, restartNow: true);
        }

        if (timerLabelText)
        {
            timerLabelText.text = normalLabel;
        }
        
        // Restart run timer
        if (timerCounter)
        {
            timerCounter.StartTimer(reset: true);
        }
        
        // Re-check initial conditions
        CheckWinCondition();
        CheckStartChallengeFromTopRow();
    }

    private void ShowPointsGained(int amount)
    {
        if (pointsGainedText == null)
        {
            return;
        }
        
        pointsGainedText.text = $"+{amount}";
        pointsGainedText.gameObject.SetActive(true);
        CancelInvoke(nameof(HidePointsGained));
        Invoke(nameof(HidePointsGained), pointsGainedShowSeconds);
    }

    private void HidePointsGained()
    {
        if (pointsGainedText == null)
        {
            return;
        }
        
        pointsGainedText.gameObject.SetActive(false);
    }
    
    /* -------------------------------------------------------
                        UI Helpers
     ----------------------------------------------------------*/
    private void ShowRecipeUI()
    {
        if (!recipePanel || !recipeText)
        {
            return;
        }
        
        // Icon
        if (recipeIconImage)
        {
            if (challengeIsCake && cakeSprite)
            {
                recipeIconImage.sprite = cakeSprite;
                recipeIconImage.enabled = true;
            }
            else
            {
                // Use the cached challengeRowId
                Sprite icon = grid.GetSpriteForId(challengeRowId);
                recipeIconImage.sprite = icon;
                recipeIconImage.enabled = (icon != null);
            }
        }
        
        // Text
        if (challengeIsCake)
        {
            recipeText.text = $"You've Made {PrettyName(cakeFlavorId)} Cake!";
        }
        else
        {
            string id = challengeRowId;

            if (id.Contains("grapes"))
            {
                recipeText.text = "You've Made Wine!";
            }
            else if (id.Contains("chocolate"))
            {
                recipeText.text = "You've Made Hot Chocolate!";
            }
            else
            {
                recipeText.text = $"You've Made {PrettyName(id)} Juice!";
            }
        }
        
        recipePanel.SetActive(true);
    }

    private string PrettyName(string id)
    {
        id = Normalize(id);
        if (string.IsNullOrEmpty(id))
        {
            return "Recipe";
        }
        
        return char.ToUpper(id[0]) + id.Substring(1);
    }

    private void ApplyPourColor(Color color)
    {
        if (pourStreamSpriteRenderer)
        {
            pourStreamSpriteRenderer.color = color;
        }

        if (smallBucketFillUISpriteRenderer)
        {
            smallBucketFillUISpriteRenderer.color = color;
        }
    }

    private void ResetFillVisual()
    {
        if (smallBucketFillUISpriteRenderer)
        {
            var s = smallBucketFillFullScale;
            s.y = 0f;
            smallBucketFillUISpriteRenderer.transform.localScale = s;
        }
    }

    private void SetFillAmount(float amount)
    {
        if (smallBucketFillUISpriteRenderer)
        {
            amount = Mathf.Clamp01(amount);
            var s = smallBucketFillFullScale;
            s.y = smallBucketFillFullScale.y * amount;
            smallBucketFillUISpriteRenderer.transform.localScale = s;
        }
    }

    private Color GetColorForIngredient(string id)
    {
        id = Normalize(id);

        if (id.Contains("carrot") || id.Contains("pumpkin") || id.Contains("orange"))
        {
            return new Color(01f, 0.55f, 0.1f);
        }

        if (id.Contains("apple") || id.Contains("tomato") || id.Contains("cherries"))
        {
            return new Color(0.9f, 0.2f, 0.2f);
        }

        if (id.Contains("watermelon"))
        {
            return new Color(1f, 0.4f, 0.4f);
        }

        if (id.Contains("kiwi"))
        {
            return new Color(0.3f, 0.85f, 0.25f);
        }

        if (id.Contains("banana") || id.Contains("lemon"))
        {
            return new Color(0.9f, 0.75f, 0.25f);
        }

        if (id.Contains("grapes"))
        {
            return new Color(0.6f, 0.25f, 0.9f);
        }

        if (id.Contains("chocolate"))
        {
            return new Color(0.25f, 0.15f, 0.08f);
        }
        
        return Color.white;
    }

    private void RestartPourAnimation()
    {
        if (!pourStreamGameObject)
        {
            return;
        }
        
        var anim = pourStreamGameObject.GetComponent<Animator>();

        if (anim)
        {
            anim.Rebind();
            anim.Update(0f);
            anim.Play(0, 0, 0f);
        }
    }
}
