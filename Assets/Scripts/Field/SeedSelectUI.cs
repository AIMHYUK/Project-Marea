using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 빈 밭을 클릭하면 뜨는 작물 선택 창. (+9/28, 이슈 72)
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「작물 화면」) 한 번 누르면 심던 줄 목록에서,
    /// 아래쪽 카드 패널 + 오른쪽 상세 + [심기] 두 단계로 바뀌었다.
    /// 골드가 모자라면 [심기]가 꺼지고 "골드 부족"으로 바뀐다. 성장 중인 밭에서는 FarmManager가 열지 않는다.
    ///
    /// 창은 밭을 모른다 — 어느 밭에 심을지는 FarmManager가 Open으로 넘겨준다.
    /// 논블로킹이다 (업그레이드 목록과 같은 판단).
    /// </summary>
    public class SeedSelectUI : UiPanel
    {
        [Header("카드")]
        [Tooltip("카드가 쌓일 곳. HorizontalLayoutGroup이 붙어 있어야 한다.")]
        [SerializeField] private Transform cardParent;
        [SerializeField] private CropCard cardPrefab;

        [Header("선택한 작물")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TextMeshProUGUI detailName;
        [SerializeField] private TextMeshProUGUI detailInfo;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private Button plantButton;
        [SerializeField] private TextMeshProUGUI plantButtonLabel;

        private readonly List<CropCard> _cards = new();
        private FarmManager _manager;
        private FarmPlotCell _cell;
        private CropData _selected;
        private Wallet _wallet;

        protected override void Awake()
        {
            base.Awake();
            if (cardParent == null || cardPrefab == null)
                Debug.LogError($"{name}: SeedSelectUI의 cardParent·cardPrefab 중 비어 있는 게 있다.", this);
            if (plantButton == null)
                Debug.LogError($"{name}: SeedSelectUI.plantButton이 비어 있다. 심을 수 없다.", this);
            else
                plantButton.onClick.AddListener(HandlePlant);
        }

        protected override void Start()
        {
            base.Start();
            _wallet = Wallet.Instance;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        public void Open(FarmManager manager, FarmPlotCell cell)
        {
            if (cardParent == null || cardPrefab == null) return;

            _manager = manager;
            _cell = cell;

            IReadOnlyList<CropData> crops = _manager.Crops;
            _selected = crops != null && crops.Count > 0 ? crops[0] : null;

            Open();
            Rebuild();
        }

        protected override void OnClosed()
        {
            _manager = null;
            _cell = null;
            _selected = null;
        }

        private void HandleGoldChanged(int gold)
        {
            if (IsOpen) RefreshDetail();
        }

        /// <summary>작물 수만큼 카드를 맞춘다. 데크마다 작물 목록이 다를 수 있어 열 때마다 맞춘다.</summary>
        private void Rebuild()
        {
            if (_manager == null) return;
            IReadOnlyList<CropData> crops = _manager.Crops;
            int count = crops?.Count ?? 0;

            while (_cards.Count < count) _cards.Add(Instantiate(cardPrefab, cardParent));
            for (int i = 0; i < _cards.Count; i++) _cards[i].gameObject.SetActive(i < count);
            for (int i = 0; i < count; i++) _cards[i].Bind(crops[i], Select);

            RefreshDetail();
        }

        private void Select(CropData crop)
        {
            _selected = crop;
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            foreach (CropCard card in _cards)
                card.SetSelected(card.gameObject.activeSelf && card.Crop == _selected);

            CropCard.SetIcon(detailIcon, _selected);
            if (detailName != null) detailName.text = _selected != null ? _selected.DisplayName : string.Empty;
            if (detailInfo != null)
                detailInfo.text = _selected != null ? $"가격 {_selected.SeedPrice:N0} G\n성장 시간 {_selected.GrowSeconds:0}초" : string.Empty;
            if (detailDescription != null)
                detailDescription.text = _selected != null && _selected.Harvest != null ? _selected.Harvest.Description : string.Empty;

            if (plantButton == null) return;
            FarmManager.PlantResult state = _manager != null && _selected != null
                ? _manager.CanPlant(_cell, _selected)
                : FarmManager.PlantResult.Misconfigured;

            plantButton.interactable = state == FarmManager.PlantResult.Ok;
            if (plantButtonLabel != null)
                plantButtonLabel.text = state == FarmManager.PlantResult.NotEnoughGold ? "골드 부족" : "심기";
        }

        private void HandlePlant()
        {
            if (_manager == null || _selected == null) return;
            if (_manager.TryPlant(_cell, _selected) == FarmManager.PlantResult.Ok) Close();
            else RefreshDetail();
        }
    }
}
