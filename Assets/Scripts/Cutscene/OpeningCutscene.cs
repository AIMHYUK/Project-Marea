using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Marea.Cutscene
{
    /// <summary>
    /// 오프닝 컷신 — 다 지은 식당에서 아빠가 요리하다가 고래가 데크로 뛰어들어 부숴 버린다. (+10/12)
    ///
    /// 새 게임에서 한 번만 튼다(저장 키 savedKey). 에디터는 저장을 안 읽어 매번 새 게임이라 playInEditor로 정한다.
    /// 흐름: 시설을 다 지은 모습으로 → 타임라인(Cinemachine 카메라 · 아빠 · 고래) → Smash 신호에서 시설을 지금 레벨(폐허)로
    /// → 끝나면 암전 → 원래 카메라 · HUD · 플레이어로 돌려놓고 밝힌다. Esc로 건너뛴다.
    ///
    /// 메인 카메라엔 CinemachineBrain이 없다 — 평소엔 CameraFollow가 움직인다. 컷신 동안만 Brain을 붙이고 CameraFollow를 끈다.
    /// </summary>
    public class OpeningCutscene : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [Tooltip("컷신에만 쓰는 것(아빠 · 고래 · 카메라 · 파편). 안 틀거나 끝나면 끈다.")]
        [SerializeField] private GameObject content;

        [Header("언제")]
        [Tooltip("에디터는 저장을 안 읽어 플레이마다 새 게임이다. 끄면 에디터에선 안 튼다.")]
        [SerializeField] private bool playInEditor = true;
        [SerializeField] private string savedKey = "openingSeen";

        [Header("부서지는 순간 (Smash 신호)")]
        [Tooltip("이펙트가 터질 자리.")]
        [SerializeField] private Transform[] smashPoints;
        [SerializeField] private VfxId smashVfx = VfxId.Dust;
        [SerializeField] private VfxId smashImpactVfx = VfxId.Fail;
        [SerializeField] private VfxId smashSplashVfx = VfxId.Splash;
        [SerializeField, Min(0.1f)] private float smashVfxScale = 6f;
        [Tooltip("(+10/12) 터질 때 첫 smashPoint에서 튀어 오르는 판자 조각 프리팹. 물리로 날아간다.")]
        [SerializeField] private GameObject[] burstPieces;
        [SerializeField, Min(0)] private int burstCount = 18;
        [SerializeField, Min(0f)] private float burstSpeed = 10f;
        [Tooltip("(+10/12) 터질 때 카메라 흔들기. 카메라에 CinemachineImpulseListener가 있어야 흔들린다.")]
        [SerializeField] private CinemachineImpulseSource impulse;
        [SerializeField, Min(0f)] private float shakeForce = 1.5f;

        [Header("아빠 도주 (+10/12)")]
        [Tooltip("뛰는 아빠. Run 신호 때 이 오브젝트가 바라보는 쪽으로 길을 비운다.")]
        [SerializeField] private Transform runner;
        [Tooltip("앞으로 이만큼(m) 안에서 가슴 높이를 막는 벽 · 기둥을 숨긴다. 컷신이 끝나면 되돌린다.")]
        [SerializeField, Min(0f)] private float runClearDistance = 12f;
        [SerializeField, Min(0.1f)] private float runClearWidth = 1.2f;

        [Header("암전")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.8f;
        [SerializeField, Min(0f)] private float blackSeconds = 0.6f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 1f;

        private readonly List<Canvas> _hiddenCanvases = new();
        private readonly List<Renderer> _hiddenPlayer = new();
        private readonly List<Renderer> _hiddenWalls = new();
        private FacilitySite[] _sites;
        private CameraFollow _follow;
        private PlayerController _player;
        private CinemachineBrain _brain;
        private bool _addedBrain, _smashed, _finishing;

        private void Awake()
        {
            if (director == null || content == null)
                Debug.LogError($"{name}: OpeningCutscene의 director · content가 비어 있다. 컷신이 안 돈다.", this);
            if (content != null) content.SetActive(false);
        }

        private IEnumerator Start()
        {
            bool play = Application.isEditor ? playInEditor : SaveFile.GetInt(savedKey, 0) == 0;
            if (!play || director == null || content == null) yield break;
            yield return null;   // FacilitySite.Start가 지금 레벨 모습을 먼저 깐 뒤에 덮는다
            Begin();
        }

        private void Update()
        {
            if (!_finishing && director != null && director.state == PlayState.Playing
                && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                director.Stop();   // stopped → Finish
        }

        private void Begin()
        {
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogError($"{name}: 메인 카메라가 없어 오프닝을 건너뛴다.", this); return; }

            _follow = cam.GetComponent<CameraFollow>();
            if (_follow != null) _follow.enabled = false;
            _brain = cam.GetComponent<CinemachineBrain>();
            if (_brain == null) { _brain = cam.gameObject.AddComponent<CinemachineBrain>(); _addedBrain = true; }
            foreach (PlayableBinding output in director.playableAsset.outputs)
                if (output.sourceObject is CinemachineTrack) director.SetGenericBinding(output.sourceObject, _brain);

            _player = FindAnyObjectByType<PlayerController>();
            if (_player != null)
            {
                _player.enabled = false;
                foreach (Renderer r in _player.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; _hiddenPlayer.Add(r); }
            }

            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.enabled = false; _hiddenCanvases.Add(c); }

            _sites = FindObjectsByType<FacilitySite>(FindObjectsSortMode.None);
            foreach (FacilitySite s in _sites) s.ShowFullyBuilt();

            content.SetActive(true);
            director.extrapolationMode = DirectorWrapMode.None;
            director.stopped += OnStopped;
            director.Play();
        }

        /// <summary>
        /// (+10/12) 타임라인 Run 신호 — 아빠가 달아나기 시작할 때. 아빠 앞 runClearDistance 안에서 가슴 높이(바닥 + 1.2m)를
        /// 지나는 렌더러(벽 · 기둥 · 선반)를 숨긴다. 바닥 · 탁자처럼 그 높이에 안 닿는 것은 그대로. 끝나면 Finish가 되돌린다.
        /// 벽은 씬 오브젝트라 프리팹에 연결할 수 없어서 이렇게 찾는다.
        /// </summary>
        public void ClearRunPath()
        {
            if (runner == null) { Debug.LogError($"{name}: OpeningCutscene.runner가 비어 있다. 아빠가 벽을 뚫고 뛴다.", this); return; }
            Vector3 dir = Vector3.ProjectOnPlane(runner.forward, Vector3.up).normalized;
            Vector3 from = runner.position + Vector3.up * 1.2f;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r.transform.IsChildOf(content.transform)) continue;
                Bounds b = r.bounds;
                if (b.min.y > from.y || b.max.y < from.y) continue;   // 가슴 높이를 안 지난다
                b.Expand(new Vector3(runClearWidth, 0f, runClearWidth));
                if (!b.IntersectRay(new Ray(from, dir), out float hit) || hit > runClearDistance) continue;
                r.enabled = false;
                _hiddenWalls.Add(r);
            }
        }

        /// <summary>타임라인 Smash 신호 — 고래가 데크에 닿는 순간. 이펙트를 터뜨리고 시설을 지금 레벨(폐허)로.</summary>
        public void Smash()
        {
            if (_smashed) return;
            _smashed = true;
            if (smashPoints != null)
                foreach (Transform p in smashPoints)
                {
                    if (p == null) continue;
                    Vfx.Play(smashVfx, p.position, smashVfxScale);
                    Vfx.Play(smashImpactVfx, p.position + Vector3.up * 0.5f, smashVfxScale);
                    Vfx.Play(smashSplashVfx, p.position + Vector3.up * 0.5f, smashVfxScale);
                }
            if (_sites != null)
                foreach (FacilitySite s in _sites) if (s != null) s.ShowCurrent();
            if (impulse != null) impulse.GenerateImpulseWithForce(shakeForce);
            Burst();
        }

        // 판자 조각을 위 · 바깥으로 튕긴다. content 아래에 두어 컷신이 끝나면 같이 꺼진다.
        private void Burst()
        {
            if (burstPieces == null || burstPieces.Length == 0 || smashPoints == null || smashPoints.Length == 0 || smashPoints[0] == null) return;
            Vector3 center = smashPoints[0].position + Vector3.up * 0.5f;
            for (int i = 0; i < burstCount; i++)
            {
                GameObject src = burstPieces[i % burstPieces.Length];
                if (src == null) continue;
                Vector2 flat = Random.insideUnitCircle;
                GameObject piece = Instantiate(src, center + new Vector3(flat.x, 0f, flat.y) * 1.5f, Random.rotation, content.transform);
                foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().convex = true;
                Rigidbody body = piece.GetComponent<Rigidbody>();
                if (body == null) body = piece.AddComponent<Rigidbody>();
                body.linearVelocity = (new Vector3(flat.x, 0f, flat.y) * 0.7f + Vector3.up).normalized * burstSpeed * Random.Range(0.6f, 1.2f);
                body.angularVelocity = Random.insideUnitSphere * 12f;
            }
        }

        private void OnStopped(PlayableDirector d)
        {
            director.stopped -= OnStopped;
            // 플레이 종료 · 씬 전환으로 꺼지면서 멈춘 것 — 되돌릴 화면이 없다.
            if (!_finishing && isActiveAndEnabled) StartCoroutine(Finish());
        }

        private IEnumerator Finish()
        {
            _finishing = true;
            SceneTransitionFade fade = SceneTransitionFade.Create();
            yield return fade.FadeTo(1f, fadeOutSeconds);

            Smash();   // 건너뛰었으면 여기서 부순다
            content.SetActive(false);
            foreach (Renderer r in _hiddenWalls) if (r != null) r.enabled = true;   // (+10/12) 도주 때 숨긴 벽
            _hiddenWalls.Clear();
            if (_addedBrain && _brain != null) Destroy(_brain);
            if (_follow != null) _follow.enabled = true;
            if (_player != null) _player.enabled = true;
            foreach (Renderer r in _hiddenPlayer) if (r != null) r.enabled = true;
            foreach (Canvas c in _hiddenCanvases) if (c != null) c.enabled = true;
            SaveFile.Set(savedKey, 1);

            yield return new WaitForSecondsRealtime(blackSeconds);
            yield return fade.FadeTo(0f, fadeInSeconds);
            Destroy(fade.gameObject);
        }
    }
}
