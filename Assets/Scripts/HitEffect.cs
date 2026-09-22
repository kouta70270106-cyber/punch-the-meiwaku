using System.Collections;
using UnityEngine;

// パンチが当たった瞬間に一瞬だけ表示する衝撃エフェクト。拡大しながらフェードして自動で消える。
public class HitEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private float startScale = 0.3f;
    [SerializeField] private float endScale = 1.3f;

    private void Start()
    {
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            transform.Rotate(0f, 0f, 180f * Time.deltaTime);

            if (sr != null)
            {
                sr.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), t);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
