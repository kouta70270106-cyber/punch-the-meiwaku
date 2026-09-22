using System.Collections.Generic;
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

    // スマホ縦画面(WebGLブラウザ表示想定)向けに、向き・UI基準解像度・配置を調整する。
    [MenuItem("Tools/パンチ・ザ・迷惑/スマホ縦画面用に調整")]
    public static void SetupPortrait()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;

        CanvasScaler scaler = Object.FindAnyObjectByType<CanvasScaler>();
        if (scaler != null)
        {
            scaler.referenceResolution = new Vector2(720f, 1280f);
            EditorUtility.SetDirty(scaler);
        }

        GameObject spawnRoot = GameObject.Find("SpawnPoints");
        if (spawnRoot != null)
        {
            int count = spawnRoot.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                float x = -1.6f + i * 0.8f; // 縦画面の狭い横幅に収まるレーン間隔
                spawnRoot.transform.GetChild(i).position = new Vector3(x, 2.0f, 0f);
            }
        }

        ArrangeBuildingsForPortrait();
        ArrangeUITextForPortrait();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] スマホ縦画面用の調整が完了しました(画面の向き・UI基準解像度・配置)。");
    }

    // UI基準解像度を1280幅→720幅に変えたのに、ScoreText等の位置(±470)がそのままだと
    // 画面の外にはみ出してしまうため、720幅に収まる位置へ調整し直す。
    [MenuItem("Tools/パンチ・ザ・迷惑/UIテキスト位置を縦画面用に調整")]
    public static void ArrangeUITextForPortrait()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            Debug.LogWarning("[ProjectSetup] Canvasが見つかりません。先に「UIを追加」を実行してください。");
            return;
        }

        (string name, float x, float y)[] layout =
        {
            ("ScoreText", -190f, 560f),
            ("TimeText", 190f, 560f),
            ("ComboText", -190f, 510f),
        };

        foreach ((string name, float x, float y) in layout)
        {
            Transform t = canvasGO.transform.Find(name);
            if (t == null) continue;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(x, y);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] UIテキストの位置を縦画面用に調整しました。");
    }

    // 縦画面は横幅が狭いため、ビルは2棟だけ画面の両端に寄せて残し、
    // 中央の2棟は道の邪魔になるので削除する。
    [MenuItem("Tools/パンチ・ザ・迷惑/ビル配置を縦画面用に整理")]
    public static void ArrangeBuildingsForPortrait()
    {
        GameObject background = GameObject.Find("Background");
        if (background == null)
        {
            Debug.LogWarning("[ProjectSetup] Backgroundが見つかりません。先に「歩道の背景を追加」を実行してください。");
            return;
        }

        foreach (string removeName in new[] { "Building_-2.3", "Building_2.3" })
        {
            Transform t = background.transform.Find(removeName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        EnsureFolder(SpriteFolder);
        Color32 windowColor = new Color32(235, 220, 150, 255);

        const float groundTopY = -1.0f;
        (string name, float x, float height, Color32 wallColor)[] buildingLayout =
        {
            ("Building_-7", -2.6f, 4.5f, new Color32(110, 100, 132, 255)),
            ("Building_7", 2.6f, 5.0f, new Color32(130, 118, 148, 255)),
        };
        foreach ((string name, float x, float height, Color32 wallColor) in buildingLayout)
        {
            Transform t = background.transform.Find(name);
            if (t == null) continue;

            Sprite sprite = CreateBuildingSprite($"{SpriteFolder}/{name}.png", wallColor, windowColor);
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = sprite;
                sr.color = Color.white;
            }

            t.position = new Vector3(x, groundTopY + height * 0.5f, 0f);
            t.localScale = new Vector3(1.2f, height / BuildingSpriteHeightUnits, 1f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] ビル配置を縦画面用に整理しました(2棟を画面端へ)。");
    }

    // 手続き生成の空・ビル・歩道・道路を、用意した1枚の街並み画像に差し替える。
    [MenuItem("Tools/パンチ・ザ・迷惑/背景を写真に差し替え")]
    public static void ReplaceBackgroundWithPhoto()
    {
        GameObject background = GameObject.Find("Background");
        if (background == null)
        {
            background = new GameObject("Background");
        }

        // 手続き生成していた各レイヤーは不要になるので撤去する
        foreach (string oldName in new[] { "Sky", "Building_-7", "Building_-2.3", "Building_2.3", "Building_7", "Sidewalk", "Road" })
        {
            Transform t = background.transform.Find(oldName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        const string photoPath = SpriteFolder + "/StreetBackground.jpg";
        AssetDatabase.ImportAsset(photoPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(photoPath);
        if (importer == null)
        {
            Debug.LogError($"[ProjectSetup] {photoPath} が見つかりません。先に画像を配置してください。");
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;

        // ピクセル密度を画像の実幅に合わせ、スプライトの基準サイズを「幅1ユニット」にそろえる
        // (Flat/Road等ほかの背景レイヤーと同じ、localScaleでそのまま最終サイズを指定できる方式)
        importer.GetSourceTextureWidthAndHeight(out int texWidthPx, out int texHeightPx);
        importer.spritePixelsPerUnit = texWidthPx;
        importer.SaveAndReimport();

        Sprite photoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(photoPath);
        if (photoSprite == null)
        {
            Debug.LogError("[ProjectSetup] StreetBackground.jpgのスプライト読み込みに失敗しました。");
            return;
        }

        // 画像の縦横比を保ったまま、カメラの縦画面(高さ10ユニット)に少し余裕を持たせて収める
        float aspect = (float)texWidthPx / texHeightPx;
        const float worldHeight = 11f;
        float worldWidth = worldHeight * aspect;
        float baseHeightUnits = (float)texHeightPx / texWidthPx; // pixelsPerUnit=texWidthPxなので基準幅=1ユニット

        Transform existing = background.transform.Find("StreetBackground");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        CreateBackgroundLayer(background.transform, "StreetBackground", photoSprite,
            Vector3.zero, new Vector3(worldWidth, worldHeight / baseHeightUnits, 1f), -30, Color.white);

        // 画像の道が奥ですぼまる位置(目測)に出現位置を合わせる
        GameObject spawnRoot = GameObject.Find("SpawnPoints");
        if (spawnRoot != null)
        {
            int count = spawnRoot.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                float x = -0.5f + i * 0.25f; // 奥の消失点付近は横幅が狭いのでレーンも狭める
                spawnRoot.transform.GetChild(i).position = new Vector3(x, 0.3f, 0f);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 背景を写真に差し替えました。出現位置も奥の消失点付近に合わせています。");
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

    // Assets/Sprites/CharacterRaw/ に置いた7枚のJPGイラスト(白系の背景で生成されたもの)を、
    // 背景透過→プレハブ化→SpawnManagerへのランダム出現プールとして登録、まで一括で行う。
    [MenuItem("Tools/パンチ・ザ・迷惑/イラスト立ち絵を取り込む")]
    public static void ImportCharacterArt()
    {
        const string rawFolder = SpriteFolder + "/CharacterRaw";
        if (!AssetDatabase.IsValidFolder(rawFolder))
        {
            Debug.LogError($"[ProjectSetup] {rawFolder} が見つかりません。先にイラストを配置してください。");
            return;
        }

        EnsureFolder(PrefabFolder);

        (string file, string prefabName, bool isBad, string voice)[] entries =
        {
            ("bad_phone_zombie", "Bad_PhoneZombie", true, "young_male"),
            ("bad_smoker", "Bad_Smoker", true, "middle_male"),
            ("bad_speaker", "Bad_Speaker", true, "young_male"),
            ("bad_drunk", "Bad_Drunk", true, "middle_male"),
            ("normal_office", "Normal_Office", false, "middle_male"),
            ("normal_student", "Normal_Student", false, "young_male"),
            ("normal_dogwalk", "Normal_DogWalk", false, "female"),
        };

        GameObject hitEffectPrefab = CreateHitEffectPrefab();

        const string audioFolder = "Assets/Audio";
        foreach (string clipName in new[] { "punch_hit", "voice_young_male", "voice_middle_male", "voice_female" })
        {
            AssetDatabase.ImportAsset($"{audioFolder}/{clipName}.mp3");
        }
        AudioClip punchClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioFolder}/punch_hit.mp3");
        Dictionary<string, AudioClip> voiceClips = new Dictionary<string, AudioClip>
        {
            ["young_male"] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioFolder}/voice_young_male.mp3"),
            ["middle_male"] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioFolder}/voice_middle_male.mp3"),
            ["female"] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioFolder}/voice_female.mp3"),
        };
        if (punchClip == null)
        {
            Debug.LogWarning($"[ProjectSetup] {audioFolder}/punch_hit.mp3 が見つかりません。打撃音なしで進めます。");
        }

        List<Character> badPrefabs = new List<Character>();
        List<Character> normalPrefabs = new List<Character>();

        foreach ((string file, string prefabName, bool isBad, string voice) in entries)
        {
            string rawPath = $"{rawFolder}/{file}.jpg";
            string cutoutPath = $"{SpriteFolder}/{prefabName}.png";

            Sprite sprite = CutoutBackgroundToSprite(rawPath, cutoutPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ProjectSetup] {rawPath} の読み込みに失敗したためスキップしました。");
                continue;
            }

            GameObject prefab = CreateCharacterPrefab(prefabName, sprite);
            Character character = prefab.GetComponent<Character>();

            SerializedObject charSO = new SerializedObject(character);
            charSO.FindProperty("hitEffectPrefab").objectReferenceValue = hitEffectPrefab;
            charSO.FindProperty("punchSfx").objectReferenceValue = punchClip;
            if (voiceClips.TryGetValue(voice, out AudioClip voiceClip))
            {
                charSO.FindProperty("voiceSfx").objectReferenceValue = voiceClip;
            }

            // 殴られたポーズの画像(<file>_hit.jpg)があれば取り込んで差し替え用に登録する
            string hitRawPath = $"{rawFolder}/{file}_hit.jpg";
            if (File.Exists(hitRawPath))
            {
                string hitCutoutPath = $"{SpriteFolder}/{prefabName}_Hit.png";
                Sprite hitSprite = CutoutBackgroundToSprite(hitRawPath, hitCutoutPath);
                if (hitSprite != null)
                {
                    charSO.FindProperty("hitSprite").objectReferenceValue = hitSprite;
                }
            }
            charSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);

            if (isBad) badPrefabs.Add(character); else normalPrefabs.Add(character);
        }

        SpawnManager spawnManager = Object.FindAnyObjectByType<SpawnManager>();
        if (spawnManager != null)
        {
            SerializedObject spawnSO = new SerializedObject(spawnManager);
            SetupCharacterPool(spawnSO, "badPersonPrefabs", badPrefabs.ToArray());
            SetupCharacterPool(spawnSO, "normalPersonPrefabs", normalPrefabs.ToArray());
            spawnSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawnManager);
        }
        else
        {
            Debug.LogWarning("[ProjectSetup] SpawnManagerが見つからないため、出現プールへの登録はスキップしました。");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ProjectSetup] イラスト立ち絵を取り込みました(クソ{badPrefabs.Count}種・普通{normalPrefabs.Count}種)。");
    }

    // 実際にUnityのSpriteRenderer+マテリアルでレンダリングした結果をPNGとして書き出す。
    // 生の画像ファイルを見るだけでは分からない、フィルタリングやミップマップに起因する
    // 縁のにじみ等の「実際の見た目」を確認するために使う。
    [MenuItem("Tools/パンチ・ザ・迷惑/確認用プレビュー画像を書き出す")]
    public static void ExportPreviewRenders()
    {
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "render_previews");
        Directory.CreateDirectory(outDir);

        string[] names =
        {
            "Bad_PhoneZombie", "Bad_PhoneZombie_Hit",
            "Bad_Smoker", "Bad_Smoker_Hit",
            "Bad_Speaker", "Bad_Speaker_Hit",
            "Bad_Drunk", "Bad_Drunk_Hit",
            "Normal_Office", "Normal_Office_Hit",
            "Normal_Student", "Normal_Student_Hit",
            "Normal_DogWalk", "Normal_DogWalk_Hit",
            "Fist",
        };

        GameObject camGO = new GameObject("__PreviewCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.55f, 0.85f, 1f); // 市松模様と混同しない単色背景
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.cullingMask = ~0;

        RenderTexture rt = new RenderTexture(512, 683, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        int count = 0;
        foreach (string name in names)
        {
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
            if (sp == null)
            {
                Debug.LogWarning($"[ProjectSetup] {name} のSpriteが見つかりません。スキップしました。");
                continue;
            }

            GameObject go = new GameObject("__PreviewSubject");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            Material unlit = GetUnlitMaterial();
            if (unlit != null) sr.sharedMaterial = unlit;

            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(Path.Combine(outDir, $"{name}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            count++;
        }

        cam.targetTexture = null;
        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);

        Debug.Log($"[ProjectSetup] プレビュー画像を{count}枚、{outDir} に書き出しました。");
    }

    private static void SetupCharacterPool(SerializedObject so, string propertyName, Character[] characters)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = characters.Length;
        for (int i = 0; i < characters.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
        }
    }

    // 白背景(またはJPGで表現された市松模様)を、画像の四辺からの塗りつぶし(flood fill)で
    // 検出して透明化する。人物本体の中にある白い靴などは外周とつながっていないため残る。
    private static Sprite CutoutBackgroundToSprite(string rawPath, string outPath)
    {
        AssetDatabase.ImportAsset(rawPath);
        TextureImporter rawImporter = (TextureImporter)AssetImporter.GetAtPath(rawPath);
        if (rawImporter == null) return null;

        rawImporter.textureType = TextureImporterType.Default;
        rawImporter.isReadable = true;
        rawImporter.textureCompression = TextureImporterCompression.Uncompressed; // 圧縮で市松模様が歪むと塗りつぶしが破綻するため
        rawImporter.maxTextureSize = 4096; // 大きめの画像でも縮小されないようにする
        rawImporter.SaveAndReimport();

        Texture2D raw = AssetDatabase.LoadAssetAtPath<Texture2D>(rawPath);
        if (raw == null) return null;

        int w = raw.width, h = raw.height;
        Color32[] pixels = raw.GetPixels32();

        // 二重チェック方式: 「隣と色が近い(マジックワンド)」かつ「そもそも無彩色で背景っぽい色」
        // の両方を満たす場合だけ背景とみなす。前者だけだとキャラ本体の陰影(グラデーション)を
        // 伝って服の中まで塗りつぶしが漏れてしまうため、後者の色相チェックで歯止めをかける。
        const int similarityTolerance = 100; // 各チャンネル差の合計。市松模様の濃淡を跨げる広さ

        bool IsSimilar(Color32 a, Color32 b)
        {
            int dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Abs(dr) + Mathf.Abs(dg) + Mathf.Abs(db) <= similarityTolerance;
        }

        bool PlausiblyBackground(Color32 c)
        {
            int maxC = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int minC = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            bool grayish = (maxC - minC) < 40; // 服等は多少なりとも色味があることが多い(JPEGノイズ分の余裕を持たせる)
            bool notTooDark = minC > 90;       // 輪郭線やキャラの濃い影を除外
            return grayish && notTooDark;
        }

        bool[] isBackground = new bool[w * h];
        Queue<int> queue = new Queue<int>();

        void Enqueue(int idx, Color32 refColor)
        {
            if (isBackground[idx]) return;
            if (!PlausiblyBackground(pixels[idx])) return;
            if (!IsSimilar(pixels[idx], refColor)) return;
            isBackground[idx] = true;
            queue.Enqueue(idx);
        }

        // 生成画像の縁についていることがある細い黒枠(レターボックス)を突破するため、
        // 外周は色判定を無視して強制的に透明の起点にする。
        void ForceEnqueue(int idx)
        {
            if (isBackground[idx]) return;
            isBackground[idx] = true;
            queue.Enqueue(idx);
        }

        const int borderMargin = 20;
        for (int by = 0; by < borderMargin; by++)
        {
            for (int x = 0; x < w; x++)
            {
                ForceEnqueue(by * w + x);
                ForceEnqueue((h - 1 - by) * w + x);
            }
        }
        for (int bx = 0; bx < borderMargin; bx++)
        {
            for (int y = 0; y < h; y++)
            {
                ForceEnqueue(y * w + bx);
                ForceEnqueue(y * w + (w - 1 - bx));
            }
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x = idx % w, y = idx / w;
            Color32 refColor = pixels[idx];
            if (x > 0) Enqueue(idx - 1, refColor);
            if (x < w - 1) Enqueue(idx + 1, refColor);
            if (y > 0) Enqueue(idx - w, refColor);
            if (y < h - 1) Enqueue(idx + w, refColor);
        }

        // 仕上げ処理: JPEGの圧縮ノイズ等で、色の連なりがわずかに途切れて孤立してしまった
        // 小さな斑点を拾う。「周囲に透明マスがあり、自分も背景っぽい色」なら追加で透明にする、
        // を変化がなくなるまで繰り返す(1回ごとに透明領域が1マスずつ外側へにじむイメージ)。
        for (int pass = 0; pass < 500; pass++)
        {
            List<int> newlyBackground = new List<int>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (isBackground[idx]) continue;
                    if (!PlausiblyBackground(pixels[idx])) continue;

                    bool touchesBackground =
                        (x > 0 && isBackground[idx - 1]) ||
                        (x < w - 1 && isBackground[idx + 1]) ||
                        (y > 0 && isBackground[idx - w]) ||
                        (y < h - 1 && isBackground[idx + w]);

                    if (touchesBackground) newlyBackground.Add(idx);
                }
            }
            if (newlyBackground.Count == 0) break;
            foreach (int idx in newlyBackground) isBackground[idx] = true;
        }

        // RGBは元の色のまま残し、アルファだけ0にする。黒(0,0,0)で塗りつぶすと
        // 拡大縮小時のフィルタリングで輪郭に黒いフチがにじみ出てしまうため。
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isBackground[i]) pixels[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 0);
        }

        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels32(pixels);
        outTex.Apply();

        File.WriteAllBytes(outPath, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);

        AssetDatabase.ImportAsset(outPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = h / 2.2f; // 身長がだいたい2.2ユニットになるよう調整
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
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

    // 画面下に常時表示するプレイヤーの両拳を追加する。右にいる相手を殴ったら右手、
    // 左にいる相手を殴ったら左手が突き出るよう、PlayerHandsControllerと配線する。
    [MenuItem("Tools/パンチ・ザ・迷惑/プレイヤーの拳を追加")]
    public static void SetupPlayerHands()
    {
        if (GameObject.Find("PlayerHands") != null)
        {
            Debug.LogWarning("[ProjectSetup] PlayerHandsは既に追加されています。二重に追加しないためスキップしました。");
            return;
        }

        EnsureFolder(SpriteFolder);
        Sprite fistSprite = CreateFistSprite(SpriteFolder + "/Fist.png");
        Material unlit = GetUnlitMaterial();

        GameObject root = new GameObject("PlayerHands");

        GameObject rightHand = new GameObject("RightHand");
        rightHand.transform.SetParent(root.transform, false);
        rightHand.transform.position = new Vector3(0.9f, -4.6f, 0f);
        SpriteRenderer rightSr = rightHand.AddComponent<SpriteRenderer>();
        rightSr.sprite = fistSprite;
        rightSr.sortingOrder = 20;
        if (unlit != null) rightSr.sharedMaterial = unlit;

        GameObject leftHand = new GameObject("LeftHand");
        leftHand.transform.SetParent(root.transform, false);
        leftHand.transform.position = new Vector3(-0.9f, -4.6f, 0f);
        SpriteRenderer leftSr = leftHand.AddComponent<SpriteRenderer>();
        leftSr.sprite = fistSprite;
        leftSr.flipX = true;
        leftSr.sortingOrder = 20;
        if (unlit != null) leftSr.sharedMaterial = unlit;

        PlayerHandsController controller = root.AddComponent<PlayerHandsController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("leftHand").objectReferenceValue = leftHand.transform;
        so.FindProperty("rightHand").objectReferenceValue = rightHand.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] プレイヤーの拳を追加しました。");
    }

    // Assets/Sprites/CharacterRaw/fist_right.jpg のイラストを取り込んで、
    // 既に配置済みのPlayerHands(手続き生成の赤いグローブ)をこちらに差し替える。
    [MenuItem("Tools/パンチ・ザ・迷惑/拳の見た目をイラストに差し替え")]
    // バッチモード(-executeMethod)から呼ぶ用。batchmodeでは対象シーンが自動で開かれないため、
    // 明示的にGame.unityを開いてから処理し、そのまま保存する。
    public static void BatchFixFistInGameScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        ReplaceFistWithArt();
        EditorSceneManager.SaveOpenScenes();
    }

    public static void ReplaceFistWithArt()
    {
        GameObject playerHands = GameObject.Find("PlayerHands");
        if (playerHands == null)
        {
            Debug.LogWarning("[ProjectSetup] PlayerHandsが見つかりません。先に「プレイヤーの拳を追加」を実行してください。");
            return;
        }

        const string rawFolder = SpriteFolder + "/CharacterRaw";
        string rawPath = $"{rawFolder}/fist_right.jpg";
        if (!File.Exists(rawPath))
        {
            Debug.LogError($"[ProjectSetup] {rawPath} が見つかりません。先に画像を配置してください。");
            return;
        }

        Sprite fistSprite = CutoutBackgroundToSprite(rawPath, $"{SpriteFolder}/Fist.png");
        if (fistSprite == null)
        {
            Debug.LogError("[ProjectSetup] fist_right.jpg の読み込みに失敗しました。");
            return;
        }

        Transform rightHand = playerHands.transform.Find("RightHand");
        Transform leftHand = playerHands.transform.Find("LeftHand");
        if (rightHand != null) rightHand.GetComponent<SpriteRenderer>().sprite = fistSprite;
        if (leftHand != null) leftHand.GetComponent<SpriteRenderer>().sprite = fistSprite;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 拳の見た目をイラストに差し替えました。");
    }

    // 丸みのある赤いグローブ型の拳を描く。左手はSpriteRenderer.flipXで使い回す。
    private static Sprite CreateFistSprite(string path)
    {
        const int texW = 96;
        const int texH = 112;
        const int ss = 4;

        Color32 gloveColor = new Color32(205, 45, 45, 255);
        Color32 gloveShade = new Color32(165, 28, 28, 255);
        Color32 cuffColor = new Color32(240, 238, 230, 255);
        Color32 outlineColor = new Color32(40, 15, 15, 255);

        float cx = texW * 0.5f;
        float mittCy = texH * 0.40f;
        float mittRx = texW * 0.34f, mittRy = texH * 0.32f;
        float thumbCx = cx + texW * 0.30f, thumbCy = texH * 0.42f, thumbR = texW * 0.15f;
        float cuffTop = texH * 0.66f, cuffBot = texH * 0.92f;
        float cuffLeft = cx - texW * 0.24f, cuffRight = cx + texW * 0.24f;

        bool InEllipse(float x, float y, float ecx, float ecy, float rx, float ry)
        {
            float dx = (x - ecx) / rx, dy = (y - ecy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
        bool InMitt(float x, float y) => InEllipse(x, y, cx, mittCy, mittRx, mittRy);
        bool InThumb(float x, float y) => InEllipse(x, y, thumbCx, thumbCy, thumbR, thumbR * 0.85f);
        bool InCuff(float x, float y) => x >= cuffLeft && x <= cuffRight && y >= cuffTop && y <= cuffBot;

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
                float cuffCov = Coverage(InCuff, px, py);
                float mittCov = Coverage((x, y) => InMitt(x, y) || InThumb(x, y), px, py);

                Color c = Color.clear;
                float a = 0f;
                if (mittCov > 0f)
                {
                    // 下寄りをやや暗くして立体感を付ける
                    float shade = Mathf.InverseLerp(mittCy - mittRy, mittCy + mittRy, py);
                    Color mittColor = Color.Lerp(gloveColor, gloveShade, shade * 0.6f);
                    c = Color.Lerp(c, mittColor, mittCov);
                    a = Mathf.Max(a, mittCov);
                }
                if (cuffCov > 0f)
                {
                    c = Color.Lerp(c, cuffColor, cuffCov);
                    a = Mathf.Max(a, cuffCov);
                }

                c.a = a;
                baseColor[px, py] = c;
                baseAlpha[px, py] = a;
            }
        }

        var final = (Color[,])baseColor.Clone();
        const int outlineRadius = 2;
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
                tex.SetPixel(px, texH - 1 - py, final[px, py]);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = texH / 1.7f; // 縦の長さがだいたい1.7ユニットになるよう調整
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // パンチが当たった瞬間に一瞬出す、星型の衝撃エフェクトのプレハブを作る。
    private static GameObject CreateHitEffectPrefab()
    {
        Sprite starSprite = CreateImpactStarSprite(SpriteFolder + "/HitEffect.png");

        GameObject go = new GameObject("HitEffect");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = starSprite;
        sr.sortingOrder = 10; // キャラより手前に表示
        Material unlit = GetUnlitMaterial();
        if (unlit != null) sr.sharedMaterial = unlit;
        go.AddComponent<HitEffect>();

        string prefabPath = $"{PrefabFolder}/HitEffect.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // 角がspikes個ある星型(コミック風の衝撃マーク)を、中心からの角度と半径の関係で描く。
    private static Sprite CreateImpactStarSprite(string path)
    {
        const int size = 128;
        const int spikes = 10;

        Color32 core = new Color32(255, 250, 200, 255);
        Color32 edge = new Color32(255, 170, 30, 255);
        Color32[] pixels = new Color32[size * size];

        float cx = size * 0.5f, cy = size * 0.5f;
        float outerR = size * 0.48f, innerR = size * 0.20f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float wave = (Mathf.Cos(angle * spikes) + 1f) * 0.5f; // 0..1で山谷を作る
                float starR = Mathf.Lerp(innerR, outerR, wave);

                Color32 c;
                if (dist <= starR)
                {
                    float t = Mathf.Clamp01(dist / starR);
                    c = Color32.Lerp(core, edge, t);
                }
                else
                {
                    c = new Color32(0, 0, 0, 0);
                }
                pixels[y * size + x] = c;
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = size / 1.6f; // 直径がだいたい1.6ユニットになるよう調整
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
        SetupCharacterPool(spawnSO, "badPersonPrefabs", new[] { badPrefab.GetComponent<Character>() });
        SetupCharacterPool(spawnSO, "normalPersonPrefabs", new[] { normalPrefab.GetComponent<Character>() });

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

        CreateBackgroundLayer(root.transform, "Sidewalk", flat,
            new Vector3(0f, SidewalkCenterY, 0f), new Vector3(24f, SidewalkHeight, 1f), -15,
            new Color32(178, 172, 160, 255));

        Sprite road = CreateTrapezoidSprite(SpriteFolder + "/Road.png", topWidthRatio: 0.12f);
        CreateBackgroundLayer(root.transform, "Road", road,
            new Vector3(0f, RoadCenterY, 0f), new Vector3(RoadBottomWidth, RoadHeight * 0.5f, 1f), -10,
            new Color32(130, 130, 136, 255));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private const float SidewalkTopY = 1.0f;
    private const float SidewalkHeight = SidewalkTopY - RoadBottomY;
    private const float SidewalkCenterY = (SidewalkTopY + RoadBottomY) * 0.5f;

    // 道路(奥が細く手前が広い台形)のパラメータ。奥の細い先端がキャラの出現位置(y=2.0)に一致する。
    private const float RoadTopY = 2.0f;
    private const float RoadBottomY = -6f;
    private const float RoadHeight = RoadTopY - RoadBottomY;
    private const float RoadCenterY = (RoadTopY + RoadBottomY) * 0.5f;
    private const float RoadBottomWidth = 5.6f;

    // 既存シーンの平らな「Ground」を、奥行きのある台形の「Road」に差し替える。
    [MenuItem("Tools/パンチ・ザ・迷惑/道路を遠近感のある形に変更")]
    public static void SetupRoadPerspective()
    {
        GameObject background = GameObject.Find("Background");
        if (background == null)
        {
            Debug.LogWarning("[ProjectSetup] Backgroundが見つかりません。先に「歩道の背景を追加」を実行してください。");
            return;
        }

        Transform oldGround = background.transform.Find("Ground");
        if (oldGround != null)
        {
            Object.DestroyImmediate(oldGround.gameObject);
        }
        Transform oldRoad = background.transform.Find("Road");
        if (oldRoad != null)
        {
            Object.DestroyImmediate(oldRoad.gameObject);
        }
        Transform oldSidewalk = background.transform.Find("Sidewalk");
        if (oldSidewalk != null)
        {
            Object.DestroyImmediate(oldSidewalk.gameObject);
        }

        EnsureFolder(SpriteFolder);
        Sprite flat = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/Flat.png") ?? CreateFlatSprite(SpriteFolder + "/Flat.png");

        CreateBackgroundLayer(background.transform, "Sidewalk", flat,
            new Vector3(0f, SidewalkCenterY, 0f), new Vector3(24f, SidewalkHeight, 1f), -15,
            new Color32(178, 172, 160, 255));

        Sprite road = CreateTrapezoidSprite(SpriteFolder + "/Road.png", topWidthRatio: 0.12f);
        CreateBackgroundLayer(background.transform, "Road", road,
            new Vector3(0f, RoadCenterY, 0f), new Vector3(RoadBottomWidth, RoadHeight * 0.5f, 1f), -10,
            new Color32(130, 130, 136, 255));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 歩道を敷き直し、道路を遠近感のある台形に変更しました。");
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

    // 屋上の濃い帯+窓の格子模様を焼き込んだビルのスプライト。
    // ベース高さはBuildingSpriteHeightUnits(=4)ユニットなので、localScale.yは height/4 で指定する。
    private const float BuildingSpriteHeightUnits = 4f;

    private static Sprite CreateBuildingSprite(string path, Color32 wallColor, Color32 windowColor)
    {
        const int texW = 40;
        const int texH = 160;
        const int cols = 3;
        const int rows = 9;
        const int roofHeight = 8;

        Color32 roofColor = new Color32(
            (byte)Mathf.Max(0, wallColor.r - 30),
            (byte)Mathf.Max(0, wallColor.g - 30),
            (byte)Mathf.Max(0, wallColor.b - 30),
            255);

        Color32[] pixels = new Color32[texW * texH];
        for (int y = 0; y < texH; y++)
        {
            Color32 rowColor = y < roofHeight ? roofColor : wallColor;
            for (int x = 0; x < texW; x++)
            {
                pixels[y * texW + x] = rowColor;
            }
        }

        float marginX = texW * 0.12f;
        float marginTopY = roofHeight + texH * 0.06f;
        float marginBotY = texH * 0.05f;
        float usableW = texW - marginX * 2f;
        float usableH = texH - marginTopY - marginBotY;
        float cellW = usableW / cols;
        float cellH = usableH / rows;
        float winW = cellW * 0.6f;
        float winH = cellH * 0.55f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float cellCx = marginX + cellW * (c + 0.5f);
                float cellTopY = marginTopY + cellH * r;
                int x0 = Mathf.RoundToInt(cellCx - winW * 0.5f);
                int x1 = Mathf.RoundToInt(cellCx + winW * 0.5f);
                int y0 = Mathf.RoundToInt(cellTopY + (cellH - winH) * 0.5f);
                int y1 = Mathf.RoundToInt(y0 + winH);

                for (int yy = Mathf.Max(0, y0); yy < Mathf.Min(texH, y1); yy++)
                    for (int xx = Mathf.Max(0, x0); xx < Mathf.Min(texW, x1); xx++)
                        pixels[yy * texW + xx] = windowColor;
            }
        }

        // テクスチャ座標(0行目=上)と画像出力(0行目=下)の向きを合わせる
        Color32[] flipped = new Color32[texW * texH];
        for (int y = 0; y < texH; y++)
            for (int x = 0; x < texW; x++)
                flipped[(texH - 1 - y) * texW + x] = pixels[y * texW + x];

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.SetPixels32(flipped);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = texW; // 幅texW px = 1ユニット、高さは texH/texW = 4ユニットがベース
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 下端が全幅・上端がtopWidthRatio倍まで細くなる台形スプライト。
    // 1x1ユニットのベース(pixelsPerUnit=texW)なので、Transform.localScaleで最終サイズを決める。
    private static Sprite CreateTrapezoidSprite(string path, float topWidthRatio)
    {
        const int texW = 128;
        const int texH = 256;

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        Color32 opaque = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[texW * texH];

        for (int y = 0; y < texH; y++)
        {
            // y=0(下端)で全幅、y=texH-1(上端)でtopWidthRatio倍の幅になるよう線形補間
            float t = y / (float)(texH - 1);
            float halfWidthRatio = Mathf.Lerp(1f, topWidthRatio, t) * 0.5f;
            int halfWidthPx = Mathf.RoundToInt(halfWidthRatio * texW);
            int cx = texW / 2;

            for (int x = 0; x < texW; x++)
            {
                bool inside = Mathf.Abs(x - cx) <= halfWidthPx;
                pixels[y * texW + x] = inside ? opaque : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = texW; // texW px = 1ユニット幅がベース
        importer.filterMode = FilterMode.Bilinear;
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
