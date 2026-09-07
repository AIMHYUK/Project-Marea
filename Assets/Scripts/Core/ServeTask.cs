using System;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 서빙 작업 하나. 어디서 받아 누구에게 옮기는가.
    ///
    /// A는 이것만 안다 — Table도 Customer도 Order도 모른다.
    /// 대상은 <see cref="Transform"/> 이지 손님 타입이 아니다. 그래서 B가 손님 구조를
    /// 어떻게 바꾸든 직원 코드는 안 바뀐다. "받았다"를 알리는 것도 B가 넘긴
    /// <see cref="OnDelivered"/> 가 하지, 직원이 손님 메서드를 부르지 않는다.
    /// </summary>
    public struct ServeTask
    {
        public Vector3 PickupPoint;      // 어디서 받나 (요리대)

        /// <summary>
        /// 대상이 없을 때 쓰는 고정 목적지. <see cref="DeliverTarget"/> 이 있으면 무시된다.
        /// ServeBoard의 ContextMenu 테스트가 이 경로를 탄다.
        /// </summary>
        public Vector3 DeliverPoint;

        /// <summary>
        /// 배달 대상. (+9/3)
        ///
        /// 좌표가 아니라 참조인 이유는 둘이다.
        ///   ① 손님이 식사를 끝내고 Destroy되면 여기가 null이 된다 — 직원이 알아챌 수 있다.
        ///   ② 손님 트리거가 직원을 감지했을 때 "내 음식이 맞나"를 참조 비교로 판정한다.
        /// null이면 <see cref="DeliverPoint"/> 로 가는 좌표 배달이다.
        /// </summary>
        public Transform DeliverTarget;

        public Sprite FoodIcon;          // 직원이 들고 갈 그림

        /// <summary>
        /// 배달이 끝났을 때 딱 한 번. (+9/3)
        ///
        /// B가 넣는다 — 보통 () => customer.ServeFood() 다.
        /// 이게 있어서 Core(A)가 Restaurant(B) 타입을 참조하지 않아도 된다.
        /// </summary>
        public Action OnDelivered;

        /// <summary>어디로 걸어가야 하나. 대상이 있으면 대상의 지금 위치다.</summary>
        public Vector3 CurrentDeliverPoint =>
            DeliverTarget != null ? DeliverTarget.position : DeliverPoint;

        /// <summary>좌표 배달. 기존 호출자(ContextMenu 테스트)를 위해 남긴다.</summary>
        public ServeTask(Vector3 pickup, Vector3 deliver, Sprite icon)
        {
            PickupPoint = pickup;
            DeliverPoint = deliver;
            DeliverTarget = null;
            FoodIcon = icon;
            OnDelivered = null;
        }

        /// <summary>대상 배달. (+9/3)</summary>
        public ServeTask(Vector3 pickup, Transform target, Sprite icon, Action onDelivered)
        {
            PickupPoint = pickup;
            DeliverPoint = target != null ? target.position : pickup;
            DeliverTarget = target;
            FoodIcon = icon;
            OnDelivered = onDelivered;
        }
    }
}
