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

    // 데모는 100유닛짜리 벌판을 멀리서 보는 기준이라 파도가 1cm(0.01)다.
    // 쿼터뷰 카메라(거리 14, 55도)에서는 안 보여서 키운다.
    const float Wave1Height = 0.07f;
    const float Wave2Height = 0.05f;
    const float DepthDistance = 0.9f;   // 얕은색→깊은색 전환 거리. 여기 최대 수심이 0.6~1.0 이다

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

        SetIfHas(existing, "_1st_Wave_Height", Wave1Height);
        SetIfHas(existing, "_2nd_Wave_Height", Wave2Height);
        SetIfHas(existing, "_Depth_Distance",  DepthDistance);
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

    static void SetIfHas(Material m, string prop, float v)
    {
        if (m.HasProperty(prop)) m.SetFloat(prop, v);
        else Debug.LogWarning($"[물세팅] 머티리얼에 {prop} 가 없다. 셰이더 버전이 다를 수 있다.");
    }

    /// <summary>해변 윗면이 수면과 만나는 x. 보고용이라 대략치면 된다.</summary>
    static float ShorelineX()
    {
        float slope = Mathf.Tan(-SeabedRotZ * Mathf.Deg2Rad);          // +X 로 갈수록 내려가는 기울기
        float topAtCenter = SeabedPos.y + SeabedScale.y * 0.5f;
        return SeabedPos.x + (topAtCenter - WaterY) / Mathf.Max(slope, 0.0001f);
    }
}
