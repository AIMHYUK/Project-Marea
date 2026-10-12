using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// 데크 출렁임. (+10/12) 지어진 데크 판들이 물 위에 뜬 것처럼 은은하게 기울었다 돌아온다. 고래가 부딪치면(Kick) 그 판만 크게 흔들린다.
    ///
    /// 판마다 건물 · 땅 쪽 가장자리(hingeSide)를 경첩으로 돌린다 — 그쪽은 안 움직여 이음매가 안 벌어지고 바깥쪽이 오르내린다.
    /// 판 = probe 바로 아래 가장 넓은 바닥 메시. 꺼져 있거나 시설 폐허(FacilitySite.BrokenVisual) 밑이면 흔들지 않는다.
    /// 식당 건물 판은 sections에 넣지 않는다 — 건물째 기운다.
    ///
    /// 움직이는 것: 그 판에 붙은 렌더러(바닥 · 탁자 · 의자 · 난간 · 받침 기둥)와 파손 장판. 씬을 열 때 있던 것만 —
    /// 접시 · 손님 · 이펙트 · 조각처럼 나중에 생겨 게임이 옮기는 것은 건드리지 않는다(쉬는 자세로 덮어쓰면 안 된다).
    /// 사람(NavMeshAgent) · 애니메이션(Animator)이 붙은 것도 뺀다. 그래서 각도는 작게 둔다 — 판 끝에서 몇 cm.
    ///
    /// 시설을 짓거나 장판이 새로 깔리면 바뀌니 refreshSeconds마다 쉬는 자세로 되돌리고 다시 모은다(같은 프레임이라 안 튄다).
    /// 고래 충돌(WhaleCrashEvent)은 Instance.Kick으로 흔들고, 부딪칠 판도 ActiveFloors에서 고른다.
    /// </summary>
    public class DeckBob : MonoBehaviour
    {
        [Serializable]
        private struct Section
        {
            public string name;
            [Tooltip("판 위의 한 점(월드). 그 아래 바닥 메시를 판으로 본다.")]
            public Vector3 probe;
            [Tooltip("안 움직일 가장자리 쪽(건물 · 땅). 그 가장자리를 경첩으로 기운다.")]
            public Vector3 hingeSide;
        }

        [SerializeField] private Section[] sections =
        {
            new() { name = "식당 남쪽", probe = new Vector3(68.9f, 9f, 32.9f), hingeSide = Vector3.forward },
            new() { name = "Lv02", probe = new Vector3(51.1f, 8.2f, 32.7f), hingeSide = Vector3.right },
            new() { name = "Lv03", probe = new Vector3(50.8f, 7.6f, 46.7f), hingeSide = Vector3.right },
            new() { name = "농장", probe = new Vector3(102.5f, 7.9f, 33.3f), hingeSide = Vector3.right },
        };

        [Header("평소 출렁임")]
        [Tooltip("최대 기울기(도). 판 폭 13m면 0.3도에 바깥 가장자리가 7cm 오르내린다.")]
        [SerializeField, Range(0f, 2f)] private float idleAngle = 0.3f;
        [Tooltip("한 번 출렁이는 데 걸리는 시간(초, 최소~최대). 판마다 다르게 골라 따로 논다.")]
        [SerializeField] private Vector2 idlePeriod = new(5f, 8f);

        [Header("다시 모으기")]
        [Tooltip("이만큼(초)마다 판 하나씩 쉬는 자세로 되돌리고 움직일 것을 다시 모은다 — 시설을 짓거나 장판이 깔리면 바뀐다.")]
        [SerializeField, Min(0.5f)] private float refreshSeconds = 3f;
        [SerializeField] private float seaLevel = 6.9f;

        /// <summary>씬에 하나. 고래 충돌이 찾는다.</summary>
        public static DeckBob Instance { get; private set; }

        private struct Shock
        {
            public Vector3 hinge, axis;
            public float down, angle, frequency, decay, seconds, start;
        }

        private class Live
        {
            public Section def;
            public bool active;
            public Bounds floor;
            public readonly List<Transform> items = new();
            public Vector3[] restPos = Array.Empty<Vector3>();
            public Quaternion[] restRot = Array.Empty<Quaternion>();
            public float period, phase;
            public readonly List<Shock> kicks = new();
        }

        private readonly List<Live> _live = new();
        private readonly HashSet<GameObject> _sceneBorn = new();
        private float _nextRefresh;
        private int _refreshIndex;

        /// <summary>지금 흔들리는(지어진) 판들의 바닥 상자.</summary>
        public IEnumerable<Bounds> ActiveFloors
        {
            get { foreach (Live l in _live) if (l.active) yield return l.floor; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) Debug.LogError($"{name}: DeckBob이 씬에 둘 있다. 둘이 같은 판을 덮어쓴다.", this);
            Instance = this;
            if (sections == null || sections.Length == 0) Debug.LogError($"{name}: DeckBob.sections가 비어 있다. 데크가 안 흔들린다.", this);
        }

        private void Start()
        {
            // 씬을 열 때 있던 것 — 꺼져 있던 것(지으면 켜지는 가구)까지
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                _sceneBorn.Add(r.gameObject);
            foreach (Section s in sections)
                _live.Add(new Live
                {
                    def = s,
                    period = UnityEngine.Random.Range(idlePeriod.x, Mathf.Max(idlePeriod.x, idlePeriod.y)),
                    phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                });
            foreach (Live l in _live) Refresh(l);
        }

        private void OnDisable()
        {
            foreach (Live l in _live) Restore(l);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// point(가장자리 위 점)가 있는 판을 바다(outward) 반대편 가장자리를 경첩으로 흔든다 — 바다 쪽이 먼저 쑥 내려갔다 출렁이며 잦아든다.
        /// 그 점이 흔들리는 판 위가 아니면 false.
        /// </summary>
        public bool Kick(Vector3 point, Vector3 outward, float angle, float frequency, float decay, float seconds)
        {
            Live l = _live.Find(x => x.active && Inside(x.floor, point, 1f));
            if (l == null) return false;
            Refresh(l);   // 부딪치면서 깔린 장판까지 같이 움직이게
            Vector3 inward = -outward;
            Vector3 hinge = point + inward * (Extent(l.floor, inward) * 2f);
            hinge.y = l.floor.max.y;
            Vector3 axis = Vector3.Cross(Vector3.up, inward);
            l.kicks.Add(new Shock
            {
                hinge = hinge, axis = axis,
                down = (Quaternion.AngleAxis(1f, axis) * outward).y > 0f ? -1f : 1f,   // 이 부호로 돌면 바다 쪽이 내려간다
                angle = angle, frequency = frequency, decay = decay, seconds = seconds, start = Time.time,
            });
            return true;
        }

        private void LateUpdate()
        {
            if (_live.Count == 0) return;
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + refreshSeconds / _live.Count;   // 한 프레임에 하나씩 돌아가며
                _refreshIndex = (_refreshIndex + 1) % _live.Count;
                Refresh(_live[_refreshIndex]);
            }
            foreach (Live l in _live) if (l.active) Apply(l);
        }

        private void Apply(Live l)
        {
            float t = Time.time;
            // 평소 — 느린 사인 둘을 섞어 규칙적으로 안 보이게
            Vector3 side = l.def.hingeSide.normalized;
            Vector3 idleHinge = l.floor.center + side * Extent(l.floor, side);
            idleHinge.y = l.floor.max.y;
            Vector3 idleAxis = Vector3.Cross(Vector3.up, side);
            float w = Mathf.PI * 2f / l.period;
            float idle = idleAngle * (0.7f * Mathf.Sin(w * t + l.phase) + 0.3f * Mathf.Sin(w * 1.63f * t + l.phase * 1.7f));
            Quaternion qIdle = Quaternion.AngleAxis(idle, idleAxis);

            for (int k = l.kicks.Count - 1; k >= 0; k--)
                if (t - l.kicks[k].start >= l.kicks[k].seconds) l.kicks.RemoveAt(k);

            for (int i = 0; i < l.items.Count; i++)
            {
                Transform item = l.items[i];
                if (item == null) continue;
                Vector3 pos = idleHinge + qIdle * (l.restPos[i] - idleHinge);
                Quaternion rot = qIdle * l.restRot[i];
                foreach (Shock kick in l.kicks)
                {
                    float age = t - kick.start;
                    // 처음엔 바다 쪽이 쑥 내려갔다가 출렁이며 잦아든다. 끝 1초는 0으로 모은다.
                    float env = Mathf.Exp(-age / kick.decay) * Mathf.Clamp01(kick.seconds - age);
                    Quaternion q = Quaternion.AngleAxis(kick.down * kick.angle * env * Mathf.Sin(2f * Mathf.PI * kick.frequency * age), kick.axis);
                    pos = kick.hinge + q * (pos - kick.hinge);
                    rot = q * rot;
                }
                item.SetPositionAndRotation(pos, rot);
            }
        }

        private void Refresh(Live l)
        {
            Restore(l);
            l.items.Clear();
            l.active = FindFloor(l.def.probe, seaLevel + 0.3f, out l.floor);
            if (!l.active) { l.kicks.Clear(); return; }

            var set = new HashSet<Transform>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !(r is MeshRenderer)) continue;
                TripHazard hazard = r.GetComponentInParent<TripHazard>();
                if (hazard == null && !_sceneBorn.Contains(r.gameObject)) continue;   // 나중에 생긴 것(접시 · 조각 · 이펙트)
                Bounds b = r.bounds;
                if (!Inside(l.floor, b.center, 0.5f)) continue;
                if (b.max.y < l.floor.min.y - 0.5f || b.max.y > l.floor.max.y + 3f) continue;   // 바닥에 닿는 것 — 물속 기둥까지, 벽 · 지붕은 빼고
                Transform t = r.transform;
                if (t.GetComponentInParent<NavMeshAgent>() != null || t.GetComponentInParent<Animator>() != null || t.GetComponentInParent<Canvas>() != null) continue;
                Rigidbody body = t.GetComponentInParent<Rigidbody>();
                if (body != null && !body.isKinematic) continue;
                set.Add(hazard != null ? hazard.transform : t);   // 장판은 데칼째(루트)
            }
            // 이미 고른 것의 자식은 부모를 따라 움직인다 — 두 번 돌리지 않게 뺀다.
            foreach (Transform t in set)
            {
                bool nested = false;
                for (Transform p = t.parent; p != null && !nested; p = p.parent) nested = set.Contains(p);
                if (!nested) l.items.Add(t);
            }
            l.restPos = new Vector3[l.items.Count];
            l.restRot = new Quaternion[l.items.Count];
            for (int i = 0; i < l.items.Count; i++) { l.restPos[i] = l.items[i].position; l.restRot[i] = l.items[i].rotation; }
        }

        private static void Restore(Live l)
        {
            for (int i = 0; i < l.items.Count && i < l.restPos.Length; i++)
                if (l.items[i] != null) l.items[i].SetPositionAndRotation(l.restPos[i], l.restRot[i]);
        }

        // probe 바로 아래 가장 넓은 바닥 메시(얇고, 윗면이 probe 높이 근처 · 수면 위). 꺼져 있거나 시설 폐허 밑이면 없다.
        private static bool FindFloor(Vector3 probe, float aboveSea, out Bounds floor)
        {
            floor = default;
            var wreck = new List<Transform>();
            foreach (FacilitySite site in FindObjectsByType<FacilitySite>(FindObjectsSortMode.None))
                if (site.BrokenVisual != null) wreck.Add(site.BrokenVisual.transform);
            float bestArea = 60f;   // 이보다 작은 건 판이 아니다
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                Bounds b = r.bounds;
                if (b.size.y > 1.5f || b.max.y < aboveSea || Mathf.Abs(b.max.y - probe.y) > 1f) continue;
                if (!Inside(b, probe, 0f)) continue;
                float area = b.size.x * b.size.z;
                if (area <= bestArea || wreck.Exists(w => r.transform.IsChildOf(w))) continue;
                bestArea = area;
                floor = b;
            }
            return bestArea > 60f;
        }

        private static bool Inside(Bounds b, Vector3 p, float margin)
            => p.x > b.min.x - margin && p.x < b.max.x + margin && p.z > b.min.z - margin && p.z < b.max.z + margin;

        private static float Extent(Bounds b, Vector3 dir) => Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
    }
}
