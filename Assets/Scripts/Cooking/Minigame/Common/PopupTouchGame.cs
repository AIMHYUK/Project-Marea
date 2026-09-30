using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 팝업 터치 미니게임 부품 — 표면 위에 무작위로 뜨는 오브젝트를 사라지기 전에 클릭한다. 기획 MG_POPUP_TOUCH. (+9/30)
    ///
    /// 스튜 3단계에선 국물 위 거품이다. 단계 진행 · 결과는 모른다 — 컨트롤러가 Begin으로 시작하고
    /// IsFinished/Score를 읽는다. 클릭은 카메라 레이를 직접 쏜다 (EventSystem 없이도 동작).
    /// </summary>
    public class PopupTouchGame : MonoBehaviour
    {
        [Header("나타날 자리")]
        [Tooltip("표면 중심. 스튜는 국물(SM_Soup_Pot_Inside).")]
        [SerializeField] private Transform surface;

        [Tooltip("표면 중심에서 이 반경(m) 안에 뜬다.")]
        [SerializeField, Min(0.05f)] private float radius = 0.45f;

        [Tooltip("표면 위로 띄우는 높이(m).")]
        [SerializeField] private float heightOffset = 0.05f;

        [Header("팝업")]
        [Tooltip("띄울 모델. 비우면 흰 구(거품)를 쓴다.")]
        [SerializeField] private GameObject popupPrefab;

        [Tooltip("다 부풀었을 때 지름(m).")]
        [SerializeField, Min(0.02f)] private float size = 0.18f;

        [Tooltip("새로 뜨는 간격(초).")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 0.6f;

        [Tooltip("떠 있는 시간 범위(초). 이 안에 못 누르면 놓친 것이다.")]
        [SerializeField] private Vector2 lifetimeRange = new Vector2(1.2f, 2.0f);

        [Tooltip("동시에 떠 있을 수 있는 최대 개수.")]
        [SerializeField, Min(1)] private int maxAlive = 4;

        [Header("시간")]
        [Tooltip("제한시간(초). 기획 CookingMiniGameData MG_POPUP_TOUCH = 10.")]
        [SerializeField, Min(1f)] private float timeLimit = 10f;

        private sealed class Popup
        {
            public Transform T;
            public Collider Col;
            public float Age;
            public float Life;
        }

        private readonly List<Popup> _alive = new();
        private float _spawnTimer;
        private float _timeLeft;
        private int _spawned;
        private int _popped;
        private bool _running;

        public bool IsFinished { get; private set; }
        public float Score => _spawned > 0 ? (float)_popped / _spawned : 1f;
        public int Popped => _popped;
        public int Spawned => _spawned;
        public float TimeLeft01 => timeLimit > 0f ? Mathf.Clamp01(_timeLeft / timeLimit) : 0f;

        /// <summary>터뜨린 수가 바뀔 때. (터뜨린 수, 나온 수)</summary>
        public event Action<int, int> OnProgress;

        public void Begin()
        {
            Cleanup();
            if (surface == null)
            {
                Debug.LogError($"{name}: PopupTouchGame.surface가 비어 있다. 띄울 자리가 없다.", this);
                IsFinished = true;
                return;
            }

            _spawned = 0;
            _popped = 0;
            _timeLeft = timeLimit;
            _spawnTimer = 0.2f;
            IsFinished = false;
            _running = true;
            OnProgress?.Invoke(_popped, _spawned);
        }

        private void Update()
        {
            if (!_running) return;

            _timeLeft -= Time.deltaTime;

            // 시간이 끝나기 직전엔 새로 안 띄운다 — 누를 틈도 없이 놓친 걸로 세면 억울하다.
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && _alive.Count < maxAlive && _timeLeft > lifetimeRange.x)
            {
                Spawn();
                _spawnTimer = spawnInterval;
            }

            HandleClick();

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Popup p = _alive[i];
                p.Age += Time.deltaTime;
                // 0.15초 동안 부풀고, 마지막 0.3초 동안 오그라든다.
                float grow = Mathf.Clamp01(p.Age / 0.15f);
                float fade = Mathf.Clamp01((p.Life - p.Age) / 0.3f);
                p.T.localScale = Vector3.one * (size * grow * Mathf.Lerp(0.4f, 1f, fade));

                if (p.Age >= p.Life)
                {
                    Destroy(p.T.gameObject);   // 놓쳤다
                    _alive.RemoveAt(i);
                    OnProgress?.Invoke(_popped, _spawned);
                }
            }

            if (_timeLeft <= 0f) Finish();
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            // 냄비 콜라이더에 가려도 거품을 맞힌 게 있으면 그걸 쓴다.
            for (int h = 0; h < hits.Length; h++)
            {
                for (int i = 0; i < _alive.Count; i++)
                {
                    if (hits[h].collider != _alive[i].Col) continue;
                    Destroy(_alive[i].T.gameObject);
                    _alive.RemoveAt(i);
                    _popped++;
                    OnProgress?.Invoke(_popped, _spawned);
                    return;
                }
            }
        }

        private void Spawn()
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 pos = surface.position + new Vector3(r.x, heightOffset, r.y);

            GameObject go;
            if (popupPrefab != null)
            {
                go = Instantiate(popupPrefab, pos, Quaternion.identity, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, true);
                go.transform.position = pos;
            }
            go.transform.localScale = Vector3.zero;

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;   // 플레이어·직원 물리와 엉키지 않게. 클릭 레이는 트리거도 맞힌다.

            _alive.Add(new Popup
            {
                T = go.transform,
                Col = col,
                Life = UnityEngine.Random.Range(lifetimeRange.x, Mathf.Max(lifetimeRange.x, lifetimeRange.y)),
            });
            _spawned++;
            OnProgress?.Invoke(_popped, _spawned);
        }

        private void Finish()
        {
            _running = false;
            IsFinished = true;
            Cleanup();
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            foreach (var p in _alive)
                if (p.T != null) Destroy(p.T.gameObject);
            _alive.Clear();
        }
    }
}
