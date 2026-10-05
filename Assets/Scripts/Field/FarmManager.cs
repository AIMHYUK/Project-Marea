using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 농사 데크의 밭 전부를 관리한다 — 심기 · 성장 · 수확. (+9/28, 이슈 72)
    ///
    /// 밭 칸(FarmPlotCell)은 클릭을 받아 여기로 넘기고, 여기가 시키는 대로 모습만 바꾼다.
    /// 로직이 칸마다 흩어지면 "씨앗값을 어디서 빼나"가 칸 수만큼 생긴다.
    ///
    /// 밭 상태는 여기 든다(설계 결정 3). 칸이 꺼져 있어도(데크 레벨이 낮아 아직 안 열린 칸)
    /// 상태는 남는다 — 지금은 꺼진 칸에 심을 방법이 없어서 늘 빈 밭이다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 전부 빈 밭이다 (보류).
    /// </summary>
    public class FarmManager : MonoBehaviour
    {
        public enum PlotState { Empty, Growing, Ready }

        public enum PlantResult { Ok, NotEmpty, NotEnoughGold, Misconfigured, Locked }   // (+10/2) Locked: 고급 작물 해금 전

        [Tooltip("이 데크에서 심을 수 있는 작물. 작물 선택 창에 이 순서대로 뜬다.")]
        [SerializeField] private CropData[] crops;

        [Tooltip("관리할 밭 칸. 비워두면 자식에서 전부 찾는다.")]
        [SerializeField] private FarmPlotCell[] plots;

        [Header("연출 (+10/6, 이슈 117) — 기획 「작물 수확」 VFX_10 / VFX_02")]
        [SerializeField] private VfxId harvestDustVfx = VfxId.Dust;
        [SerializeField] private VfxId harvestSparkleVfx = VfxId.Sparkle;

        private sealed class Plot
        {
            public PlotState State;
            public CropData Crop;
            public float PlantedAt;
            public float GrowSeconds;   // (+10/2) 심을 때의 업그레이드로 정한다 — 자라는 중에 레벨이 올라도 이 밭은 그대로
        }

        // ── (+10/2) 수급 시설 업그레이드 효과 (기획 업그레이드 테이블) ──

        /// <summary>업그레이드가 반영된 성장 시간. 작물 성장 시간 감소(CROP_GROW_TIME_REDUCE).</summary>
        public float GrowSecondsOf(CropData crop)
        {
            if (crop == null) return 0f;
            float reduce = FacilityLevels.Instance != null
                ? FacilityLevels.Instance.EffectValue(FacilityKind.Farm, FacilityEffectType.CropGrowTimeReduce)
                : 0f;
            return crop.GrowSeconds * Mathf.Clamp01(1f - reduce);
        }

        /// <summary>
        /// 고급(RARE 이상) 작물은 CROP_GRADE_UNLOCK RARE 전까지 못 심는다. 잠겼으면 열리는 레벨을 준다.
        /// 수확물 등급으로 본다 — 작물 자체엔 등급 칸이 없다 (토마토 · 파인애플이 RARE).
        /// </summary>
        public bool IsCropUnlocked(CropData crop, out int requiredLevel)
        {
            requiredLevel = 0;
            if (crop == null || crop.Harvest == null || crop.Harvest.Grade < ItemGrade.Rare) return true;
            return FacilityLevels.Instance == null
                || FacilityLevels.Instance.IsTargetUnlocked(FacilityKind.Farm, FacilityEffectType.CropGradeUnlock, "RARE", out requiredLevel);
        }

        /// <summary>수확량 증가(HARVEST_BONUS) — 확률(없으면 항상)로 값만큼 더한다.</summary>
        private int HarvestBonus()
        {
            if (FacilityLevels.Instance == null
                || !FacilityLevels.Instance.TryGetEffect(FacilityKind.Farm, FacilityEffectType.HarvestBonus, out FacilityUpgradeStep step))
                return 0;
            bool hit = step.effectChance <= 0f || Random.value < step.effectChance;
            return hit ? Mathf.RoundToInt(step.effectValue) : 0;
        }

        private readonly Dictionary<FarmPlotCell, Plot> _plots = new();
        private SeedSelectUI _ui;
        private WorldLabelUI _labels;

        public IReadOnlyList<CropData> Crops => crops;

        private void Awake()
        {
            if (plots == null || plots.Length == 0) plots = GetComponentsInChildren<FarmPlotCell>(true);
            _ui = FindAnyObjectByType<SeedSelectUI>(FindObjectsInactive.Include);
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);

            if (crops == null || crops.Length == 0)
                Debug.LogError($"{name}: FarmManager.crops가 비어 있다. 빈 밭을 눌러도 심을 게 없다.", this);
            if (plots.Length == 0)
                Debug.LogError($"{name}: 밭 칸(FarmPlotCell)이 하나도 없다.", this);
            if (_ui == null)
                Debug.LogError($"{name}: 씬에 SeedSelectUI가 없다. 빈 밭을 눌러도 작물 선택 창이 안 뜬다.", this);
            if (_labels == null)
                Debug.LogError($"{name}: 씬에 WorldLabelUI가 없다. 밭의 남은 시간이 안 뜬다.", this);

            foreach (FarmPlotCell cell in plots)
            {
                if (cell == null) continue;
                _plots[cell] = new Plot();
                cell.ShowEmpty();
            }
        }

        private void Update()
        {
            foreach (var pair in _plots)
            {
                Plot plot = pair.Value;
                if (plot.State != PlotState.Growing) continue;

                float t = (Time.time - plot.PlantedAt) / plot.GrowSeconds;
                if (t < 1f)
                {
                    pair.Key.ShowGrowing(t);

                    // (+9/28) 남은 시간. 성장 중인 동안만 부른다 — 다 자라면 수확 표식이 대신한다.
                    if (_labels != null)
                        _labels.Set(pair.Key, pair.Key.transform.position + Vector3.up * 1.2f,
                                    TimeText.Format(plot.PlantedAt + plot.GrowSeconds - Time.time));
                    continue;
                }

                plot.State = PlotState.Ready;
                pair.Key.ShowReady(plot.Crop.Harvest != null ? plot.Crop.Harvest.Icon : null);
            }
        }

        public PlotState StateOf(FarmPlotCell cell)
            => _plots.TryGetValue(cell, out Plot plot) ? plot.State : PlotState.Empty;

        /// <summary>성장 중인 밭은 클릭을 안 받는다 (기획 9 밭 상태 관리 4번).</summary>
        public bool CanInteract(FarmPlotCell cell)
            => _plots.ContainsKey(cell) && StateOf(cell) != PlotState.Growing;

        /// <summary>플레이어가 밭에 도착했다. 빈 밭이면 작물 선택 창, 다 자랐으면 수확.</summary>
        public void Interact(FarmPlotCell cell)
        {
            switch (StateOf(cell))
            {
                case PlotState.Empty:
                    if (_ui != null) _ui.Open(this, cell);
                    break;
                case PlotState.Ready:
                    Harvest(cell);
                    break;
            }
        }

        /// <summary>지금 이 작물을 심을 수 있나. 아무것도 안 바꾼다 — 창이 버튼을 그릴 때 부른다.</summary>
        public PlantResult CanPlant(FarmPlotCell cell, CropData crop)
        {
            if (crop == null || Wallet.Instance == null) return PlantResult.Misconfigured;
            if (StateOf(cell) != PlotState.Empty) return PlantResult.NotEmpty;
            if (!IsCropUnlocked(crop, out _)) return PlantResult.Locked;
            if (Wallet.Instance.Gold < crop.SeedPrice) return PlantResult.NotEnoughGold;
            return PlantResult.Ok;
        }

        /// <summary>씨앗값을 빼고 심는다. Ok가 아니면 골드도 밭도 안 건드린다.</summary>
        public PlantResult TryPlant(FarmPlotCell cell, CropData crop)
        {
            PlantResult check = CanPlant(cell, crop);
            if (check != PlantResult.Ok) return check;
            if (!Wallet.Instance.TrySpend(crop.SeedPrice)) return PlantResult.NotEnoughGold;

            Plot plot = _plots[cell];
            plot.State = PlotState.Growing;
            plot.Crop = crop;
            plot.PlantedAt = Time.time;
            plot.GrowSeconds = Mathf.Max(0.1f, GrowSecondsOf(crop));
            cell.Plant(crop.StagePrefabs);   // (+9/30) 단계 모델을 한 번에 만들어 둔다
            cell.ShowGrowing(0f);
            return PlantResult.Ok;
        }

        private void Harvest(FarmPlotCell cell)
        {
            Plot plot = _plots[cell];

            // 창고가 없으면 수확물이 사라진다. 밭을 비우기 전에 멈춘다 — 비운 뒤에 알면 되돌릴 게 없다.
            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 수확물이 갈 곳이 없어 밭을 그대로 둔다.", this);
                return;
            }
            if (plot.Crop.Harvest == null)
            {
                Debug.LogError($"{name}: '{plot.Crop.name}'의 harvest 재료가 비어 있다.", plot.Crop);
                return;
            }

            warehouse.Add(plot.Crop.Harvest, plot.Crop.HarvestCount + HarvestBonus());

            // (+10/6) 흙 조각 + 획득 반짝임
            Vector3 at = cell.transform.position;
            Vfx.Play(harvestDustVfx, at, 0.6f);
            Vfx.Play(harvestSparkleVfx, at + Vector3.up * 0.6f, 0.5f);

            plot.State = PlotState.Empty;
            plot.Crop = null;
            cell.ShowEmpty();
        }
    }
}
