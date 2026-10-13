using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Data;
using Marea.Economy;
using Marea.Field;
using Marea.Hud;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// (+10/13) 연출(시연 영상)용 씬 전용 — 플레이를 누르면 끝판 상태로 시작한다. Main_Showcase에만 둔다.
    ///
    /// Awake: 시설을 전부 해금하고 최대 레벨로. 다른 시스템이 Start에서 지금 레벨을 읽어 모습을 까니,
    /// 그 전에 올려야 데크 · 직원이 해금 연출(카메라 이동) 없이 처음부터 지어진 채로 나온다.
    /// 그래서 FacilityLevels(기본 순서 0)의 Awake 다음, 모든 Start 전에 돈다.
    ///
    /// Start 다음 프레임: 밭 칸을 전부 켜고(수급 시설 최대로도 3칸이라 ShowFullyBuilt로 8칸) 작물을 돌아가며 심고,
    /// 탐사정을 가장 깊은 해역으로 보내고, 추가 직원을 켜고, 도움말 유도 배지를 끈다.
    ///
    /// (+10/13) 영업도 바로 연다 — 빈자리가 keepEmptyForBoats보다 많으면 입구에서 손님을 바로 들여 채우고(배는 한 척씩,
    /// 내린 손님이 다 떠나야 다음 배라 배만으론 좌석이 안 찬다), 기다리는 손님마다 주문 메뉴를 조리대에 자동으로 올려
    /// 직원이 나르게 한다. 선장(IsPriority)은 직접 가져다줘야 하는 손님이라 뺀다.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShowcaseSetup : MonoBehaviour
    {
        [Tooltip("보낼 해역. 비우면 탐사정 목록의 마지막(가장 깊은) 해역.")]
        [SerializeField] private ExpeditionAreaData expeditionArea;
        [Tooltip("StaffHire가 켜는 직원 말고 더 켤 직원들. 씬에 꺼진 채로 둔다.")]
        [SerializeField] private GameObject[] extraStaff;
        [Tooltip("밭에 돌아가며 심을 작물. 비우면 FarmManager의 작물 목록 전부.")]
        [SerializeField] private CropData[] plantOrder;

        [Header("영업 (+10/13)")]
        [SerializeField] private bool openBusiness = true;
        [Tooltip("이만큼은 배 손님 몫으로 비워 두고, 그보다 많이 비면 입구에서 손님을 바로 들인다.")]
        [SerializeField, Min(0)] private int keepEmptyForBoats = 5;
        [Tooltip("주문한 손님마다 조리대에 음식을 자동으로 올린다.")]
        [SerializeField] private bool autoCook = true;
        [SerializeField, Min(0.2f)] private float tickSeconds = 1.5f;
        [Tooltip("자동 요리가 S랭크(Perfect)로 나올 확률. 나머지는 A랭크(Good).")]
        [SerializeField, Range(0f, 1f)] private float perfectChance = 0.5f;

        private readonly HashSet<CustomerController> _cookedFor = new();

        private void Awake()
        {
            FacilityLevels levels = FacilityLevels.Instance;
            if (levels == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. 연출용 끝판 상태를 만들 수 없다.", this);
                return;
            }
            foreach (FacilityKind kind in System.Enum.GetValues(typeof(FacilityKind)))
            {
                if (levels.DataOf(kind) == null) continue;
                levels.TryUnlock(kind);
                while (levels.TryRaiseLevel(kind)) { }
            }
        }

        private IEnumerator Start()
        {
            yield return null;   // FacilitySite · FarmManager · ExpeditionManager가 Start에서 지금 모습을 깐 뒤에 덮는다

            FarmManager farm = FindAnyObjectByType<FarmManager>();
            foreach (FacilitySite site in FindObjectsByType<FacilitySite>(FindObjectsSortMode.None))
                if (site.Kind == FacilityKind.Farm) site.ShowFullyBuilt();
            if (farm == null) Debug.LogError($"{name}: 씬에 FarmManager가 없다. 밭을 못 채운다.", this);
            else
            {
                IReadOnlyList<CropData> crops = plantOrder != null && plantOrder.Length > 0 ? plantOrder : farm.Crops;
                int i = 0;
                foreach (FarmPlotCell cell in FindObjectsByType<FarmPlotCell>(FindObjectsSortMode.InstanceID))
                {
                    if (crops == null || crops.Count == 0) break;
                    FarmManager.PlantResult r = farm.TryPlant(cell, crops[i++ % crops.Count]);
                    if (r != FarmManager.PlantResult.Ok) Debug.LogWarning($"{name}: {cell.name}에 못 심었다 — {r}", cell);
                }
            }

            ExpeditionManager expedition = FindAnyObjectByType<ExpeditionManager>();
            if (expedition == null) Debug.LogError($"{name}: 씬에 ExpeditionManager가 없다. 탐사정을 못 보낸다.", this);
            else
            {
                ExpeditionAreaData area = expeditionArea != null ? expeditionArea
                    : expedition.Areas != null && expedition.Areas.Length > 0 ? expedition.Areas[expedition.Areas.Length - 1] : null;
                if (!expedition.TryDispatch(area))
                    Debug.LogError($"{name}: 탐사정을 '{(area != null ? area.name : "?")}'로 못 보냈다(상태 {expedition.Current}).", this);
            }

            if (extraStaff != null)
                foreach (GameObject s in extraStaff)
                    if (s != null) s.SetActive(true);

            // 도움말을 한 번 연 것으로 친다 — 첫 게임 유도 배지(MainHud)가 연출 화면에 안 뜨게.
            HelpWindow help = FindAnyObjectByType<HelpWindow>(FindObjectsInactive.Include);
            if (help != null) { help.Open(); help.Close(); }

            if (!openBusiness) yield break;
            BusinessManager business = BusinessManager.Instance;
            if (business == null) { Debug.LogError($"{name}: BusinessManager가 없다. 영업을 못 연다.", this); yield break; }
            business.StartBusiness();

            CustomerManager customers = FindAnyObjectByType<CustomerManager>();
            CookingCounter counter = FindAnyObjectByType<CookingCounter>();
            if (customers == null || counter == null)
                Debug.LogError($"{name}: CustomerManager · CookingCounter 중 없는 게 있다. 손님 채우기 · 자동 요리를 못 한다.", this);
            while (customers != null && counter != null)
            {
                yield return new WaitForSeconds(tickSeconds);
                if (business.CurrentState != BusinessState.Open) continue;
                if (customers.EmptySeatCount > keepEmptyForBoats) customers.SpawnNow();
                if (autoCook) CookOne(customers, counter);
                KeepExpeditionAway();
            }
        }

        // 탐사정이 돌아오면 바로 다시 보낸다 — 녹화 내내 깊은 바다에 나가 있게.
        private void KeepExpeditionAway()
        {
            ExpeditionManager expedition = FindAnyObjectByType<ExpeditionManager>();
            if (expedition == null || expedition.Current == ExpeditionManager.State.Away) return;
            if (expedition.Current == ExpeditionManager.State.Returned) expedition.Collect();
            ExpeditionAreaData area = expeditionArea != null ? expeditionArea
                : expedition.Areas != null && expedition.Areas.Length > 0 ? expedition.Areas[expedition.Areas.Length - 1] : null;
            expedition.TryDispatch(area);
        }

        // 아직 음식을 안 올린 손님 하나에게 주문 메뉴를 올린다. 조리대가 꽉 차면 다음 틱에.
        private void CookOne(CustomerManager customers, CookingCounter counter)
        {
            _cookedFor.RemoveWhere(c => c == null);
            if (counter.IsFull) return;
            foreach (CustomerController c in customers.GetWaitingCustomers())
            {
                if (c.IsPriority || c.OrderedMenu == null || _cookedFor.Contains(c)) continue;
                var food = new CookingResult
                {
                    isSuccess = true,
                    menuData = c.OrderedMenu,
                    bestGrade = Random.value < perfectChance ? HitGrade.Perfect : HitGrade.Good,
                    finalPrice = c.OrderedMenu.BasePrice,
                };
                if (counter.TryPlaceFood(food)) _cookedFor.Add(c);
                return;
            }
        }
    }
}
