using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 서빙 작업 하나. 어디서 받아 어디로 옮기는가.
    ///
    /// A는 이 셋만 안다 — Table도 Customer도 Order도 모른다.
    /// 그래서 B가 손님 구조를 어떻게 바꾸든 직원 코드는 안 바뀐다.
    /// </summary>
    public struct ServeTask
    {
        public Vector3 PickupPoint;    // 어디서 받나 (요리대)
        public Vector3 DeliverPoint;   // 어디로 (테이블 옆)
        public Sprite FoodIcon;        // 직원이 들고 갈 그림

        public ServeTask(Vector3 pickup, Vector3 deliver, Sprite icon)
        {
            PickupPoint = pickup;
            DeliverPoint = deliver;
            FoodIcon = icon;
        }
    }
}
