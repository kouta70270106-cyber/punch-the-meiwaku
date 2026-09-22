using System.Collections;
using UnityEngine;

// 画面下に常時表示される両手(拳)。殴った相手のX座標に応じて左右どちらかの拳を突き出す。
public class PlayerHandsController : MonoBehaviour
{
    public static PlayerHandsController Instance { get; private set; }

    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private float punchDistance = 1.6f;
    [SerializeField] private float punchDuration = 0.12f;

    private Vector3 leftRest;
    private Vector3 rightRest;
    private Coroutine leftRoutine;
    private Coroutine rightRoutine;

    private void Awake()
    {
        Instance = this;
        if (leftHand != null) leftRest = leftHand.localPosition;
        if (rightHand != null) rightRest = rightHand.localPosition;
    }

    // targetWorldX: 殴った相手のワールド座標X。0以上なら右手、未満なら左手を振る。
    public void Punch(float targetWorldX)
    {
        bool useRight = targetWorldX >= 0f;
        Transform hand = useRight ? rightHand : leftHand;
        if (hand == null) return;

        Vector3 rest = useRight ? rightRest : leftRest;
        Coroutine running = useRight ? rightRoutine : leftRoutine;
        if (running != null) StopCoroutine(running);

        Coroutine started = StartCoroutine(PunchMotion(hand, rest));
        if (useRight) rightRoutine = started; else leftRoutine = started;
    }

    private IEnumerator PunchMotion(Transform hand, Vector3 rest)
    {
        Vector3 forward = rest + new Vector3(0f, punchDistance, 0f);
        float half = punchDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            hand.localPosition = Vector3.Lerp(rest, forward, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            hand.localPosition = Vector3.Lerp(forward, rest, t / half);
            yield return null;
        }

        hand.localPosition = rest;
    }
}
