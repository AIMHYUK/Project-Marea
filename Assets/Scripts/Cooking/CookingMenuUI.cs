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
        Steak     // 스테이크 요리
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

        [Header("1단계: 카테고리 패널 (꼬치 / 스튜 / 스테이크)")]
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private Button btnSkewer;
        [SerializeField] private Button btnStew;
        [SerializeField] private Button btnSteak;

        [Header("2단계: 세부 메뉴 패널")]
        [SerializeField] private GameObject subMenuPanel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private MenuCardSlot cardPrefab;
        [SerializeField] private TextMeshProUGUI txtCategoryTitle;
        [SerializeField] private Button btnBackToCategory;
        [SerializeField] private Button btnStartCooking;

        [Header("미니게임 UI 연동")]
        [SerializeField] private SkewerMinigameUI skewerMinigameUI;

        [Header("데이터 등록")]
        [SerializeField] private List<CookingCategoryGroup> categoryDataList;

        private readonly List<MenuCardSlot> _activeSlots = new();
        private MenuStep _currentStep = MenuStep.Category;
        private CookingType _selectedType;
        private MenuData _selectedMenu;
        private IInteractor _currentActor;

        private void Awake()
        {
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (btnBackToCategory != null) btnBackToCategory.onClick.AddListener(ShowCategoryStep);
            if (btnStartCooking != null) btnStartCooking.onClick.AddListener(StartCooking);

            if (btnSkewer != null) btnSkewer.onClick.AddListener(() => OnSelectCategory(CookingType.Skewer));
            if (btnStew != null) btnStew.onClick.AddListener(() => OnSelectCategory(CookingType.Stew));
            if (btnSteak != null) btnSteak.onClick.AddListener(() => OnSelectCategory(CookingType.Steak));

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

            // 메뉴 선택 중 손님 생성 일시정지
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
            // 메뉴 닫기(취소 또는 요리 미시작) 시 손님 생성 재개
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
                    CookingType.Steak => "스테이크 요리 선택",
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

            var targetGroup = categoryDataList.Find(g => g.cookingType == type);
            if (targetGroup.subMenus == null) return;

            foreach (var menu in targetGroup.subMenus)
            {
                if (menu == null) continue;

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
                }
            }

            if (btnStartCooking != null)
            {
                btnStartCooking.interactable = (_selectedMenu != null);
            }
        }

        private void StartCooking()
        {
            if (_selectedMenu == null) return;

            Debug.Log($"[CookingMenuUI] 요리 시작 -> 카테고리: {_selectedType}, 선택 메뉴: {_selectedMenu.DisplayName}");

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            switch (_selectedType)
            {
                case CookingType.Skewer:
                    if (skewerMinigameUI != null)
                    {
                        skewerMinigameUI.StartGame(_selectedMenu, OnMinigameFinished);
                    }
                    else
                    {
                        Debug.LogWarning("[CookingMenuUI] SkewerMinigameUI가 연결되지 않았습니다.");
                        Close();
                    }
                    break;

                case CookingType.Stew:
                case CookingType.Steak:
                    Debug.Log($"[CookingMenuUI] {_selectedType} 미니게임은 아직 구현 준비 중입니다.");
                    Close();
                    break;
            }
        }

        private void OnMinigameFinished(CookingResult result)
        {
            Debug.Log($"[CookingMenuUI] 요리 완료 결과: 성공여부={result.isSuccess}, 최종가격={result.finalPrice}G, 판정={result.bestGrade}");

            // TODO: 결과 팝업 표시 또는 인벤토리/수익 데이터 반영

            // 플레이어 조작 잠금 해제
            Close();
        }
    }
}