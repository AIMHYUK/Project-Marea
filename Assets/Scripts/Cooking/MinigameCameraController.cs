using Marea.Player;
using System.Collections;
using UnityEngine;

namespace Marea.Cooking
{
    public class MinigameCameraController : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float transitionDuration = 0.4f;

        private Camera _cam;
        private CameraFollow _cameraFollow;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Coroutine _moveRoutine;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            _cameraFollow = GetComponent<CameraFollow>();
            if (_cameraFollow == null)
            {
                Debug.LogError("[MinigameCameraController] 같은 오브젝트에서 CameraFollow 컴포넌트를 찾지 못했습니다!");
            }
        }

        public void MoveToViewPoint(Transform targetViewPoint)
        {
            if (targetViewPoint == null)
            {
                Debug.LogError("[MinigameCameraController] targetViewPoint가 비어있습니다. StewMinigameController의 CameraViewPoint를 연결하세요.");
                return;
            }

            if (_cam == null)
            {
                Debug.LogError("[MinigameCameraController] Camera 컴포넌트가 없습니다.");
                return;
            }

            if (_cameraFollow != null)
            {
                _cameraFollow.enabled = false;
                //Debug.Log("[MinigameCameraController] CameraFollow 컴포넌트 비활성화 완료");
            }

            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
            }

            _originalPosition = _cam.transform.position;
            _originalRotation = _cam.transform.rotation;

            //Debug.Log($"[MinigameCameraController] 목표 위치로 이동 시작: {targetViewPoint.position}");
            _moveRoutine = StartCoroutine(TransitionRoutine(targetViewPoint.position, targetViewPoint.rotation));
        }

        public void ReturnToOriginalPosition()
        {
            if (_cam == null) return;

            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
            }

            //Debug.Log("[MinigameCameraController] 원래 카메라 위치로 복귀 시작");
            _moveRoutine = StartCoroutine(ReturnRoutine(_originalPosition, _originalRotation));
        }

        private IEnumerator TransitionRoutine(Vector3 destPos, Quaternion destRot)
        {
            Vector3 startPos = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

                _cam.transform.position = Vector3.Lerp(startPos, destPos, t);
                _cam.transform.rotation = Quaternion.Slerp(startRot, destRot, t);
                yield return null;
            }

            _cam.transform.position = destPos;
            _cam.transform.rotation = destRot;
            _moveRoutine = null;
        }

        private IEnumerator ReturnRoutine(Vector3 destPos, Quaternion destRot)
        {
            yield return TransitionRoutine(destPos, destRot);

            if (_cameraFollow != null)
            {
                _cameraFollow.enabled = true;
                //Debug.Log("[MinigameCameraController] CameraFollow 컴포넌트 다시 활성화됨");
            }
        }
    }
}
