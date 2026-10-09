using Marea.Core;
using Marea.Economy;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 파손된 장판. 음식을 든 서빙 직원이 밟으면 확률로 넘어진다. 클릭하면 골드를 내고 수리한다. (+9/30)
    /// (+10/7) 음식을 든 플레이어도 넘어진다 — PlayerTrip.
    ///
    /// 넘어지는 처리(음식 손실 · 골드 차감 · 애니메이션)는 ServingStaff.Trip 그대로다. 여기는
    /// "어디서 · 몇 % 로"만 든다.
    ///
    /// 밟았는지는 자식 트리거(stepTrigger)로 안다. 트리거 이벤트는 둘 중 하나에 Rigidbody가 있어야
    /// 오는데 직원(NavMeshAgent)엔 없어서, 이쪽에 Kinematic Rigidbody를 둔다. 루트 콜라이더는 클릭 수리용이다.
    /// 들고 있는지 · 이번 배달에 이미 굴렸는지는 직원이 판단한다 (ServingStaff.OnSteppedHazard).
    ///
    /// 수리 상태는 여기 든다(인스턴스마다 다르다). 한 번 고치면 다시 부서지지 않는다 — 다시 부서지는 규칙은 기획에 없다.
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「바닥 수리」) 클릭 즉시 수리하던 것을 장판 위 확인창
    /// (수리 비용 · 보유 골드 · [취소] / [수리])을 거치게 바꿨다. 골드가 모자라면 같은 창에서 [수리]가 꺼진다.
    /// </summary>
    public class TripHazard : InteractableBase
    {
        [Tooltip("음식을 든 직원이 밟았을 때 넘어질 확률 (0~1). 한 배달에 장판 하나당 한 번 판정한다.")]
        [SerializeField, Range(0f, 1f)] private float tripChance = 0.25f;

        [Tooltip("수리비(골드). 기획 UnlockData UNLOCK_FLOOR_REPAIR 1,000G. (+10/2)")]
        [SerializeField, Min(0)] private int repairCost = 1000;

        [Tooltip("파손된 모습. 수리하면 끈다.")]
        [SerializeField] private GameObject brokenVisual;

        [Tooltip("수리된 모습. 수리하면 켠다. 비워도 된다.")]
        [SerializeField] private GameObject repairedVisual;

        [Tooltip("밟았는지 보는 트리거. 자식에 두고 isTrigger를 켠다. 루트 콜라이더(클릭용)와 따로 둔다.")]
        [SerializeField] private Collider stepTrigger;

        [Header("연출 (+10/6, 이슈 117) — 기획 「파손 바닥 수리 완료」 VFX_10 / VFX_02")]
        [SerializeField] private Marea.Data.VfxId repairDustVfx = Marea.Data.VfxId.Dust;
        [SerializeField] private Marea.Data.VfxId repairSparkleVfx = Marea.Data.VfxId.Sparkle;

        private Collider _collider;
        private bool _repaired;
        private WorldLabelUI _labels;
        private WorldConfirmUI _confirm;
        private float _messageUntil;
        private string _message;

        public float TripChance => tripChance;
        public bool IsRepaired => _repaired;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);
            _confirm = FindAnyObjectByType<WorldConfirmUI>(FindObjectsInactive.Include);
            if (_confirm == null)
                Debug.LogError($"{name}: 씬에 WorldConfirmUI가 없다. 클릭해도 수리 창이 안 뜬다.", this);
            if (brokenVisual == null)
                Debug.LogError($"{name}: TripHazard.brokenVisual이 비어 있다. 수리해도 모습이 안 바뀐다.", this);
            if (stepTrigger == null || !stepTrigger.isTrigger)
                Debug.LogError($"{name}: TripHazard.stepTrigger가 비었거나 isTrigger가 꺼져 있다. 밟아도 안 넘어진다.", this);
            if (GetComponent<Rigidbody>() == null)
                Debug.LogError($"{name}: TripHazard에 Rigidbody(Kinematic)가 없다. 직원이 밟아도 트리거 이벤트가 안 온다.", this);
            Apply();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_repaired) return;
            var staff = other.GetComponentInParent<ServingStaff>();
            if (staff != null) staff.OnSteppedHazard(this);
            // (+10/7) 플레이어도 음식을 든 채 밟으면 넘어진다.
            var player = other.GetComponentInParent<Marea.Player.PlayerTrip>();
            if (player != null) player.OnSteppedHazard(this);
        }

        private void Update()
        {
            if (_labels != null && Time.time < _messageUntil)
                _labels.Set(this, transform.position + Vector3.up * 1.2f, _message);
        }

        public override bool CanInteract(IInteractor actor) => !_repaired;

        public override string InteractLabel(IInteractor actor) => $"수리 ({repairCost}G)";

        public override bool AllowKey => false;   // (+10/9) 수리 창은 클릭해야만 — 지나가다 E로 열리지 않게

        public override void Interact(IInteractor actor)
        {
            if (_repaired || _confirm == null) return;
            _confirm.Open(transform, Vector3.up * 0.5f, BuildView, TryRepair);
        }

        private ConfirmView BuildView()
        {
            int gold = Wallet.Instance != null ? Wallet.Instance.Gold : 0;
            bool enough = gold >= repairCost;
            return new ConfirmView
            {
                Title = "바닥 수리",
                Body = "음식을 든 직원이 밟으면 넘어질 수 있습니다.",
                Status = $"수리 비용 {repairCost:N0} G\n보유 골드 {gold:N0} G",
                ConfirmLabel = enough ? "수리" : "골드 부족",
                CanConfirm = enough,
            };
        }

        private bool TryRepair()
        {
            if (_repaired) return true;

            Wallet wallet = Wallet.Instance;
            if (wallet == null)
            {
                Debug.LogError($"{name}: 씬에 Wallet이 없다. 수리비를 낼 수 없다.", this);
                return false;
            }
            if (!wallet.TrySpend(repairCost)) return false;

            _repaired = true;
            Apply();
            Vfx.Play(repairDustVfx, transform.position, 0.7f);
            Vfx.Play(repairSparkleVfx, transform.position + Vector3.up * 0.4f, 0.6f);
            ShowMessage($"-{repairCost}G");
            return true;
        }

        private void Apply()
        {
            if (brokenVisual != null) brokenVisual.SetActive(!_repaired);
            if (repairedVisual != null) repairedVisual.SetActive(_repaired);
            // 고친 뒤엔 클릭을 받지 않는다. 콜라이더가 남으면 그 자리 바닥 클릭이 여기 막힌다.
            if (_collider != null) _collider.enabled = !_repaired;
            if (stepTrigger != null) stepTrigger.enabled = !_repaired;
        }

        private void ShowMessage(string text)
        {
            _message = text;
            _messageUntil = Time.time + 1.5f;
        }
    }
}
