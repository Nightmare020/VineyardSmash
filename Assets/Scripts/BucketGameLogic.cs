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
                AwardChallengePoints();
                EndChallengeAndDeleteTopRow();
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
}
