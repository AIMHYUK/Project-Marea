using Marea.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Marea.Cooking
{
    public class MinigameCameraController : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float transitionDuration = 0.4f;

        [Header("초점 흐림 (+10/2) — 비워 두면 안 쓴다")]
        [Tooltip("Depth of Field만 든 Volume. 평소 weight 0, 미니게임 시점에 있는 동안 1.")]
        [SerializeField] private Volume focusVolume;
        [Tooltip("시점 정면으로 레이를 쏴 닿은 곳까지를 초점으로 잡는다. 안 닿으면 이 거리.")]
        [SerializeField, Min(0.1f)] private float fallbackFocusDistance = 3f;
        [SerializeField, Min(0.5f)] private float focusRayLength = 20f;

        private Camera _cam;
        private DepthOfField _dof;
        private float _defaultAperture;
        private UniversalAdditionalCameraData _camData;
        private bool _postFxDefault; // 평소엔 포스트프로세싱을 안 켠다 — 흐림을 쓰는 동안만 켜고 돌려놓는다
        private CameraFollow _cameraFollow;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _hasOriginal; // 미니게임 중(시점에 가 있거나 가는 중)이면 true
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

            PhysicsRaycaster raycaster = GetComponent<PhysicsRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<PhysicsRaycaster>();
            }

            // 게임 시작 시 무조건 비활성화
            raycaster.enabled = false;

            if (focusVolume != null)
            {
                _camData = _cam.GetComponent<UniversalAdditionalCameraData>();
                if (_camData != null) _postFxDefault = _camData.renderPostProcessing;
                // .profile은 인스턴스 사본이라 런타임에 초점을 바꿔도 에셋이 안 바뀐다.
                if (!focusVolume.profile.TryGet(out _dof))
                    Debug.LogError($"{name}: focusVolume 프로필에 Depth Of Field가 없다. 초점 흐림이 안 된다.", focusVolume);
                else
                    _defaultAperture = _dof.aperture.value;
                focusVolume.weight = 0f;
            }
        }

        /// <summary>시점 정면에 닿는 곳(냄비 · 도마 등)에 초점을 맞추고 흐림을 켠다.</summary>
        private void FocusOn(Transform viewPoint)
        {
            if (focusVolume == null || _dof == null) return;

            float distance = fallbackFocusDistance;
            ViewPointFocus focus = viewPoint.GetComponent<ViewPointFocus>();
            if (focus != null && focus.Target != null)
                distance = Mathf.Max(0.1f, Vector3.Dot(focus.Target.position - viewPoint.position, viewPoint.forward));
            else if (Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, focusRayLength, ~0, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            _dof.focusDistance.Override(distance);
            _dof.aperture.Override(focus != null && focus.Aperture > 0f ? focus.Aperture : _defaultAperture);
            focusVolume.weight = 1f;
            if (_camData != null) _camData.renderPostProcessing = true;
        }

        private void ClearFocus()
        {
            if (focusVolume != null) focusVolume.weight = 0f;
            if (_camData != null) _camData.renderPostProcessing = _postFxDefault;
        }

        public void MoveToViewPoint(Transform targetViewPoint)
        {
            if (targetViewPoint == null)
            {
                Debug.LogError("[MinigameCameraController] targetViewPoint가 비어있습니다. 미니게임 컨트롤러의 cameraViewPoint / stepNViewPoint를 연결하세요.");
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

            // 복귀 지점은 미니게임 시작 때 한 번만 저장한다.
            // 단계마다 시점을 옮길 때 덮어쓰면 끝나고 앞 단계 시점으로 돌아간다.
            if (!_hasOriginal)
            {
                _originalPosition = _cam.transform.position;
                _originalRotation = _cam.transform.rotation;
                _hasOriginal = true;
            }

            //Debug.Log($"[MinigameCameraController] 목표 위치로 이동 시작: {targetViewPoint.position}");
            _moveRoutine = StartCoroutine(TransitionRoutine(targetViewPoint.position, targetViewPoint.rotation));
            FocusOn(targetViewPoint);
        }

        public void ReturnToOriginalPosition()
        {
            if (_cam == null) return;
            if (!_hasOriginal) return; // 이미 복귀했다 — CameraFollow가 움직인 카메라를 다시 끌어오지 않는다

            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
            }

            //Debug.Log("[MinigameCameraController] 원래 카메라 위치로 복귀 시작");
            ClearFocus();
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
            _hasOriginal = false;

            if (_cameraFollow != null)
            {
                _cameraFollow.enabled = true;
                //Debug.Log("[MinigameCameraController] CameraFollow 컴포넌트 다시 활성화됨");
            }
        }
    }
}
