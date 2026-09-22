using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    private const int TotalCount = 50;
    private const int BadCount = 40;
    private const int NormalCount = 10;
    private const float SpawnInterval = 1.2f;

    [SerializeField] private Character[] badPersonPrefabs;
    [SerializeField] private Character[] normalPersonPrefabs;
    [SerializeField] private Transform[] spawnPoints;

    private Coroutine spawnRoutine;

    public void BeginSpawning()
    {
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        foreach (PersonType type in BuildSequence())
        {
            SpawnOne(type);
            yield return new WaitForSeconds(SpawnInterval);
        }
    }

    // 「普通の人」を、クソな人40体が作るBadCount+1個の隙間に1体ずつ差し込むことで、
    // 抽選やり直しなしで「普通の人が連続しない」出現順を作る。
    private List<PersonType> BuildSequence()
    {
        int gapCount = BadCount + 1;
        List<int> gaps = new List<int>(gapCount);
        for (int i = 0; i < gapCount; i++)
        {
            gaps.Add(i);
        }
        Shuffle(gaps);

        HashSet<int> normalGaps = new HashSet<int>(gaps.GetRange(0, NormalCount));

        List<PersonType> sequence = new List<PersonType>(TotalCount);
        for (int i = 0; i < gapCount; i++)
        {
            if (normalGaps.Contains(i))
            {
                sequence.Add(PersonType.Normal);
            }
            if (i < BadCount)
            {
                sequence.Add(PersonType.Bad);
            }
        }

        return sequence;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void SpawnOne(PersonType type)
    {
        Character[] pool = type == PersonType.Bad ? badPersonPrefabs : normalPersonPrefabs;
        Character prefab = pool[Random.Range(0, pool.Length)];
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];

        Character instance = Instantiate(prefab, point.position, point.rotation);
        instance.Setup(type);
    }
}
