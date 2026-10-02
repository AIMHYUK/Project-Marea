using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 오늘의 메뉴 창. 판매할 메뉴를 고르고 [영업 시작]. (+10/2, 이슈 92 — 와이어프레임 「오늘의 메뉴」)
    ///
    /// 진입: 간판(RestaurantSign, A)을 누르면 열린다. B의 화면 [영업 시작] 버튼은 아직 바로 영업을 연다 —
    /// 그쪽이 이 창의 <see cref="UiPanel.Open"/>을 부르도록 바꾸는 건 B와 맞출 일이다.
    ///
    /// [영업 시작]은 고른 목록을 확정(TodayMenu.Confirm)한 뒤 B의 BusinessManager.StartBusiness를 부른다.
    /// 영업 상태는 B가 든다 — 여기서 들고 있지 않는다 (RestaurantSign과 같은 판단).
    /// 예상 수익은 기획 미확정이라 넣지 않는다.
    /// </summary>
    public class TodayMenuUI : UiPanel
    {
        [Header("카드")]
        [SerializeField] private Transform cardParent;
        [SerializeField] private MenuCard cardPrefab;

        [Header("표시")]
        [SerializeField] private TextMeshProUGUI goldLabel;
        [SerializeField] private TextMeshProUGUI countLabel;

        [Header("영업 시작")]
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI startButtonLabel;

        private readonly List<MenuCard> _cards = new();
        private TodayMenu _menu;
        private Wallet _wallet;

        protected override void Awake()
        {
            base.Awake();
            if (cardParent == null || cardPrefab == null)
                Debug.LogError($"{name}: TodayMenuUI의 cardParent·cardPrefab 중 비어 있는 게 있다.", this);
            if (startButton == null)
                Debug.LogError($"{name}: TodayMenuUI.startButton이 비어 있다. 영업을 시작할 수 없다.", this);
            else
                startButton.onClick.AddListener(HandleStart);
        }

        protected override void Start()
        {
            base.Start();
            _menu = TodayMenu.Instance;
            _wallet = Wallet.Instance;
            if (_menu == null)
                Debug.LogError($"{name}: 씬에 TodayMenu가 없다. 메뉴를 고를 수 없다.", this);
            else
                _menu.OnSelectionChanged += Refresh;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            if (_menu != null) _menu.OnSelectionChanged -= Refresh;
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        protected override void OnOpened() => Rebuild();

        private void HandleGoldChanged(int gold)
        {
            if (IsOpen) Refresh();
        }

        /// <summary>후보 수만큼 카드를 맞춘다. 열 때마다 — 그 사이 재고가 바뀌었을 수 있다.</summary>
        private void Rebuild()
        {
            if (_menu == null || cardParent == null || cardPrefab == null) return;
            IReadOnlyList<MenuData> candidates = _menu.Candidates;

            while (_cards.Count < candidates.Count) _cards.Add(Instantiate(cardPrefab, cardParent));
            for (int i = 0; i < _cards.Count; i++) _cards[i].gameObject.SetActive(i < candidates.Count);
            for (int i = 0; i < candidates.Count; i++) _cards[i].Bind(candidates[i], HandleCardClicked);

            Refresh();
        }

        private void HandleCardClicked(MenuData menu)
        {
            // 실패(한도 · 재료 부족 · 영업 중)는 조용하다 — 카드에 이미 이유가 보인다.
            if (_menu != null) _menu.Toggle(menu);
        }

        private void Refresh()
        {
            if (_menu == null) return;

            foreach (MenuCard card in _cards)
                if (card.gameObject.activeSelf)
                    card.Refresh(_menu.IsSelected(card.Menu), TodayMenu.ServingsAvailable(card.Menu));

            if (goldLabel != null && _wallet != null) goldLabel.text = $"보유 골드 {_wallet.Gold:N0} G";
            if (countLabel != null) countLabel.text = $"판매할 메뉴 선택  {_menu.Selected.Count} / {_menu.MaxCount}";

            bool canStart = _menu.CanEdit && _menu.Selected.Count > 0;
            if (startButton != null) startButton.interactable = canStart;
            if (startButtonLabel != null)
                startButtonLabel.text = !_menu.CanEdit ? "영업 중" : _menu.Selected.Count == 0 ? "메뉴를 고르세요" : "영업 시작";
        }

        private void HandleStart()
        {
            if (_menu == null || !_menu.Confirm()) return;

            BusinessManager business = BusinessManager.Instance;
            if (business == null)
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 메뉴는 확정했지만 영업을 시작할 수 없다.", this);
            else
                business.StartBusiness();

            Close();
        }
    }
}
