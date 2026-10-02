using System;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 오늘의 메뉴 — 이번 영업에 팔 메뉴. 계약 7이다 (2차 분담). A가 주고 B가 읽는다. (+10/2, 이슈 92)
    ///
    /// - B는 <see cref="Selected"/> 안에서만 주문을 만든다. (B 쪽 연결은 아직 — 지금 손님은 자기 목록에서 주문한다.)
    /// - 선택은 영업 시작 전(Ready)에만 바뀐다. 영업 중엔 <see cref="CanEdit"/>가 false.
    /// - <see cref="Selected"/>는 영업 중에 바뀌지 않는다.
    ///
    /// 해금 메뉴는 아직 없어서(MenuData 해금 미구현) MenuDatabase의 활성 메뉴 전부가 후보다.
    /// 선택 한도도 시설 데이터에 없어서 <see cref="maxCount"/> 임시값이다 — 성장 데이터가 생기면 거기서 읽는다.
    /// </summary>
    public class TodayMenu : MonoBehaviour
    {
        public static TodayMenu Instance { get; private set; }

        [Tooltip("후보 메뉴를 꺼내 올 곳.")]
        [SerializeField] private MenuDatabase menuDatabase;

        [Tooltip("한 번에 고를 수 있는 메뉴 수. 임시값 — 성장 데이터가 생기면 거기서 읽는다.")]
        [SerializeField, Min(1)] private int maxCount = 3;

        /// <summary>(+10/2) 메뉴 하나를 여는 주방 업그레이드 대상. 메뉴 데이터에 문자열 키가 없어 여기서 잇는다.</summary>
        [Serializable]
        private struct MenuUnlock
        {
            public MenuData menu;
            [Tooltip("MenuUnlock(메뉴 오픈) 또는 CookingStationUnlock(조리대 오픈 — 그 조리대 기본 레시피).")]
            public FacilityEffectType type;
            [Tooltip("업그레이드 대상 키. 예: MENU_GRILLED_CLAM, STATION_GRILL")]
            public string key;
        }

        [Tooltip("(+10/2) 주방 업그레이드로 열리는 메뉴. 여기 없는 메뉴는 처음부터 고를 수 있다.")]
        [SerializeField] private MenuUnlock[] menuUnlocks;

        private readonly List<MenuData> _selected = new();
        private readonly List<MenuData> _candidates = new();

        /// <summary>이번 영업에 팔 메뉴. 계약 7.</summary>
        public IReadOnlyList<MenuData> Selected => _selected;

        /// <summary>고를 수 있는 최대 개수. 계약 7.</summary>
        public int MaxCount => maxCount;

        /// <summary>[영업 시작]으로 확정됐을 때. 계약 7.</summary>
        public event Action OnConfirmed;

        /// <summary>고른 목록이 바뀔 때. UI가 듣는다.</summary>
        public event Action OnSelectionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{name}: 씬에 TodayMenu가 둘 이상이다. '{Instance.name}'이 먼저 잡혔고 이 오브젝트는 무시된다.", this);
                return;
            }
            Instance = this;

            if (menuDatabase == null)
                Debug.LogError($"{name}: TodayMenu.menuDatabase가 비어 있다. 고를 메뉴가 없다.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>후보 메뉴(활성 메뉴 전부). 부를 때마다 다시 모은다 — 메뉴가 늘어도 따로 할 일이 없다.</summary>
        public IReadOnlyList<MenuData> Candidates
        {
            get
            {
                _candidates.Clear();
                if (menuDatabase != null) menuDatabase.CollectAllActive(_candidates);
                return _candidates;
            }
        }

        /// <summary>영업 준비 단계에서만 바꿀 수 있다. B의 영업 관리자가 없으면 막지 않는다(테스트 씬).</summary>
        public bool CanEdit => BusinessManager.Instance == null || BusinessManager.Instance.CurrentState == BusinessState.Ready;

        public bool IsSelected(MenuData menu) => _selected.Contains(menu);

        /// <summary>(+10/2) 주방 업그레이드로 열렸나. 잠겼으면 열리는 주방 레벨을 준다.</summary>
        public bool IsMenuUnlocked(MenuData menu, out int requiredLevel)
        {
            requiredLevel = 0;
            if (menu == null || menuUnlocks == null || FacilityLevels.Instance == null) return true;
            foreach (MenuUnlock u in menuUnlocks)
                if (u.menu == menu)
                    return FacilityLevels.Instance.IsTargetUnlocked(FacilityKind.Kitchen, u.type, u.key, out requiredLevel);
            return true;
        }

        /// <summary>지금 창고 재고로 몇 그릇 만들 수 있나. 레시피가 비면 int.MaxValue.</summary>
        public static int ServingsAvailable(MenuData menu)
        {
            Warehouse warehouse = Warehouse.Instance;
            if (menu == null || warehouse == null) return 0;
            if (menu.Recipe == null) return int.MaxValue;

            int servings = int.MaxValue;
            foreach (RecipeEntry entry in menu.Recipe)
            {
                if (entry.ingredient == null || entry.requiredAmount <= 0) continue;
                servings = Mathf.Min(servings, warehouse.CountOf(entry.ingredient) / entry.requiredAmount);
            }
            return servings;
        }

        /// <summary>
        /// 고르거나 뺀다. 영업 중이면, 한도가 찼으면(새로 고를 때), 재료로 한 그릇도 못 만들면 false.
        /// </summary>
        public bool Toggle(MenuData menu)
        {
            if (menu == null || !CanEdit) return false;

            if (_selected.Remove(menu))
            {
                OnSelectionChanged?.Invoke();
                return true;
            }

            if (!IsMenuUnlocked(menu, out _)) return false;
            if (_selected.Count >= maxCount || ServingsAvailable(menu) <= 0) return false;

            _selected.Add(menu);
            OnSelectionChanged?.Invoke();
            return true;
        }

        /// <summary>[영업 시작]. 하나도 안 골랐거나 영업 중이면 false.</summary>
        public bool Confirm()
        {
            if (!CanEdit || _selected.Count == 0) return false;
            OnConfirmed?.Invoke();
            return true;
        }
    }
}
