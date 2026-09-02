using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Restaurant
{
    public class CustomerManager : MonoBehaviour
    {
        private bool _isSpawningPaused;

        [Header("좌석 목록")]
        [SerializeField] private List<Seat> seatList = new();

        [Header("손님 프리팹")]
        [SerializeField] private GameObject customerPrefab;

        [Header("스폰 설정")]
        [SerializeField] private float spawnInterval = 5.0f; // 스폰 체크 주기
        [SerializeField] private bool autoSpawn = true;

        private Coroutine _spawnRoutine;

        private void Awake()
        {
            // 좌석 리스트가 비어있다면 씬 내 좌석 컴포넌트 자동 탐색
            if (seatList == null || seatList.Count == 0)
            {
                seatList = new List<Seat>(FindObjectsOfType<Seat>());
            }
        }

        private void Start()
        {
            if (autoSpawn)
            {
                StartSpawning();
            }
        }

        public void StartSpawning()
        {
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        public void StopSpawning()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        public void PauseSpawning(bool pause)
        {
            _isSpawningPaused = pause;
            Debug.Log($"[CustomerManager] 손님 스폰 일시정지 상태: {pause}");
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnInterval);

                // 미니게임 진행 등으로 일시정지 중이면 스폰 건너뛰기
                if (_isSpawningPaused)
                {
                    continue;
                }

                Seat emptySeat = GetRandomEmptySeat();
                if (emptySeat != null && customerPrefab != null)
                {
                    SpawnCustomerAtSeat(emptySeat);
                }
                else
                {
                    Debug.Log("[CustomerManager] 빈 좌석이 없거나 프리팹이 설정되지 않아 스폰을 대기합니다.");
                }
            }
        }

        private Seat GetRandomEmptySeat()
        {
            List<Seat> emptySeats = seatList.FindAll(seat => seat != null && !seat.IsOccupied);
            if (emptySeats.Count == 0) return null;

            int randomIndex = Random.Range(0, emptySeats.Count);
            return emptySeats[randomIndex];
        }

        private void SpawnCustomerAtSeat(Seat targetSeat)
        {
            if (targetSeat == null || targetSeat.SitPoint == null) return;

            // 생성할 때부터 좌석의 월드 위치와 회전값으로 생성
            GameObject customerObj = Instantiate(customerPrefab, targetSeat.SitPoint.position, targetSeat.SitPoint.rotation);
            CustomerController customer = customerObj.GetComponent<CustomerController>();

            if (customer != null)
            {
                customer.Initialize(targetSeat);
                Debug.Log($"[CustomerManager] 좌석({targetSeat.name})에 손님이 착석했습니다.");
            }
        }
    }
}