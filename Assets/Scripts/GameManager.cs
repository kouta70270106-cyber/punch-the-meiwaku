using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const float TimeLimit = 60f;
    private const int TheoreticalMaxScore = 136; // クソな人40体×VeryGood(3) + コンボボーナス8回×2
    private const int ComboStep = 5;
    private const int ComboBonus = 2;

    [SerializeField] private SpawnManager spawnManager;

    public float TimeRemaining { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public bool IsPlaying { get; private set; }

    public event Action<int, int> OnScoreChanged; // (score, combo)
    public event Action<float> OnTimeChanged;
    public event Action<int, float> OnGameOver;   // (rawScore, normalizedScore)

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        TimeRemaining = Mathf.Max(TimeRemaining - Time.deltaTime, 0f);
        OnTimeChanged?.Invoke(TimeRemaining);

        if (TimeRemaining <= 0f)
        {
            EndGame();
        }
    }

    public void StartGame()
    {
        Score = 0;
        Combo = 0;
        TimeRemaining = TimeLimit;
        IsPlaying = true;

        OnScoreChanged?.Invoke(Score, Combo);
        OnTimeChanged?.Invoke(TimeRemaining);

        spawnManager.BeginSpawning();
    }

    public void RegisterJudge(JudgeResult result)
    {
        if (!IsPlaying) return;

        int gained = result.ToScore();

        if (result.IsSuccess())
        {
            Combo++;
            if (Combo % ComboStep == 0)
            {
                gained += ComboBonus;
            }
        }
        else
        {
            Combo = 0;
        }

        Score += gained;
        OnScoreChanged?.Invoke(Score, Combo);
    }

    public void EndGame()
    {
        if (!IsPlaying) return;

        IsPlaying = false;
        TimeRemaining = 0f;
        spawnManager.StopSpawning();

        OnGameOver?.Invoke(Score, GetNormalizedScore());
    }

    public float GetNormalizedScore()
    {
        float ratio = (float)Score / TheoreticalMaxScore;
        return Mathf.Clamp(ratio * 100f, 0f, 100f);
    }
}
