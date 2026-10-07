using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace Marea.Cooking
{
    public class SeafoodSkewersGrill : MonoBehaviour
    {
        [Header("UI 연동")]
        [SerializeField] private Slider donenessSlider;
        [SerializeField] private RectTransform targetZoneRect;
        [SerializeField] private Image sliderFillImage;
        [SerializeField] private TextMeshProUGUI txtStatus;

        [Header("3D 및 연출")]
        [SerializeField] private GameObject skewer3DModel;
        [SerializeField] private ParticleSystem smokeEffect;
        [Header("꼬치 클릭 영역")]
        [SerializeField, Min(0f)] private float skewerClickPadding = 48f;

        [Header("굽기 설정")]
        [SerializeField] private float cookSpeed = 0.25f;
        [SerializeField] private float targetFrontMin = 0.4f;
        [SerializeField] private float targetFrontMax = 0.6f;
        [SerializeField] private float targetBackMin = 0.4f;
        [SerializeField] private float targetBackMax = 0.6f;

        [Header("뒤집기 애니메이션 설정")]
        [SerializeField] private float flipDuration = 0.4f; // 뒤집히는 시간 (초)
        [SerializeField] private float liftHeight = 0.4f;   // 위로 들리는 높이

        // 앞면(시작) 회전 및 위치값
        private readonly Vector3 _frontPosition = new Vector3(1.7679f, 0.6888f, -2.922f);
        private readonly Quaternion _frontRotation = Quaternion.Euler(-90f, 0f, 0f);

        // 뒷면(뒤집음) 회전 및 위치값
        private readonly Vector3 _backPosition = new Vector3(1.8352f, 0.6396f, -2.922f);
        private readonly Quaternion _backRotation = Quaternion.Euler(90f, -90f, 90f);

        private float _frontDoneness;
        private float _backDoneness;
        private bool _isFlipped;
        private bool _isCooking;
        private bool _isCompleted;
        private bool _isAnimating; // 애니메이션 진행 중 중복 클릭 방지
        private Camera _mainCamera;

        public bool IsCompleted => _isCompleted;
        public float FinalGrillScore { get; private set; }

        public event Action OnGrillCompleted;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public void ResetGrill()
        {
            StopAllCoroutines();

            _frontDoneness = 0f;
            _backDoneness = 0f;
            _isFlipped = false;
            _isCooking = true;
            _isCompleted = false;
            _isAnimating = false;
            FinalGrillScore = 0f;

            if (donenessSlider != null) donenessSlider.value = 0f;

            if (skewer3DModel != null)
            {
                skewer3DModel.transform.localPosition = _frontPosition;
                skewer3DModel.transform.localRotation = _frontRotation;
            }

            if (smokeEffect != null) smokeEffect.Play();

            SetupTargetZoneUI();
            UpdateUI();
        }

        public void ResetGrillState()
        {
            StopAllCoroutines();

            _frontDoneness = 0f;
            _backDoneness = 0f;
            _isFlipped = false;
            _isCooking = false;
            _isCompleted = false;
            _isAnimating = false;
            FinalGrillScore = 0f;

            if (donenessSlider != null) donenessSlider.value = 0f;

            if (skewer3DModel != null)
            {
                skewer3DModel.transform.localPosition = _frontPosition;
                skewer3DModel.transform.localRotation = _frontRotation;
            }

            if (smokeEffect != null)
            {
                smokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetupTargetZoneUI();
            UpdateGaugeColor();
            UpdateUI();
        }

        private void SetupTargetZoneUI()
        {
            if (donenessSlider == null || targetZoneRect == null) return;

            RectTransform sliderRect = donenessSlider.GetComponent<RectTransform>();
            if (sliderRect == null) return;

            float sliderWidth = sliderRect.rect.width;
            float minRatio = !_isFlipped ? targetFrontMin : targetBackMin;
            float maxRatio = !_isFlipped ? targetFrontMax : targetBackMax;

            float zoneWidth = sliderWidth * (maxRatio - minRatio);
            float startX = (sliderWidth * minRatio) - (sliderWidth * 0.5f) + (zoneWidth * 0.5f);

            targetZoneRect.sizeDelta = new Vector2(zoneWidth, sliderRect.rect.height);
            targetZoneRect.anchoredPosition = new Vector2(startX, 0f);
        }

        private void Update()
        {
            if (!_isCooking || _isCompleted) return;

            // 시간에 따른 익힘도 증가 (애니메이션 연출 중에는 잠시 멈춤)
            if (!_isAnimating)
            {
                if (!_isFlipped)
                {
                    _frontDoneness = Mathf.Clamp01(_frontDoneness + cookSpeed * Time.deltaTime);
                    if (donenessSlider != null) donenessSlider.value = _frontDoneness;
                }
                else
                {
                    _backDoneness = Mathf.Clamp01(_backDoneness + cookSpeed * Time.deltaTime);
                    if (donenessSlider != null) donenessSlider.value = _backDoneness;
                }
            }

            UpdateGaugeColor();

            // 마우스 클릭 시 꼬치 선택 감지
            if (!_isAnimating && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckSkewerClick();
            }

            // 1.0 도달 시 (태웠을 때) 조기 완료/실패 처리
            if ((!_isFlipped && _frontDoneness >= 1.0f) || (_isFlipped && _backDoneness >= 1.0f))
            {
                FinishGrill();
            }
        }

        private void CheckSkewerClick()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || skewer3DModel == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (CookingClickArea.Contains(_mainCamera, skewer3DModel, mousePos, skewerClickPadding, out _))
                OnSkewerClicked();
        }

        private void OnSkewerClicked()
        {
            if (!_isCooking || _isCompleted || _isAnimating) return;

            if (!_isFlipped)
            {
                // 1번째 클릭: 뒷면으로 들려서 뒤집기
                _isFlipped = true;
                StartCoroutine(AnimateFlipCoroutine(_frontPosition, _backPosition, _frontRotation, _backRotation, isFinalFinish: false));
            }
            else
            {
                // 2번째 클릭: 다시 들려서 뒤집히며 완성
                StartCoroutine(AnimateFlipCoroutine(_backPosition, _frontPosition, _backRotation, _frontRotation, isFinalFinish: true));
            }
        }

        // 꼬치를 위로 들었다가 뒤집고 다시 내리는 부드러운 애니메이션 코루틴
        private IEnumerator AnimateFlipCoroutine(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, bool isFinalFinish)
        {
            _isAnimating = true;

            float elapsedTime = 0f;

            while (elapsedTime < flipDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / flipDuration);

                // sin 곡선을 통해 중앙(t = 0.5)에서 최고 높이(liftHeight)까지 올라갔다 착지
                float arcY = Mathf.Sin(t * Mathf.PI) * liftHeight;

                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                currentPos.y += arcY;

                Quaternion currentRot = Quaternion.Slerp(startRot, endRot, t);

                if (skewer3DModel != null)
                {
                    skewer3DModel.transform.localPosition = currentPos;
                    skewer3DModel.transform.localRotation = currentRot;
                }

                yield return null;
            }

            // 최종 위치 및 회전 보정
            if (skewer3DModel != null)
            {
                skewer3DModel.transform.localPosition = endPos;
                skewer3DModel.transform.localRotation = endRot;
            }

            _isAnimating = false;

            if (isFinalFinish)
            {
                Debug.Log($"[SeafoodSkewersGrill] 2번째 클릭 완료! 꼬치 완성 연출 후 미니게임 완결 (뒷면 익힘도: {_backDoneness:F2})");
                FinishGrill();
            }
            else
            {
                SetupTargetZoneUI();
                UpdateUI();
                Debug.Log($"[SeafoodSkewersGrill] 1번째 클릭 뒤집기 애니메이션 완료 (앞면 익힘도: {_frontDoneness:F2})");
            }
        }

        private void FinishGrill()
        {
            _isCooking = false;
            _isCompleted = true;

            if (smokeEffect != null) smokeEffect.Stop();

            float frontScore = EvaluateSideScore(_frontDoneness, targetFrontMin, targetFrontMax);
            float backScore = EvaluateSideScore(_backDoneness, targetBackMin, targetBackMax);

            FinalGrillScore = (frontScore + backScore) * 0.5f;

            Debug.Log($"[SeafoodSkewersGrill] 3단계 완료! 앞면: {_frontDoneness:F2}, 뒷면: {_backDoneness:F2}, 최종점수: {FinalGrillScore:F2}");

            OnGrillCompleted?.Invoke();
        }

        private float EvaluateSideScore(float doneness, float minTarget, float maxTarget)
        {
            if (doneness >= minTarget && doneness <= maxTarget) return 1.0f;
            if (doneness > maxTarget && doneness < 0.9f) return 0.7f;
            if (doneness < minTarget && doneness >= 0.2f) return 0.6f;
            return 0.3f;
        }

        private void UpdateGaugeColor()
        {
            if (sliderFillImage == null) return;

            float current = _isFlipped ? _backDoneness : _frontDoneness;

            if (current < targetFrontMin)
            {
                sliderFillImage.color = Color.yellow;
            }
            else if (current <= targetFrontMax)
            {
                sliderFillImage.color = Color.green;
            }
            else
            {
                sliderFillImage.color = Color.red;
            }
        }

        private void UpdateUI()
        {
            if (txtStatus != null)
            {
                txtStatus.text = !_isFlipped ? "앞면 굽는 중... (Target 영역에서 꼬치를 클릭해 뒤집으세요!)" : "뒷면 굽는 중... (Target 영역에서 꼬치를 클릭해 완성하세요!)";
            }
        }
    }
}
