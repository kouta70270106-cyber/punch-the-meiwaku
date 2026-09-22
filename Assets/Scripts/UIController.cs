using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private Text scoreText;
    [SerializeField] private Text timeText;
    [SerializeField] private Text comboText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;

    private GameManager gameManager;

    private void Start()
    {
        gameManager = GameManager.Instance;

        gameManager.OnScoreChanged += HandleScoreChanged;
        gameManager.OnTimeChanged += HandleTimeChanged;
        gameManager.OnGameOver += HandleGameOver;

        resultPanel.SetActive(false);
        HandleScoreChanged(gameManager.Score, gameManager.Combo);
        HandleTimeChanged(gameManager.TimeRemaining);
    }

    private void OnDestroy()
    {
        if (gameManager == null) return;

        gameManager.OnScoreChanged -= HandleScoreChanged;
        gameManager.OnTimeChanged -= HandleTimeChanged;
        gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleScoreChanged(int score, int combo)
    {
        scoreText.text = $"Score: {score}";
        comboText.text = combo > 0 ? $"Combo: {combo}" : string.Empty;
    }

    private void HandleTimeChanged(float timeRemaining)
    {
        timeText.text = $"Time: {Mathf.CeilToInt(timeRemaining)}";
    }

    private void HandleGameOver(int rawScore, float normalizedScore)
    {
        resultPanel.SetActive(true);
        resultText.text = $"最終スコア\n{rawScore}点\n\n{normalizedScore:0.0} / 100";
    }
}
