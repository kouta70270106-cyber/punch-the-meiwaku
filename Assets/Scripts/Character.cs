using System.Collections;
using UnityEngine;

public enum PersonType { Bad, Normal }

// OnMouseDownで拾うためCollider2Dが必須(シーンにカメラも必要)
[RequireComponent(typeof(Collider2D))]
public class Character : MonoBehaviour
{
    [SerializeField] private float lifeTime = 1.5f;   // 出現してから何もされず消えるまでの時間
    [SerializeField] private float peakOffset = 0.6f;  // 出現からベストタイミングまでのオフセット

    public PersonType Type { get; private set; }
    public bool IsResolved { get; private set; }
    public float PeakTime { get; private set; }

    private Coroutine lifeRoutine;

    public void Setup(PersonType type)
    {
        Type = type;
        IsResolved = false;
        PeakTime = Time.time + peakOffset;
        lifeRoutine = StartCoroutine(LifeCycle());
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
