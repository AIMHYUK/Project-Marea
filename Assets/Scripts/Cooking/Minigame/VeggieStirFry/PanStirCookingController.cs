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

        [Header("팬에 넣을 재료 클릭 영역")]
        [Tooltip("재료 모델 가장자리에 추가할 클릭 여유 (1920×1080 기준 픽셀)")]
        [SerializeField, Min(0f)] private float veggieClickPadding = 48f;

        [Header("주걱 수평 이동 제한")]
        [SerializeField] private Vector2 spatulaMoveRange = new Vector2(1.5f, 1.5f); // 팬 안에서 주걱이 이동할 X, Z 범위
        [SerializeField] private float spatulaSpeedMultiplier = 0.005f;
        [Tooltip("이동 방향을 향해 회전하는 속도(초당 각도).")]
        [SerializeField, Min(0f)] private float spatulaTurnSpeed = 360f;
        [Tooltip("초기 회전 기준 X축 앞뒤 기울기 최대 각도.")]
        [SerializeField, Range(0f, 45f)] private float spatulaPitchAngle = 20f;
        [Tooltip("초기 회전 기준 Z축 좌우 기울기 최대 각도.")]
        [SerializeField, Range(0f, 20f)] private float spatulaRollAngle = 15f;

        [Header("팬 안 재료 움직임")]
        [Tooltip("팬 내부 중심부터 가장자리까지의 반지름 (월드 단위). 재료 크기를 제외한 범위 안에서 움직입니다.")]
        [InspectorName("재료 이동 반지름")]
        [SerializeField, Min(0.01f)] private float ingredientMoveRadius = 0.65f;
        [Header("주걱 접촉 밀기")]
        [Tooltip("주걱 끝부분의 접촉 지점. 비워 두면 모델의 아래쪽 끝을 사용합니다.")]
        [SerializeField] private Transform spatulaContactPoint;
        [InspectorName("주걱 접촉 반지름")]
        [SerializeField, Min(0.01f)] private float spatulaContactRadius = 0.12f;
        [InspectorName("재료 밀기 속도")]
        [SerializeField, Min(0f)] private float ingredientPushSpeed = 0.4f;
        [InspectorName("최대 밀림 거리")]
        [SerializeField, Min(0f)] private float ingredientMaxPushDistance = 0.12f;
        [Tooltip("팬 이동 반경 중 재료 사이 간격에 사용할 비율. 높일수록 재료가 넓게 배치됩니다.")]
        [SerializeField, Range(0f, 0.8f)] private float ingredientSpreadRatio = 0.5f;
        [SerializeField, Min(0f)] private float ingredientFollowMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float ingredientTurnDegreesPerUnit = 70f;
        [Tooltip("재료마다 다르게 움직일 수 있는 거리 (팬 반경에 대한 비율).")]
        [SerializeField, Range(0f, 0.2f)] private float ingredientMotionVariation = 0.12f;
        [Tooltip("재료가 주걱 움직임을 따라가는 반응 속도. 각 재료에 무작위 편차가 적용됩니다.")]
        [SerializeField, Min(0.1f)] private float ingredientResponseSpeed = 10f;

        [Header("재료별 조리 시간과 점수")]
        [Tooltip("각 재료를 실제로 저어야 하는 시간. 이 시간을 채우면 기본 점수가 만점입니다.")]
        [InspectorName("재료별 필요 볶기 시간 (초)")]
        [SerializeField, Min(0.1f)] private float requiredIngredientStirSeconds = 4f;
        [Tooltip("투입 후 이 시간까지는 감점하지 않습니다. 젓지 않는 시간도 포함됩니다.")]
        [InspectorName("감점 시작 체류 시간 (초)")]
        [SerializeField, Min(0f)] private float ingredientSafePanSeconds = 12f;
        [Tooltip("체류 시간을 초과한 재료의 점수에서 매초 감점하는 양 (100점 기준).")]
        [InspectorName("시간 초과 감점 (점/초)")]
        [SerializeField, Range(0.1f, 100f)] private float ingredientOvertimePenaltyPerSecond = 10f;

        [Header("남은 시간 게이지")]
        [Tooltip("첫 재료를 넣은 뒤 이 시간이 지나면 볶기를 종료하고 다음 단계로 넘어갑니다.")]
        [InspectorName("2단계 진행 시간 (초)")]
        [SerializeField, Min(0.1f)] private float stageDurationSeconds = 8f;
        [SerializeField] private UnityEngine.UI.Image remainingTimeFill;
        private float _stageElapsedSeconds;

        [Header("시각 연출 (Material & Particle)")]
        [SerializeField] private Renderer veggieRenderer;           // 익힘/탄 재질을 적용할 렌더러
        [SerializeField] private Color rawColor = Color.white;      // 날것 색상
        [SerializeField] private Color cookedColor = new Color(1f, 0.8f, 0.4f); // 노릇노릇하게 익은 색상
        [SerializeField] private Color burnedColor = Color.black;   // 탄 색상
        [SerializeField] private ParticleSystem cookingSteamEffect; // 조리 시 연기 이펙트
        [SerializeField] private AudioSource stirAudioSource;       // 볶는 소리 사운드
        [SerializeField] private AudioSource ingredientInsertAudioSource;

        public bool IsVeggieInPan { get; private set; }
        public bool IsCookCompleted { get; private set; }
        public bool IsBurned { get; private set; }
        public float CookProgress { get; private set; }
        public float BurnProgress { get; private set; }
        public float CookingScore { get; private set; }

        private Vector3 _initialSpatulaLocalPos;
        private Quaternion _initialSpatulaLocalRotation;
        private Quaternion _referenceSpatulaWorldRotation;
        private Quaternion _targetSpatulaWorldRotation;
        private float _spatulaWorldHeight;
        private Vector3 _initialSpatulaWorldPosition;
        private Vector2 _lastMousePos;
        private bool _isStirringThisFrame;
        private bool _isDraggingSpatulaDirectly;
        private Camera _mainCamera;
        private readonly Dictionary<GameObject, Vector3> _panIngredientOffsets =
            new Dictionary<GameObject, Vector3>();
        private Vector3 _panGroupOffset;
        private Vector3 _spatulaContactLocalPoint;
        private sealed class IngredientMotion
        {
            public float FollowScale;
            public float ResponseScale;
            public float TurnScale;
            public float Phase;
            public float Frequency;
            public Vector2 InitialWave;
            public Vector2 Drift;
            public Vector3 Offset;
            public Vector3 PushOffset;
            public float StirSeconds;
            public float PanSeconds;
            public float Penalty;
        }
        private readonly Dictionary<GameObject, IngredientMotion> _ingredientMotions =
            new Dictionary<GameObject, IngredientMotion>();

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
                _initialSpatulaLocalRotation = spatulaTransform.localRotation;
                ResetSpatulaPose();
                // Cache the working end so the contact point rotates with the spatula model.
                Vector3 tip = spatulaTransform.position;
                float lowestHeight = float.PositiveInfinity;
                foreach (Renderer renderer in spatulaTransform.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.bounds.min.y >= lowestHeight) continue;
                    lowestHeight = renderer.bounds.min.y;
                    tip = renderer.bounds.center;
                    tip.y = lowestHeight;
                }
                _spatulaContactLocalPoint = spatulaTransform.InverseTransformPoint(tip);
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
            UpdateRemainingTimeGauge();
        }

        public void ResetStep()
        {
            IsVeggieInPan = false;
            IsCookCompleted = false;
            IsBurned = false;

            CookProgress = 0f;
            BurnProgress = 0f;
            CookingScore = 0f;
            _stageElapsedSeconds = 0f;

            _isDraggingSpatulaDirectly = false;
            _isStirringThisFrame = false;
            _panIngredientOffsets.Clear();
            _ingredientMotions.Clear();
            UpdateRemainingTimeGauge();
            _panGroupOffset = Vector3.zero;

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
                spatulaTransform.localRotation = _initialSpatulaLocalRotation;
                ResetSpatulaPose();
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
            if (ingredientInsertAudioSource != null) ingredientInsertAudioSource.Stop();
        }

        private void Update()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            // 3D 야채 클릭 감지 (팬에 넣기 전)
            if (!IsCookCompleted &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckVeggieClickHit();
            }

            // UI Drag 방식이 작동하지 않는 환경을 대비한 3D 마우스 직접 드래그 모드
            HandleDirectMouseDrag();
            UpdateSpatulaPose();

            if (!IsVeggieInPan ||
                IsCookCompleted ||
                IsBurned)
            {
                return;
            }

            // 저어주는 상태 처리
            UpdateIngredientPositions();
            if (_isStirringThisFrame)
            {
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
                if (stirAudioSource != null &&
                    stirAudioSource.isPlaying)
                {
                    stirAudioSource.Pause();
                }
            }

            float cookingDelta = Mathf.Min(Time.deltaTime,
                Mathf.Max(0f, stageDurationSeconds - _stageElapsedSeconds));
            _stageElapsedSeconds += cookingDelta;
            UpdateIngredientCooking(cookingDelta, _isStirringThisFrame);
            UpdateRemainingTimeGauge();
            _isStirringThisFrame = false;

            // 익힘 상태 시각 반영
            UpdateVisualState();

            if (IsCookCompleted)
            {
                _isDraggingSpatulaDirectly = false;
                Debug.Log($"[PanStirCooking] 모든 재료 조리 종료. 점수: {CookingScore:P0}");

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

        private void UpdateIngredientCooking(float deltaTime, bool stirring)
        {
            float requiredSeconds = Mathf.Max(0.1f, requiredIngredientStirSeconds);
            float totalCookRatio = 0f;
            float totalPenalty = 0f;
            float totalScore = 0f;
            int ingredientCount = 0;
            int burnedCount = 0;

            // Iterate all recipe ingredients, including ones not yet inserted, so they cannot be skipped.
            foreach (var entry in _initialVeggieParents)
            {
                if (entry.Key == null) continue;
                ingredientCount++;
                if (!_ingredientMotions.TryGetValue(entry.Key, out IngredientMotion motion)) continue;

                motion.PanSeconds += deltaTime;
                if (stirring && motion.Penalty < 1f)
                    motion.StirSeconds = Mathf.Min(requiredSeconds, motion.StirSeconds + deltaTime);

                // Overcooking is irreversible: stirring again does not remove the time penalty.
                float overtime = Mathf.Max(0f, motion.PanSeconds - ingredientSafePanSeconds);
                motion.Penalty = Mathf.Max(motion.Penalty,
                    Mathf.Clamp01(overtime * ingredientOvertimePenaltyPerSecond / 100f));
                float cookRatio = Mathf.Clamp01(motion.StirSeconds / requiredSeconds);
                totalCookRatio += cookRatio;
                totalPenalty += motion.Penalty;
                totalScore += cookRatio * (1f - motion.Penalty);
                if (motion.Penalty >= 1f) burnedCount++;
            }

            if (ingredientCount == 0) return;
            CookProgress = totalCookRatio / ingredientCount * 100f;
            BurnProgress = totalPenalty / ingredientCount * 100f;
            CookingScore = Mathf.Clamp01(totalScore / ingredientCount);
            IsCookCompleted = _stageElapsedSeconds >= stageDurationSeconds;
            IsBurned = IsCookCompleted && burnedCount == ingredientCount;
        }

        private void UpdateRemainingTimeGauge()
        {
            float duration = Mathf.Max(0.1f, stageDurationSeconds);
            float ratio = Mathf.Clamp01(1f - _stageElapsedSeconds / duration);
            if (remainingTimeFill != null)
            {
                remainingTimeFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                remainingTimeFill.color = ratio > 0.5f ? new Color(0.18f, 0.55f, 0.35f)
                    : ratio > 0.25f ? new Color(1f, 0.78f, 0.22f) : new Color(0.9f, 0.3f, 0.2f);
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

            // The model parent can be tilted, so use the horizontal world plane.
            Vector3 previousWorldPosition = spatulaTransform.position;
            Vector3 right = _mainCamera != null
                ? Vector3.ProjectOnPlane(_mainCamera.transform.right, Vector3.up).normalized : Vector3.right;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
            Vector3 scale = spatulaTransform.parent != null ? spatulaTransform.parent.lossyScale : Vector3.one;
            float scaleX = Mathf.Abs(scale.x);
            float scaleZ = Mathf.Abs(scale.z);
            Vector3 offset = previousWorldPosition - _initialSpatulaWorldPosition;
            float side = Mathf.Clamp(Vector3.Dot(offset, right) + delta.x * spatulaSpeedMultiplier * scaleX,
                -spatulaMoveRange.x * scaleX, spatulaMoveRange.x * scaleX);
            float depth = Mathf.Clamp(Vector3.Dot(offset, forward) + delta.y * spatulaSpeedMultiplier * scaleZ,
                -spatulaMoveRange.y * scaleZ, spatulaMoveRange.y * scaleZ);
            Vector3 position = _initialSpatulaWorldPosition + right * side + forward * depth;
            position.y = _spatulaWorldHeight;
            spatulaTransform.position = position;

            Vector3 movement = spatulaTransform.position - previousWorldPosition;
            movement.y = 0f;
            if (movement.sqrMagnitude <= 0.000001f) return;
            Vector3 direction = movement.normalized;
            Vector3 rotationOffset = new Vector3(
                -Vector3.Dot(direction, forward) * spatulaPitchAngle,
                0f,
                -Vector3.Dot(direction, right) * spatulaRollAngle);
            _targetSpatulaWorldRotation = Quaternion.Euler(_referenceSpatulaWorldRotation.eulerAngles + rotationOffset);
            _isStirringThisFrame = true;
            MoveIngredientsWithSpatula(movement);
        }

        private void ResetSpatulaPose()
        {
            _spatulaWorldHeight = spatulaTransform.position.y;
            _initialSpatulaWorldPosition = spatulaTransform.position;
            _referenceSpatulaWorldRotation = spatulaTransform.rotation;
            _targetSpatulaWorldRotation = _referenceSpatulaWorldRotation;
        }

        private void UpdateSpatulaPose()
        {
            if (spatulaTransform == null || !IsVeggieInPan) return;
            Vector3 position = spatulaTransform.position;
            position.y = _spatulaWorldHeight;
            spatulaTransform.position = position;
            if (IsCookCompleted || IsBurned) return;
            // Front/back movement tilts X; sideways movement tilts Z.
            spatulaTransform.rotation = Quaternion.RotateTowards(
                spatulaTransform.rotation, _targetSpatulaWorldRotation, spatulaTurnSpeed * Time.deltaTime);
        }

        private void MoveIngredientsWithSpatula(Vector3 worldMovement)
        {
            if (panInsideTransform == null || clickableVeggieObjects == null) return;

            Vector3 movement = panInsideTransform.InverseTransformVector(worldMovement) * ingredientFollowMultiplier;
            movement.y = 0f;
            Vector3 panScale = panInsideTransform.lossyScale;
            float radiusX = ingredientMoveRadius / Mathf.Max(0.001f, Mathf.Abs(panScale.x));
            float radiusZ = ingredientMoveRadius / Mathf.Max(0.001f, Mathf.Abs(panScale.z));

            // Move the group within a smaller ellipse so individual offsets never collapse at the rim.
            float groupRange = Mathf.Max(0.001f, 1f - ingredientSpreadRatio - ingredientMotionVariation);
            _panGroupOffset += movement;
            Vector2 normalized = new Vector2(
                _panGroupOffset.x / (radiusX * groupRange),
                _panGroupOffset.z / (radiusZ * groupRange));
            if (normalized.sqrMagnitude > 1f)
            {
                normalized.Normalize();
                _panGroupOffset.x = normalized.x * radiusX * groupRange;
                _panGroupOffset.z = normalized.y * radiusZ * groupRange;
            }

            foreach (var entry in _panIngredientOffsets)
            {
                GameObject veggie = entry.Key;
                if (veggie == null || !veggie.activeInHierarchy || veggie.transform.parent != panInsideTransform) continue;
                if (!_ingredientMotions.TryGetValue(veggie, out IngredientMotion motion)) continue;

                // Sample random values once on insertion, then vary smoothly with actual stirring distance.
                float distance = movement.magnitude;
                Vector2 normalizedMovement = new Vector2(movement.x / radiusX, movement.z / radiusZ);
                motion.Drift = Vector2.ClampMagnitude(
                    motion.Drift * Mathf.Exp(-distance * 3f) + normalizedMovement * (motion.FollowScale - 1f),
                    ingredientMotionVariation * 0.5f);
                float previousWave = Mathf.Sin(motion.Phase);
                motion.Phase += distance * motion.Frequency;
                Vector2 wave = new Vector2(Mathf.Sin(motion.Phase), Mathf.Cos(motion.Phase * 0.83f));
                Vector2 variation = Vector2.ClampMagnitude(
                    motion.Drift + (wave - motion.InitialWave) * (ingredientMotionVariation * 0.25f),
                    ingredientMotionVariation);
                motion.Offset = new Vector3(variation.x * radiusX, 0f, variation.y * radiusZ);
                float turn = (movement.x - movement.z) * ingredientTurnDegreesPerUnit * motion.TurnScale
                    + (Mathf.Sin(motion.Phase) - previousWave) * 5f;
                veggie.transform.localRotation = Quaternion.AngleAxis(turn, Vector3.up) * veggie.transform.localRotation;
            }
        }

        private void UpdateIngredientPositions()
        {
            foreach (var entry in _panIngredientOffsets)
            {
                GameObject veggie = entry.Key;
                if (veggie == null || !veggie.activeInHierarchy || veggie.transform.parent != panInsideTransform ||
                    !_ingredientMotions.TryGetValue(veggie, out IngredientMotion motion)) continue;
                Vector3 target = _panGroupOffset + entry.Value + motion.Offset;
                if (_isStirringThisFrame && spatulaTransform != null)
                {
                    Bounds bounds = GetIngredientBounds(veggie);
                    Vector3 contact = spatulaContactPoint != null ? spatulaContactPoint.position
                        : spatulaTransform.TransformPoint(_spatulaContactLocalPoint);
                    Vector3 away = bounds.center - contact;
                    float heightDifference = Mathf.Abs(away.y);
                    away.y = 0f;
                    float distance = away.magnitude;
                    float contactRange = spatulaContactRadius + GetFootprintRadius(bounds);
                    motion.PushOffset *= Mathf.Exp(-Time.deltaTime * 0.5f);
                    if (distance < contactRange && heightDifference < bounds.extents.y + spatulaContactRadius)
                    {
                        if (distance < 0.001f)
                        {
                            away = bounds.center - panInsideTransform.position;
                            away.y = 0f;
                            if (away.sqrMagnitude < 0.000001f) away = Vector3.right;
                        }
                        float strength = Mathf.Clamp01((contactRange - distance) / Mathf.Max(0.001f, contactRange));
                        Vector3 push = away.normalized * ingredientPushSpeed * strength * Time.deltaTime;
                        Vector3 worldOffset = panInsideTransform.TransformVector(motion.PushOffset) + push;
                        motion.PushOffset = panInsideTransform.InverseTransformVector(
                            Vector3.ClampMagnitude(worldOffset, ingredientMaxPushDistance));
                    }
                }
                target += motion.PushOffset;
                float blend = 1f - Mathf.Exp(-ingredientResponseSpeed * motion.ResponseScale * Time.deltaTime);
                veggie.transform.localPosition = Vector3.Lerp(veggie.transform.localPosition, target, blend);
                ClampIngredientToPan(veggie);
            }
        }

        private static Bounds GetIngredientBounds(GameObject veggie)
        {
            Bounds bounds = new Bounds(veggie.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in veggie.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static float GetFootprintRadius(Bounds bounds)
        {
            return new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        }

        private void ClampIngredientToPan(GameObject veggie)
        {
            Bounds bounds = GetIngredientBounds(veggie);
            Vector3 offset = bounds.center - panInsideTransform.position;
            offset.y = 0f;
            float availableRadius = Mathf.Max(0f, ingredientMoveRadius - GetFootprintRadius(bounds));
            Vector3 correction = Vector3.ClampMagnitude(offset, availableRadius) - offset;
            // Correct the visible mesh center, since some imported models have an offset pivot.
            veggie.transform.position += correction;
        }

        private void CheckVeggieClickHit()
        {
            if (_mainCamera == null)
            {
                return;
            }

            Vector2 mousePos =
                Mouse.current.position.ReadValue();

            if (clickableVeggieObjects == null) return;
            GameObject closestVeggie = null;
            float closestScore = float.PositiveInfinity;
            foreach (var veggie in clickableVeggieObjects)
            {
                if (veggie == null || _panIngredientOffsets.ContainsKey(veggie)) continue;
                if (CookingClickArea.Contains(_mainCamera, veggie, mousePos, veggieClickPadding, out float score)
                    && score < closestScore)
                {
                    closestVeggie = veggie;
                    closestScore = score;
                }
            }
            if (closestVeggie != null) InsertVeggie(closestVeggie);
        }

        public void OnClickVeggieToPan()
        {
            if (IsCookCompleted || clickableVeggieObjects == null) return;
            // Keep existing UnityEvent bindings, but insert only one remaining ingredient per call.
            foreach (var veggie in clickableVeggieObjects)
            {
                if (veggie != null && !_panIngredientOffsets.ContainsKey(veggie))
                {
                    InsertVeggie(veggie);
                    return;
                }
            }
        }

        private void InsertVeggie(GameObject veggie)
        {
            if (panInsideTransform == null || veggie == null ||
                IsCookCompleted || IsBurned || _panIngredientOffsets.ContainsKey(veggie)) return;

            var ingredients = new List<GameObject>();
            foreach (var candidate in clickableVeggieObjects)
                if (candidate != null && !ingredients.Contains(candidate)) ingredients.Add(candidate);
            int index = ingredients.IndexOf(veggie);
            if (index < 0) return;

            float angle = index * Mathf.PI * 2f / ingredients.Count + Mathf.PI * 0.25f;
            Vector3 panScale = panInsideTransform.lossyScale;
            Vector3 offset = ingredients.Count == 1 ? Vector3.zero : new Vector3(
                Mathf.Cos(angle) * ingredientMoveRadius / Mathf.Max(0.001f, Mathf.Abs(panScale.x)) * ingredientSpreadRatio,
                0f,
                Mathf.Sin(angle) * ingredientMoveRadius / Mathf.Max(0.001f, Mathf.Abs(panScale.z)) * ingredientSpreadRatio);
            veggie.transform.SetParent(panInsideTransform, true);
            veggie.transform.localPosition = _panGroupOffset + offset;
            ClampIngredientToPan(veggie);
            _panIngredientOffsets.Add(veggie, offset);
            float phase = Random.Range(0f, Mathf.PI * 2f);
            _ingredientMotions.Add(veggie, new IngredientMotion
            {
                FollowScale = Random.Range(0.75f, 1.25f),
                ResponseScale = Random.Range(0.7f, 1.3f),
                TurnScale = Random.Range(0.65f, 1.35f),
                Phase = phase,
                Frequency = Random.Range(6f, 10f),
                InitialWave = new Vector2(Mathf.Sin(phase), Mathf.Cos(phase * 0.83f))
            });

            // Any inserted ingredient can be stirred while the remaining ingredients are still waiting.
            IsVeggieInPan = _panIngredientOffsets.Count > 0;
            UpdateRemainingTimeGauge();

            Debug.Log(
                "[PanStirCooking] 야채가 프라이팬 내부로 투입되었습니다."
            );
            if (panInsideTransform != null && ingredientInsertAudioSource != null &&
                ingredientInsertAudioSource.clip != null)
                ingredientInsertAudioSource.PlayOneShot(ingredientInsertAudioSource.clip);
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
                    100f
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
