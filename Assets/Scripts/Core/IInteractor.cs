using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 상호작용을 거는 쪽. 지금은 플레이어 하나뿐이지만 인터페이스로 노출한다.
    /// B가 PlayerController 구체 타입에 묶이지 않게 하는 게 목적이다.
    /// </summary>
    public interface IInteractor
    {
        Transform Transform { get; }

        // Warehouse를 여기서 뺐다 (+9/9). 창고는 행위자 것이 아니라 가게 것이다 —
        // 기획 「자원 관리」가 "플레이어가 식재료를 직접 들고 이동하지 않는다"와
        // "요리 시스템에서도 동일한 식재료 보유 데이터를 참조한다"를 못박았다.
        // 창고가 필요하면 Warehouse.Instance를 쓴다. 설계 결정 10이 예고한 작업이다.

        /// <summary>
        /// 상호작용이 여러 프레임 이어질 때(미니게임 등) 감싼다.
        /// 안 감싸면 미니게임 도중에 WASD로 걸어나갈 수 있다.
        /// EndBusy를 빠뜨리면 플레이어가 영구히 잠기니 완료 콜백에서 반드시 부른다.
        /// </summary>
        void BeginBusy();

        void EndBusy();
    }
}
