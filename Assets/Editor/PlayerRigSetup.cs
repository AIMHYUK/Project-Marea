// ⚠️ 나중에 지운다 — Ray 모델과 이동 애니메이터를 PF_Player에 붙이는 일회성 세팅 도구다.
// 게임 코드가 아니고 빌드에도 안 들어간다(Assets/Editor 안이라 에디터 전용).
// 한 번 붙이고 나면 이 파일은 지워도 된다. 값이 안 맞으면 아래 상수만 고치고 다시 실행하면
// 컨트롤러·모델·컴포넌트를 통째로 다시 만든다. WaterSceneSetup.cs 와 같은 방식이다.
//
// 쓰는 법: 상단 메뉴  Marea > 플레이어 리그 세팅 (Ray + 애니메이터)
//
// 하는 일:
//   1. AC_Player.controller 를 만든다 — Speed(float) 파라미터 + 1D 블렌드 트리(Idle→Walk).
//      클립은 ServingStaff가 쓰는 Sujung 휴머노이드 클립을 리타겟한다(Ray도 Humanoid).
//      run 클립이 오면 BuildController 의 tree.AddChild(run, RunSpeed) 한 줄만 켜면 된다.
//   2. PF_Player 에 Ray 모델을 자식 "Model"로 넣고, 그 Animator에 위 컨트롤러 + 아바타를 건다.
//   3. 루트에 PlayerAnimator 를 붙이고 animator 참조를 채운다.
//   4. 임시 큐브 비주얼은 렌더러만 끈다(충돌체는 남긴다).

using System.IO;
using System.Linq;
using Marea.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerRigSetup
{
    // ── 경로 ──────────────────────────────────────────────────────
    const string PlayerPrefabPath = "Assets/Prefabs/PF_Player.prefab";
    const string RayFbxPath        = "Assets/Art/Asset/Character/Ray/Ray_T pose.fbx";
    const string ControllerPath    = "Assets/Animations/AC_Player.controller";

    // 블렌드 트리 노드 클립. ServingStaff가 쓰는 Sujung 휴머노이드 클립을 그대로 쓴다 —
    // Ray도 Humanoid라 아바타만 다르지 클립은 공유된다(AC_ServingStaff와 같은 GUID).
    const string IdleClipPath = "Assets/Art/Asset/Character/Sujung/Animation/AN_Sujung_Standing Idle.fbx";
    const string WalkClipPath = "Assets/Art/Asset/Character/Sujung/Animation/AN_Sujung_Walking.fbx";

    // 블렌드 트리 임계값(m/s). WASD·클릭 이동 순항 속도가 AgentMover.moveSpeed(=4)라 Walk를
    // 거기 둔다. 순항 중인데 Walk가 덜 섞이면(Idle이 비치면) 이 값을 조금 낮춘다.
    // run 클립이 오면 RunSpeed 자리에 노드를 하나 더 얹으면 gait이 늘어난다.
    const float WalkSpeed = 4f;
    const float RunSpeed  = 8f;   // run 클립 붙일 때 쓸 자리. 지금은 안 쓴다.

    // ── 모델 배치 ─────────────────────────────────────────────────
    // ⚠️ 스케일은 반드시 Model(자식)에만 준다. 루트는 1로 강제한다 — 루트에 NavMeshAgent가
    //    있어서 루트를 키우면 클릭 이동(경로 추종)이 기어간다(WASD는 멀쩡). PF_ServingStaff도
    //    루트 1 / Model 150 구조다. Ray FBX가 1/150로 작게 들어와서 150배가 필요하다.
    //    Ray가 바닥을 뚫거나 뜨면 ModelOffset.y 를, 크기가 안 맞으면 ModelScale 을 고치고 재실행.
    const string ModelChildName = "Model";
    static readonly Vector3 ModelOffset = new(0f, 0f, 0f);
    static readonly Vector3 ModelScale  = new(150f, 150f, 150f);

    [MenuItem("Marea/플레이어 리그 세팅 (Ray + 애니메이터)")]
    public static void Setup()
    {
        AnimatorController controller = BuildController();
        if (controller == null) return;   // 클립·경로 문제면 BuildController가 이미 에러를 냈다

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerRigSetup] {PlayerPrefabPath} 를 못 열었다. 경로를 확인할 것.");
            return;
        }

        try
        {
            HideOldCube(root);
            if (!SetupModel(root, controller)) return;
            WireAnimatorComponent(root);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[PlayerRigSetup] 완료 — PF_Player에 Ray + PlayerAnimator + AC_Player를 붙였다. " +
                      "▶ 플레이해서 걸을 때 Walking, 멈추면 Idle이 나오는지 확인. " +
                      "Ray가 바닥과 안 맞으면 ModelOffset.y / ModelScale을 고치고 메뉴를 다시 누를 것.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 1) 컨트롤러 ───────────────────────────────────────────────

    static AnimatorController BuildController()
    {
        AnimationClip idle = LoadClip(IdleClipPath);
        AnimationClip walk = LoadClip(WalkClipPath);
        if (idle == null || walk == null) return null;

        // 매번 새로 만든다. 컨트롤러를 인스펙터에서 직접 만지기 시작했으면 이 메뉴를
        // 다시 누르지 말 것 — 손으로 맞춘 게 통째로 날아간다.
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // 속도 하나로 Idle→Walk(→Run)를 섞는 1D 블렌드 트리. 상태·전환을 안 늘리고
        // 임계값에 노드만 더하면 gait이 추가된다 — run이 공짜인 이유다.
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;   // 임계값을 m/s로 직접 준다
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        // run 클립이 생기면 아래 한 줄만 켜면 Walk→Run이 붙는다:
        // tree.AddChild(LoadClip(RunClipPath), RunSpeed);

        controller.layers[0].stateMachine.defaultState = locomotion;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip LoadClip(string path)
    {
        // FBX 안의 AnimationClip 서브에셋을 꺼낸다. 에디터 미리보기용(__preview)은 거른다.
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

        if (clip == null)
            Debug.LogError($"[PlayerRigSetup] 클립을 못 찾았다: {path} — Sujung 애니메이션 임포트를 확인할 것.");
        return clip;
    }

    // ── 2) Ray 모델 ───────────────────────────────────────────────

    static bool SetupModel(GameObject root, AnimatorController controller)
    {
        // 이미 붙여둔 Model이 있으면 지우고 새로. 다시 실행해도 쌓이지 않게.
        Transform existing = root.transform.Find(ModelChildName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // 루트는 스케일 1로 강제한다. NavMeshAgent가 루트에 있어서, 루트를 키우면 클릭 이동이
        // 스케일만큼 브레이크 거리를 부풀려 기어간다 — 이 버그를 손으로 밟았던 자리다. (+9/23)
        // 캐릭터 크기는 바로 아래 Model 스케일로 키운다.
        root.transform.localScale = Vector3.one;

        var rayFbx = AssetDatabase.LoadAssetAtPath<GameObject>(RayFbxPath);
        if (rayFbx == null)
        {
            Debug.LogError($"[PlayerRigSetup] Ray FBX를 못 찾았다: {RayFbxPath}");
            return false;
        }

        // 프리팹 편집용 프리뷰 씬에 넣어야 한다 — root.scene을 명시하지 않으면 활성 씬으로 간다.
        var model = (GameObject)PrefabUtility.InstantiatePrefab(rayFbx, root.scene);
        model.name = ModelChildName;
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = ModelOffset;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = ModelScale;

        // 휴머노이드 FBX는 임포트되며 Animator(+아바타)를 자동으로 얻는다. 없으면 만들어 붙인다.
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;   // 위치는 AgentMover가 민다. 켜면 서로 싸운다.

        // 아바타는 FBX가 들고 온 걸 그대로 쓴다. null이면 리그가 Humanoid가 아니거나 생성 실패다.
        if (animator.avatar == null)
            Debug.LogWarning("[PlayerRigSetup] Ray 모델에 아바타가 없다. FBX Rig를 Humanoid로 두었는지 확인할 것 — "
                           + "이대로면 Sujung 클립이 리타겟되지 않는다.");
        return true;
    }

    // ── 3) 드라이버 컴포넌트 ──────────────────────────────────────

    static void WireAnimatorComponent(GameObject root)
    {
        PlayerAnimator pa = root.GetComponent<PlayerAnimator>();
        if (pa == null) pa = root.AddComponent<PlayerAnimator>();

        // animator 참조는 비워둬도 PlayerAnimator.Awake가 자식에서 찾는다.
        // 그래도 명시해두면 인스펙터에서 바로 보인다.
        var so = new SerializedObject(pa);
        so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 4) 임시 큐브 ──────────────────────────────────────────────

    static void HideOldCube(GameObject root)
    {
        // 큐브는 임시 비주얼이다. Ray를 넣으면 겹치니 렌더러만 끈다.
        // 지우지 않는 이유: 큐브에 BoxCollider + Rigidbody가 달려 있어서, 그게 클릭·트리거에
        // 쓰이는지 여기서 단정할 수 없다. 눈에서만 지우고 충돌체는 남긴다.
        Transform cube = root.transform.Find("Cube");
        if (cube == null) return;

        var mr = cube.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
    }
}
