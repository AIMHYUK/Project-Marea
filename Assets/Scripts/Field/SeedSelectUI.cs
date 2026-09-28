using System.Collections.Generic;
using Marea.Data;
using Marea.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 빈 밭을 클릭하면 뜨는 작물 선택 창. 작물마다 이름 · 씨앗 가격 · 성장 시간 · 심기. (+9/28, 이슈 72)
    ///
    /// 창은 밭을 모른다 — 어느 밭에 심을지는 FarmManager가 Open으로 넘겨준다.
    /// 논블로킹이다 (업그레이드 목록과 같은 판단).
    /// </summary>
    public class SeedSelectUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("줄이 쌓일 곳. VerticalLayoutGroup이 붙어 있어야 한다.")]
        [SerializeField] private Transform rowParent;
        [SerializeField] private ChoiceRow rowPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<ChoiceRow> _rows = new();
        private FarmManager _manager;
        private FarmPlotCell _cell;
        private Wallet _wallet;

        private void Awake()
        {
            if (panel == null || rowParent == null || rowPrefab == null)
                Debug.LogError($"{name}: SeedSelectUI의 panel·rowParent·rowPrefab 중 비어 있는 게 있다.", this);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            _wallet = Wallet.Instance;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        public void Open(FarmManager manager, FarmPlotCell cell)
        {
            if (panel == null || rowParent == null || rowPrefab == null) return;

            _manager = manager;
            _cell = cell;
            panel.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            _manager = null;
            _cell = null;
        }

        private void HandleGoldChanged(int gold)
        {
            if (panel != null && panel.activeSelf) Rebuild();
        }

        /// <summary>작물 수만큼 줄을 맞추고 다시 그린다. 데크마다 작물 목록이 다를 수 있어 매번 맞춘다.</summary>
        private void Rebuild()
        {
            if (_manager == null) return;
            IReadOnlyList<CropData> crops = _manager.Crops;
            int count = crops?.Count ?? 0;

            while (_rows.Count < count) _rows.Add(Instantiate(rowPrefab, rowParent));
            for (int i = 0; i < _rows.Count; i++) _rows[i].gameObject.SetActive(i < count);

            for (int i = 0; i < count; i++)
            {
                CropData crop = crops[i];
                FarmManager.PlantResult state = _manager.CanPlant(_cell, crop);

                _rows[i].Set(
                    crop.DisplayName,
                    $"{crop.SeedPrice:N0} G   {crop.GrowSeconds:0}초   수확 {crop.HarvestCount}개",
                    state == FarmManager.PlantResult.NotEnoughGold ? "골드 부족" : "심기",
                    state == FarmManager.PlantResult.Ok,
                    () => HandlePlant(crop));
            }
        }

        private void HandlePlant(CropData crop)
        {
            if (_manager == null) return;
            if (_manager.TryPlant(_cell, crop) == FarmManager.PlantResult.Ok) Close();
            else Rebuild();
        }
    }
}
