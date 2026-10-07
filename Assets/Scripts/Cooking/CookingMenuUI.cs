using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public enum CookingType
    {
        Skewer,   // 꼬치 요리
        Stew,     // 스튜 요리 (스튜, 야채볶음 등)
        FishGrill // 생선 구이 (또는 스테이크/해산물 구이) 요리
    }

    [Serializable]
    public struct CookingCategoryGroup
    {
        public CookingType cookingType;
        public List<MenuData> subMenus;
    }

    public class CookingMenuUI : MonoBehaviour
    {
        private enum MenuStep { Category, SubMenu }

        [Header("전체 패널")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Button btnClose;

        [Header("카테고리 패널 (꼬치 / 스튜 / 생선구이)")]
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private Button btnSkewer;
        [SerializeField] private Button btnStew;
        [SerializeField] private Button btnFishGrill;

        [Header("세부 메뉴 패널")]
        [SerializeField] private GameObject subMenuPanel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private MenuCardSlot cardPrefab;
        [SerializeField] private TextMeshProUGUI txtCategoryTitle;
        [SerializeField] private Button btnBackToCategory;
        [SerializeField] private Button btnStartCooking;

        [Header("Skewer 세부 요리별 컨트롤러")]
        [SerializeField] private SkewerMinigameController skewerMinigameController; // 기본 꼬치 컨트롤러
        [SerializeField] private SeafoodSkewersMinigameController seafoodSkewersController; // 해물 꼬치 전용 컨트롤러

        [Header("Stew 세부 요리별 컨트롤러")]
        [SerializeField] private StewMinigameController stewMinigameController;
        [SerializeField] private VeggieStirFryMinigameController veggieStirFryController; // 야채 볶음 전용

        [Header("FishGrill 세부 요리별 컨트롤러")]
        [SerializeField] private FishGrillMinigameController fishGrillController; // 생선 구이 전용
        [SerializeField] private ClamGrillMinigameController clamGrillController; // 조개 구이 전용

        [Header("조리대 연동")]
        [SerializeField] private CookingCounter cookingCounter;

        [Header("데이터 등록")]
        [SerializeField] private List<CookingCategoryGroup> categoryDataList;
        [SerializeField] private MenuDatabase menuDatabase;

        private readonly List<MenuCardSlot> _activeSlots = new();
        private MenuStep _currentStep = MenuStep.Category;
        private CookingType _selectedType;
        private MenuData _selectedMenu;
        private MenuData _cookingMenu;
        private IInteractor _currentActor;

        private void Awake()
        {
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (btnBackToCategory != null) btnBackToCategory.onClick.AddListener(ShowCategoryStep);
            if (btnStartCooking != null) btnStartCooking.onClick.AddListener(StartCooking);

            if (btnSkewer != null) btnSkewer.onClick.AddListener(() => OnSelectCategory(CookingType.Skewer));
            if (btnStew != null) btnStew.onClick.AddListener(() => OnSelectCategory(CookingType.Stew));
            if (btnFishGrill != null) btnFishGrill.onClick.AddListener(() => OnSelectCategory(CookingType.FishGrill));

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            EnsureMinigameControllers();
        }

        private void EnsureMinigameControllers()
        {
            if (skewerMinigameController == null)
            {
                skewerMinigameController = FindFirstObjectByType<SkewerMinigameController>(FindObjectsInactive.Include);
            }

            if (seafoodSkewersController == null)
            {
                seafoodSkewersController = FindFirstObjectByType<SeafoodSkewersMinigameController>(FindObjectsInactive.Include);
            }

            if (stewMinigameController == null)
            {
                stewMinigameController = FindFirstObjectByType<StewMinigameController>(FindObjectsInactive.Include);
            }

            if (veggieStirFryController == null)
            {
                veggieStirFryController = FindFirstObjectByType<VeggieStirFryMinigameController>(FindObjectsInactive.Include);
            }

            if (fishGrillController == null)
            {
                fishGrillController = FindFirstObjectByType<FishGrillMinigameController>(FindObjectsInactive.Include);
            }

            if (clamGrillController == null)
            {
                clamGrillController = FindFirstObjectByType<ClamGrillMinigameController>(FindObjectsInactive.Include);
            }
        }

        private void Update()
        {
            if (rootPanel == null || !rootPanel.activeSelf) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_currentStep == MenuStep.SubMenu)
                {
                    ShowCategoryStep();
                }
                else
                {
                    Close();
                }
            }
        }

        public void Open(IInteractor actor)
        {
            _currentActor = actor;

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            EnsureMinigameControllers();

            // (+10/7, A) 와이어프레임 요리 선택 창(CookingSelectUI)이 있으면 분류 창 대신 그걸 띄운다.
            // 거기서 고른 요리는 StartCookingMenu로 아래 분기를 그대로 탄다. 분류 창은 지우지 않았다.
            CookingSelectUI select = FindAnyObjectByType<CookingSelectUI>(FindObjectsInactive.Include);
            if (select != null)
            {
                if (rootPanel != null) rootPanel.SetActive(false);
                select.Open(this);
                return;
            }

            if (rootPanel != null) rootPanel.SetActive(true);
            ShowCategoryStep();
        }

        /// <summary>
        /// (+10/7, A) 영업이 끝나 정산이 뜰 때 — 요리 선택 창 · 분류 창을 닫고 돌던 미니게임을 버린다.
        /// 미니게임은 결과 콜백 없이 멈춰서 음식이 안 나오고 재료도 안 깎인다. 플레이어는 Close가 풀어 준다.
        /// </summary>
        public void AbortCooking()
        {
            foreach (BaseCookingMinigame game in FindObjectsByType<BaseCookingMinigame>(FindObjectsSortMode.None))
                if (game.IsRunning) game.Abort();

            CookingSelectUI select = FindAnyObjectByType<CookingSelectUI>(FindObjectsInactive.Include);
            if (select != null && select.IsOpen) select.Close();

            _cookingMenu = null;
            Close();
        }

        /// <summary>(+10/7, A) CookingSelectUI에서 고른 요리로 조리를 시작한다 — 기존 [요리 시작]과 같은 길.</summary>
        public void StartCookingMenu(MenuData menu)
        {
            if (menu == null) return;
            _selectedMenu = menu;
            _selectedType = menu.MiniGameId switch
            {
                MiniGameId.Stew => CookingType.Stew,
                MiniGameId.FishGrill => CookingType.FishGrill,
                _ => CookingType.Skewer
            };
            StartCooking();
        }

        public void Close()
        {
            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            if (_currentActor != null)
            {
                _currentActor.EndBusy();
                _currentActor = null;
            }
        }

        private void ShowCategoryStep()
        {
            _currentStep = MenuStep.Category;
            _selectedMenu = null;

            if (categoryPanel != null) categoryPanel.SetActive(true);
            if (subMenuPanel != null) subMenuPanel.SetActive(false);
            if (btnStartCooking != null) btnStartCooking.interactable = false;
        }

        private void OnSelectCategory(CookingType type)
        {
            _selectedType = type;
            _currentStep = MenuStep.SubMenu;

            if (categoryPanel != null) categoryPanel.SetActive(false);
            if (subMenuPanel != null) subMenuPanel.SetActive(true);

            if (txtCategoryTitle != null)
            {
                txtCategoryTitle.text = type switch
                {
                    CookingType.Skewer => "꼬치 요리 선택",
                    CookingType.Stew => "스튜/볶음 요리 선택",
                    CookingType.FishGrill => "구이 요리 선택",
                    _ => "메뉴 선택"
                };
            }

            RefreshSubMenuSlots(type);
        }

        private void RefreshSubMenuSlots(CookingType type)
        {
            foreach (var slot in _activeSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _activeSlots.Clear();
            _selectedMenu = null;

            if (btnStartCooking != null) btnStartCooking.interactable = false;
            if (cardPrefab == null || cardContainer == null) return;

            var targetGroup = categoryDataList?.Find(g => g.cookingType == type);
            List<MenuData> source = targetGroup.HasValue ? targetGroup.Value.subMenus : null;

            if (source == null || source.Count == 0)
            {
                if (menuDatabase != null)
                {
                    var buffer = new List<MenuData>();
                    menuDatabase.CollectActive(ToMiniGameId(type), buffer);
                    source = buffer;
                }
            }

            if (source == null) return;

            foreach (var menu in source)
            {
                if (menu == null || !menu.IsActive) continue;

                var slot = Instantiate(cardPrefab, cardContainer);
                slot.Setup(menu, OnSelectSubMenu);
                _activeSlots.Add(slot);
            }
        }

        private void OnSelectSubMenu(MenuData menu)
        {
            _selectedMenu = menu;

            foreach (var slot in _activeSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(slot.MenuData == menu);
                    slot.UpdateRecipeDisplay();
                }
            }

            if (btnStartCooking != null)
            {
                bool hasIngredients = (Warehouse.Instance == null || Warehouse.Instance.Has(_selectedMenu.Recipe));

                btnStartCooking.interactable = (_selectedMenu != null && hasIngredients);
            }
        }

        private void StartCooking()
        {
            if (_selectedMenu == null) return;

            if (cookingCounter != null && cookingCounter.IsFull)
            {
                Debug.LogWarning("[CookingMenuUI] 조리대가 가득 차서 더 이상 요리할 수 없습니다.");
                return;
            }

            if (Warehouse.Instance != null && !Warehouse.Instance.Has(_selectedMenu.Recipe))
            {
                Debug.LogWarning($"[CookingMenuUI] 창고에 {_selectedMenu.DisplayName}에 필요한 재료가 부족합니다.");
                return;
            }

            WarnOnMiniGameMismatch(_selectedMenu, _selectedType);

            _cookingMenu = _selectedMenu;

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            EnsureMinigameControllers();

            switch (_selectedMenu.MiniGameId)
            {
                case MiniGameId.Skewer:
                    // Skewer 카테고리 내부에서 SkewerSubtype 기준 세부 미니게임 분기
                    DispatchSkewerMenu(_selectedMenu);
                    break;

                case MiniGameId.Stew:
                    // Stew 카테고리 내부에서 StewSubtype 기준 세부 미니게임 분기
                    DispatchStewMenu(_selectedMenu);
                    break;

                case MiniGameId.FishGrill:
                    // FishGrill 카테고리 내부에서 FishGrillSubtype 기준 세부 미니게임 분기
                    DispatchFishGrillMenu(_selectedMenu);
                    break;

                case MiniGameId.None:
                default:
                    Debug.LogError($"[CookingMenuUI] '{_selectedMenu.DisplayName}'에 유효한 MiniGameId가 없습니다.");
                    Close();
                    break;
            }
        }

        /// <summary>
        /// MenuData.SkewerSubtype Enum 값을 대조하여 기본 꼬치 / 해물 꼬치 미니게임 컨트롤러 분기
        /// </summary>
        private void DispatchSkewerMenu(MenuData menu)
        {
            if (menu == null) return;

            switch (menu.SkewerSubtype)
            {
                case SkewerSubtype.SeafoodSkewers:
                    if (seafoodSkewersController != null)
                    {
                        seafoodSkewersController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] SeafoodSkewersMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case SkewerSubtype.HawaiianSkewers:
                case SkewerSubtype.None:
                default:
                    if (skewerMinigameController != null)
                    {
                        skewerMinigameController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] SkewerMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;
            }
        }

        /// <summary>
        /// MenuData.StewSubtype Enum 값을 대조하여 기본 스튜 / 야채 볶음 미니게임 컨트롤러 분기
        /// </summary>
        private void DispatchStewMenu(MenuData menu)
        {
            if (menu == null) return;

            switch (menu.StewSubtype)
            {
                case StewSubtype.VeggieStirFry:
                    if (veggieStirFryController != null)
                    {
                        veggieStirFryController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] VeggieStirFryMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case StewSubtype.SeafoodStew:
                case StewSubtype.None:
                default:
                    if (stewMinigameController != null)
                    {
                        stewMinigameController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] StewMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;
            }
        }

        /// <summary>
        /// MenuData.FishGrillSubtype Enum 값을 대조하여 고등어 / 조개 미니게임 컨트롤러 분기
        /// </summary>
        private void DispatchFishGrillMenu(MenuData menu)
        {
            if (menu == null) return;

            switch (menu.FishGrillSubtype)
            {
                case FishGrillSubtype.Clam:
                    if (clamGrillController != null)
                    {
                        clamGrillController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] ClamGrillMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case FishGrillSubtype.fish:
                case FishGrillSubtype.None:
                default:
                    if (fishGrillController != null)
                    {
                        fishGrillController.StartMinigame(menu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] fishGrillController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;
            }
        }

        private static void WarnOnMiniGameMismatch(MenuData menu, CookingType selectedType)
        {
            if (menu == null) return;

            MiniGameId expected = ToMiniGameId(selectedType);
            if (menu.MiniGameId == expected) return;

            Debug.LogWarning($"[CookingMenuUI] UI 카테고리({selectedType})와 '{menu.DisplayName}'의 MiniGameId({menu.MiniGameId})가 일치하지 않습니다.");
        }

        private static MiniGameId ToMiniGameId(CookingType type) => type switch
        {
            CookingType.Skewer => MiniGameId.Skewer,
            CookingType.Stew => MiniGameId.Stew,
            CookingType.FishGrill => MiniGameId.FishGrill,
            _ => MiniGameId.None
        };

        private void OnMinigameFinished(CookingResult result)
        {
            Debug.Log($"[CookingMenuUI] 요리 완료 결과: 성공여부={result.isSuccess}, 최종가격={result.finalPrice}G, 판정={result.bestGrade}");

            // (+10/2, A) 공통 완성 팝업 — 만든 요리 · 이름 · 랭크 · 판매가.
            DishRevealUI.TryShow(result, result.menuData != null ? result.menuData : _cookingMenu);

            if (result.isSuccess)
            {
                MenuData completedMenu = result.menuData != null ? result.menuData : _cookingMenu;
                if (completedMenu != null && Warehouse.Instance != null)
                {
                    bool consumed = Warehouse.Instance.Consume(completedMenu.Recipe);
                    if (consumed)
                    {
                        Debug.Log($"[CookingMenuUI] '{completedMenu.DisplayName}' 조리 완료로 창고 재료를 차감했습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"[CookingMenuUI] '{completedMenu.DisplayName}' 조리 완료 후 재료 차감에 실패했습니다.");
                    }
                }

                if (cookingCounter != null)
                {
                    cookingCounter.TryPlaceFood(result);
                }
                else
                {
                    PlayerServingController playerServing = FindFirstObjectByType<PlayerServingController>();
                    if (playerServing != null)
                    {
                        playerServing.PickUpFood(result);
                    }
                }
            }

            _cookingMenu = null;
            Close();
        }
    }
}
