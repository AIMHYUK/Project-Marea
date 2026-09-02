using UnityEngine;

namespace Marea.Restaurant
{
    public class Seat : MonoBehaviour
    {
        [Header("착석 위치 기준점")]
        [SerializeField] private Transform sitPoint;

        private bool _isOccupied;
        private CustomerController _currentCustomer;

        public bool IsOccupied => _isOccupied;
        public Transform SitPoint => sitPoint != null ? sitPoint : transform;

        public void AssignCustomer(CustomerController customer)
        {
            _isOccupied = true;
            _currentCustomer = customer;
        }

        public void ReleaseSeat()
        {
            _isOccupied = false;
            _currentCustomer = null;
        }
    }
}