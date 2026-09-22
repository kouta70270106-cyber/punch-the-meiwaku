using System.Collections;
using UnityEngine;

public enum PersonType { Bad, Normal }

// OnMouseDownで拾うためCollider2Dが必須(シーンにカメラも必要)
[RequireComponent(typeof(Collider2D))]
public class Character : MonoBehaviour
{
    [SerializeField] private float lifeTime = 1.5f;   // 出現してから何もされず消えるまでの時間
    [SerializeField] private float peakOffset = 0.6f;  // 出現からベストタイミングまでのオフセット

    [Header("歩いて近づいてくる演出")]
    [SerializeField] private Vector3 approachOffset = new Vector3(0f, -3.5f, 0f); // 出現位置からの移動量(奥→手前)
    [SerializeField] private float startScale = 0.45f; // 遠くにいる時の縮小率
    [SerializeField] private float endScale = 1.4f;    // 手前まで来た時の拡大率

    public PersonType Type { get; private set; }
    public bool IsResolved { get; private set; }
    public float PeakTime { get; private set; }

    private Coroutine lifeRoutine;
    private Coroutine walkRoutine;

    public void Setup(PersonType type)
    {
        Type = type;
        IsResolved = false;
        PeakTime = Time.time + peakOffset;

        transform.localScale = Vector3.one * startScale;
        lifeRoutine = StartCoroutine(LifeCycle());
        walkRoutine = StartCoroutine(WalkTowardPlayer());
    }

    // 出現位置(奥)から手前へ、lifeTimeかけて歩いて近づいてくる(サイズも拡大)
    private IEnumerator WalkTowardPlayer()
    {
        Vector3 start = transform.position;
        Vector3 end = start + approachOffset;
        float elapsed = 0f;

        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifeTime);
            transform.position = Vector3.Lerp(start, end, t);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            yield return null;
        }
    }

    private IEnumerator LifeCycle()
    {
        yield return new WaitForSeconds(lifeTime);

        if (!IsResolved)
        {
            IsResolved = true;
            if (Type == PersonType.Bad)
            {
                // 殴られないままクソな人を逃した
                GameManager.Instance.RegisterJudge(JudgeResult.Miss);
            }
            Despawn();
        }
    }

    private void OnMouseDown()
    {
        HandlePunch();
    }

    public void HandlePunch()
    {
        if (IsResolved) return;
        IsResolved = true;

        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
        }
        if (walkRoutine != null)
        {
            StopCoroutine(walkRoutine);
        }

        JudgeResult result = JudgeSystem.Instance.JudgePunch(this);
        GameManager.Instance.RegisterJudge(result);

        PlayHitReaction();
    }

    private void PlayHitReaction()
    {
        StartCoroutine(KnockbackAndFade());
    }

    private IEnumerator KnockbackAndFade()
    {
        const float duration = 0.35f;
        float elapsed = 0f;

        Vector3 start = transform.position;
        Vector3 end = start + new Vector3(1.5f, 2.0f, 0f);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = elapsed / duration;

            transform.position = Vector3.Lerp(start, end, ratio);
            transform.Rotate(0f, 0f, 720f * Time.deltaTime);

            if (sr != null)
            {
                sr.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), ratio);
            }

            yield return null;
        }

        Despawn();
    }

    private void Despawn()
    {
        Destroy(gameObject);
    }
}
