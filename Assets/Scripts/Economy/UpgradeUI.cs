using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// U(또는 메인 [메뉴] → [성장])로 여는 시설 성장 창. (+9/10)
    ///
    /// 폴더가 UI/가 아닌 이유는 UI/ 가 B 몫이라서다. A의 UI는 각자 자기 시스템 폴더에
    /// 둔다 — WarehouseUI가 Field/에 있는 것과 같은 규칙이다 (2차 분담 2절).
    ///
    /// 논블로킹이다. 열어둔 채 걸어다녀도 되고 클릭 이동도 된다. 설계 결정 7의 UIStack은
    /// B 몫인데 아직 없다.
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「시설 성장 테크」) 세로 목록에서
    /// 왼쪽 카테고리 탭 · 가운데 노드 · 오른쪽 선택한 업그레이드 / 업그레이드 조건 / [업그레이드] · 아래 세부 설명으로 바뀌었다.
    /// 카테고리와 노드 트리 데이터가 아직 없어서:
    /// - 카테고리는 시설 종류를 여기서 임시로 나눈다 (<see cref="CategoryOf"/>). 교감은 비어 있다.
    /// - 노드는 시설 하나에 하나다 (<see cref="FacilityNode"/>). 필요 자원은 "없음"으로 자리만 있다.
    /// 잠긴 시설은 같은 버튼이 해금을 한다 (+9/28, 이슈 71).
    /// </summary>
    public class UpgradeUI : UiPanel
    {
        public enum Category { Production, Cooking, Exploration, Bond }

        [Header("재화")]
        [Tooltip("보유 골드를 찍을 곳. 비워둬도 창은 돈다.")]
        [SerializeField] private TextMeshProUGUI goldLabel;

        [Header("카테고리 탭")]
        [SerializeField] private Button tabProduction;
        [SerializeField] private Button tabCooking;
        [SerializeField] private Button tabExploration;
        [SerializeField] private Button tabBond;
        [SerializeField] private Sprite tabOnSprite;
        [SerializeField] private Sprite tabOffSprite;

        [Header("노드")]
        [Tooltip("노드가 쌓일 곳.")]
        [SerializeField] private Transform nodeParent;
        [SerializeField] private FacilityNode nodePrefab;
        [Tooltip("고른 카테고리에 시설이 없을 때 켜는 안내.")]
        [SerializeField] private GameObject emptyHint;

        [Header("선택한 업그레이드")]
        [SerializeField] private TextMeshProUGUI selectedName;
        [SerializeField] private TextMeshProUGUI selectedEffect;
        [SerializeField] private TextMeshProUGUI conditionLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonLabel;

        [Tooltip("비워두면 같은 씬에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        private readonly List<FacilityNode> _nodes = new();
        private FacilityLevels _levels;
        private Wallet _wallet;
        private Category _category = Category.Production;
        private FacilityKind? _selected;

        protected override void Awake()
        {
            base.Awake();
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();

            if (nodeParent == null || nodePrefab == null)
                Debug.LogError($"{name}: UpgradeUI의 nodeParent·nodePrefab 중 비어 있는 게 있다. 노드를 만들 수 없다.", this);
            if (upgradeButton == null)
                Debug.LogError($"{name}: UpgradeUI.upgradeButton이 비어 있다. 업그레이드할 수 없다.", this);
            else
                upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            if (input == null)
                Debug.LogError($"{name}: 씬에서 PlayerInputReader를 못 찾았다. U로 열 수 없다.", this);

            if (tabProduction != null) tabProduction.onClick.AddListener(() => SetCategory(Category.Production));
            if (tabCooking != null) tabCooking.onClick.AddListener(() => SetCategory(Category.Cooking));
            if (tabExploration != null) tabExploration.onClick.AddListener(() => SetCategory(Category.Exploration));
            if (tabBond != null) tabBond.onClick.AddListener(() => SetCategory(Category.Bond));
        }

        // FacilityLevels.Awake가 에셋 표를 만든 뒤라야 DataOf가 값을 준다.
        protected override void Start()
        {
            base.Start();
            _levels = FacilityLevels.Instance;
            _wallet = Wallet.Instance;

            if (_levels == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. 성장 창이 비어 있게 된다.", this);
                return;
            }
            if (_wallet == null)
                Debug.LogError($"{name}: 씬에 Wallet이 없다. 비용을 치를 수 없다.", this);

            _levels.OnLevelChanged += HandleLevelChanged;
            _levels.OnUnlocked += HandleUnlocked;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;

            SetCategory(_category);
        }

        private void OnDestroy()
        {
            if (_levels != null) _levels.OnLevelChanged -= HandleLevelChanged;
            if (_levels != null) _levels.OnUnlocked -= HandleUnlocked;
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        private void Update()
        {
            if (input == null) return;
            if (input.UpgradePressed) Toggle();
        }

        // 닫혀 있는 동안에도 골드는 변한다(정산). 열 때 한 번 맞춰준다.
        protected override void OnOpened() => RefreshAll();

        /// <summary>
        /// 시설 → 카테고리. 기획 데이터가 없어 여기서 임시로 나눈다. 교감은 해당 시설이 아직 없다.
        /// 시설을 늘리면(계약 8) 여기에도 한 줄 넣는다 — 빠뜨리면 생산 탭에 들어간다.
        /// </summary>
        public static Category CategoryOf(FacilityKind kind) => kind switch
        {
            FacilityKind.Business => Category.Cooking,
            FacilityKind.Kitchen => Category.Cooking,
            FacilityKind.Explorer => Category.Exploration,
            _ => Category.Production,   // Storage, Farm
        };

        private void SetCategory(Category category)
        {
            _category = category;
            PaintTab(tabProduction, category == Category.Production);
            PaintTab(tabCooking, category == Category.Cooking);
            PaintTab(tabExploration, category == Category.Exploration);
            PaintTab(tabBond, category == Category.Bond);

            RebuildNodes();
        }

        private void PaintTab(Button tab, bool on)
        {
            if (tab == null || tabOnSprite == null || tabOffSprite == null) return;
            if (tab.targetGraphic is Image image) image.sprite = on ? tabOnSprite : tabOffSprite;
        }

        /// <summary>이 카테고리의 시설만 enum 순서대로 노드로 만든다. 에셋이 빠진 시설은 건너뛴다.</summary>
        private void RebuildNodes()
        {
            if (_levels == null || nodeParent == null || nodePrefab == null) return;

            var kinds = new List<FacilityKind>();
            foreach (FacilityKind kind in System.Enum.GetValues(typeof(FacilityKind)))
                if (_levels.DataOf(kind) != null && CategoryOf(kind) == _category) kinds.Add(kind);

            while (_nodes.Count < kinds.Count) _nodes.Add(Instantiate(nodePrefab, nodeParent));
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].gameObject.SetActive(i < kinds.Count);
            for (int i = 0; i < kinds.Count; i++) _nodes[i].Bind(kinds[i], Select);

            if (emptyHint != null) emptyHint.SetActive(kinds.Count == 0);

            _selected = kinds.Count > 0 ? kinds[0] : null;
            RefreshAll();
        }

        private void Select(FacilityKind kind)
        {
            _selected = kind;
            RefreshAll();
        }

        private void HandleUpgradeClicked()
        {
            if (_selected == null || _levels == null) return;
            // 실패해도 조용하다 — 버튼이 이미 꺼져 있어서 드물고, 오면 그 사이 골드가 줄어든 경우다.
            FacilityKind kind = _selected.Value;
            if (_levels.IsUnlocked(kind)) FacilityUpgrade.TryUpgrade(kind);
            else FacilityUpgrade.TryUnlock(kind);
            RefreshAll();
        }

        private void HandleLevelChanged(FacilityKind kind, int level) => RefreshAll();
        private void HandleUnlocked(FacilityKind kind) => RefreshAll();
        private void HandleGoldChanged(int gold) => RefreshAll();

        /// <summary>노드와 오른쪽 상세를 다시 그린다. 한 시설을 올리면 골드가 줄어 버튼 상태도 바뀐다.</summary>
        private void RefreshAll()
        {
            if (goldLabel != null && _wallet != null) goldLabel.text = $"{_wallet.Gold:N0} G";

            foreach (FacilityNode node in _nodes)
            {
                if (!node.gameObject.activeSelf) continue;
                node.Refresh();
                node.SetSelected(_selected != null && node.Kind == _selected.Value);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            FacilityData data = _selected != null && _levels != null ? _levels.DataOf(_selected.Value) : null;
            if (data == null)
            {
                SetText(selectedName, _category == Category.Bond ? "교감" : string.Empty);
                SetText(selectedEffect, string.Empty);
                SetText(conditionLabel, string.Empty);
                SetText(descriptionLabel, _category == Category.Bond ? "교감 성장은 준비 중입니다." : string.Empty);
                if (upgradeButton != null) upgradeButton.interactable = false;
                SetText(upgradeButtonLabel, "업그레이드");
                return;
            }

            FacilityKind kind = _selected.Value;
            bool unlocked = _levels.IsUnlocked(kind);
            int level = _levels.LevelOf(kind);

            if (!unlocked)
            {
                FacilityUpgrade.Result state = FacilityUpgrade.CanUnlock(kind);
                FacilityKind? missing = FacilityUpgrade.FirstMissingPrerequisite(kind);

                SetText(selectedName, $"{NameOf(kind)} 해금");
                SetText(selectedEffect, missing != null ? $"선행: {NameOf(missing.Value)} 해금" : "선행 없음");
                SetText(conditionLabel, $"필요 골드 {data.UnlockCost:N0} G\n필요 자원 없음");
                SetText(descriptionLabel, data.UnlockDescription);
                if (upgradeButton != null) upgradeButton.interactable = state == FacilityUpgrade.Result.Ok;
                SetText(upgradeButtonLabel, state switch
                {
                    FacilityUpgrade.Result.Ok                  => "해금",
                    FacilityUpgrade.Result.NotEnoughGold       => "골드 부족",
                    FacilityUpgrade.Result.MissingPrerequisite => "선행 필요",
                    _                                          => "설정 오류",
                });
                return;
            }

            bool isMax = data.IsMaxLevel(level);
            FacilityUpgrade.Result up = FacilityUpgrade.CanUpgrade(kind);

            SetText(selectedName, isMax ? $"{NameOf(kind)} Lv.{level} (최대)" : $"{NameOf(kind)} Lv.{level + 1}");
            SetText(selectedEffect, isMax || !data.HasEffectTable
                ? $"지금  {EffectText(data, level)}"
                : $"지금  {EffectText(data, level)}\n다음  {EffectText(data, level + 1)}");
            SetText(conditionLabel, isMax ? "더 올릴 수 없습니다" : $"필요 골드 {data.CostToNext(level):N0} G\n필요 자원 없음");
            SetText(descriptionLabel, data.HasEffectTable
                ? "영업 시간과 정산 배율이 늘어납니다."
                : "효과 수치는 기획 확정 후 들어갑니다.");
            if (upgradeButton != null) upgradeButton.interactable = up == FacilityUpgrade.Result.Ok;
            SetText(upgradeButtonLabel, up switch
            {
                FacilityUpgrade.Result.Ok            => "업그레이드",
                FacilityUpgrade.Result.MaxLevel      => "최대 단계",
                FacilityUpgrade.Result.NotEnoughGold => "골드 부족",
                _                                    => "설정 오류",
            });
        }

        public static string NameOf(FacilityKind kind)
        {
            FacilityData data = FacilityLevels.Instance != null ? FacilityLevels.Instance.DataOf(kind) : null;
            if (data == null) return kind.ToString();
            return string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
        }

        /// <summary>
        /// 그 레벨에서의 효과 문구. 효과표가 없는 시설(주방·창고)은 기획이 수치를
        /// 확정하지 않았다는 걸 그대로 보여준다 — 빈 줄로 두면 버그처럼 보인다.
        /// </summary>
        // 기호를 안 쓴다. Pretendard-Bold SDF에 ·, ×, → 가 없어서 TMP가 공백으로 바꾼다.
        private static string EffectText(FacilityData data, int level)
        {
            if (!data.HasEffectTable) return "효과 미정";
            return $"영업 {data.BusinessDurationAt(level):0}초   정산 {data.SettlementBonusAt(level):0.00}배";
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
