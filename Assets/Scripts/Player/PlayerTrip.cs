using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using Marea.Field;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 플레이어도 파손된 장판에서 넘어진다. (+10/7) 서빙 직원(ServingStaff.Trip)과 같은 규칙이다.
    ///
    /// 음식을 든 채 밟으면 장판의 확률로 넘어진다 — 한 번 집은 음식에 장판 하나당 한 번만 굴린다.
    /// 넘어지면 이동이 끊기고, 든 음식을 잃고, 그 음식값만큼 골드를 낸다. 일어날 때까지 조작이 막힌다.
    /// 밟았는지는 TripHazard의 트리거가 알려 준다(OnSteppedHazard).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerTrip : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("음식값을 알 수 없을 때만 내는 골드. 보통은 들고 있던 음식값(finalPrice)이 빠진다. 모자라면 있는 만큼만.")]
        [SerializeField, Min(0)] private int tripPenalty = 50;
        [Tooltip("넘어져서 다시 움직일 때까지 초. AC_Player의 Tripped(2.8초 ÷ 2) + StandingUp(11.4초 ÷ 6). (+10/7 2배속)")]
        [SerializeField, Min(0.1f)] private float tripSeconds = 3.3f;

        [Header("연출 — 직원과 같다")]
        [SerializeField] private VfxId tripDustVfx = VfxId.Dust;
        [SerializeField] private VfxId tripFailVfx = VfxId.Fail;
        [SerializeField] private AudioClip tripClip;
        [SerializeField, Range(0f, 1f)] private float tripVolume = 0.8f;

        private static readonly int TripHash = Animator.StringToHash("Trip");

        private PlayerController _player;
        private PlayerServingController _serving;
        private WorldLabelUI _labels;
        private readonly HashSet<TripHazard> _rolled = new();
        private bool _wasHolding;
        private float _tripEndsAt;
        private float _labelUntil;
        private string _labelText;

        public bool IsTripped { get; private set; }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _serving = GetComponentInChildren<PlayerServingController>();
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);
            if (_serving == null)
                Debug.LogError($"{name}: PlayerTrip — PlayerServingController가 없다. 음식을 들었는지 몰라 안 넘어진다.", this);
            if (animator == null)
                Debug.LogError($"{name}: PlayerTrip.animator가 비어 있다. 넘어져도 넘어지는 동작이 안 나온다.", this);
        }

        private void Update()
        {
            bool holding = _serving != null && _serving.IsHoldingFood;
            if (holding && !_wasHolding) _rolled.Clear();   // 새로 집은 음식 — 장판마다 다시 굴린다
            _wasHolding = holding;

            if (IsTripped && Time.time >= _tripEndsAt)
            {
                IsTripped = false;
                _player.EndBusy();
            }
            if (_labels != null && Time.time < _labelUntil)
                _labels.Set(this, transform.position + Vector3.up * 2.4f, _labelText);
        }

        /// <summary>파손된 장판을 밟았다 — TripHazard의 트리거가 부른다.</summary>
        public void OnSteppedHazard(TripHazard hazard)
        {
            if (IsTripped || hazard == null || _serving == null || !_serving.IsHoldingFood) return;
            if (!_rolled.Add(hazard)) return;
            if (Random.value < hazard.TripChance) Trip();
        }

        private void Trip()
        {
            IsTripped = true;
            _tripEndsAt = Time.time + tripSeconds;
            _player.Interrupt();
            _player.BeginBusy();
            int price = _serving.LastCookingResult.finalPrice;   // 버리기 전에 — 들고 있던 음식값을 물어낸다
            _serving.ClearHeldFood();
            if (animator != null) animator.SetTrigger(TripHash);

            Vector3 drop = transform.position + transform.forward * 0.6f;
            Vfx.Play(tripDustVfx, drop, 0.6f);
            Vfx.Play(tripFailVfx, drop + Vector3.up * 0.3f, 0.6f);
            SoundManager.PlayAt(tripClip, drop, tripVolume);

            Wallet wallet = Wallet.Instance;
            if (wallet == null)
            {
                Debug.LogError("[PlayerTrip] 씬에 Wallet이 없다. 넘어졌는데 골드를 못 뺐다.", this);
                return;
            }
            int paid = Mathf.Min(wallet.Gold, price > 0 ? price : tripPenalty);
            if (paid > 0 && wallet.TrySpend(paid))
            {
                _labelText = $"-{paid}G";
                _labelUntil = Time.time + 2f;
            }
        }
    }
}
