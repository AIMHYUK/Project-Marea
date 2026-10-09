using Marea.Economy;
using Marea.Field;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Hud
{
    /// <summary>
    /// 메인 플레이 화면의 A 몫 HUD. (+10/2, 이슈 92 — 와이어프레임 01)
    ///
    /// - 우상단 재화(골드)
    /// - 좌하단 [메뉴] → 탐사정 / 창고 / 성장 펼침
    /// - 좌측 알람 (지금은 탐사정 귀환 하나)
    ///
    /// 와이어프레임의 접수된 주문 · 남은 영업 시간 · [영업 시작]은 B 몫(BusinessUIController)
    /// 이라 여기 없다. 시각은 WorldClockUI가 그대로 찍고, 날씨는 데이터가 없어 프리팹에 자리만 있다.
    ///
    /// 창들은 각자 단축키(Tab, U)도 그대로 둔다. 여기서는 같은 창을 버튼으로 여는 길을 하나 더 낼 뿐이다.
    /// </summary>
    public class MainHud : MonoBehaviour
    {
        [Header("재화")]
        [SerializeField] private TextMeshProUGUI goldLabel;

        [Header("메뉴")]
        [Tooltip("누르면 아래 버튼 묶음이 펼쳐지고 접힌다.")]
        [SerializeField] private Button menuButton;
        [Tooltip("탐사정 · 창고 · 성장 버튼이 든 묶음.")]
        [SerializeField] private GameObject menuGroup;
        [SerializeField] private Button expeditionButton;
        [SerializeField] private Button warehouseButton;
        [SerializeField] private Button growthButton;

        [Header("알림 표식 (!)")]
        [Tooltip("탐사정이 돌아왔을 때 [메뉴] 버튼에 붙는 표식. 접혀 있어도 보이게.")]
        [SerializeField] private GameObject menuBadge;
        [SerializeField] private GameObject expeditionBadge;
        [SerializeField] private HudAlarm alarm;

        [Header("여는 대상 (비우면 씬에서 찾는다)")]
        [SerializeField] private WarehouseUI warehouseUI;
        [SerializeField] private UpgradeUI upgradeUI;
        [SerializeField] private ExpeditionManager expedition;

        private Wallet _wallet;
        private FacilityLevels _levels;

        private void Awake()
        {
            if (warehouseUI == null) warehouseUI = FindAnyObjectByType<WarehouseUI>(FindObjectsInactive.Include);
            if (upgradeUI == null) upgradeUI = FindAnyObjectByType<UpgradeUI>(FindObjectsInactive.Include);
            if (expedition == null) expedition = FindAnyObjectByType<ExpeditionManager>(FindObjectsInactive.Include);

            if (goldLabel == null) Debug.LogError($"{name}: MainHud.goldLabel이 비어 있다. 골드가 안 보인다.", this);
            if (menuButton == null || menuGroup == null)
                Debug.LogError($"{name}: MainHud.menuButton/menuGroup이 비어 있다. [메뉴]가 안 펼쳐진다.", this);
            if (warehouseUI == null) Debug.LogError($"{name}: 씬에 WarehouseUI가 없다. [창고]가 아무것도 안 연다.", this);
            if (upgradeUI == null) Debug.LogError($"{name}: 씬에 UpgradeUI가 없다. [성장]이 아무것도 안 연다.", this);
            if (expedition == null)
                Debug.LogError($"{name}: 씬에 ExpeditionManager가 없다. [탐사정]이 아무것도 안 연다.", this);

            if (menuButton != null) menuButton.onClick.AddListener(ToggleMenu);
            // ?. 를 쓰지 않는다 — 파괴된 UnityEngine.Object는 ?. 로는 null로 안 걸린다.
            if (warehouseButton != null && warehouseUI != null) warehouseButton.onClick.AddListener(warehouseUI.Toggle);
            if (growthButton != null && upgradeUI != null) growthButton.onClick.AddListener(upgradeUI.Toggle);
            if (expeditionButton != null) expeditionButton.onClick.AddListener(OpenExpedition);

            if (menuGroup != null) menuGroup.SetActive(false);
        }

        // Wallet·ExpeditionManager의 Awake 뒤에 잡는다 (Instance, 첫 상태).
        private void Start()
        {
            _wallet = Wallet.Instance;
            if (_wallet == null)
                Debug.LogError($"{name}: 씬에 Wallet이 없다. 골드가 안 보인다.", this);
            else
            {
                _wallet.OnGoldChanged += HandleGoldChanged;
                HandleGoldChanged(_wallet.Gold);
            }

            // 탐사 선착장이 해금되기 전에는 [탐사정]으로도 못 연다 — 월드에서는 부서진 선착장이라
            // 누를 탐사정이 없는데 메뉴로 우회하면 해금 비용을 건너뛰게 된다.
            _levels = FacilityLevels.Instance;
            if (_levels == null)
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. [탐사정] 해금 여부를 알 수 없어 막아둔다.", this);
            else
                _levels.OnUnlocked += HandleUnlocked;

            if (expedition != null)
            {
                expedition.OnStateChanged += HandleExpeditionChanged;
                HandleExpeditionChanged(expedition.Current);
            }
        }

        private bool ExplorerUnlocked => _levels != null && _levels.IsUnlocked(FacilityKind.Explorer);

        private void HandleUnlocked(FacilityKind kind)
        {
            if (kind == FacilityKind.Explorer && expedition != null) HandleExpeditionChanged(expedition.Current);
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
            if (expedition != null) expedition.OnStateChanged -= HandleExpeditionChanged;
            if (_levels != null) _levels.OnUnlocked -= HandleUnlocked;
        }

        private void ToggleMenu()
        {
            if (menuGroup != null) menuGroup.SetActive(!menuGroup.activeSelf);
        }

        /// <summary>
        /// 대기면 지역 선택, 탐사 중이면 남은 시간, 귀환이면 결과 창 — 월드의 탐사정을 누른 것과 같다.
        /// (+10/2) 1묶음에선 대기 중에만 열었다. 탐사 중 · 귀환 창이 생겨서 막을 이유가 없어졌다.
        /// (+10/9, 이슈 128) 귀환이면 창 대신 플레이어가 배 앞으로 걸어간다 — 보상은 배 앞에서만 받는다.
        /// </summary>
        private void OpenExpedition()
        {
            if (expedition == null || !ExplorerUnlocked) return;
            expedition.InteractFromHud();
        }

        private void HandleGoldChanged(int gold)
        {
            if (goldLabel != null) goldLabel.text = $"{gold:N0} G";
        }

        private void HandleExpeditionChanged(ExpeditionManager.State state)
        {
            bool returned = state == ExpeditionManager.State.Returned;

            if (menuBadge != null) menuBadge.SetActive(returned);
            if (expeditionBadge != null) expeditionBadge.SetActive(returned);
            if (expeditionButton != null)
                expeditionButton.interactable = ExplorerUnlocked;

            if (alarm == null) return;
            if (returned) alarm.Post(expedition, "탐사정이 돌아왔습니다");
            else alarm.Clear(expedition);
        }
    }
}
