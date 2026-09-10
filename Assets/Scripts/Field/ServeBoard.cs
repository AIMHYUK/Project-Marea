using System.Collections.Generic;
using Marea.Cooking;
using Marea.Data;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 서빙 배정판. "조리대에 있는 이 음식을 그걸 주문한 저 손님에게" 를 정해서
    /// 유휴 직원에게 맡긴다. 설계 결정 9의 "서빙 일감의 단일 창구" 그대로다.
    ///
    /// <b>Core/ 에서 Field/ 로 옮겼다 (+9/10).</b> Core 에 있는 동안은 B 타입
    /// (<see cref="CustomerController"/>, <see cref="CookingResult"/>)을 볼 수 없어서
    /// 배정 판단을 여기서 할 수가 없었다. 그래서 그 판단이 B의 미니게임 컨트롤러로
    /// 새어나갔고(<c>PickTarget</c> 3벌), 새어나간 자리에서 주문 대조가 빠졌다 — 이슈 #33.
    /// 원인은 폴더 배치였다. <c>AgentMover</c>·<c>IInteractable</c> 은 게임이 뭐든 성립하는
    /// 기반이지만 서빙 배정은 식당 도메인이라, Core 에 있을 물건이 아니었다.
    ///
    /// 직원은 B 타입을 모른다. 음식을 꺼내는 것도 손님에게 건네는 것도 여기서 한다 —
    /// 직원은 "어디로 걸어가서 누구에게 닿나" 만 안다.
    ///
    /// 배정은 예약이다. 예약한 음식을 플레이어가 먼저 집어갈 수 있으므로
    /// <see cref="TryPickup"/> 이 실패할 수 있고, 그때 <see cref="Abandon"/> 으로
    /// 예약이 풀려 다음 프레임에 다시 배정된다. 1차의 이슈 #5 미해결("board에 되돌리는
    /// 함수가 없다")이 여기서 닫힌다.
    /// </summary>
    public class ServeBoard : MonoBehaviour
    {
        [Header("픽업 지점 (+9/3)")]
        [Tooltip("직원이 음식을 받아가는 자리. 비워두면 이 오브젝트 자신의 위치를 쓴다. " +
                 "조리대 옆 NavMesh 위에 둘 것.")]
        [SerializeField] private Transform pickupPoint;

        [Header("참조 (+9/10)")]
        [Tooltip("음식이 쌓이는 곳. 직원이 여기서 꺼내간다. 플레이어도 같은 데서 집는다.")]
        [SerializeField] private CookingCounter counter;

        [Tooltip("대기 손님을 물어볼 곳. 비워두면 씬에서 찾는다.")]
        [SerializeField] private CustomerManager customers;

        /// <summary>배정 하나. 직원·손님·메뉴가 묶인다.</summary>
        private sealed class Assignment
        {
            public ServingStaff Staff;
            public CustomerController Customer;
            public MenuData Menu;

            /// <summary>조리대에서 실제로 꺼냈나. 꺼내기 전에는 음식이 아직 조리대에 있다.</summary>
            public bool PickedUp;

            /// <summary>꺼낸 음식. <see cref="PickedUp"/> 이 true일 때만 의미가 있다.</summary>
            public CookingResult Food;
        }

        private readonly List<ServingStaff> _staffs = new();
        private readonly List<Assignment> _assignments = new();

        /// <summary>직원이 음식을 받아가는 자리. (+9/3)</summary>
        public Vector3 PickupPosition => pickupPoint != null ? pickupPoint.position : transform.position;

        /// <summary>지금 진행 중인 배정 수. 확인용.</summary>
        public int AssignedCount => _assignments.Count;

        private void Awake()
        {
            if (customers == null) customers = FindAnyObjectByType<CustomerManager>();

            // 둘 중 하나라도 없으면 배정이 영영 안 돌고, 화면상으로는 "직원이 그냥 서 있다"와
            // 구분이 안 된다. 조용히 넘어가지 않는다.
            if (counter == null)
                Debug.LogError($"{name}: ServeBoard.counter가 비어 있다. 인스펙터에 CookingCounter를 넣을 것. "
                             + "직원이 음식을 꺼낼 곳이 없어서 서빙이 아예 돌지 않는다.", this);
            if (customers == null)
                Debug.LogError($"{name}: 씬에서 CustomerManager를 못 찾았다. "
                             + "누구에게 배달할지 물어볼 곳이 없어서 서빙이 아예 돌지 않는다.", this);
        }

        /// <summary>직원이 켜질 때 스스로 등록한다. 보드가 씬을 뒤지지 않는다.</summary>
        public void Register(ServingStaff staff)
        {
            if (staff == null || _staffs.Contains(staff)) return;
            _staffs.Add(staff);
        }

        public void Unregister(ServingStaff staff)
        {
            if (staff == null) return;
            _staffs.Remove(staff);

            // 꺼져버린 직원의 예약을 남겨두면 그 손님이 영영 다시 배정되지 않는다.
            Abandon(staff, "직원이 꺼졌다");
        }

        // 매 프레임 본다. 배정을 다시 시도해야 하는 계기가 여럿이라(음식이 올라왔다 /
        // 직원이 유휴가 됐다 / 손님이 새로 주문했다 / 예약이 풀렸다) 신호를 하나씩
        // 잇는 대신 여기서 한 번에 본다. 유휴 직원이 없으면 첫 줄에서 빠진다.
        private void Update()
        {
            TryAssign();
        }

        /// <summary>
        /// 유휴 직원에게 배정한다.
        ///
        /// 손님을 기준으로 돈다 — 조리대를 기준으로 돌면 "이 음식을 받을 사람이 있나"를
        /// 묻게 되고, 그러면 오래 기다린 손님이 뒤로 밀린다. 대기 순서가 우선이다.
        /// </summary>
        private void TryAssign()
        {
            if (counter == null || customers == null) return;

            foreach (ServingStaff staff in _staffs)
            {
                if (staff == null || !staff.IsIdle) continue;
                if (Find(staff) != null) continue;   // 유휴인데 예약이 남아 있다면 그게 먼저 풀려야 한다

                if (!TryMatch(out CustomerController customer, out MenuData menu)) return;

                _assignments.Add(new Assignment
                {
                    Staff = staff,
                    Customer = customer,
                    Menu = menu,
                });

                staff.Assign(customer.transform, menu != null ? menu.Icon : null);
            }
        }

        /// <summary>
        /// 배달할 짝을 찾는다 — 주문한 음식이 조리대에 있고, 아직 아무도 안 맡은 손님.
        /// </summary>
        private bool TryMatch(out CustomerController customer, out MenuData menu)
        {
            customer = null;
            menu = null;

            foreach (CustomerController c in customers.GetWaitingCustomers())
            {
                if (c == null) continue;
                if (IsAssigned(c)) continue;

                MenuData ordered = c.OrderedMenu;
                if (ordered == null) continue;

                // 조리대에 있는 그 메뉴의 접시 수에서 이미 예약된 것을 뺀다.
                // 안 빼면 직원 둘이 같은 접시를 예약하고 한 명은 빈손으로 돌아온다.
                if (counter.CountOf(ordered) - ReservedCount(ordered) <= 0) continue;

                customer = c;
                menu = ordered;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이 직원이 걸어갈 픽업 자리. (+9/10)
        ///
        /// 배정된 접시가 놓인 슬롯 앞으로 보낸다 — 조리대가 길면(슬롯이 8m에 걸쳐 있다)
        /// 한 자리에 세워두면 음식이 그 거리를 순간이동하는 것처럼 보인다.
        ///
        /// 슬롯을 못 찾으면 <see cref="PickupPosition"/> 으로 보낸다. 걸어가는 동안
        /// 접시가 앞 슬롯으로 당겨질 수 있는데, 도착해서 꺼낼 때 메뉴로 다시 찾으므로
        /// 한 칸 옆에서 집는 정도의 오차만 남는다.
        /// </summary>
        public Vector3 GetPickupTarget(ServingStaff staff)
        {
            Assignment a = Find(staff);
            if (a == null || counter == null) return PickupPosition;

            if (counter.TryGetSlotPosition(a.Menu, out Vector3 slot)) return slot;

            return PickupPosition;
        }

        /// <summary>
        /// 직원이 픽업 지점에 도착해서 부른다. 조리대에서 실제로 한 접시를 꺼낸다.
        ///
        /// <b>여기가 "한 번 더 확인" 하는 자리다 (+9/10).</b> 직원이 걸어오는 동안
        /// 플레이어가 같은 접시를 집어갔을 수 있다. 그때 false 를 주고, 직원은
        /// <see cref="Abandon"/> 으로 예약을 풀고 유휴로 돌아간다 — 음식은 조리대에
        /// 그대로 남아 있으니 다음 프레임에 다시 배정된다.
        /// </summary>
        /// <param name="carried">조리대에 놓여 있던 실물 오브젝트. 직원이 손에 달고 간다 (+9/10).
        /// 그 메뉴에 프리팹이 없으면 null 이고, 그때 직원은 아이콘으로 대신한다.
        /// <b>파괴 책임은 직원에게 넘어간다.</b></param>
        /// <returns>꺼냈으면 true.</returns>
        public bool TryPickup(ServingStaff staff, out GameObject carried)
        {
            carried = null;

            Assignment a = Find(staff);
            if (a == null) return false;
            if (counter == null) return false;

            // 픽업하러 오는 동안 손님이 식사를 포기하고 나갔다. 꺼내지 않는다 —
            // 꺼내면 받을 사람이 없어서 그대로 버려진다.
            if (a.Customer == null) return false;

            if (!counter.TryTakeFood(a.Menu, out CookingResult food, out GameObject visual)) return false;

            a.Food = food;
            a.PickedUp = true;
            carried = visual;
            return true;
        }

        /// <summary>
        /// 직원이 손님에게 닿아서 부른다. 건네는 것은 보드가 한다 —
        /// 직원은 <see cref="CustomerController"/> 를 모른다.
        ///
        /// 주문 대조도 여기서 일어난다. <c>TryReceiveFood</c> 는 플레이어가 건널 때
        /// 쓰는 것과 같은 문이다 — 경로마다 판정이 갈리지 않는다 (이슈 #33).
        /// </summary>
        public void Deliver(ServingStaff staff)
        {
            Assignment a = Find(staff);
            if (a == null) return;

            _assignments.Remove(a);

            if (!a.PickedUp)
            {
                Debug.LogError($"[ServeBoard] 꺼내지도 않은 배달이 완료로 들어왔다 ({staff?.name}). "
                             + "ServingStaff 상태 전이가 깨진 것이다.", this);
                return;
            }

            if (a.Customer == null)
            {
                Debug.LogWarning("[ServeBoard] 건네려는 순간 손님이 사라졌다 — 음식을 버린다.", this);
                return;
            }

            // 주문이 맞으면 손님이 받고(매출은 CustomerController.ServeFood 가 센다),
            // 다르면 손님이 거절하고 퇴장한다. 여기서 고른 메뉴가 곧 그 손님의 주문이라
            // 정상 경로에서는 거절이 나지 않는다 — 나면 배정이 틀린 것이다.
            if (!a.Customer.TryReceiveFood(a.Food))
            {
                Debug.LogWarning($"[ServeBoard] 손님이 거절했다 — 배정한 메뉴({a.Menu?.DisplayName})와 "
                               + $"주문({a.Customer.OrderedMenu?.DisplayName})이 어긋났다.", this);
            }
        }

        /// <summary>
        /// 직원이 배달을 포기하고 부른다. 예약을 푼다.
        ///
        /// 꺼내기 전이면 음식은 조리대에 그대로라 다음 프레임에 다시 배정된다.
        /// 이미 꺼냈으면 들고 있던 음식은 사라진다 — 조리대에 되돌리면 자리가 없을 때
        /// 또 갈라지고, 그 경우를 다루려면 "들고 대기" 상태가 하나 더 필요하다.
        /// 지금은 버리고 로그를 남긴다.
        /// </summary>
        public void Abandon(ServingStaff staff, string reason)
        {
            Assignment a = Find(staff);
            if (a == null) return;

            _assignments.Remove(a);

            if (a.PickedUp)
            {
                Debug.LogWarning($"[ServeBoard] 배달 포기 — {reason}. 꺼내온 "
                               + $"{a.Menu?.DisplayName} 하나를 버린다.", this);
            }
        }

        private Assignment Find(ServingStaff staff)
        {
            if (staff == null) return null;

            foreach (Assignment a in _assignments)
            {
                if (a.Staff == staff) return a;
            }

            return null;
        }

        private bool IsAssigned(CustomerController customer)
        {
            foreach (Assignment a in _assignments)
            {
                if (a.Customer == customer) return true;
            }

            return false;
        }

        /// <summary>아직 조리대에 있는데 이미 예약된 접시 수. 꺼낸 것은 세지 않는다.</summary>
        private int ReservedCount(MenuData menu)
        {
            int n = 0;

            foreach (Assignment a in _assignments)
            {
                if (!a.PickedUp && a.Menu == menu) n++;
            }

            return n;
        }
    }
}
