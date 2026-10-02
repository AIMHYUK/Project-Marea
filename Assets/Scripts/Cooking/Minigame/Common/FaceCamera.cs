using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 늘 메인 카메라를 바라본다 (빌보드). 납작한 스프라이트 거품을 어느 시점에서도 동그랗게 보이게. (+10/2, 이슈 84)
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
