using UnityEngine;

public enum JudgeResult { VeryGood, Good, Miss, Gobaku }

public static class JudgeResultExtensions
{
    public static int ToScore(this JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.VeryGood: return 3;
            case JudgeResult.Good: return 2;
            case JudgeResult.Miss: return -1;
            case JudgeResult.Gobaku: return -3;
            default: return 0;
        }
    }

    public static bool IsSuccess(this JudgeResult result)
    {
        return result == JudgeResult.VeryGood || result == JudgeResult.Good;
    }
}

// Very Good / Good の判定幅だけを扱う。Miss(逃した)はCharacter側のタイムアウトで、
// 誤爆(普通の人を殴った)はここで対象の種類を見て即決する。
public class JudgeSystem : MonoBehaviour
{
    public static JudgeSystem Instance { get; private set; }

    [Header("Very Goodの判定幅（Character.PeakTimeからの許容誤差・秒）")]
    [SerializeField] private float veryGoodWindow = 0.15f;

    private void Awake()
    {
        Instance = this;
    }

    // Missは「殴らずに逃した」場合のみ(Character.LifeCycle側で判定)。
    // ここに来る＝生存中に殴れているので、最低でもGood以上が確定する。
    public JudgeResult JudgePunch(Character target)
    {
        if (target.Type == PersonType.Normal)
        {
            return JudgeResult.Gobaku;
        }

        float diff = Mathf.Abs(Time.time - target.PeakTime);
        return diff <= veryGoodWindow ? JudgeResult.VeryGood : JudgeResult.Good;
    }
}
