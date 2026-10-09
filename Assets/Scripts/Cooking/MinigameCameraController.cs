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

        private Camera _cam;
        private DepthOfField _dof;
        private float _defaultAperture;
        private DepthOfFieldMode _defaultDofMode;
        private float _defaultFocalLength;
        private UniversalAdditionalCameraData _camData;
        private bool _postFxDefault; // 평소엔 포스트프로세싱을 안 켠다 — 흐림을 쓰는 동안만 켜고 돌려놓는다
        private CameraFollow _cameraFollow;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _hasOriginal; // 미니게임 중(시점에 가 있거나 가는 중)이면 true
        private Coroutine _moveRoutine;

        public bool HasMinigameView => _hasOriginal;

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
                {
                    _defaultAperture = _dof.aperture.value;
                    _defaultDofMode = _dof.mode.value;
                    _defaultFocalLength = _dof.focalLength.value;
                }
                focusVolume.weight = 0f;
            }
        }

        /// <summary>시점 정면에 닿는 곳(냄비 · 도마 등)에 초점을 맞추고 흐림을 켠다.</summary>
        private Transform _focusViewPoint;   // (+10/7, A) 지금 초점을 건 시점. 원위치로 돌아가면 비운다.

        /// <summary>
        /// (+10/7, A) 플레이 중 인스펙터에서 ViewPointFocus 값을 바꾸면 그 시점을 보고 있을 때 바로 다시 건다.
        /// ViewPointFocus.OnValidate가 부른다. 플레이 중에 바꾼 값은 플레이를 끄면 되돌아간다.
        /// </summary>
        public void RefreshFocus(ViewPointFocus changed)
        {
            if (_focusViewPoint != null && changed != null && changed.transform == _focusViewPoint) FocusOn(_focusViewPoint);
        }

        private void FocusOn(Transform viewPoint)
        {
            ViewPointFocus focus = viewPoint.GetComponent<ViewPointFocus>();
            ApplyEdgeBlur(focus);   // (+10/7, A) 거리 흐림과 따로 — 초점 볼륨이 없어도 건다

            if (focusVolume == null || _dof == null) return;

            // (+10/2) 초점 대상을 정한 시점에서만 흐린다. 주방 가구엔 콜라이더가 없어 정면 레이가 요리를 지나
            // 뒷벽 · 바닥에 닿았고, 그러면 정작 요리가 흐려졌다. 모르는 시점은 흐림을 끄는 게 안전하다.
            if (focus == null || focus.Target == null || focus.BlurMode == MinigameBlurMode.Off)
            {
                ClearFocus();
                return;
            }
            float distance = Mathf.Max(0.1f, Vector3.Dot(focus.Target.position - viewPoint.position, viewPoint.forward));

            if (focus.BlurMode == MinigameBlurMode.Gaussian)
            {
                // Far-field blur keeps moving knives and ingredients in front of the target sharp.
                _dof.mode.Override(DepthOfFieldMode.Gaussian);
                float start = distance + focus.BackgroundStartOffset;
                _dof.gaussianStart.Override(start);
                _dof.gaussianEnd.Override(start + focus.BackgroundFadeDistance);
                _dof.gaussianMaxRadius.Override(focus.BlurRadius);
                _dof.highQualitySampling.Override(focus.HighQualitySampling);
            }
            else
            {
                _dof.mode.Override(focus.BlurMode == MinigameBlurMode.Bokeh ? DepthOfFieldMode.Bokeh : _defaultDofMode);
                _dof.focusDistance.Override(distance);
                _dof.aperture.Override(focus.Aperture > 0f ? focus.Aperture : _defaultAperture);
                _dof.focalLength.Override(focus.BlurMode == MinigameBlurMode.Bokeh ? focus.FocalLength : _defaultFocalLength);
            }
            focusVolume.weight = 1f;
            if (_camData != null) _camData.renderPostProcessing = true;
        }

        /// <summary>(+10/7, A) 시점의 가장자리 블러를 건다. ViewPointFocus가 없거나 0이면 끈다.</summary>
        private static void ApplyEdgeBlur(ViewPointFocus focus)
        {
            if (focus == null || focus.EdgeBlur <= 0f) { Marea.Core.EdgeBlurRendererFeature.Clear(); return; }
            Marea.Core.EdgeBlurRendererFeature.Set(focus.EdgeBlur, focus.EdgeInner, focus.EdgeOuter, focus.EdgeRadius, focus.EdgeDarken);
        }

        // (+10/7, A) 셰이더 전역값은 플레이를 꺼도 남는다 — 켜질 때 · 꺼질 때 비운다.
        private void OnEnable() => Marea.Core.EdgeBlurRendererFeature.Clear();
        private void OnDisable() => Marea.Core.EdgeBlurRendererFeature.Clear();

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
            _focusViewPoint = targetViewPoint;   // (+10/7, A) 인스펙터에서 값을 바꾸면 다시 적용하려고 기억
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
            _focusViewPoint = null;   // (+10/7, A)
            ClearFocus();
            Marea.Core.EdgeBlurRendererFeature.Clear();   // (+10/7, A) 미니게임이 끝나면 가장자리 블러도 끈다
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
