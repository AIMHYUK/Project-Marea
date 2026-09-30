using System;
using System.Collections.Generic;
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

        [Header("시간")]
        [Tooltip("제한시간(초). 기획 CookingMiniGameData MG_OBJECT_CATCH = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        private sealed class Falling
        {
            public Transform T;
            public float Speed;
            public bool Resolved;
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
                f.T.position += Vector3.down * (f.Speed * Time.deltaTime);
                f.T.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

                if (f.Resolved)
                {
                    // 놓친 재료는 입구 아래로 조금 더 떨어진 뒤 지운다.
                    if (f.T.position.y < rimY - 2f) Destroy(f.T.gameObject);
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
                        Destroy(f.T.gameObject);
                        OnProgress?.Invoke(_caught, _total);
                    }
                }
            }
            _falling.RemoveAll(f => f.T == null);

            _timeLeft -= Time.deltaTime;
            bool allDone = _queue.Count == 0 && _falling.TrueForAll(f => f.Resolved);
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

        private void Spawn(GameObject prefab)
        {
            Vector3 pos = _mouthHome + _axis * UnityEngine.Random.Range(-moveRange, moveRange)
                        + Vector3.up * spawnHeight;

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

            _falling.Add(new Falling
            {
                T = go.transform,
                Speed = UnityEngine.Random.Range(fallSpeedRange.x, Mathf.Max(fallSpeedRange.x, fallSpeedRange.y)),
            });
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
