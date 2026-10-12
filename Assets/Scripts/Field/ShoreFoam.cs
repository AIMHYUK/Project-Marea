using System;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marea.Field
{
    /// <summary>
    /// 구조물이 바다에 닿은 자리에 잔물거품을 띄운다. 기획 「구조물과 바다 접촉」 VFX_11 / VFX_04. (+10/6, 이슈 117)
    ///
    /// 구조물마다 손으로 점을 찍지 않는다 — 해수면을 가로지르는 렌더러(부두 · 기둥 · 배 · 다리)를 스스로 찾아,
    /// 카메라 근처 · 화면 안에 있는 것 중 하나를 골라 가끔 거품을 낸다. 구조물을 새로 놓아도 따로 할 일이 없다.
    /// 작은 것(기둥)은 가운데, 큰 것(부두)은 둘레 아무 데서. 간헐적으로 작은 물방울도 튄다.
    ///
    /// 지형 · 물 자체처럼 큰 것은 maxSize로 뺀다.
    ///
    /// (+10/6) 찾기는 시작할 때 한 번, 그 뒤엔 시설이 해금 · 레벨업할 때만 다시 한다. 예전엔 10초마다 씬의
    /// MeshRenderer를 전부 훑었다 — 아트 에셋이 많은 씬이라 그 프레임이 튄다. 바다에 닿는 구조물이 바뀌는 건
    /// 시설 모습이 바뀔 때뿐이다(런타임에 생기는 구조물 없음, 움직이는 배는 처음 찾은 렌더러를 그대로 쓴다).
    ///
    /// (+10/12) 거품 자리를 월드 축 상자(bounds) 둘레에서 골랐더니, 비스듬한 울타리 · 난파선 · 섬 벽처럼 상자가 실물보다 훨씬 큰 것은
    /// 상자 모서리 — 아무것도 없는 물 — 에 거품이 떴다("허공에 물결"). 이제 그 물체 방향대로의 상자(localBounds) 겉면이
    /// 수면과 만나는 점만 쓴다. 못 찾으면 그번은 건너뛴다.
    /// </summary>
    public class ShoreFoam : MonoBehaviour
    {
        [Tooltip("해수면 높이(월드 y). WhaleSwimmer와 같은 값.")]
        [SerializeField] private float seaLevel = 6.9f;
        [Tooltip("가로 · 세로가 이보다 크면 구조물이 아니라 지형 · 바다로 본다(m).")]
        [SerializeField, Min(1f)] private float maxSize = 60f;
        [Tooltip("카메라에서 이 거리 안의 구조물만(m).")]
        [SerializeField, Min(1f)] private float cameraRange = 45f;
        [Tooltip("거품 간격(초) 범위. 화면 전체에서 이만큼마다 하나.")]
        [SerializeField] private Vector2 interval = new Vector2(0.25f, 0.6f);
        [Tooltip("거품과 함께 작은 물방울이 튈 확률.")]
        [SerializeField, Range(0f, 1f)] private float splashChance = 0.15f;
        [SerializeField] private VfxId foamVfx = VfxId.Wake;
        [SerializeField] private VfxId splashVfx = VfxId.Splash;
        [Tooltip("시설이 해금 · 레벨업한 뒤 이만큼 기다렸다 다시 찾는다(초). 해금 연출(카메라 이동 + 1.5초 뒤 모습 변경)보다 길어야 바뀐 모습을 잡는다.")]
        [SerializeField, Min(0f)] private float recollectDelay = 5f;

        private readonly List<Renderer> _structures = new();
        private readonly List<Renderer> _visible = new();
        private float _nextFoam;
        private float _recollectAt = float.PositiveInfinity;   // 다시 찾기 예약. 없으면 무한대
        private int _waterLayer;
        private FacilityLevels _levels;

        private void Awake() => _waterLayer = LayerMask.NameToLayer("Water");

        // FacilitySite.Start가 처음 모습을 켠 뒤에 찾아야 한다 — 같은 Start 순서라 한 프레임 미룬다.
        private void Start()
        {
            _recollectAt = 0f;
            _levels = FacilityLevels.Instance;
            if (_levels == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. 시설이 바뀌어도 거품 자리를 다시 안 찾는다.", this);
                return;
            }
            _levels.OnUnlocked += HandleUnlocked;
            _levels.OnLevelChanged += HandleLevelChanged;
        }

        private void OnDestroy()
        {
            if (_levels == null) return;
            _levels.OnUnlocked -= HandleUnlocked;
            _levels.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleUnlocked(FacilityKind _) => _recollectAt = Time.time + recollectDelay;
        private void HandleLevelChanged(FacilityKind _, int __) => _recollectAt = Time.time + recollectDelay;

        private void Update()
        {
            if (Time.time >= _recollectAt)
            {
                _recollectAt = float.PositiveInfinity;
                Collect();
            }
            if (Time.time < _nextFoam) return;
            _nextFoam = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));

            Camera cam = Camera.main;
            if (cam == null || _structures.Count == 0) return;

            _visible.Clear();
            Vector3 eye = cam.transform.position;
            foreach (Renderer r in _structures)
            {
                if (r == null || !r.isVisible) continue;
                Bounds b = r.bounds;
                if (b.min.y > seaLevel || b.max.y < seaLevel) continue;   // 움직이다 물에서 떨어졌다
                if ((b.ClosestPoint(eye) - eye).sqrMagnitude > cameraRange * cameraRange) continue;
                _visible.Add(r);
            }
            if (_visible.Count == 0) return;

            Renderer pickR = _visible[Random.Range(0, _visible.Count)];
            if (!ContactPoint(pickR, out Vector3 at)) return;
            Bounds pick = pickR.bounds;
            float size = Mathf.Clamp(Mathf.Min(pick.size.x, pick.size.z), 0.6f, 2.5f);
            Vfx.Play(foamVfx, at, size);
            if (Random.value < splashChance) Vfx.Play(splashVfx, at, size * 0.6f);
        }

        /// <summary>
        /// (+10/12) 물체 방향대로의 상자 겉면에서 아무 점이나 뽑아, 수면 높이 근처면 그 점(해수면 높이)을 쓴다. 작은 것(기둥)은 가운데.
        /// 겉면이 수면을 지나는 자리라 실물 둘레에 붙는다 — 월드 축 상자 둘레처럼 빈 물에 뜨지 않는다.
        /// </summary>
        private bool ContactPoint(Renderer r, out Vector3 at)
        {
            Bounds world = r.bounds;
            at = new Vector3(world.center.x, seaLevel, world.center.z);
            if (Mathf.Max(world.size.x, world.size.z) < 1.5f) return true;

            Bounds local = r.localBounds;
            Matrix4x4 m = r.localToWorldMatrix;
            for (int i = 0; i < 24; i++)
            {
                int axis = Random.Range(0, 3);
                Vector3 q = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                q[axis] = Random.value < 0.5f ? -1f : 1f;   // 한 면 위
                Vector3 p = m.MultiplyPoint3x4(local.center + Vector3.Scale(q, local.extents));
                if (Mathf.Abs(p.y - seaLevel) > 0.3f) continue;
                at = new Vector3(p.x, seaLevel, p.z);
                return true;
            }
            return false;
        }

        private void Collect()
        {
            _structures.Clear();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                if (b.min.y > seaLevel || b.max.y < seaLevel) continue;
                if (b.size.x > maxSize || b.size.z > maxSize) continue;
                if (r.gameObject.layer == _waterLayer) continue;
                string n = r.name;
                if (Has(n, "water") || Has(n, "sea") || Has(n, "ocean")) continue;
                _structures.Add(r);
            }
        }

        private static bool Has(string s, string word) => s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
