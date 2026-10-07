using System;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 받기 미니게임 부품 — 그릇을 좌우로 움직여 위에서 떨어지는 재료를 받는다. 기획 MG_OBJECT_CATCH. (+9/30)
    ///
    /// 단계 진행 · 카메라 · 결과는 모른다. 컨트롤러가 Begin으로 시작하고 IsFinished/Score를 읽는다
    /// (KnifeCutGuide와 같은 급의 부품). 스튜 1단계와 토마토주스 1단계가 같이 쓴다.
    ///
    /// 떨어지는 속도는 재료마다 다르다 — 생길 때 fallSpeedRange 안에서 뽑아 Update에서 그 속도로 내린다.
    /// 받았는지는 물리 충돌 없이 본다: 재료가 그릇 입구 높이를 지나는 순간 수평 거리가 catchRadius 안이면 받은 것.
    /// </summary>
    public class CatchGame : MonoBehaviour
    {
        [Header("그릇")]
        [Tooltip("받는 그릇. 이걸 좌우로 움직인다. 스튜는 냄비(PF_Soup_Set).")]
        [SerializeField] private Transform catcher;

        [Tooltip("그릇 입구 중심. 그릇 자식에 둔다. 비우면 그릇 위치 + rimHeight.")]
        [SerializeField] private Transform mouth;

        [Tooltip("그릇 입구 반경(m). 재료 중심이 이 안으로 들어오면 받은 것이다.")]
        [SerializeField, Min(0.05f)] private float catchRadius = 0.6f;

        [Tooltip("mouth가 비었을 때만 쓴다. 그릇 기준 입구 높이(m).")]
        [SerializeField] private float rimHeight = 0.8f;

        [Tooltip("시작 위치에서 좌우로 움직일 수 있는 거리(m).")]
        [SerializeField, Min(0f)] private float moveRange = 1.8f;

        [Tooltip("그릇이 마우스를 따라가는 빠르기. 클수록 즉시 붙는다.")]
        [SerializeField, Min(0.1f)] private float followSpeed = 12f;

        [Header("재료")]
        [Tooltip("입구 위 몇 m에서 떨어지기 시작하나.")]
        [SerializeField, Min(0.1f)] private float spawnHeight = 2.2f;

        [Tooltip("재료가 나오는 간격(초).")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 0.9f;

        [Tooltip("떨어지는 속도 범위(m/s). 재료마다 이 안에서 따로 뽑는다.")]
        [SerializeField] private Vector2 fallSpeedRange = new Vector2(1.2f, 3.2f);

        [Tooltip("재료 모델이 없을 때 대신 쓸 구의 지름(m).")]
        [SerializeField, Min(0.05f)] private float fallbackSize = 0.3f;

        [Tooltip("(+10/2) 떨어뜨릴 개수. 받은 재료 목록보다 크면 섞어서 반복한다. 0이면 목록 그대로.")]
        [SerializeField, Min(0)] private int dropCount;

        [Header("움직임 (+10/2) — 재료마다 가중치로 하나를 뽑는다. 기본은 곧게만")]
        [SerializeField, Min(0f)] private float straightWeight = 1f;
        [Tooltip("좌우로 흔들리며 떨어진다.")]
        [SerializeField, Min(0f)] private float zigzagWeight;
        [Tooltip("입구 위까지 내려오다 멈칫, 다시 튀어 올랐다가 옆으로 비켜 다시 떨어진다.")]
        [SerializeField, Min(0f)] private float feintWeight;
        [Tooltip("떨어지다 갑자기 옆으로 휙 비켜선다.")]
        [SerializeField, Min(0f)] private float dashWeight;
        [SerializeField] private Vector2 zigzagAmplitude = new Vector2(0.3f, 0.7f);
        [SerializeField] private Vector2 zigzagFrequency = new Vector2(1.2f, 2.4f);
        [Tooltip("속임수가 멈칫하는 높이(입구 위, m).")]
        [SerializeField] private Vector2 feintHeight = new Vector2(0.5f, 1.0f);
        [Tooltip("속임수가 다시 튀어 오르는 속도(m/s).")]
        [SerializeField, Min(0f)] private float feintBounce = 3.5f;
        [Tooltip("옆걸음 거리(m).")]
        [SerializeField] private Vector2 dashDistance = new Vector2(0.6f, 1.2f);
        [Tooltip("빙글빙글 도는 속도(도/초).")]
        [SerializeField] private Vector2 spinSpeed = new Vector2(90f, 90f);

        [Header("연출 (+10/6, 이슈 117) — 기획 「냄비·팬에 재료 투입」 VFX_04")]
        [Tooltip("재료가 그릇에 들어간 자리에서 튀는 방울.")]
        [SerializeField] private VfxId catchVfx = VfxId.Splash;
        [Tooltip("방울 색 — 국물 · 기름 색에 맞춘다.")]
        [SerializeField] private Color catchTint = new Color(0.85f, 0.7f, 0.5f, 1f);
        [SerializeField, Min(0.1f)] private float catchVfxScale = 1f;
        [Tooltip("(+10/6) 받은 재료가 여기(국물 면 — SM_Soup_Pot_Inside)에 닿을 때 튀고 사라진다. 비우면 예전처럼 입구에서 바로.")]
        [SerializeField] private Renderer splashSurface;

        [Header("놓침 (+10/7) — 기획 VFX_13 「실패 · 떨어뜨림」")]
        [Tooltip("놓친 재료가 바닥(입구에서 missDrop 아래)에 닿을 때 터지는 것.")]
        [SerializeField] private VfxId missVfx = VfxId.Fail;
        [SerializeField, Min(0.1f)] private float missVfxScale = 0.5f;
        [Tooltip("바닥(조리대 윗면)이 입구보다 이만큼 아래(m). 조리대는 프리팹 밖이라 높이로 둔다. 0이면 입구 높이를 지나는 순간 터진다.")]
        [SerializeField, Min(0f)] private float missDrop;
        [SerializeField] private AudioClip missClip;
        [SerializeField, Range(0f, 1f)] private float missVolume = 0.7f;

        [Header("소리 (+10/6, 이슈 117) — 기획 sfx_ingredient_catch")]
        [Tooltip("재료가 그릇에 들어갈 때.")]
        [SerializeField] private AudioClip catchClip;
        [SerializeField, Range(0f, 1f)] private float catchVolume = 0.8f;

        [Header("시간")]
        [Tooltip("제한시간(초). 기획 CookingMiniGameData MG_OBJECT_CATCH = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        private enum Motion { Straight, Zigzag, Feint, Dash }

        private sealed class Falling
        {
            public Transform T;
            public float Speed;
            public bool Resolved;
            public bool Sinking;     // (+10/6) 받았고 국물 면으로 떨어지는 중 — 닿으면 튀고 사라진다
            public Motion Kind;
            public float Y;          // 입구 높이 기준 높이(m)
            public float Along;      // 좌우 축 위치(m, 입구 기준)
            public float Age;
            public float SwayAmp, SwayFreq, Phase;
            public Vector3 SpinAxis;
            public float Spin;
            // 속임수
            public int FeintState;   // 0 내려옴 · 1 튀어 오름 · 2 다시 떨어짐
            public float FeintY, VY, FeintFrom, FeintTo;
            // 옆걸음
            public bool Dashed;
            public float DashY, DashFrom, DashTo, DashT = -1f;
        }

        private readonly List<GameObject> _queue = new();
        private readonly List<Falling> _falling = new();
        private Vector3 _home;
        private bool _hasHome;
        private Vector3 _mouthHome;
        private Vector3 _axis;
        private float _spawnTimer;
        private float _timeLeft;
        private int _total;
        private int _caught;
        private bool _running;

        /// <summary>지금 입구 중심(월드). 그릇이 움직이면 같이 움직인다.</summary>
        private Vector3 Mouth => mouth != null ? mouth.position : catcher.position + Vector3.up * rimHeight;

        public bool IsFinished { get; private set; }
        public float Score => _total > 0 ? (float)_caught / _total : 1f;
        public int Caught => _caught;
        public int Total => _total;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;

        /// <summary>받은 수가 바뀔 때. (받은 수, 전체)</summary>
        public event Action<int, int> OnProgress;

        /// <summary>이 재료들을 순서를 섞어 떨어뜨린다. null 칸은 대체 구로 떨어진다.</summary>
        public void Begin(IReadOnlyList<GameObject> items)
        {
            Cleanup();
            if (catcher == null)
            {
                Debug.LogError($"{name}: CatchGame.catcher가 비어 있다. 받을 그릇이 없다.", this);
                IsFinished = true;
                return;
            }

            _home = catcher.position;
            _hasHome = true;
            _mouthHome = Mouth;
            Camera cam = Camera.main;
            _axis = Vector3.ProjectOnPlane(cam != null ? cam.transform.right : catcher.right, Vector3.up).normalized;

            _queue.Clear();
            if (items != null) _queue.AddRange(items);
            // (+10/2) 개수를 채운다 — 받은 목록을 돌려 쓴다(비었으면 대체 구).
            int baseCount = _queue.Count;
            for (int i = baseCount; i < dropCount; i++) _queue.Add(baseCount > 0 ? _queue[i % baseCount] : null);
            for (int i = _queue.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
            }

            _total = _queue.Count;
            _caught = 0;
            _timeLeft = timeLimit;
            _spawnTimer = 0.3f;   // 첫 재료는 조금 뒤에 — 화면이 넘어오는 동안 떨어지지 않게
            IsFinished = _total == 0;
            _running = !IsFinished;
            OnProgress?.Invoke(_caught, _total);
        }

        private void Update()
        {
            if (!_running) return;

            MoveCatcher();

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && _queue.Count > 0)
            {
                Spawn(_queue[_queue.Count - 1]);
                _queue.RemoveAt(_queue.Count - 1);
                _spawnTimer = spawnInterval;
            }

            Vector3 mouthNow = Mouth;
            float rimY = mouthNow.y;
            for (int i = 0; i < _falling.Count; i++)
            {
                Falling f = _falling[i];
                if (f.T == null) continue;

                float prevY = f.T.position.y;
                Move(f, Time.deltaTime);
                f.T.position = _mouthHome + _axis * f.Along + Vector3.up * f.Y;
                f.T.Rotate(f.SpinAxis, f.Spin * Time.deltaTime, Space.World);

                if (f.Sinking)
                {
                    // (+10/6) 국물 면에 닿으면 그 자리에서 튄다. 냄비가 움직여서 높이를 매 프레임 다시 잰다.
                    if (f.T.position.y <= SurfaceY(rimY)) Splash(f);
                    continue;
                }
                if (f.Resolved)
                {
                    // (+10/7) 놓친 재료는 바닥에 닿으면 터지고 사라진다. 바닥이 없으면 입구에서 이미 터졌고, 조금 더 떨어진 뒤 지운다.
                    if (missDrop > 0f && f.T.position.y <= rimY - missDrop) Miss(f, rimY - missDrop);
                    else if (f.T.position.y < rimY - Mathf.Max(2f, missDrop + 0.5f)) Destroy(f.T.gameObject);
                    continue;
                }

                if (prevY >= rimY && f.T.position.y < rimY)
                {
                    f.Resolved = true;
                    Vector3 d = f.T.position - mouthNow;
                    d.y = 0f;
                    if (d.magnitude <= catchRadius)
                    {
                        _caught++;
                        OnProgress?.Invoke(_caught, _total);
                        // (+10/6) 점수는 입구에서 바로, 튀는 건 국물 면에 닿을 때. 국물 면이 없으면 예전처럼 여기서.
                        if (splashSurface != null) f.Sinking = true;
                        else Splash(f);
                    }
                    else if (missDrop <= 0f) PlayMiss(f.T.position);   // (+10/7) 바닥 높이가 없으면 놓친 순간에
                }
            }
            _falling.RemoveAll(f => f.T == null);

            _timeLeft -= Time.deltaTime;
            // 가라앉는 중인 재료가 국물에 닿기 전에 끝내면 마지막 하나가 안 튄다.
            bool allDone = _queue.Count == 0 && _falling.TrueForAll(f => f.Resolved && !f.Sinking);
            if (allDone || _timeLeft <= 0f) Finish();
        }

        private void MoveCatcher()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;

            // 마우스를 그릇 입구 높이의 수평면에 투영해서, 좌우 축 성분만 쓴다.
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, _mouthHome);
            if (!plane.Raycast(ray, out float enter)) return;

            float along = Vector3.Dot(ray.GetPoint(enter) - _mouthHome, _axis);
            Vector3 target = _home + _axis * Mathf.Clamp(along, -moveRange, moveRange);
            catcher.position = Vector3.Lerp(catcher.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        /// <summary>재료 하나를 한 프레임 움직인다 — Y(높이)와 Along(좌우)만 바꾼다.</summary>
        private void Move(Falling f, float dt)
        {
            f.Age += dt;
            switch (f.Kind)
            {
                case Motion.Zigzag:
                    f.Y -= f.Speed * 0.85f * dt;   // 흔들리는 만큼 조금 느리게
                    f.Along = f.FeintFrom + Mathf.Sin(f.Age * f.SwayFreq * Mathf.PI * 2f + f.Phase) * f.SwayAmp;
                    break;

                case Motion.Feint:
                    if (f.FeintState == 0)
                    {
                        f.Y -= f.Speed * dt;
                        if (f.Y <= f.FeintY) { f.FeintState = 1; f.VY = feintBounce; f.Age = 0f; }
                    }
                    else
                    {
                        // 튀어 올랐다가 중력으로 다시 떨어진다. 그동안 옆으로 비켜 선다.
                        f.VY = Mathf.Max(f.VY - 12f * dt, -f.Speed * 1.15f);
                        f.Y += f.VY * dt;
                        f.Along = Mathf.Lerp(f.FeintFrom, f.FeintTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f.Age / 0.45f)));
                        if (f.VY < 0f) f.FeintState = 2;
                    }
                    break;

                case Motion.Dash:
                    f.Y -= f.Speed * dt;
                    if (!f.Dashed && f.Y <= f.DashY) { f.Dashed = true; f.DashT = 0f; }
                    if (f.DashT >= 0f && f.DashT < 1f)
                    {
                        f.DashT = Mathf.Min(1f, f.DashT + dt / 0.16f);
                        f.Along = Mathf.Lerp(f.DashFrom, f.DashTo, 1f - (1f - f.DashT) * (1f - f.DashT));
                    }
                    break;

                default:
                    f.Y -= f.Speed * dt;
                    break;
            }
            f.Along = Mathf.Clamp(f.Along, -moveRange, moveRange);
        }

        private Motion PickMotion()
        {
            float total = straightWeight + zigzagWeight + feintWeight + dashWeight;
            if (total <= 0f) return Motion.Straight;
            float r = UnityEngine.Random.value * total;
            if ((r -= straightWeight) < 0f) return Motion.Straight;
            if ((r -= zigzagWeight) < 0f) return Motion.Zigzag;
            if ((r -= feintWeight) < 0f) return Motion.Feint;
            return Motion.Dash;
        }

        // 반대쪽으로 비켜 선다 — 범위 끝이면 안쪽으로.
        private float SideStep(float from, Vector2 range)
        {
            float d = UnityEngine.Random.Range(range.x, Mathf.Max(range.x, range.y)) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float to = from + d;
            if (Mathf.Abs(to) > moveRange) to = from - d;
            return Mathf.Clamp(to, -moveRange, moveRange);
        }

        private void Spawn(GameObject prefab)
        {
            float along = UnityEngine.Random.Range(-moveRange, moveRange);
            Vector3 pos = _mouthHome + _axis * along + Vector3.up * spawnHeight;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, pos, prefab.transform.rotation, transform);
                // 재료 모델에 물리가 붙어 있어도 여기선 끈다. 떨어지는 건 이 부품이 직접 움직인다.
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, true);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * fallbackSize;
            }

            var f = new Falling
            {
                T = go.transform,
                Speed = UnityEngine.Random.Range(fallSpeedRange.x, Mathf.Max(fallSpeedRange.x, fallSpeedRange.y)),
                Kind = PickMotion(),
                Y = spawnHeight,
                Along = along,
                FeintFrom = along,
                Phase = UnityEngine.Random.value * Mathf.PI * 2f,
                SwayAmp = UnityEngine.Random.Range(zigzagAmplitude.x, Mathf.Max(zigzagAmplitude.x, zigzagAmplitude.y)),
                SwayFreq = UnityEngine.Random.Range(zigzagFrequency.x, Mathf.Max(zigzagFrequency.x, zigzagFrequency.y)),
                SpinAxis = UnityEngine.Random.onUnitSphere,
                Spin = UnityEngine.Random.Range(spinSpeed.x, Mathf.Max(spinSpeed.x, spinSpeed.y)),
            };
            if (f.Kind == Motion.Straight) f.SpinAxis = Vector3.up;   // 예전 모습 그대로(제자리 회전)
            f.FeintY = UnityEngine.Random.Range(feintHeight.x, Mathf.Max(feintHeight.x, feintHeight.y));
            f.FeintTo = SideStep(along, dashDistance);
            f.DashY = UnityEngine.Random.Range(spawnHeight * 0.35f, spawnHeight * 0.75f);
            f.DashFrom = along;
            f.DashTo = SideStep(along, dashDistance);
            _falling.Add(f);
        }

        /// <summary>(+10/6) 국물 면 높이 — 렌더러 상자 위쪽. 없으면 입구 높이.</summary>
        private float SurfaceY(float rimY) => splashSurface != null ? splashSurface.bounds.max.y : rimY;

        private void Splash(Falling f)
        {
            Vfx.Play(catchVfx, f.T.position, catchVfxScale, catchTint);   // (+10/6)
            SoundManager.Play(catchClip, catchVolume, 0.08f);             // (+10/6)
            Destroy(f.T.gameObject);
            f.Sinking = false;
        }

        /// <summary>(+10/7) 놓친 재료가 바닥에 닿았다 — 터지고 사라진다.</summary>
        private void Miss(Falling f, float floorY)
        {
            PlayMiss(new Vector3(f.T.position.x, floorY, f.T.position.z));
            Destroy(f.T.gameObject);
        }

        private void PlayMiss(Vector3 at)
        {
            Vfx.Play(missVfx, at, missVfxScale);
            SoundManager.Play(missClip, missVolume, 0.08f);
        }

        private void Finish()
        {
            _running = false;
            IsFinished = true;
            Cleanup();
        }

        // 단계가 끝나 패널이 꺼질 때도 남은 재료를 치우고 그릇을 제자리로 돌린다.
        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            foreach (var f in _falling)
                if (f.T != null) Destroy(f.T.gameObject);
            _falling.Clear();
            _queue.Clear();
            if (catcher != null && _hasHome) catcher.position = _home;
        }
    }
}
