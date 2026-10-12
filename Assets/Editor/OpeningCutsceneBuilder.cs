using System.Collections.Generic;
using Marea.Cutscene;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 오프닝 컷신 초안을 만든다 — 프리팹 · 타임라인 · 신호 · 고래 경로 클립. (+10/12)
///
/// 열린 씬(Main과 같은 배치)의 조리대 · 바닥을 보고 아빠 · 카메라 자리를 잡는다. 프리팹은 월드 원점에 둔다
/// (안의 좌표가 월드 좌표다). 다시 돌리면 타임라인 · 프리팹을 덮어쓴다 — 에디터에서 다듬은 뒤엔 돌리지 말 것.
/// </summary>
public static class OpeningCutsceneBuilder
{
    private const string Dir = "Assets/Cutscene/Opening";
    private const string PrefabPath = "Assets/Prefabs/Cutscene/PF_OpeningCutscene.prefab";

    // 컷 시간(초)
    private const float SeaAt = 4f, CloseAt = 8.5f, JumpAt = 10.5f, DadAt = 12.8f, RunAt = 13.1f, ImpactAt = 13.8f, SmashAt = 14.6f, SinkAt = 15.6f, AfterAt = 17.5f, End = 20f;
    private const float SeaLevel = 6.9f, SwimY = 3.9f;   // WhaleSwimmer는 6.9 - 6.4(거의 잠김). 컷신은 등이 보이게 3m만 잠근다
    // 주방(파란 지붕, x 59~78 · z 34~54) 서쪽의 확장 데크(ExpansionSite_Lv03)에 떨어진다. 북쪽(z 56~)이 열린 바다다.
    private static readonly Vector3 ImpactXZ = new(50f, 0f, 44f);

    [MenuItem("Marea/컷신/오프닝 초안 만들기")]
    public static void Build()
    {
        GameObject station = GameObject.Find("PF_CookingStation");
        var swimmer = Object.FindAnyObjectByType<Marea.Field.WhaleSwimmer>(FindObjectsInactive.Include);
        GameObject sceneWhale = swimmer != null ? swimmer.gameObject : null;
        if (station == null) { Debug.LogError("[오프닝] 열린 씬에 PF_CookingStation이 없다. Main 배치의 씬에서 돌릴 것."); return; }

        // 다시 돌리면 이전 것을 지우고 새로 만든다.
        GameObject old = GameObject.Find("PF_OpeningCutscene");
        if (old != null) Object.DestroyImmediate(old);
        AssetDatabase.DeleteAsset($"{Dir}/TL_Opening.playable");
        AssetDatabase.DeleteAsset($"{Dir}/SG_OpeningSmash.signal");
        AssetDatabase.DeleteAsset($"{Dir}/SG_OpeningRun.signal");
        System.IO.Directory.CreateDirectory(Dir);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));

        var root = new GameObject("PF_OpeningCutscene");
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        var cutscene = root.AddComponent<OpeningCutscene>();
        var receiver = root.AddComponent<SignalReceiver>();
        var content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);

        // --- 아빠: 조리대 뒤(-forward)에서 조리대를 본다 ---
        Vector3 fwd = station.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 dadPos = Floor(station.transform.position - fwd * 1.1f, station.transform);
        GameObject dadModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Asset/Character/NPC_01/Npc01_T-Pose.fbx"));
        dadModel.name = "Dad";
        dadModel.transform.SetParent(content.transform, false);
        dadModel.transform.SetPositionAndRotation(dadPos, Quaternion.LookRotation(fwd));
        dadModel.transform.localScale = Vector3.one * 3f;   // (+10/12) PF_Customer(2)의 1.5배
        if (!dadModel.TryGetComponent(out Animator dadAnim)) dadAnim = dadModel.AddComponent<Animator>();
        dadAnim.applyRootMotion = false;
        Vector3 dadHead = dadPos + Vector3.up * 2.55f;   // (+10/12) 1.5배 키

        // --- 고래: 경로는 Whale, 헤엄 · 점프는 Model ---
        // 경로를 Animator가 붙은 오브젝트 자신에 걸면 Timeline이 루트 모션(시작점 기준 이동량)으로 바꿔 버린다.
        // 그래서 WhaleRig(Animator)의 자식 Whale을 경로 "Whale"로 움직인다 — 값이 그대로 월드 좌표다(프리팹이 원점).
        var rig = new GameObject("WhaleRig");
        rig.transform.SetParent(content.transform, false);
        rig.AddComponent<Animator>();
        var whale = new GameObject("Whale");
        whale.transform.SetParent(rig.transform, false);
        GameObject whaleModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Asset/Whale/FT_Whale_Rigged.fbx"));
        whaleModel.name = "Model";
        whaleModel.transform.SetParent(whale.transform, false);
        whaleModel.transform.localScale = Vector3.one * 11f;            // 씬 고래와 같다
        whaleModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 모델 앞이 -z
        if (!whaleModel.TryGetComponent(out Animator whaleAnim)) whaleAnim = whaleModel.AddComponent<Animator>();
        whaleAnim.runtimeAnimatorController = null;
        whaleAnim.applyRootMotion = false;
        CopyMaterials(sceneWhale, whaleModel);
        var whaleAim = new GameObject("Aim").transform;
        whaleAim.SetParent(whale.transform, false);
        whaleAim.localPosition = new Vector3(0f, 2f, 3f);

        // 경로(타임라인 SeaAt부터의 시간). 북쪽 바다에서 동쪽으로 헤엄 → 남쪽으로 돌아 → 데크로 뛰어든다.
        Vector3 impact = Floor(ImpactXZ + Vector3.up * 10f, station.transform);
        var path = new (float t, Vector3 p, Vector3 euler)[]
        {
            (0f,                 new Vector3(impact.x - 14f, SwimY, impact.z + 22f), new Vector3(0f, 90f, 0f)),
            (CloseAt - SeaAt,    new Vector3(impact.x - 4f,  SwimY, impact.z + 23f), new Vector3(0f, 100f, 0f)),
            (JumpAt - SeaAt,     new Vector3(impact.x,       SwimY, impact.z + 20f), new Vector3(0f, 170f, 0f)),
            (12.3f - SeaAt,      new Vector3(impact.x,       impact.y + 11f, impact.z + 11f), new Vector3(-15f, 180f, 0f)),
            (SmashAt - SeaAt,    new Vector3(impact.x,       impact.y + 1f, impact.z), new Vector3(35f, 180f, 0f)),
            (SinkAt - SeaAt,     new Vector3(impact.x,       SeaLevel - 7f, impact.z - 3f), new Vector3(55f, 180f, 0f)),   // 데크를 뚫고 바닷속으로
        };
        AnimationClip pathClip = PathClip(path);

        // --- 부서지는 자리 ---
        var smashPoints = new List<Transform>();
        foreach (Vector3 off in new[] { Vector3.zero, new Vector3(-4f, 0f, -3f), new Vector3(4f, 0f, -2f) })
        {
            var p = new GameObject("SmashPoint").transform;
            p.SetParent(content.transform, false);
            p.position = Floor(impact + off, station.transform);
            smashPoints.Add(p);
        }
        var debris = new GameObject("Debris");
        debris.transform.SetParent(content.transform, false);
        string[] pieces = { "PF_BrokenPlank_HoleLarge", "PF_BrokenPlank_HoleDouble", "PF_BrokenPlank_HoleSingle", "PF_BrokenPlank_Split",
                            "PF_LoosePlank_Long", "PF_LoosePlank_Short", "PF_WoodDamage_HoleLarge", "PF_WoodDamage_Crack", "PF_WoodDamage_HoleCrack" };
        var rng = new System.Random(7);
        for (int i = 0; i < 14; i++)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Asset/Island/WoodDamage/{pieces[i % pieces.Length]}.prefab");
            if (src == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.SetParent(debris.transform, false);
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1f + (float)rng.NextDouble() * 7f;
            go.transform.position = Floor(impact + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.8f), station.transform);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * go.transform.rotation;
        }

        // --- 카메라 ---
        Transform cams = new GameObject("Cameras").transform;
        cams.SetParent(content.transform, false);
        // 자리는 "대상에서 이쪽으로 이만큼"을 주고, 시야가 막히면 대상 둘레로 돌려 트인 곳을 고른다(Clear).
        Vector3 dadChest = dadHead - Vector3.up * 0.5f;
        // 주방 안 컷(요리 · 아빠 클로즈업)은 아빠 뒤 · 옆에서, 바다 · 점프 · 파손 컷은 지붕 밖 서쪽 데크에서 찍는다.
        CinemachineCamera camCook = Cam(cams, "CM_Cook", Clear(dadChest, dadHead - fwd * 3f + Vector3.right * 1.2f + Vector3.up * 1.2f), dadChest, 42f, null);
        CinemachineCamera camSea = Cam(cams, "CM_Sea", new Vector3(impact.x - 5f, impact.y + 2.2f, impact.z + 7f), path[0].p, 45f, whaleAim);
        CinemachineCamera camClose = Cam(cams, "CM_WhaleClose", path[1].p + new Vector3(9f, SeaLevel + 3f - SwimY, -11f), path[1].p, 40f, whaleAim);
        CinemachineCamera camJump = Cam(cams, "CM_Jump", new Vector3(impact.x - 3f, impact.y + 2.5f, impact.z - 11f), impact + new Vector3(0f, 8f, 10f), 60f, whaleAim);
        CinemachineCamera camDad = Cam(cams, "CM_DadClose", Clear(dadHead, dadHead + Quaternion.Euler(0f, -15f, 0f) * fwd * 2.4f + Vector3.up * 0.15f), dadHead, 32f, null);
        CinemachineCamera camImpact = Cam(cams, "CM_Impact", new Vector3(impact.x - 7f, impact.y + 4f, impact.z - 14f), impact + Vector3.up * 4f, 58f, null);
        CinemachineCamera camAfter = Cam(cams, "CM_After", new Vector3(impact.x - 10f, impact.y + 10f, impact.z - 12f), impact, 50f, null);
        // (+10/12) 터질 때 흔들기 — 충돌 · 뒤 컷 카메라가 Impulse를 받는다.
        camImpact.gameObject.AddComponent<CinemachineImpulseListener>();
        camAfter.gameObject.AddComponent<CinemachineImpulseListener>();
        var impulse = root.AddComponent<CinemachineImpulseSource>();
        impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
        impulse.ImpulseDefinition.ImpulseDuration = 1f;

        var audioSource = content.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // --- 타임라인 ---
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, $"{Dir}/TL_Opening.playable");
        AssetDatabase.AddObjectToAsset(pathClip, timeline);
        director.playableAsset = timeline;

        var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cameras");
        Shot(cmTrack, director, camCook, 0f, SeaAt);
        Shot(cmTrack, director, camSea, SeaAt, CloseAt);
        Shot(cmTrack, director, camClose, CloseAt, JumpAt);
        Shot(cmTrack, director, camJump, JumpAt, DadAt);
        Shot(cmTrack, director, camDad, DadAt, ImpactAt);
        Shot(cmTrack, director, camImpact, ImpactAt, AfterAt);
        Shot(cmTrack, director, camAfter, AfterAt, End);

        // (+10/12) 아빠는 조리대 앞에 서 있다가 RunAt에 고래 반대쪽(충돌 지점 → 아빠 방향, 동쪽)으로 달아난다(루트 모션 초속 5m).
        // 트랙 기준 자리 · 방향을 조리대 앞으로 두고, 달리기 클립만 92° 돌린다.
        var dadTrack = timeline.CreateTrack<AnimationTrack>(null, "Dad");
        dadTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
        dadTrack.position = dadPos;
        dadTrack.rotation = Quaternion.LookRotation(fwd);
        Clip(dadTrack, Load<AnimationClip>("Assets/Art/Asset/Character/NPC_01/Animation/Npc01_Standing.fbx", "AN_Npc01_Standing"), 0f, RunAt);
        Clip(dadTrack, Load<AnimationClip>("Assets/Cutscene/Opening/Anim/XBot_FastRun.fbx", "AN_Dad_FastRun"), RunAt, End - RunAt);
        foreach (TimelineClip c in dadTrack.GetClips())
            if (c.start >= RunAt) ((AnimationPlayableAsset)c.asset).rotation = Quaternion.Euler(0f, 92f, 0f);   // (+10/12) 고래 반대쪽(동쪽)으로
        director.SetGenericBinding(dadTrack, dadAnim);

        var pathTrack = timeline.CreateTrack<AnimationTrack>(null, "Whale Path");
        Clip(pathTrack, pathClip, SeaAt, SinkAt - SeaAt);
        director.SetGenericBinding(pathTrack, rig.GetComponent<Animator>());

        var whaleTrack = timeline.CreateTrack<AnimationTrack>(null, "Whale Body");
        whaleTrack.trackOffset = TrackOffset.ApplySceneOffsets;
        Clip(whaleTrack, Load<AnimationClip>("Assets/Art/Asset/Whale/FT_Whale_Rigged.fbx", "Whale_Swim"), SeaAt, JumpAt - SeaAt);
        Clip(whaleTrack, Load<AnimationClip>("Assets/Art/Asset/Whale/FT_Whale_Rigged.fbx", "Whale_Jump"), JumpAt, SinkAt - JumpAt);
        director.SetGenericBinding(whaleTrack, whaleAnim);

        var whaleOn = timeline.CreateTrack<ActivationTrack>(null, "Whale On");
        whaleOn.CreateDefaultClip();
        SetTime(whaleOn, SeaAt, SinkAt);
        director.SetGenericBinding(whaleOn, whale);

        var debrisOn = timeline.CreateTrack<ActivationTrack>(null, "Debris On");
        debrisOn.CreateDefaultClip();
        SetTime(debrisOn, SmashAt, End);
        director.SetGenericBinding(debrisOn, debris);

        var audioTrack = timeline.CreateTrack<AudioTrack>(null, "Sound");
        Sound(audioTrack, "Assets/Sounds/SFX/CookingMinigame/Stir_fry.wav", 0f, SeaAt);
        Sound(audioTrack, "Assets/Sounds/SFX/Normal/sfx_whale_call.wav", SeaAt + 0.5f, -1f);
        Sound(audioTrack, "Assets/Sounds/SFX/Normal/sfx_whale_call.wav", CloseAt + 0.3f, -1f);
        director.SetGenericBinding(audioTrack, audioSource);

        var signal = ScriptableObject.CreateInstance<SignalAsset>();
        AssetDatabase.CreateAsset(signal, $"{Dir}/SG_OpeningSmash.signal");
        var signalTrack = timeline.CreateTrack<SignalTrack>(null, "Smash");
        signalTrack.CreateMarker<SignalEmitter>(SmashAt).asset = signal;
        director.SetGenericBinding(signalTrack, receiver);
        var reaction = new UnityEvent();
        UnityEventTools.AddPersistentListener(reaction, cutscene.Smash);
        receiver.AddReaction(signal, reaction);
        // (+10/12) 아빠가 뛰기 시작할 때 앞길의 벽을 잠시 숨긴다(벽을 뚫고 지나가 보였다).
        var runSignal = ScriptableObject.CreateInstance<SignalAsset>();
        AssetDatabase.CreateAsset(runSignal, $"{Dir}/SG_OpeningRun.signal");
        signalTrack.CreateMarker<SignalEmitter>(RunAt).asset = runSignal;
        var runReaction = new UnityEvent();
        UnityEventTools.AddPersistentListener(runReaction, cutscene.ClearRunPath);
        receiver.AddReaction(runSignal, runReaction);

        timeline.fixedDuration = End;
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        EditorUtility.SetDirty(timeline);

        var so = new SerializedObject(cutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("runner").objectReferenceValue = dadModel.transform;
        so.FindProperty("impulse").objectReferenceValue = impulse;
        SerializedProperty bp = so.FindProperty("burstPieces");
        string[] burst = { "PF_LoosePlank_Long", "PF_LoosePlank_Short" };
        bp.arraySize = burst.Length;
        for (int i = 0; i < burst.Length; i++)
            bp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Asset/Island/WoodDamage/{burst[i]}.prefab");
        SerializedProperty sp = so.FindProperty("smashPoints");
        sp.arraySize = smashPoints.Count;
        for (int i = 0; i < smashPoints.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = smashPoints[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        content.SetActive(false);
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        Selection.activeGameObject = root;
        Debug.Log($"[오프닝] {PrefabPath} · {Dir}/TL_Opening.playable 생성, 열린 씬에 인스턴스를 놓았다. 아빠 {dadPos} · 충돌 {impact}");
    }

    // 위에서 쏴서 처음 닿는 바닥(조리대 · 트리거 제외).
    private static Vector3 Floor(Vector3 p, Transform skip)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 20f, p.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
            if (!h.collider.transform.IsChildOf(skip)) return h.point;
        return p;
    }

    // target에서 ideal까지의 거리 · 높이는 그대로, 수평 각도만 돌려 가며 카메라 자리가 비어 있고 target까지 안 막힌 첫 자리.
    private static Vector3 Clear(Vector3 target, Vector3 ideal)
    {
        Vector3 off = ideal - target;
        var flat = new Vector3(off.x, 0f, off.z);
        foreach (float a in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 120f, -120f, 160f, -160f })
        {
            Vector3 p = target + Quaternion.Euler(0f, a, 0f) * flat + Vector3.up * off.y;
            if (Physics.CheckSphere(p, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.Linecast(p, target, ~0, QueryTriggerInteraction.Ignore)) continue;
            return p;
        }
        Debug.LogWarning($"[오프닝] {target} 둘레에 트인 카메라 자리가 없다. 처음 자리를 쓴다.");
        return ideal;
    }

    private static CinemachineCamera Cam(Transform parent, string name, Vector3 pos, Vector3 look, float fov, Transform track)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        var cam = go.AddComponent<CinemachineCamera>();
        cam.Lens.FieldOfView = fov;
        if (track != null)
        {
            cam.Target.TrackingTarget = track;
            go.AddComponent<CinemachineRotationComposer>().Damping = Vector2.zero;   // 빠른 고래를 놓치지 않게
        }
        return cam;
    }

    private static void Shot(CinemachineTrack track, PlayableDirector director, CinemachineCamera cam, float start, float end)
    {
        TimelineClip clip = track.CreateClip<CinemachineShot>();
        clip.start = start;
        clip.duration = end - start;
        clip.displayName = cam.name;
        var shot = (CinemachineShot)clip.asset;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
    }

    private static void Clip(AnimationTrack track, AnimationClip anim, float start, float duration)
    {
        if (anim == null) return;
        TimelineClip clip = track.CreateClip(anim);
        clip.start = start;
        clip.duration = duration;
    }

    private static void SetTime(TrackAsset track, float start, float end)
    {
        foreach (TimelineClip c in track.GetClips()) { c.start = start; c.duration = end - start; }
    }

    private static void Sound(AudioTrack track, string path, float start, float duration)
    {
        var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (audio == null) { Debug.LogError($"[오프닝] 소리 없음: {path}"); return; }
        TimelineClip clip = track.CreateClip(audio);
        clip.start = start;
        if (duration > 0f) clip.duration = duration;
    }

    private static T Load<T>(string path, string name) where T : Object
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is T t && o.name == name) return t;
        Debug.LogError($"[오프닝] {path} 안에 {name} 없음");
        return null;
    }

    private static AnimationClip PathClip((float t, Vector3 p, Vector3 euler)[] keys)
    {
        var clip = new AnimationClip { name = "AN_OpeningWhalePath" };
        string[] pos = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        string[] rot = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        for (int axis = 0; axis < 3; axis++)
        {
            var pc = new AnimationCurve();
            var rc = new AnimationCurve();
            foreach (var k in keys) { pc.AddKey(k.t, k.p[axis]); rc.AddKey(k.t, k.euler[axis]); }
            for (int i = 0; i < pc.length; i++) { AnimationUtility.SetKeyLeftTangentMode(pc, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(pc, i, AnimationUtility.TangentMode.Auto); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Whale", typeof(Transform), pos[axis]), pc);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Whale", typeof(Transform), rot[axis]), rc);
        }
        return clip;
    }

    private static void CopyMaterials(GameObject from, GameObject to)
    {
        if (from == null) return;
        var src = new Dictionary<string, Material[]>();
        foreach (Renderer r in from.GetComponentsInChildren<Renderer>(true)) src[r.name] = r.sharedMaterials;
        foreach (Renderer r in to.GetComponentsInChildren<Renderer>(true))
            if (src.TryGetValue(r.name, out Material[] m)) r.sharedMaterials = m;
    }
}
