using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 미니게임 시점이 초점을 맞출 대상. 시점(ViewPoint) 오브젝트에 붙인다. (+10/2)
    ///
    /// MinigameCameraController는 이게 없으면 시점 정면으로 레이를 쏴 닿은 곳에 초점을 맞춘다.
    /// 비스듬히 내려다보는 시점은 레이가 냄비 위를 지나 뒷벽에 닿아서, 대상을 직접 정해 준다.
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
