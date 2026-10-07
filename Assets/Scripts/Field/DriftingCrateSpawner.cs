using System.Collections.Generic;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 보급품 상자를 주기적으로 바다에 띄워 데크 가장자리로 흘려 보낸다. (+10/7)
    ///
    /// 닿을 자리(landingSpots)마다 상자 하나. 자식 "Stand"가 있으면 그게 플레이어가 설 데크 위 자리,
    /// 없으면 닿을 자리에서 데크 쪽으로 가장 가까운 NavMesh 점을 쓴다.
    /// 상자는 닿을 자리에서 driftFrom 방향으로 driftDistance만큼 떨어진 바다에서 출발한다.
    /// </summary>
    public class DriftingCrateSpawner : MonoBehaviour
    {
        [SerializeField] private DriftingCrate cratePrefab;
        [Tooltip("상자가 흘러와 멈출 곳(데크 가장자리 바로 앞 물). 높이는 수면.")]
        [SerializeField] private Transform[] landingSpots;

        [Header("흘러오기")]
        [Tooltip("닿을 자리에서 이 방향으로 떨어진 바다에서 출발한다.")]
        [SerializeField] private Vector3 driftFrom = new(0f, 0f, -1f);
        [SerializeField, Min(1f)] private float driftDistance = 30f;
        [SerializeField, Min(1f)] private float driftSeconds = 14f;

        [Header("주기")]
        [SerializeField, Min(0f)] private float firstDelay = 3f;
        [Tooltip("하나를 띄운 뒤(또는 주운 뒤) 다음 상자까지.")]
        [SerializeField, Min(1f)] private float interval = 30f;
        [SerializeField, Min(1)] private int maxAlive = 2;

        private readonly Dictionary<Transform, DriftingCrate> _occupied = new();
        private float _nextAt;

        private void Awake()
        {
            if (cratePrefab == null || landingSpots == null || landingSpots.Length == 0)
            {
                Debug.LogError($"{name}: DriftingCrateSpawner.cratePrefab / landingSpots가 비어 있다. 보급품 상자가 안 온다.", this);
                enabled = false;
            }
        }

        private void Start() => _nextAt = Time.time + firstDelay;

        private void Update()
        {
            if (Time.time < _nextAt) return;
            _nextAt = Time.time + interval;

            if (AliveCount() >= maxAlive) return;
            Transform spot = PickFreeSpot();
            if (spot != null) Spawn(spot);
        }

        private int AliveCount()
        {
            int n = 0;
            foreach (DriftingCrate c in _occupied.Values) if (c != null) n++;
            return n;
        }

        private Transform PickFreeSpot()
        {
            var free = new List<Transform>();
            foreach (Transform s in landingSpots)
                if (s != null && (!_occupied.TryGetValue(s, out DriftingCrate c) || c == null)) free.Add(s);
            return free.Count == 0 ? null : free[Random.Range(0, free.Count)];
        }

        private void Spawn(Transform spot)
        {
            Vector3 dir = driftFrom;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.001f ? Vector3.back : dir.normalized;
            Vector3 to = spot.position;
            Vector3 from = to + dir * driftDistance;

            DriftingCrate crate = Instantiate(cratePrefab, from, Quaternion.identity, transform);
            crate.Launch(from, to, StandPointFor(spot), driftSeconds);
            crate.Picked += c => _nextAt = Mathf.Min(_nextAt, Time.time + interval);   // 주우면 그때부터 다시 잰다
            _occupied[spot] = crate;
        }

        private static Vector3 StandPointFor(Transform spot)
        {
            Transform stand = spot.Find("Stand");
            if (stand != null) return stand.position;
            return UnityEngine.AI.NavMesh.SamplePosition(spot.position, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)
                ? hit.position : spot.position;
        }
    }
}
