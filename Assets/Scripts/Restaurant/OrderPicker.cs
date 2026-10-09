using System.Collections.Generic;
using Marea.Data;
using Marea.Economy;
using UnityEngine;

namespace Marea.Restaurant
{
    /// <summary>
    /// 손님 주문 고르기. (+10/9, A — 이슈 128 「주문 다양성」)
    ///
    /// - 오늘의 메뉴(<see cref="TodayMenu.Selected"/>, 계약 7) 안에서만 고른다. 오늘의 메뉴를 안 골랐으면
    ///   손님 프리팹의 메뉴 목록(fallback)에서 고른다 — 예전 동작.
    /// - 같은 메뉴가 이어지지 않게: 바로 앞 주문과 같은 메뉴는 후보가 둘 이상이면 빼고,
    ///   지금 음식을 기다리는 손님이 이미 많이 시킨 메뉴일수록 덜 뽑는다(가중치 1 / (1 + 대기 중 같은 주문 수)).
    ///
    /// 손님 프리팹마다 있던 고르기(CustomerController.DecideOrder)를 한 곳으로 모았다 — 바로 앞 주문은
    /// 손님 하나가 아니라 가게 전체 기준이라 손님 안에서는 못 안다.
    /// </summary>
    public static class OrderPicker
    {
        private static MenuData _last;
        private static readonly List<MenuData> _candidates = new();
        private static readonly List<float> _weights = new();
        private static readonly Dictionary<MenuData, int> _waiting = new();

        /// <summary>주문 하나를 고른다. 고를 게 없으면 null.</summary>
        public static MenuData Pick(IReadOnlyList<MenuData> fallback)
        {
            _candidates.Clear();
            TodayMenu today = TodayMenu.Instance;
            if (today != null && today.Selected.Count > 0) AddActive(today.Selected);
            if (_candidates.Count == 0 && fallback != null) AddActive(fallback);
            if (_candidates.Count == 0) return null;

            CountWaitingOrders();

            _weights.Clear();
            float total = 0f;
            foreach (MenuData menu in _candidates)
            {
                float w = 1f / (1 + (_waiting.TryGetValue(menu, out int n) ? n : 0));
                if (menu == _last && _candidates.Count > 1) w = 0f;
                _weights.Add(w);
                total += w;
            }

            MenuData picked = _candidates[_candidates.Count - 1];
            float roll = Random.value * total;
            for (int i = 0; i < _candidates.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f) { picked = _candidates[i]; break; }
            }

            _last = picked;
            return picked;
        }

        private static void AddActive(IReadOnlyList<MenuData> menus)
        {
            foreach (MenuData menu in menus)
                if (menu != null && menu.IsActive && !_candidates.Contains(menu)) _candidates.Add(menu);
        }

        private static void CountWaitingOrders()
        {
            _waiting.Clear();
            foreach (CustomerController c in Object.FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
            {
                if (c == null || c.State != CustomerState.WaitingOrder || c.OrderedMenu == null) continue;
                _waiting[c.OrderedMenu] = _waiting.TryGetValue(c.OrderedMenu, out int n) ? n + 1 : 1;
            }
        }
    }
}
