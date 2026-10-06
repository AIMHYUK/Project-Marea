using System;
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
    /// 논블로킹이다. 설계 결정 7의 UIStack은 B 몫인데 아직 없다.
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「시설 성장 테크」 + 기획 「업그레이드_해금 데이터테이블」)
    /// - 탭 = 기획 성장 분야 다섯(영업 / 주방 / 수급 / 탐사 / 직원). 탭 하나가 시설 하나다. 창고는 표에 없어 안 보인다.
    /// - 노드 = 그 시설의 업그레이드 단계(Lv.1~). 산 단계 / 다음 단계 / 아직 못 사는 단계로 나뉜다.
    /// - 오른쪽 = 고른 단계의 효과 · 선행 · 비용(골드 + 특수 자원) · [업그레이드]. 다음 단계만 살 수 있다.
    /// - 수급(농사터) · 탐사(탐사정)는 해금 전이면 [해금]이 먼저다 (+9/28, 이슈 71).
    /// </summary>
    public class UpgradeUI : UiPanel
    {
        [Serializable]
        private struct Tab
        {
            public Button button;
            public FacilityKind kind;
        }

        [Header("재화")]
        [Tooltip("보유 골드를 찍을 곳. 비워둬도 창은 돈다.")]
        [SerializeField] private TextMeshProUGUI goldLabel;

        [Header("분야 탭 (탭 하나 = 시설 하나)")]
        [SerializeField] private Tab[] tabs;
        [SerializeField] private Sprite tabOnSprite;
        [SerializeField] private Sprite tabOffSprite;

        [Header("노드")]
        [Tooltip("노드가 쌓일 곳.")]
        [SerializeField] private Transform nodeParent;
        [SerializeField] private FacilityNode nodePrefab;
        [Tooltip("해금 전이거나 단계가 없을 때 노드 자리에 띄우는 안내.")]
        [SerializeField] private TextMeshProUGUI emptyHint;

        [Header("선택한 업그레이드")]
        [SerializeField] private TextMeshProUGUI selectedName;
        [SerializeField] private TextMeshProUGUI selectedEffect;
        [SerializeField] private TextMeshProUGUI conditionLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private Button upgradeButton;
        [Tooltip("(+10/6, 이슈 117) 해금 · 업그레이드에 성공하면 새로 가진 단계 칸에서 터지는 효과. 기획 「레시피·기능 해금」 VFX_02.")]
        [SerializeField] private Data.VfxId boughtVfx = Data.VfxId.Sparkle;
        [Tooltip("(+10/6, 이슈 117) 해금 · 업그레이드 구매 확정음 + 코인 소모음. 기획 UPG-01 ui_upgrade_buy. 골드 부족(UPG-03)은 꺼진 버튼이라 UiClickSound 거절음이 낸다.")]
        [SerializeField] private AudioClip buyClip;
        [SerializeField, Range(0f, 1f)] private float buyVolume = 0.8f;
        [SerializeField] private TextMeshProUGUI upgradeButtonLabel;

        [Tooltip("비워두면 같은 씬에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        private readonly List<FacilityNode> _nodes = new();
        private FacilityLevels _levels;
        private Wallet _wallet;
        private Warehouse _warehouse;
        private FacilityKind _kind = FacilityKind.Business;
        private int _selectedLevel;   // 고른 단계 (1~). 0이면 고른 게 없다.

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

            if (tabs == null || tabs.Length == 0)
                Debug.LogError($"{name}: UpgradeUI.tabs가 비어 있다. 분야를 고를 수 없다.", this);
            else
            {
                foreach (Tab tab in tabs)
                {
                    if (tab.button == null) continue;
                    FacilityKind kind = tab.kind;
                    tab.button.onClick.AddListener(() => SetFacility(kind));
                }
                _kind = tabs[0].kind;
            }
        }

        // FacilityLevels.Awake가 에셋 표를 만든 뒤라야 DataOf가 값을 준다.
        protected override void Start()
        {
            base.Start();
            _levels = FacilityLevels.Instance;
            _wallet = Wallet.Instance;
            _warehouse = Warehouse.Instance;

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
            if (_warehouse != null) _warehouse.OnCountChanged += HandleStockChanged;

            SetFacility(_kind);
        }

        private void OnDestroy()
        {
            if (_levels != null) _levels.OnLevelChanged -= HandleLevelChanged;
            if (_levels != null) _levels.OnUnlocked -= HandleUnlocked;
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
            if (_warehouse != null) _warehouse.OnCountChanged -= HandleStockChanged;
        }

        private void Update()
        {
            if (input == null) return;
            if (input.UpgradePressed) Toggle();
        }

        // 닫혀 있는 동안에도 골드는 변한다(정산). 열 때 한 번 맞춰준다.
        protected override void OnOpened() => RefreshAll();

        private void SetFacility(FacilityKind kind)
        {
            _kind = kind;
            if (tabs != null)
                foreach (Tab tab in tabs) PaintTab(tab.button, tab.kind == kind);

            RebuildNodes();
        }

        private void PaintTab(Button tab, bool on)
        {
            if (tab == null || tabOnSprite == null || tabOffSprite == null) return;
            if (tab.targetGraphic is Image image) image.sprite = on ? tabOnSprite : tabOffSprite;
        }

        /// <summary>이 시설의 단계 수만큼 노드를 맞춘다. 고른 단계는 "다음 단계"로 둔다.</summary>
        private void RebuildNodes()
        {
            if (_levels == null || nodeParent == null || nodePrefab == null) return;
            FacilityData data = _levels.DataOf(_kind);
            int count = data != null ? data.MaxLevel : 0;

            while (_nodes.Count < count) _nodes.Add(Instantiate(nodePrefab, nodeParent));
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].gameObject.SetActive(i < count);
            for (int i = 0; i < count; i++)
            {
                data.TryGetStep(i + 1, out FacilityUpgradeStep step);
                _nodes[i].Bind(i + 1, step.description, Select);
            }

            _selectedLevel = count == 0 ? 0 : Mathf.Min(_levels.LevelOf(_kind) + 1, count);
            RefreshAll();
        }

        private void Select(int level)
        {
            _selectedLevel = level;
            RefreshAll();
        }

        private void HandleUpgradeClicked()
        {
            if (_levels == null) return;
            // 실패해도 조용하다 — 버튼이 이미 꺼져 있어서 드물고, 오면 그 사이 비용이 모자라진 경우다.
            FacilityUpgrade.Result result = _levels.IsUnlocked(_kind)
                ? FacilityUpgrade.TryUpgrade(_kind)
                : FacilityUpgrade.TryUnlock(_kind);

            // 산 다음엔 그다음 단계를 보여준다.
            FacilityData data = _levels.DataOf(_kind);
            if (data != null) _selectedLevel = Mathf.Min(_levels.LevelOf(_kind) + 1, data.MaxLevel);
            RefreshAll();

            // (+10/6) 방금 가진 단계 칸에서 반짝 — 해금이면 첫 칸(0레벨 해금은 칸이 없어 버튼에서).
            // (+10/6) 해금 연출이 창을 가렸으면 반짝이도 없다 — 안 보이는 버튼 자리 허공에서 터진다.
            if (result == FacilityUpgrade.Result.Ok) SoundManager.Play(buyClip, buyVolume);   // (+10/6) 창이 가려져도 산 소리는 난다
            if (result != FacilityUpgrade.Result.Ok || !IsVisible) return;
            int owned = _levels.LevelOf(_kind);
            RectTransform at = owned >= 1 && owned <= _nodes.Count && _nodes[owned - 1].gameObject.activeInHierarchy
                ? (RectTransform)_nodes[owned - 1].transform
                : upgradeButton != null ? (RectTransform)upgradeButton.transform : null;
            Vfx.PlayOnUI(boughtVfx, at);
        }

        private void HandleLevelChanged(FacilityKind kind, int level) => RefreshAll();
        private void HandleUnlocked(FacilityKind kind) => RefreshAll();
        private void HandleGoldChanged(int gold) => RefreshAll();
        private void HandleStockChanged(IngredientData item, int count) { if (IsOpen) RefreshAll(); }

        private void RefreshAll()
        {
            if (_levels == null) return;
            if (goldLabel != null && _wallet != null) goldLabel.text = $"{_wallet.Gold:N0} G";

            FacilityData data = _levels.DataOf(_kind);
            bool unlocked = _levels.IsUnlocked(_kind);
            int level = _levels.LevelOf(_kind);

            foreach (FacilityNode node in _nodes)
            {
                if (!node.gameObject.activeSelf) continue;
                FacilityNode.State state = node.Level <= level ? FacilityNode.State.Owned
                    : unlocked && node.Level == level + 1 ? FacilityNode.State.Next
                    : FacilityNode.State.Locked;
                node.SetState(state, node.Level == _selectedLevel);
            }

            if (emptyHint != null)
            {
                bool showHint = data == null || data.MaxLevel == 0 || !unlocked;
                emptyHint.gameObject.SetActive(showHint);
                emptyHint.text = data == null ? "준비 중"
                    : !unlocked ? $"{data.UnlockName}을(를) 해금하면 업그레이드할 수 있습니다"
                    : "준비 중";
            }

            RefreshDetail(data, unlocked, level);
        }

        private void RefreshDetail(FacilityData data, bool unlocked, int level)
        {
            SetText(descriptionLabel, data != null ? data.Description : string.Empty);

            if (data == null)
            {
                SetText(selectedName, string.Empty);
                SetText(selectedEffect, string.Empty);
                SetText(conditionLabel, string.Empty);
                SetButton(false, "업그레이드");
                return;
            }

            // 해금 전: 해금이 먼저다.
            if (!unlocked)
            {
                FacilityUpgrade.Result state = FacilityUpgrade.CanUnlock(_kind);
                FacilityKind? missing = FacilityUpgrade.FirstMissingPrerequisite(_kind);

                SetText(selectedName, $"{data.UnlockName} 해금");
                SetText(selectedEffect, data.UnlockDescription
                    + (missing != null ? $"\n선행: {NameOf(missing.Value)} 해금" : string.Empty));
                SetText(conditionLabel, $"필요 골드 {data.UnlockCost:N0} G  (보유 {GoldNow:N0} G)");
                SetButton(state == FacilityUpgrade.Result.Ok, state switch
                {
                    FacilityUpgrade.Result.Ok                  => "해금",
                    FacilityUpgrade.Result.NotEnoughGold       => "골드 부족",
                    FacilityUpgrade.Result.MissingPrerequisite => "선행 필요",
                    _                                          => "설정 오류",
                });
                return;
            }

            if (!data.TryGetStep(_selectedLevel, out FacilityUpgradeStep step))
            {
                SetText(selectedName, $"{data.DisplayName} Lv.{level}");
                SetText(selectedEffect, "업그레이드 단계가 없습니다.");
                SetText(conditionLabel, string.Empty);
                SetButton(false, "업그레이드");
                return;
            }

            SetText(selectedName, $"{data.DisplayName} Lv.{_selectedLevel}");
            SetText(selectedEffect, $"{step.description}\n"
                + (_selectedLevel > 1 && data.TryGetStep(_selectedLevel - 1, out FacilityUpgradeStep prev)
                    ? $"선행: Lv.{_selectedLevel - 1} {prev.description}"
                    : "선행 없음"));
            SetText(conditionLabel, CostText(step));

            if (_selectedLevel <= level)
            {
                SetButton(false, "완료");
                return;
            }
            if (_selectedLevel > level + 1)
            {
                SetButton(false, "선행 필요");
                return;
            }

            FacilityUpgrade.Result up = FacilityUpgrade.CanUpgrade(_kind);
            SetButton(up == FacilityUpgrade.Result.Ok, up switch
            {
                FacilityUpgrade.Result.Ok                => "업그레이드",
                FacilityUpgrade.Result.MaxLevel          => "최대 단계",
                FacilityUpgrade.Result.NotEnoughGold     => "골드 부족",
                FacilityUpgrade.Result.NotEnoughResource => "자원 부족",
                _                                        => "설정 오류",
            });
        }

        private int GoldNow => _wallet != null ? _wallet.Gold : 0;

        // 기호를 안 쓴다 — Pretendard-Bold SDF에 ·, × 가 없다.
        private string CostText(FacilityUpgradeStep step)
        {
            string text = $"필요 골드 {step.goldCost:N0} G  (보유 {GoldNow:N0} G)";
            if (step.resource != null && step.resourceAmount > 0)
            {
                int have = _warehouse != null ? _warehouse.CountOf(step.resource) : 0;
                text += $"\n필요 자원 {step.resource.DisplayName} {step.resourceAmount}  (보유 {have})";
            }
            else text += "\n필요 자원 없음";
            return text;
        }

        private void SetButton(bool interactable, string label)
        {
            if (upgradeButton != null) upgradeButton.interactable = interactable;
            SetText(upgradeButtonLabel, label);
        }

        public static string NameOf(FacilityKind kind)
        {
            FacilityData data = FacilityLevels.Instance != null ? FacilityLevels.Instance.DataOf(kind) : null;
            if (data == null) return kind.ToString();
            return string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
