using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Restaurant
{
    public class CustomerManager : MonoBehaviour
    {
        private bool _isSpawningPaused;

        [Header("스폰/출구 기준점")]
        [SerializeField] private Transform spawnPoint; // 손님이 들어오고 나갈 입구 위치

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
            // 영업 중이 아닐 때는 외부(UI 등)에서 스폰을 켜려고(false) 해도 무시
            if (!pause && BusinessManager.Instance != null && BusinessManager.Instance.CurrentState != BusinessState.Open)
            {
                _isSpawningPaused = true;
                return;
            }

            _isSpawningPaused = pause;
            //Debug.Log($"[CustomerManager] 손님 스폰 일시정지 상태: {pause}");
        }

        /// <summary>
        /// 아직 음식을 못 받은 손님들. 오래 기다린 순. (+9/3)
        ///
        /// 스폰 목록을 따로 안 들고 씬을 뒤지는 이유는, 손님이 식사를 끝내면
        /// 스스로 Destroy되기 때문이다. 목록을 들면 죽은 참조를 지우는 일이 또 생긴다.
        /// </summary>
        public List<CustomerController> GetWaitingCustomers()
        {
            List<CustomerController> waiting = new List<CustomerController>();

            foreach (CustomerController c in FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
            {
                if (c != null && c.State == CustomerState.WaitingOrder)
                {
                    waiting.Add(c);
                }
            }

            waiting.Sort((a, b) => a.WaitingSince.CompareTo(b.WaitingSince));
            return waiting;
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnInterval);

                // 영업 상태 매니저가 존재한다면 영업 중 상태가 아닐 때 스폰 스킵
                if (BusinessManager.Instance != null && BusinessManager.Instance.CurrentState != BusinessState.Open)
                {
                    continue;
                }

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

            // 입구(spawnPoint) 위치가 지정되어 있으면 입구에서 생성하고, 없으면 본체 위치 사용
            Vector3 originPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion originRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            GameObject customerObj = Instantiate(customerPrefab, originPos, originRot);
            CustomerController customer = customerObj.GetComponent<CustomerController>();

            if (customer != null)
            {
                // 입구 좌표를 함께 넘겨주어 좌석으로 걸어가고, 식사 후 다시 입구로 퇴장하도록 연동
                customer.Initialize(targetSeat, originPos);
                Debug.Log($"[CustomerManager] 손님이 입구에서 생성되어 좌석({targetSeat.name})으로 이동을 시작합니다.");
            }
        }

        public void ClearAllCustomers()
        {
            CustomerController[] activeCustomers = FindObjectsByType<CustomerController>(FindObjectsSortMode.None);
            foreach (var customer in activeCustomers)
            {
                if (customer != null)
                {
                    Destroy(customer.gameObject);
                }
            }

            // 모든 좌석 점유 해제
            Seat[] allSeats = FindObjectsByType<Seat>(FindObjectsSortMode.None);
            foreach (var seat in allSeats)
            {
                if (seat != null)
                {
                    seat.ReleaseSeat();
                }
            }

            Debug.Log("[CustomerManager] 영업 종료로 인해 모든 손님이 퇴장하고 좌석이 초기화되었습니다.");
        }
    }
}
