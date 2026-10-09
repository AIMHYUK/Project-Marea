using Marea.Core;
using Marea.Field;
using UnityEngine;

namespace Marea.Restaurant
{
    /// <summary>
    /// (+10/9, A) 플레이어가 음식을 건네는 건 근접 E 경로(ProximityInteractor)로 — 음식을 든 채 주문 대기 손님에게
    /// 가까이 가면 "[E] 전달하기"가 뜬다. 예전엔 여기서 트리거 + E를 직접 읽어 표시가 없었다. 클릭은 안 받는다.
    /// 직원 배달(트리거)은 그대로.
    /// </summary>
    [RequireComponent(typeof(CustomerController))]
    public class CustomerInteractable : InteractableBase
    {
        private CustomerController _customer;

        private void Awake()
        {
            _customer = GetComponent<CustomerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // 직원이 내 음식을 들고 왔나. (+9/3)
            //
            // 여기서 ServeFood를 직접 부르지 않는다. 수령 통보는 ServeBoard의 완료
            // 콜백이 한다 - 직원 쪽에는 트리거가 안 잡힐 때 쓰는 거리 판정 경로가
            // 하나 더 있어서, 양쪽이 각자 부르면 어느 쪽으로 끝났는지가 흐려진다.
            ServingStaff staff = other.GetComponentInParent<ServingStaff>();
            if (staff != null) staff.TryHandOff(gameObject);
        }

        public override bool AllowClick => false;

        public override string InteractLabel(IInteractor actor) => "전달하기";

        public override bool CanInteract(IInteractor actor)
        {
            PlayerServingController serving = PlayerServingController.Of(actor);
            return serving != null && serving.IsHoldingFood && _customer != null && _customer.State == CustomerState.WaitingOrder;
        }

        public override void Interact(IInteractor actor)
        {
            // 서빙 시도: 주문 일치 여부 검사, 손 비우기 및 손님 반응 처리 일괄 수행
            //
            // 매출 기록은 여기서 하지 않는다 (+9/10). CustomerController.ServeFood 가
            // 센다 — 직원 배달도 거기로 들어오므로 경로마다 세면 한 벌씩 늘어난다.
            // 여기에 남겨두면 플레이어가 건넨 것만 두 번 잡힌다.
            PlayerServingController serving = PlayerServingController.Of(actor);
            if (serving != null) serving.TryServeToCustomer(_customer);
        }
    }
}
