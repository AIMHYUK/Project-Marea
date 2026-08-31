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
        Warehouse Warehouse { get; }

        /// <summary>
        /// 상호작용이 여러 프레임 이어질 때(미니게임 등) 감싼다.
        /// 안 감싸면 미니게임 도중에 WASD로 걸어나갈 수 있다.
        /// EndBusy를 빠뜨리면 플레이어가 영구히 잠기니 완료 콜백에서 반드시 부른다.
        /// </summary>
        void BeginBusy();

        void EndBusy();
    }
}
