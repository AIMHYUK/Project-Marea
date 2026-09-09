using UnityEngine;

namespace Marea.UI
{
    public class BillboardUI : MonoBehaviour
    {
        private Camera _mainCam;

        private void Start()
        {
            _mainCam = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null) return;
            }

            // 카메라가 바라보는 전방 방향과 캔버스 전방을 일치시켜 왜곡 방지
            transform.forward = _mainCam.transform.forward;
        }
    }
}
