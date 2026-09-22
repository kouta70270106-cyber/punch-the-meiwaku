using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// プレハブ生成とシーンへの配線を自動化するエディタ拡張。
// 手作業でのInspector参照付けはミスしやすいため、一括セットアップできるようにしている。
public static class ProjectSetup
{
    private const string SpriteFolder = "Assets/Sprites";
    private const string PrefabFolder = "Assets/Prefabs";
    private const int SpawnPointCount = 5;

    [MenuItem("Tools/パンチ・ザ・迷惑/プレハブとシーンを自動配線")]
    public static void SetupAll()
    {
        EnsureFolder(SpriteFolder);
        EnsureFolder(PrefabFolder);

        Sprite badSprite = CreateColorSprite(SpriteFolder + "/BadPerson.png", new Color32(216, 51, 51, 255));
        Sprite normalSprite = CreateColorSprite(SpriteFolder + "/NormalPerson.png", new Color32(77, 128, 230, 255));

        GameObject badPrefab = CreateCharacterPrefab("BadPerson", badSprite);
        GameObject normalPrefab = CreateCharacterPrefab("NormalPerson", normalSprite);

        SetupScene(badPrefab, normalPrefab);

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] プレハブとシーンの配線が完了しました。Playボタンで60秒プレイが始まります。");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Sprite CreateColorSprite(string path, Color32 color)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateCharacterPrefab(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Character>();

        string prefabPath = $"{PrefabFolder}/{name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetupScene(GameObject badPrefab, GameObject normalPrefab)
    {
        GameObject spawnPointsRoot = new GameObject("SpawnPoints");
        Transform[] spawnPoints = new Transform[SpawnPointCount];
        for (int i = 0; i < SpawnPointCount; i++)
        {
            GameObject p = new GameObject($"SpawnPoint_{i}");
            p.transform.SetParent(spawnPointsRoot.transform);
            p.transform.position = new Vector3(-4f + i * 2f, 0f, 0f);
            spawnPoints[i] = p.transform;
        }

        JudgeSystem judgeSystem = new GameObject("JudgeSystem").AddComponent<JudgeSystem>();
        SpawnManager spawnManager = new GameObject("SpawnManager").AddComponent<SpawnManager>();
        GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();

        SerializedObject spawnSO = new SerializedObject(spawnManager);
        spawnSO.FindProperty("badPersonPrefab").objectReferenceValue = badPrefab.GetComponent<Character>();
        spawnSO.FindProperty("normalPersonPrefab").objectReferenceValue = normalPrefab.GetComponent<Character>();

        SerializedProperty spawnPointsProp = spawnSO.FindProperty("spawnPoints");
        spawnPointsProp.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }
        spawnSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject gameSO = new SerializedObject(gameManager);
        gameSO.FindProperty("spawnManager").objectReferenceValue = spawnManager;
        gameSO.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnManager);
        EditorUtility.SetDirty(gameManager);
        EditorUtility.SetDirty(judgeSystem);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
