using System;
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
        Stew,     // 스튜 요리
        FishGrill // 생선 구이 (또는 스테이크) 요리
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

        [Header("1단계: 카테고리 패널 (꼬치 / 스튜 / 생선구이)")]
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private Button btnSkewer;
        [SerializeField] private Button btnStew;
        [SerializeField] private Button btnFishGrill;

        [Header("2단계: 세부 메뉴 패널")]
        [SerializeField] private GameObject subMenuPanel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private MenuCardSlot cardPrefab;
        [SerializeField] private TextMeshProUGUI txtCategoryTitle;
        [SerializeField] private Button btnBackToCategory;
        [SerializeField] private Button btnStartCooking;

        [Header("미니게임 연동")]
        [SerializeField] private SkewerMinigameController skewerMinigameController;
        [SerializeField] private StewMinigameController stewMinigameController;
        [SerializeField] private FishGrillMinigameController fishGrillMinigameController;

        [Header("조리대 연동")]
        [SerializeField] private CookingCounter cookingCounter;

        [Header("데이터 등록")]
        [SerializeField] private List<CookingCategoryGroup> categoryDataList;
        [SerializeField] private MenuDatabase menuDatabase; // MenuDatabase 필드 추가

        private readonly List<MenuCardSlot> _activeSlots = new();
        private MenuStep _currentStep = MenuStep.Category;
        private CookingType _selectedType;
        private MenuData _selectedMenu;
        private MenuData _cookingMenu; // 진행 중인 미니게임의 메뉴 캐싱
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

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            ShowCategoryStep();
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
                    CookingType.Stew => "스튜 요리 선택",
                    CookingType.FishGrill => "생선 구이 선택",
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

            // 1순위: 인스펙터 categoryDataList 조회
            var targetGroup = categoryDataList?.Find(g => g.cookingType == type);
            List<MenuData> source = targetGroup.HasValue ? targetGroup.Value.subMenus : null;

            // 2순위: 그룹이 비었으면 MenuDatabase에서 CollectActive로 수집
            if (source == null || source.Count == 0)
            {
                Debug.LogWarning($"[CookingMenuUI] categoryDataList에 {type} 메뉴가 없다. "
                               + "MenuDatabase에서 IsActive/MiniGameId로 채운다.");

                if (menuDatabase != null)
                {
                    var buffer = new List<MenuData>();
                    menuDatabase.CollectActive(ToMiniGameId(type), buffer);
                    source = buffer;
                }
            }

            if (source == null) return;

            // 슬롯 생성 및 IsActive 필터링 적용
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

            // 창고 재료 보유량 사전 검증
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

            // 미니게임 디스패치 기준을 _selectedType에서 _selectedMenu.MiniGameId로 이관
            switch (_selectedMenu.MiniGameId)
            {
                case MiniGameId.Skewer:
                    if (skewerMinigameController != null)
                    {
                        skewerMinigameController.StartMinigame(_selectedMenu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] SkewerMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case MiniGameId.Stew:
                    if (stewMinigameController != null)
                    {
                        stewMinigameController.StartMinigame(_selectedMenu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] StewMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case MiniGameId.FishGrill:
                    if (fishGrillMinigameController != null)
                    {
                        fishGrillMinigameController.StartMinigame(_selectedMenu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] FishGrillMinigameController가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case MiniGameId.None:
                default:
                    Debug.LogError($"[CookingMenuUI] '{_selectedMenu.DisplayName}'에 유효한 MiniGameId가 할당되지 않았습니다 ({_selectedMenu.MiniGameId}). 조리를 취소합니다.");
                    Close();
                    break;
            }
        }

        /// <summary>
        /// 카테고리 UI 분류와 실제 미니게임 에셋 ID가 일치하지 않을 때 정보성 경고 출력.
        /// 실행은 에셋의 MiniGameId를 우선한다.
        /// </summary>
        private static void WarnOnMiniGameMismatch(MenuData menu, CookingType selectedType)
        {
            if (menu == null) return;

            MiniGameId expected = ToMiniGameId(selectedType);
            if (menu.MiniGameId == expected) return;

            Debug.LogWarning($"[CookingMenuUI] UI 카테고리({selectedType})와 '{menu.DisplayName}'의 MiniGameId({menu.MiniGameId})가 일치하지 않습니다. "
                           + "실행은 에셋의 MiniGameId를 기준으로 진행합니다.");
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

            if (result.isSuccess)
            {
                // 미니게임 완료 시점에 창고 재료 일괄 차감
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
                        Debug.LogWarning($"[CookingMenuUI] '{completedMenu.DisplayName}' 조리 완료 후 재료 차감에 실패했습니다 (재료 부족).");
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
