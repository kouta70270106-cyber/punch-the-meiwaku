using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

    [MenuItem("Tools/パンチ・ザ・迷惑/UIを追加")]
    public static void SetupUIOnly()
    {
        if (Object.FindAnyObjectByType<UIController>() != null)
        {
            Debug.LogWarning("[ProjectSetup] UIは既に追加されています。二重に追加しないためスキップしました。");
            return;
        }

        SetupUI();

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] UIの追加が完了しました。");
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

    private static void SetupUI()
    {
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Unity 6000.5.5f1 + URPの環境で、Anchorを画面端(top/left等)に置くとGame viewに描画されない
        // 不具合を確認したため、全テキストをcenter/middleアンカー+オフセット位置で配置している。
        canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)(-1); // Everything

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text scoreText = CreateAnchoredText(canvasGO.transform, "ScoreText", font, "Score: 0", TextAnchor.MiddleLeft,
            new Vector2(-470f, 260f), new Vector2(300f, 40f));

        Text timeText = CreateAnchoredText(canvasGO.transform, "TimeText", font, "Time: 60", TextAnchor.MiddleRight,
            new Vector2(470f, 260f), new Vector2(300f, 40f));

        Text comboText = CreateAnchoredText(canvasGO.transform, "ComboText", font, string.Empty, TextAnchor.MiddleLeft,
            new Vector2(-470f, 220f), new Vector2(300f, 40f));

        GameObject resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
        resultPanel.transform.SetParent(canvasGO.transform, false);
        Image panelImage = resultPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform panelRT = resultPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        GameObject resultTextGO = new GameObject("ResultText", typeof(RectTransform));
        resultTextGO.transform.SetParent(resultPanel.transform, false);
        Text resultText = resultTextGO.AddComponent<Text>();
        resultText.font = font;
        resultText.fontSize = 36;
        resultText.alignment = TextAnchor.MiddleCenter;
        resultText.color = Color.white;
        resultText.text = string.Empty;
        RectTransform resultTextRT = resultTextGO.GetComponent<RectTransform>();
        resultTextRT.anchorMin = Vector2.zero;
        resultTextRT.anchorMax = Vector2.one;
        resultTextRT.offsetMin = Vector2.zero;
        resultTextRT.offsetMax = Vector2.zero;

        UIController controller = new GameObject("UIController").AddComponent<UIController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("timeText").objectReferenceValue = timeText;
        so.FindProperty("comboText").objectReferenceValue = comboText;
        so.FindProperty("resultPanel").objectReferenceValue = resultPanel;
        so.FindProperty("resultText").objectReferenceValue = resultText;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // center/middleアンカー固定。anchoredPositionは画面中央からのオフセットとして扱う。
    private static Text CreateAnchoredText(Transform parent, string name, Font font, string initialText,
        TextAnchor alignment, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 28;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = initialText;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        return text;
    }
}
