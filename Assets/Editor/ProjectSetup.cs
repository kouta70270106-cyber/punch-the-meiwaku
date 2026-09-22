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

        Sprite badSprite = CreateFlatChibiSprite(SpriteFolder + "/BadPerson.png", isBad: true);
        Sprite normalSprite = CreateFlatChibiSprite(SpriteFolder + "/NormalPerson.png", isBad: false);

        GameObject badPrefab = CreateCharacterPrefab("BadPerson", badSprite);
        GameObject normalPrefab = CreateCharacterPrefab("NormalPerson", normalSprite);

        SetupScene(badPrefab, normalPrefab);
        SetupBackground();

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

    // 既存プレハブが参照するpngを直接上書きするので、プレハブの作り直しは不要。
    [MenuItem("Tools/パンチ・ザ・迷惑/フラットデザインのスプライトに差し替え")]
    public static void RegenerateSprites()
    {
        EnsureFolder(SpriteFolder);

        CreateFlatChibiSprite(SpriteFolder + "/BadPerson.png", isBad: true);
        CreateFlatChibiSprite(SpriteFolder + "/NormalPerson.png", isBad: false);

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] スプライトをフラットデザインの丸みキャラに差し替えました。");
    }

    // 既存シーンの出現位置(SpawnPoints配下)を、歩道の奥のレーンへ移動する。
    [MenuItem("Tools/パンチ・ザ・迷惑/出現位置を歩道用に更新")]
    public static void RepositionSpawnPoints()
    {
        GameObject root = GameObject.Find("SpawnPoints");
        if (root == null)
        {
            Debug.LogWarning("[ProjectSetup] SpawnPointsが見つかりません。先に「プレハブとシーンを自動配線」を実行してください。");
            return;
        }

        int count = root.transform.childCount;
        for (int i = 0; i < count; i++)
        {
            root.transform.GetChild(i).position = new Vector3(-3f + i * 1.5f, 2.0f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 出現位置を歩道用に更新しました。");
    }

    // 既に配線済みのシーンに、背景(空・ビル・歩道)だけを追加で入れたい時用。
    [MenuItem("Tools/パンチ・ザ・迷惑/歩道の背景を追加")]
    public static void SetupBackgroundOnly()
    {
        if (GameObject.Find("Background") != null)
        {
            Debug.LogWarning("[ProjectSetup] 背景は既に追加されています。二重に追加しないためスキップしました。");
            return;
        }

        EnsureFolder(SpriteFolder);
        SetupBackground();

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 歩道の背景を追加しました。");
    }

    // 既存のBackground・プレハブが使っているSprite-Lit-Defaultは、
    // シーンにLight 2Dが無いと暗く描画されてしまうため、Unlit素材に一括修正する。
    [MenuItem("Tools/パンチ・ザ・迷惑/見た目をUnlitマテリアルに修正")]
    public static void FixMaterialsToUnlit()
    {
        Material unlit = GetUnlitMaterial();
        if (unlit == null)
        {
            Debug.LogError("[ProjectSetup] Sprite-Unlit-Defaultマテリアルが見つかりませんでした。");
            return;
        }

        int count = 0;
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            sr.sharedMaterial = unlit;
            EditorUtility.SetDirty(sr);
            count++;
        }

        foreach (string prefabName in new[] { "BadPerson", "NormalPerson" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{prefabName}.prefab");
            SpriteRenderer psr = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
            if (psr == null) continue;
            psr.sharedMaterial = unlit;
            EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ProjectSetup] {count}個のSpriteRendererをUnlitマテリアルに修正しました。");
    }

    private static Material cachedUnlitMaterial;

    private static Material GetUnlitMaterial()
    {
        if (cachedUnlitMaterial != null) return cachedUnlitMaterial;

        string[] guids = AssetDatabase.FindAssets("Sprite-Unlit-Default t:Material");
        if (guids.Length > 0)
        {
            cachedUnlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (cachedUnlitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null) cachedUnlitMaterial = new Material(shader);
        }

        return cachedUnlitMaterial;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    // フラットデザイン風の丸みキャラ(大きめの頭+小さい体のゆるキャラ体型)を、
    // 円・カプセル形状の組み合わせ+スーパーサンプリングで滑らかな輪郭に描く。
    // isBad=trueだとサングラス・逆立った髪・への字口の「クソな人」になる。
    private static Sprite CreateFlatChibiSprite(string path, bool isBad)
    {
        const int texW = 96;
        const int texH = 128;
        const int ss = 4; // スーパーサンプリング(4x4)でジャギーを抑える
        const int outlineRadius = 3;

        Color32 skin = new Color32(255, 214, 176, 255);
        Color32 hairColor = isBad ? new Color32(45, 45, 50, 255) : new Color32(130, 90, 50, 255);
        Color32 shirtColor = isBad ? new Color32(190, 60, 60, 255) : new Color32(95, 170, 210, 255);
        Color32 pantsColor = new Color32(60, 60, 72, 255);
        Color32 blackColor = new Color32(35, 30, 28, 255);
        Color32 outlineColor = new Color32(35, 28, 26, 255);

        float cx = texW * 0.5f;
        const float headCy = 34f, headR = 28f;
        const float hairR = 31f, hairLimitY = 30f;
        const float bodyTopY = 58f, bodyBotY = 96f, bodyR = 20f;
        float armLx = cx - 28f, armRx = cx + 28f;
        const float armTopY = 62f, armBotY = 90f, armR = 9f;
        float legLx = cx - 11f, legRx = cx + 11f;
        const float legTopY = 96f, legBotY = 122f, legR = 10f;

        bool InCircle(float x, float y, float cxp, float cyp, float r) =>
            (x - cxp) * (x - cxp) + (y - cyp) * (y - cyp) <= r * r;

        bool InCapsule(float x, float y, float cxp, float topY, float botY, float r) =>
            InCircle(x, y, cxp, Mathf.Clamp(y, topY, botY), r);

        bool NearSegment(float x, float y, float x1, float x2, float yLine, float r)
        {
            float cxp = Mathf.Clamp(x, x1, x2);
            float dx = x - cxp, dy = y - yLine;
            return dx * dx + dy * dy <= r * r;
        }

        bool InHead(float x, float y) => InCircle(x, y, cx, headCy, headR);
        bool InHair(float x, float y) => InCircle(x, y, cx, headCy - 3f, hairR) && y <= hairLimitY;
        bool InSpike(float x, float y)
        {
            if (!isBad) return false;
            float top = headCy - hairR - 9f, baseY = headCy - hairR + 5f, halfW = 6f;
            if (y < top || y > baseY) return false;
            float t = Mathf.InverseLerp(top, baseY, y);
            foreach (float sx in new[] { cx - 22f, cx - 8f, cx + 6f, cx + 20f })
                if (Mathf.Abs(x - sx) <= halfW * t) return true;
            return false;
        }
        bool InBody(float x, float y) => InCapsule(x, y, cx, bodyTopY, bodyBotY, bodyR);
        bool InArms(float x, float y) => InCapsule(x, y, armLx, armTopY, armBotY, armR) || InCapsule(x, y, armRx, armTopY, armBotY, armR);
        bool InLegs(float x, float y) => InCapsule(x, y, legLx, legTopY, legBotY, legR) || InCapsule(x, y, legRx, legTopY, legBotY, legR);

        float Coverage(System.Func<float, float, bool> test, int px, int py)
        {
            int hit = 0;
            for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                    if (test(px + (i + 0.5f) / ss, py + (j + 0.5f) / ss)) hit++;
            return hit / (float)(ss * ss);
        }

        var baseColor = new Color[texW, texH];
        var baseAlpha = new float[texW, texH];

        for (int py = 0; py < texH; py++)
        {
            for (int px = 0; px < texW; px++)
            {
                Color c = Color.clear;
                float a = 0f;

                void Blend(Color32 layerColor, float cov)
                {
                    if (cov <= 0f) return;
                    c = Color.Lerp(c, layerColor, cov);
                    a = Mathf.Max(a, cov);
                }

                Blend(pantsColor, Coverage(InLegs, px, py));
                Blend(shirtColor, Coverage(InArms, px, py));
                Blend(shirtColor, Coverage(InBody, px, py));
                Blend(skin, Coverage(InHead, px, py));
                Blend(hairColor, Coverage((x, y) => InHair(x, y) || InSpike(x, y), px, py));

                c.a = a;
                baseColor[px, py] = c;
                baseAlpha[px, py] = a;
            }
        }

        void DrawFeature(System.Func<float, float, bool> test)
        {
            for (int py = 0; py < texH; py++)
            {
                for (int px = 0; px < texW; px++)
                {
                    float cov = Coverage(test, px, py);
                    if (cov <= 0f) continue;
                    Color c = Color.Lerp(baseColor[px, py], blackColor, cov);
                    c.a = Mathf.Max(baseAlpha[px, py], cov);
                    baseColor[px, py] = c;
                    baseAlpha[px, py] = c.a;
                }
            }
        }

        const float eyeY = headCy + 2f;
        float eyeLx = cx - 10f, eyeRx = cx + 10f;
        const float eyeR = 3.2f;
        DrawFeature((x, y) => InCircle(x, y, eyeLx, eyeY, eyeR));
        DrawFeature((x, y) => InCircle(x, y, eyeRx, eyeY, eyeR));

        if (isBad)
        {
            const float glassTop = eyeY - 5f, glassBot = eyeY + 5f;
            DrawFeature((x, y) => x >= eyeLx - 8f && x <= eyeRx + 8f && y >= glassTop && y <= glassBot && InHead(x, y));
            DrawFeature((x, y) => NearSegment(x, y, cx - 7f, cx + 7f, headCy + 13f, 2.2f));
        }
        else
        {
            DrawFeature((x, y) =>
            {
                float dx = x - cx, dy = y - (headCy + 9f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                return dist <= 8.5f && dist >= 6f && dy > -1f;
            });
        }

        // シルエット外周に輪郭線を足す
        var final = (Color[,])baseColor.Clone();
        for (int py = 0; py < texH; py++)
        {
            for (int px = 0; px < texW; px++)
            {
                if (baseAlpha[px, py] > 0.5f) continue;

                bool near = false;
                for (int dy = -outlineRadius; dy <= outlineRadius && !near; dy++)
                {
                    for (int dx = -outlineRadius; dx <= outlineRadius && !near; dx++)
                    {
                        if (dx * dx + dy * dy > outlineRadius * outlineRadius) continue;
                        int nx = px + dx, ny = py + dy;
                        if (nx < 0 || nx >= texW || ny < 0 || ny >= texH) continue;
                        if (baseAlpha[nx, ny] > 0.5f) near = true;
                    }
                }
                if (near) final[px, py] = outlineColor;
            }
        }

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        for (int px = 0; px < texW; px++)
            for (int py = 0; py < texH; py++)
                tex.SetPixel(px, texH - 1 - py, final[px, py]); // 上が0行目の設計座標→テクスチャは下が0行目
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 70f;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateCharacterPrefab(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        Material unlit = GetUnlitMaterial();
        if (unlit != null) sr.sharedMaterial = unlit;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = sprite.bounds.size;
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
            // 画面奥(歩道の先)に横並びで出現させ、Character側で手前へ歩いて近づいてくる
            p.transform.position = new Vector3(-3f + i * 1.5f, 2.0f, 0f);
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

    // 空・ビルのシルエット・歩道の3層で「街を散歩している」雰囲気の背景を作る。
    // 全レイヤーで同じ単色の白スプライトを使い回し、SpriteRenderer.colorで色分けする。
    private static void SetupBackground()
    {
        GameObject root = new GameObject("Background");
        Sprite flat = CreateFlatSprite(SpriteFolder + "/Flat.png");

        CreateBackgroundLayer(root.transform, "Sky", flat,
            new Vector3(0f, 1f, 0f), new Vector3(24f, 14f, 1f), -30,
            new Color32(160, 214, 235, 255));

        (float x, float height, Color32 color)[] buildings =
        {
            (-7f, 3.0f, new Color32(120, 110, 140, 255)),
            (-2.3f, 4.2f, new Color32(100, 95, 125, 255)),
            (2.3f, 3.6f, new Color32(135, 120, 150, 255)),
            (7f, 3.9f, new Color32(110, 100, 132, 255)),
        };
        const float groundTopY = -1.0f;
        foreach ((float x, float height, Color32 color) in buildings)
        {
            float cy = groundTopY + height * 0.5f;
            CreateBackgroundLayer(root.transform, $"Building_{x}", flat,
                new Vector3(x, cy, 0f), new Vector3(3.2f, height, 1f), -20, color);
        }

        CreateBackgroundLayer(root.transform, "Ground", flat,
            new Vector3(0f, -3.5f, 0f), new Vector3(24f, 5f, 1f), -10,
            new Color32(150, 148, 142, 255));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static GameObject CreateBackgroundLayer(Transform parent, string name, Sprite sprite,
        Vector3 position, Vector3 scale, int sortingOrder, Color32 color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        sr.color = color;
        Material unlit = GetUnlitMaterial();
        if (unlit != null) sr.sharedMaterial = unlit;

        return go;
    }

    // 1x1の白い正方形スプライト。Transform.localScaleと色掛け合わせで背景の板として使い回す。
    private static Sprite CreateFlatSprite(string path)
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f; // 4x4px = 1x1ユニット、以降はlocalScaleで拡大縮小する
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
