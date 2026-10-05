using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 미니게임 시점이 초점을 맞출 대상. 시점(ViewPoint) 오브젝트에 붙인다. (+10/2)
    ///
    /// MinigameCameraController는 이게 붙은 시점에서만 초점 흐림을 켠다 — 없으면 흐리지 않는다.
    /// (+10/2) 예전엔 없을 때 정면 레이캐스트를 썼는데, 주방 가구에 콜라이더가 없어 레이가 요리를 지나
    /// 뒷벽에 닿았고 요리가 흐려졌다.
    /// </summary>
    public class ViewPointFocus : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("이 시점에서만 쓸 조리개(f값). 0이면 프로필 값. 가까이서 내려다보는 시점은 심도가 얕아 조여야 대상 전체가 선명하다.")]
        [SerializeField, Min(0f)] private float aperture;

        public Transform Target => target;
        public float Aperture => aperture;
    }
}
