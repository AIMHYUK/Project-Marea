using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Player
{
    /// <summary>
    /// Input System에 의존하는 유일한 파일. 다른 코드는 여기서 값만 읽는다.
    ///
    /// 이렇게 가둬두는 이유는 리바인딩·게임패드가 들어올 때 바깥이 안 바뀌게 하려는 것이다.
    /// 지금은 Move/Point/Click 셋뿐이라 액션 에셋의 이득이 크지 않지만,
    /// 패드가 들어오면 같은 버튼이 상황마다 다른 뜻이 되고 그때는 액션 맵이 필요해진다.
    /// 그 변화가 이 파일 안에서 끝난다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        private PlayerInputAction _actions;

        /// <summary>WASD. 2D Vector 컴포지트라 대각선이 정상적으로 나온다.</summary>
        public Vector2 MoveAxis => _actions.Player.Move.ReadValue<Vector2>();

        /// <summary>마우스 스크린 좌표.</summary>
        public Vector2 PointerPosition => _actions.Player.Point.ReadValue<Vector2>();

        /// <summary>이번 프레임에 좌클릭이 눌렸는가.</summary>
        public bool ClickPressed => _actions.Player.Click.WasPressedThisFrame();

        private void Awake() => _actions = new PlayerInputAction();

        private void OnEnable() => _actions.Enable();

        // 안 끄면 씬을 바꿔도 액션이 살아남아 입력이 이중으로 들어온다.
        private void OnDisable() => _actions.Disable();

        private void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }
    }
}
