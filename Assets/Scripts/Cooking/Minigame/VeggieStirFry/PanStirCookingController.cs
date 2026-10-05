using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class PanStirCookingController : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        [Header("오브젝트 바인딩")]
        [SerializeField] private List<GameObject> clickableVeggieObjects; // 클릭할 3D 야채(들)
        [SerializeField] private Transform panInsideTransform;            // 프라이팬 내부 투입 지점
        [SerializeField] private Transform spatulaTransform;             // 3D 주걱 오브젝트

        [Header("주걱 이동 제한 (Local Offset)")]
        [SerializeField] private Vector2 spatulaMoveRange = new Vector2(1.5f, 1.5f); // 팬 안에서 주걱이 이동할 X, Z 범위
        [SerializeField] private float spatulaSpeedMultiplier = 0.005f;

        [Header("수치 설정")]
        [SerializeField] private float requiredCookProgress = 100f; // 목표 조리 진행도
        [SerializeField] private float burnRate = 15f;              // 안 저을 때 초당 타는 게이지 증가량
        [SerializeField] private float cookRate = 25f;              // 저을 때 초당 조리 게이지 증가량

        [Header("시각 연출 (Material & Particle)")]
        [SerializeField] private Renderer veggieRenderer;           // 익힘/탄 재질을 적용할 렌더러
        [SerializeField] private Color rawColor = Color.white;      // 날것 색상
        [SerializeField] private Color cookedColor = new Color(1f, 0.8f, 0.4f); // 노릇노릇하게 익은 색상
        [SerializeField] private Color burnedColor = Color.black;   // 탄 색상
        [SerializeField] private ParticleSystem cookingSteamEffect; // 조리 시 연기 이펙트
        [SerializeField] private AudioSource stirAudioSource;       // 볶는 소리 사운드

        public bool IsVeggieInPan { get; private set; }
        public bool IsCookCompleted { get; private set; }
        public bool IsBurned { get; private set; }
        public float CookProgress { get; private set; }
        public float BurnProgress { get; private set; }

        private Vector3 _initialSpatulaLocalPos;
        private Vector2 _lastMousePos;
        private bool _isStirringThisFrame;
        private bool _isDraggingSpatulaDirectly;
        private Camera _mainCamera;

        private readonly Dictionary<GameObject, Transform> _initialVeggieParents =
            new Dictionary<GameObject, Transform>();

        private readonly Dictionary<GameObject, Vector3> _initialVeggieLocalPositions =
            new Dictionary<GameObject, Vector3>();

        private readonly Dictionary<GameObject, Quaternion> _initialVeggieLocalRotations =
            new Dictionary<GameObject, Quaternion>();

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (spatulaTransform != null)
            {
                _initialSpatulaLocalPos =
                    spatulaTransform.localPosition;
            }

            if (clickableVeggieObjects != null)
            {
                foreach (var veggie in clickableVeggieObjects)
                {
                    if (veggie == null) continue;

                    _initialVeggieParents[veggie] =
                        veggie.transform.parent;

                    _initialVeggieLocalPositions[veggie] =
                        veggie.transform.localPosition;

                    _initialVeggieLocalRotations[veggie] =
                        veggie.transform.localRotation;
                }
            }
        }

        public void ResetStep()
        {
            IsVeggieInPan = false;
            IsCookCompleted = false;
            IsBurned = false;

            CookProgress = 0f;
            BurnProgress = 0f;

            _isDraggingSpatulaDirectly = false;
            _isStirringThisFrame = false;

            if (clickableVeggieObjects != null)
            {
                foreach (var veggie in clickableVeggieObjects)
                {
                    if (veggie == null) continue;

                    veggie.SetActive(true);

                    if (_initialVeggieParents.TryGetValue(
                        veggie,
                        out Transform parent))
                    {
                        veggie.transform.SetParent(parent, false);
                    }

                    if (_initialVeggieLocalPositions.TryGetValue(
                        veggie,
                        out Vector3 position))
                    {
                        veggie.transform.localPosition = position;
                    }

                    if (_initialVeggieLocalRotations.TryGetValue(
                        veggie,
                        out Quaternion rotation))
                    {
                        veggie.transform.localRotation = rotation;
                    }
                }
            }

            if (spatulaTransform != null)
            {
                spatulaTransform.localPosition =
                    _initialSpatulaLocalPos;
            }

            UpdateVisualState();

            if (cookingSteamEffect != null)
            {
                cookingSteamEffect.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            if (stirAudioSource != null)
            {
                stirAudioSource.Stop();
            }
        }

        private void Update()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            // 1. 3D 야채 클릭 감지 (팬에 넣기 전)
            if (!IsVeggieInPan &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckVeggieClickHit();
            }

            // 2. UI Drag 방식이 작동하지 않는 환경을 대비한 3D 마우스 직접 드래그 모드
            HandleDirectMouseDrag();

            if (!IsVeggieInPan ||
                IsCookCompleted ||
                IsBurned)
            {
                return;
            }

            // 3. 저어주는 상태 처리
            if (_isStirringThisFrame)
            {
                CookProgress +=
                    cookRate * Time.deltaTime;

                BurnProgress = Mathf.Max(
                    0f,
                    BurnProgress -
                    (burnRate * 0.5f * Time.deltaTime)
                );

                if (cookingSteamEffect != null &&
                    !cookingSteamEffect.isPlaying)
                {
                    cookingSteamEffect.Play();
                }

                if (stirAudioSource != null &&
                    !stirAudioSource.isPlaying)
                {
                    stirAudioSource.Play();
                }
            }
            else
            {
                // 안 저으면 야채가 탐
                BurnProgress +=
                    burnRate * Time.deltaTime;

                if (stirAudioSource != null &&
                    stirAudioSource.isPlaying)
                {
                    stirAudioSource.Pause();
                }
            }

            _isStirringThisFrame = false;

            // 4. 익힘 상태 시각 반영
            UpdateVisualState();

            // 5. 완료/실패 판정
            if (BurnProgress >= 100f)
            {
                IsBurned = true;

                Debug.LogWarning(
                    "[PanStirCooking] 야채가 타버렸습니다!"
                );

                if (cookingSteamEffect != null)
                {
                    cookingSteamEffect.Stop();
                }

                if (stirAudioSource != null)
                {
                    stirAudioSource.Stop();
                }
            }
            else if (CookProgress >= requiredCookProgress)
            {
                IsCookCompleted = true;
                CookProgress = requiredCookProgress;

                Debug.Log(
                    "[PanStirCooking] 볶기 완벽 성공!"
                );

                if (cookingSteamEffect != null)
                {
                    cookingSteamEffect.Stop();
                }

                if (stirAudioSource != null)
                {
                    stirAudioSource.Stop();
                }
            }
        }

        // 3D 공간 상에서의 직접 마우스 드래그 처리
        private void HandleDirectMouseDrag()
        {
            if (Mouse.current == null ||
                !IsVeggieInPan ||
                IsCookCompleted ||
                IsBurned)
            {
                return;
            }

            Vector2 currentMousePos =
                Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _lastMousePos = currentMousePos;
                _isDraggingSpatulaDirectly = true;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _isDraggingSpatulaDirectly = false;
            }

            if (_isDraggingSpatulaDirectly &&
                Mouse.current.leftButton.isPressed)
            {
                Vector2 delta =
                    currentMousePos - _lastMousePos;

                if (delta.sqrMagnitude > 1f)
                {
                    MoveSpatula(delta);
                }

                _lastMousePos =
                    currentMousePos;
            }
        }

        // UI EventSystem IDragHandler 연동
        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastMousePos =
                eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsVeggieInPan ||
                IsCookCompleted ||
                IsBurned)
            {
                return;
            }

            Vector2 delta =
                eventData.position - _lastMousePos;

            MoveSpatula(delta);

            _lastMousePos =
                eventData.position;
        }

        // 주걱 위치 이동 공통 메서드
        private void MoveSpatula(Vector2 delta)
        {
            if (spatulaTransform == null)
            {
                return;
            }

            _isStirringThisFrame = true;

            // Delta 값을 주걱의 Local Position으로 변환 및 이동
            Vector3 newPos =
                spatulaTransform.localPosition +
                new Vector3(delta.x, 0f, delta.y) *
                spatulaSpeedMultiplier;

            // 이동 범위 제한 (Clamp)
            newPos.x = Mathf.Clamp(
                newPos.x,
                _initialSpatulaLocalPos.x - spatulaMoveRange.x,
                _initialSpatulaLocalPos.x + spatulaMoveRange.x
            );

            newPos.z = Mathf.Clamp(
                newPos.z,
                _initialSpatulaLocalPos.z - spatulaMoveRange.y,
                _initialSpatulaLocalPos.z + spatulaMoveRange.y
            );

            spatulaTransform.localPosition =
                newPos;
        }

        private void CheckVeggieClickHit()
        {
            if (_mainCamera == null)
            {
                return;
            }

            Vector2 mousePos =
                Mouse.current.position.ReadValue();

            Ray ray =
                _mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                100f))
            {
                if (clickableVeggieObjects != null &&
                    clickableVeggieObjects.Contains(
                        hit.collider.gameObject))
                {
                    OnClickVeggieToPan();
                }
            }
        }

        public void OnClickVeggieToPan()
        {
            if (IsVeggieInPan)
            {
                return;
            }

            IsVeggieInPan = true;

            if (clickableVeggieObjects != null &&
                panInsideTransform != null)
            {
                foreach (var veggie in clickableVeggieObjects)
                {
                    if (veggie != null)
                    {
                        veggie.transform.position =
                            panInsideTransform.position;

                        veggie.transform.SetParent(
                            panInsideTransform,
                            true
                        );
                    }
                }
            }

            Debug.Log(
                "[PanStirCooking] 야채가 프라이팬 내부로 투입되었습니다."
            );
        }

        private void UpdateVisualState()
        {
            if (veggieRenderer == null)
            {
                return;
            }

            float cookRatio =
                Mathf.Clamp01(
                    CookProgress /
                    requiredCookProgress
                );

            float burnRatio =
                Mathf.Clamp01(
                    BurnProgress / 100f
                );

            Color currentColor =
                Color.Lerp(
                    rawColor,
                    cookedColor,
                    cookRatio
                );

            currentColor =
                Color.Lerp(
                    currentColor,
                    burnedColor,
                    burnRatio
                );

            veggieRenderer.material.color =
                currentColor;
        }
    }
}
