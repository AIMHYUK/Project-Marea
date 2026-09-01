// ⚠️ 나중에 지운다 — 물 셰이더를 눈으로 확인하려고 만든 임시 에디터 도구다.
// 게임 코드가 아니고 빌드에도 안 들어간다(Assets/Editor 안이라 에디터 전용).
// 바다 연출이 확정되면 결과 오브젝트만 씬에 남기고 이 파일은 삭제한다.
//
// 쓰는 법: 상단 메뉴 Marea > 물 세팅 만들기
//          값이 마음에 안 들면 아래 상수만 고치고 다시 실행하면 통째로 다시 만든다.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

public static class WaterSceneSetup
{
    // ── 이 씬에 맞춘 값 ────────────────────────────────────────────
    // Junhyuk_TestScene 은 Plane(스케일 10) = 100x100 바닥이 y=0 에 깔려 있다.
    // y=0 아래는 그 바닥에 전부 가려지므로, 해변을 바닥 "위에" 올린다.
    // 플레이 오브젝트가 x∈[-10,5] 에 몰려 있어서 해변은 x=7 부터 시작한다.

    const float SeabedRotZ   = -1.2f;                       // 해변 기울기(도). 음수면 +X 쪽으로 내려간다
    static readonly Vector3 SeabedPos   = new(30f, -0.6f, 6f);
    static readonly Vector3 SeabedScale = new(46f, 2f, 70f);

    const float WaterY = 0.6f;                              // 수면 높이
    static readonly Vector3 WaterCenter = new(42f, WaterY, 6f);
    static readonly Vector2 WaterSize   = new(70f, 80f);    // 월드 기준 가로x세로. 메시 크기를 재서 맞춘다

    // ⚠️ 템플릿 8개 중 Wavy / Wavy 2 만 정점 파도(_ENABLEWAVE)가 켜져 있다.
    //    나머지(Tropical 포함)는 파도를 끄고 노멀맵으로만 표면을 만든다.
    //    토글이 꺼져 있으면 셰이더가 그 분기를 컴파일에서 아예 빼기 때문에,
    //    높이만 올리고 토글을 안 켜면 아무 일도 안 일어난다.
    const bool  EnableWave  = true;

    // 인스펙터에서 직접 값을 맞추기 시작했으면 이걸 false 로 내려라.
    // 그러면 메뉴를 다시 눌러도 씬 오브젝트만 다시 만들고 머티리얼은 안 건드린다.
    // 안 그러면 손으로 맞춘 파도 값이 아래 상수로 매번 되돌아간다.
    const bool  TuneMaterial = true;

    // ── 파도 ──────────────────────────────────────────────────────
    // 정점 파도가 둘뿐이라 값을 잘못 주면 바로 격자가 보인다. 세 가지를 지킨다.
    //
    //  1. 파장 비가 떨어지면 안 된다. 3 과 5 는 최소공배수가 15 라
    //     15유닛마다 똑같은 무늬가 반복된다. 70x80 판에 4~5번 들어와서 눈에 띈다.
    //  2. 두 파의 방향이 직교하면 다이아몬드 격자가 생긴다. 실제 바다는
    //     한 방향 스웰에 약간의 산포다. 사이각 30도쯤으로 좁힌다.
    //  3. 높이가 비슷하면 둘이 대등하게 싸운다. 주 스웰 / 잔물결로 나눈다.
    //  4. 비율보다 먼저 절대 파장을 본다. 화면에 보이는 물이 30~40유닛인데
    //     파장이 2.7 이면 마루가 12~15개 들어와서 바다가 아니라 골판지가 된다.
    //     쿼터뷰에서 바다로 읽히려면 화면에 마루가 3~4개다.
    //  5. 사이각은 55~70도. 135도면 다이아몬드 격자, 30도면 평행한 골이 된다.
    //     둘 다 "균일하다"로 보인다.
    //
    // 그래도 2개로는 격자를 완전히 못 없앤다. 나머지는 노멀맵이 깨야 한다
    // (_Normal_Strength / _Normal_Scale — 이건 스크립트가 안 건드린다).

    const float Wave1Height = 0.12f;    // 주 스웰. 데모 기본은 0.01(1cm)이라 쿼터뷰에선 안 보인다
    const float Wave1Length = 13f;      // 화면에 보이는 물이 30~40유닛이라 마루가 3~4개 들어온다
    const float Wave1Speed  = 0.9f;
    const float Wave1Sharp  = 0.4f;
    static readonly Vector4 Wave1Dir = new(1f, 0f, 0.25f, 0f);

    const float Wave2Height = 0.05f;    // 잔물결. 1st 와 2.4:1
    const float Wave2Length = 7.4f;     // 13 과 비가 1.76
    const float Wave2Speed  = 1.45f;    // 0.9 와 비가 1.61
    const float Wave2Sharp  = 0.2f;     // 1st 보다 둥글게. 성격을 다르게
    static readonly Vector4 Wave2Dir = new(0.75f, 0f, -0.66f, 0f);
    // 얕은색 → 깊은색이 완전히 벌어지는 수심. 여기 최대 수심이 0.6 이라 거기 맞춘다.
    // _WorldSpaceDepth 토글이 켜져 있어서(기본 1) 이 값은 월드 유닛이다.
    //
    // ⚠️ 템플릿 머티리얼에 있는 _Depth_Distance 는 죽은 키다. 셰이더가 노출하지 않는다.
    //    셰이더그래프에서 이름이 바뀌었는데 Unity 가 m_Floats 의 고아 항목을 안 지운다.
    //    인스펙터에도 안 보이니 그 값을 만지려 하지 마라 — 아무 일도 안 일어난다.
    const float WaterDepth = 0.6f;

    const string AssetRoot   = "Assets/Shaders/Uber Stylized Water";
    const string SourceMat   = AssetRoot + "/Template Materials/UWa-Template-Tropical.mat";
    const string TunedMatDir = "Assets/Materials";
    const string TunedMat    = TunedMatDir + "/UWa-Marea-Sea.mat";
    const string RootName    = "Sea (임시)";

    // 바다 바닥 재질. 데모 지형이 쓰던 모래 텍스처를 그대로 빌려 쓴다.
    // 물 셰이더는 바닥이 무슨 재질인지 모른다(깊이만 읽는다) — 이건 순전히 보기용이다.
    // 안 붙이면 회색 콘크리트 판 위에서 코스틱이 어른거린다.
    const string SandAlbedo = AssetRoot + "/Demo/Terrain/sand_01_color_2k.png";
    const string SandNormal = AssetRoot + "/Demo/Terrain/sand_01_normal_gl_2k.png";
    static readonly Vector2 SandTiling = new(12f, 18f);   // 큐브가 46x70 이라 약 4유닛마다 반복

    const string SeabedMat   = TunedMatDir + "/Seabed-Sand.mat";

    [MenuItem("Marea/물 세팅 만들기")]
    public static void Build()
    {
        var prefab = FindWaterPrefab();
        if (prefab == null)
        {
            Debug.LogError($"[물세팅] 물 템플릿 프리팹을 못 찾았다. {AssetRoot}/Prefabs/ 를 확인해라.");
            return;
        }

        Clear();   // 여러 번 눌러도 하나만 남게

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "물 세팅");

        // ── 해변(바다 바닥) ────────────────────────────────────────
        // 이 셰이더는 "내 밑에 뭐가 얼마나 가까이 있나"로 색·거품·코스틱을 만든다.
        // 밑에 아무것도 없으면 전부 최심부 색 하나로 칠해진다 — 그게 파랑파랑한 판이다.
        var seabed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seabed.name = "Seabed";
        seabed.transform.SetParent(root.transform, false);
        seabed.transform.SetPositionAndRotation(SeabedPos, Quaternion.Euler(0f, 0f, SeabedRotZ));
        seabed.transform.localScale = SeabedScale;
        ApplyMaterial(seabed, GetOrCreateSeabedMaterial());

        // ── 수면 ───────────────────────────────────────────────────
        var water = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        water.name = "Water";
        water.transform.SetParent(root.transform, false);
        water.transform.SetPositionAndRotation(WaterCenter, Quaternion.identity);
        water.transform.localScale = Vector3.one;

        FitToSize(water, WaterSize);
        ApplyMaterial(water, GetOrCreateTunedMaterial());

        // 클릭 레이캐스트를 가리지 않게. ClickSelector 의 interactableMask 가 ~0(전부)이고
        // Physics.Raycast 는 가장 가까운 것 하나만 주므로, 물에 콜라이더가 있으면
        // 물 너머의 밭·요리대가 에러 없이 클릭이 안 잡힌다.
        int removed = 0;
        foreach (var c in water.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); removed++; }

        // NavMesh 를 다시 구우면 수면이 걸어다닐 수 있는 바닥이 된다(Collect Objects = All Game Objects).
        // 물 위를 걷지 않게 Not Walkable 로 박아둔다.
        var mod = water.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1;   // 1 = Not Walkable

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        float shoreX = ShorelineX();
        Debug.Log($"[물세팅] 완료. 물가 선이 대략 x={shoreX:F1} 근처다. " +
                  $"수심은 0 → {WaterY:F2} 로 벌어진다. 콜라이더 {removed}개 제거." +
                  "\n  NavMesh 를 다시 구워야 해변을 걸을 수 있다 (NavMeshSurface > Bake).");
    }

    [MenuItem("Marea/물 세팅 지우기")]
    public static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }

    // ── 이하 잡일 ──────────────────────────────────────────────────

    /// <summary>프리팹 파일명에 오타가 있다("Water Tempate Tropical"). 이름으로 찾지 않고 폴더를 뒤진다.</summary>
    static GameObject FindWaterPrefab()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Water Template/"))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    /// <summary>메시 실제 크기를 재서 원하는 월드 크기가 되도록 스케일을 맞춘다. 평면 크기를 추측하지 않는다.</summary>
    static void FitToSize(GameObject go, Vector2 target)
    {
        var r = go.GetComponentInChildren<Renderer>();
        if (r == null) return;

        // 루트 스케일을 1로 되돌린 상태의 월드 크기를 잰다.
        // 프리팹 안쪽에 스케일이 걸린 자식이 있어도 이러면 안 틀린다.
        go.transform.localScale = Vector3.one;
        Vector3 size = r.bounds.size;
        if (size.x <= 0.0001f || size.z <= 0.0001f) return;

        go.transform.localScale = new Vector3(target.x / size.x, 1f, target.y / size.z);
    }

    static void ApplyMaterial(GameObject go, Material mat)
    {
        if (mat == null) return;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterial = mat;
    }

    /// <summary>
    /// 템플릿 머티리얼을 건드리지 않고 복사본을 만든다.
    /// 서드파티 폴더 밖에 두는 이유 — 에셋을 업데이트하면 그 폴더는 통째로 덮인다.
    /// </summary>
    static Material GetOrCreateTunedMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(TunedMat);
        if (existing == null)
        {
            EnsureMaterialFolder();

            if (!AssetDatabase.CopyAsset(SourceMat, TunedMat))
            {
                Debug.LogError($"[물세팅] 머티리얼 복사 실패: {SourceMat}");
                return null;
            }
            AssetDatabase.ImportAsset(TunedMat);
            existing = AssetDatabase.LoadAssetAtPath<Material>(TunedMat);
        }

        if (existing == null) return null;

        if (!TuneMaterial) return existing;   // 인스펙터에서 직접 맞추는 중이면 손대지 않는다

        SetToggle(existing, "_ENABLEWAVE", EnableWave);
        SetIfHas(existing, "_Water_Depth", WaterDepth);

        SetIfHas(existing, "_1st_Wave_Height",    Wave1Height);
        SetIfHas(existing, "_1st_Wave_Length",    Wave1Length);
        SetIfHas(existing, "_1st_Wave_Speed",     Wave1Speed);
        SetIfHas(existing, "_1st_Wave_Sharpness", Wave1Sharp);
        SetVectorIfHas(existing, "_1st_Wave_Direction", Wave1Dir);

        SetIfHas(existing, "_2nd_Wave_Height",    Wave2Height);
        SetIfHas(existing, "_2nd_Wave_Length",    Wave2Length);
        SetIfHas(existing, "_2nd_Wave_Speed",     Wave2Speed);
        SetIfHas(existing, "_2nd_Wave_Sharpness", Wave2Sharp);
        SetVectorIfHas(existing, "_2nd_Wave_Direction", Wave2Dir);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    /// <summary>모래 바닥 머티리얼. 없으면 URP/Lit 로 새로 만든다.</summary>
    static Material GetOrCreateSeabedMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SeabedMat);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[물세팅] URP/Lit 셰이더를 못 찾았다. 바닥은 기본 머티리얼로 둔다.");
                return null;
            }

            EnsureMaterialFolder();
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, SeabedMat);
        }

        if (!TuneMaterial) return mat;   // 물 머티리얼과 같은 규칙

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(SandAlbedo);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(SandNormal);

        if (albedo != null) mat.SetTexture("_BaseMap", albedo);
        else Debug.LogWarning($"[물세팅] 모래 텍스처가 없다: {SandAlbedo} (Demo/Terrain 을 지웠나?)");

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");   // 이거 안 켜면 노멀맵을 꽂아도 아무 일도 안 일어난다
        }

        mat.SetTextureScale("_BaseMap", SandTiling);
        mat.SetFloat("_Smoothness", 0.25f);    // 젖은 모래. 1이면 거울이 된다
        mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(TunedMatDir))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    /// <summary>
    /// [Toggle(_XXX)] 로 선언된 기능 토글을 켠다.
    /// float 값만 바꾸면 인스펙터 체크박스만 움직이고 셰이더는 그대로다 —
    /// 키워드를 같이 켜야 그 분기가 컴파일에 들어간다.
    /// </summary>
    static void SetVectorIfHas(Material m, string prop, Vector4 v)
    {
        if (m.HasProperty(prop)) m.SetVector(prop, v);
        else Debug.LogWarning($"[물세팅] 셰이더가 {prop} 를 노출하지 않는다.");
    }

    static void SetToggle(Material m, string keyword, bool on)
    {
        if (m.HasProperty(keyword)) m.SetFloat(keyword, on ? 1f : 0f);
        if (on) m.EnableKeyword(keyword);
        else    m.DisableKeyword(keyword);
    }

    static void SetIfHas(Material m, string prop, float v)
    {
        if (m.HasProperty(prop)) m.SetFloat(prop, v);
        else Debug.LogWarning($"[물세팅] 셰이더가 {prop} 를 노출하지 않는다. " +
                              "머티리얼 파일에 값이 보여도 죽은 키일 수 있다 — .shader 의 Properties 블록을 봐라.");
    }

    /// <summary>해변 윗면이 수면과 만나는 x. 보고용이라 대략치면 된다.</summary>
    static float ShorelineX()
    {
        float slope = Mathf.Tan(-SeabedRotZ * Mathf.Deg2Rad);          // +X 로 갈수록 내려가는 기울기
        float topAtCenter = SeabedPos.y + SeabedScale.y * 0.5f;
        return SeabedPos.x + (topAtCenter - WaterY) / Mathf.Max(slope, 0.0001f);
    }
}
