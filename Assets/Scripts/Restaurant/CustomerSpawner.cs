using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Restaurant
{
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("스폰 설정")]
        [SerializeField] private CustomerController customerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float spawnInterval = 5f;

        [Header("좌석 목록")]
        [SerializeField] private List<Seat> allSeats = new();

        private bool _isSpawningPaused;
        private Coroutine _spawnRoutine;

        private void Start()
        {
            if (allSeats.Count == 0)
            {
                allSeats.AddRange(FindObjectsByType<Seat>(FindObjectsSortMode.None));
            }

            _spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        public void PauseSpawning(bool pause)
        {
            _isSpawningPaused = pause;
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnInterval);

                if (_isSpawningPaused) continue;

                Seat emptySeat = FindEmptySeat();
                if (emptySeat != null && customerPrefab != null)
                {
                    SpawnCustomerAt(emptySeat);
                }
            }
        }

        private Seat FindEmptySeat()
        {
            List<Seat> emptySeats = new();
            foreach (var seat in allSeats)
            {
                if (seat != null && !seat.IsOccupied)
                {
                    emptySeats.Add(seat);
                }
            }

            if (emptySeats.Count == 0) return null;

            int randomIndex = Random.Range(0, emptySeats.Count);
            return emptySeats[randomIndex];
        }

        private void SpawnCustomerAt(Seat seat)
        {
            Vector3 originPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion originRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            CustomerController customer = Instantiate(customerPrefab, originPos, originRot);
            customer.Initialize(seat, originPos);
        }
    }
}
