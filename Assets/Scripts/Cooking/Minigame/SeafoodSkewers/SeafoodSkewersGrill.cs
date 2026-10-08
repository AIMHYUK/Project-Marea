using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Marea.Core;
using Marea.Data;

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
        [SerializeField] private VfxId grillFlameVfx = VfxId.Flame;
        [SerializeField] private Transform flameAnchor;
        private VfxLoop _grillFlame;
        [Header("꼬치 배치와 클릭")]
        [SerializeField, Min(0.05f)] private float skewerSpacing = 0.5f;
        [SerializeField] private float skewerRotationX = -85.5f;
        [SerializeField, Min(0f)] private float skewerClickPadding = 48f;
        [Header("공용 굽기 타이밍")]
        [SerializeField] private float cookSpeed = 0.25f;
        [SerializeField] private float targetFrontMin = 0.4f;
        [SerializeField] private float targetFrontMax = 0.6f;
        [SerializeField] private float targetBackMin = 0.4f;
        [SerializeField] private float targetBackMax = 0.6f;
        [Header("뒤집기 애니메이션")]
        [SerializeField] private float flipDuration = 0.4f;
        [SerializeField] private float liftHeight = 0.4f;
        [Header("꼬치 뒤집기 효과음")]
        [SerializeField] private AudioClip flipAudioClip;
        [SerializeField] private AudioSource flipAudioSource;
        [Header("3단계 그릴 크기")]
        [Tooltip("씬의 그릴 model (2). 비워두면 현재 요리 위치에 가장 가까운 model (2)를 찾습니다.")]
        [SerializeField] private Transform grillTransform;
        [SerializeField] private float stageGrillScaleX = 2.45f;
        private Vector3 _originalGrillScale;
        private bool _grillScaleChanged;

        private sealed class SkewerState
        {
            public GameObject Model;
            public Vector3 Position;
            public Quaternion Rotation;
            public int FlipCount;
            public bool Animating;
            public float ScoreSum;
        }
        private readonly List<SkewerState> _skewers = new();
        private Vector3 _templatePosition;
        private Quaternion _templateRotation;
        private bool _templateCached;
        private float _doneness;
        private int _flippedCount;
        private int _animatingCount;
        private int _round;
        private float TargetMin => _round == 0 ? targetFrontMin : targetBackMin;
        private float TargetMax => _round == 0 ? targetFrontMax : targetBackMax;
        private bool _isCooking;
        private bool _isCompleted;
        private Camera _mainCamera;
        public bool IsCompleted => _isCompleted;
        public float FinalGrillScore { get; private set; }
        public event Action OnGrillCompleted;

        public void ResetGrill()
        {
            ResetGrillState();
            if (skewer3DModel == null) return;
            _mainCamera = Camera.main;
            ExpandGrill();
            Transform parent = skewer3DModel.transform.parent;
            Vector3 axis = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            for (int i = 0; i < 3; i++)
            {
                GameObject model = i == 1 ? skewer3DModel : Instantiate(skewer3DModel, parent);
                if (i != 1) model.name = "GrillSkewer_" + (i + 1);
                model.SetActive(true);
                Vector3 displacement = axis * ((i - 1) * skewerSpacing);
                Vector3 offset = parent != null ? parent.InverseTransformVector(displacement) : displacement;
                model.transform.localPosition = _templatePosition + offset;
                model.transform.localRotation = _templateRotation;
                _skewers.Add(new SkewerState { Model = model, Position = model.transform.localPosition,
                    Rotation = model.transform.localRotation });
            }
            _isCooking = true;
            UpdateSmokePlacement();
            if (smokeEffect != null) smokeEffect.Play();
            if (flameAnchor != null) _grillFlame = Vfx.PlayLoop(grillFlameVfx, flameAnchor, Vector3.zero);
            SetupTargetZoneUI();
            UpdateUI();
        }

        public void ResetGrillState()
        {
            StopAllCoroutines();
            ClearSkewers();
            RestoreGrillScale();
            _doneness = 0f;
            _flippedCount = _animatingCount = 0;
            _round = 0;
            _isCooking = _isCompleted = false;
            FinalGrillScore = 0f;
            if (donenessSlider != null) donenessSlider.value = 0f;
            StopEffects();
            SetupTargetZoneUI();
            UpdateGaugeColor();
            UpdateUI();
        }

        private void ClearSkewers()
        {
            foreach (SkewerState skewer in _skewers)
                if (skewer.Model != null && skewer.Model != skewer3DModel)
                {
                    skewer.Model.SetActive(false);
                    Destroy(skewer.Model);
                }
            _skewers.Clear();
            if (skewer3DModel == null) return;
            if (!_templateCached)
            {
                _templatePosition = skewer3DModel.transform.localPosition;
                _templateRotation = skewer3DModel.transform.localRotation;
                _templateCached = true;
            }
            Vector3 rotation = _templateRotation.eulerAngles;
            rotation.x = skewerRotationX;
            _templateRotation = Quaternion.Euler(rotation);
            skewer3DModel.transform.localPosition = _templatePosition;
            skewer3DModel.transform.localRotation = _templateRotation;
        }

        private void ExpandGrill()
        {
            // A scene may contain an empty model (2) at exactly the same position as the visible grill.
            if (grillTransform != null && grillTransform.GetComponentInChildren<MeshRenderer>(true) == null)
                grillTransform = null;
            if (grillTransform == null)
            {
                Vector3 center = flameAnchor != null ? flameAnchor.position : skewer3DModel.transform.position;
                float nearest = 25f;
                foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.name != "model (2)" || candidate.gameObject.scene != gameObject.scene
                        || candidate.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                    float distance = (candidate.position - center).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    grillTransform = candidate;
                }
            }
            if (grillTransform == null) return;
            _originalGrillScale = grillTransform.localScale;
            Vector3 scale = _originalGrillScale;
            scale.x = stageGrillScaleX;
            grillTransform.localScale = scale;
            _grillScaleChanged = true;
        }

        private void RestoreGrillScale()
        {
            if (_grillScaleChanged && grillTransform != null) grillTransform.localScale = _originalGrillScale;
            _grillScaleChanged = false;
        }

        private void SetupTargetZoneUI()
        {
            if (donenessSlider == null || targetZoneRect == null) return;
            RectTransform rect = donenessSlider.GetComponent<RectTransform>();
            float width = rect.rect.width * (TargetMax - TargetMin);
            targetZoneRect.sizeDelta = new Vector2(width, rect.rect.height);
            targetZoneRect.anchoredPosition = new Vector2(rect.rect.width *
                ((TargetMin + TargetMax) * 0.5f - 0.5f), 0f);
        }

        private void Update()
        {
            if (!_isCooking || _isCompleted) return;
            // One continuous outward and return trip; clicks and animations do not pause it.
            _doneness = Mathf.Clamp01(_doneness + (_round == 0 ? 1f : -1f)
                * Mathf.Max(0.01f, cookSpeed) * Time.deltaTime);
            if (_round == 0 && _doneness >= 1f)
            {
                _round = 1;
                _flippedCount = 0;
                SetupTargetZoneUI();
                UpdateUI();
            }
            if (donenessSlider != null) donenessSlider.value = _doneness;
            UpdateGaugeColor();
            UpdateSmokePlacement();
            if (_doneness > 0f && _doneness < 1f && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                CheckSkewerClick();
            if (_animatingCount != 0) return;
            if (_round == 1 && (_flippedCount == _skewers.Count || _doneness <= 0f))
                FinishGrill();
        }

        private void CheckSkewerClick()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            SkewerState closest = null;
            float best = float.PositiveInfinity;
            foreach (SkewerState skewer in _skewers)
            {
                if (skewer.FlipCount != _round || skewer.Animating || skewer.Model == null) continue;
                if (CookingClickArea.Contains(_mainCamera, skewer.Model, pointer, skewerClickPadding, out float score)
                    && score < best) { best = score; closest = skewer; }
            }
            if (closest == null) return;
            closest.FlipCount++;
            closest.ScoreSum += _round == 0
                ? EvaluateSideScore(_doneness, TargetMin, TargetMax)
                : EvaluateSideScore(1f - _doneness, 1f - TargetMax, 1f - TargetMin);
            _flippedCount++;
            if (flipAudioClip != null && flipAudioSource != null) flipAudioSource.PlayOneShot(flipAudioClip);
            StartCoroutine(AnimateFlip(closest));
            UpdateUI();
        }

        private IEnumerator AnimateFlip(SkewerState skewer)
        {
            skewer.Animating = true;
            _animatingCount++;
            Quaternion start = skewer.Model.transform.localRotation;
            Quaternion end = skewer.Rotation * Quaternion.AngleAxis(skewer.FlipCount * 180f, Vector3.up);
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.05f, flipDuration))
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, flipDuration));
                Transform model = skewer.Model.transform;
                Vector3 rest = model.parent != null ? model.parent.TransformPoint(skewer.Position) : skewer.Position;
                model.position = rest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * liftHeight);
                model.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            skewer.Model.transform.localPosition = skewer.Position;
            skewer.Model.transform.localRotation = end;
            skewer.Animating = false;
            _animatingCount--;
        }

        private void FinishGrill()
        {
            if (_isCompleted) return;
            _isCooking = false;
            _isCompleted = true;
            StopEffects();
            float total = 0f;
            foreach (SkewerState skewer in _skewers)
                total += skewer.ScoreSum + (2 - skewer.FlipCount) * 0.3f;
            FinalGrillScore = total / Mathf.Max(1, _skewers.Count * 2);
            UpdateUI();
            OnGrillCompleted?.Invoke();
        }

        private void UpdateSmokePlacement()
        {
            if (smokeEffect == null) return;
            Bounds bounds = default;
            bool found = false;
            foreach (SkewerState skewer in _skewers)
            {
                if (skewer.Animating || skewer.Model == null) continue;
                foreach (Renderer renderer in skewer.Model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            var emission = smokeEffect.emission;
            emission.enabled = found;
            if (!found) return;
            smokeEffect.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + 0.01f, bounds.center.z),
                Quaternion.Euler(-90f, 0f, 0f));
            var shape = smokeEffect.shape;
            shape.scale = new Vector3(Mathf.Max(0.04f, bounds.size.x * 0.75f),
                Mathf.Max(0.04f, bounds.size.z * 0.85f), 0.02f);
        }

        private void StopEffects()
        {
            if (smokeEffect != null) smokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _grillFlame.Stop();
            _grillFlame = default;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _isCooking = false;
            _animatingCount = 0;
            StopEffects();
            RestoreGrillScale();
            ClearSkewers();
        }

        private float EvaluateSideScore(float doneness, float minTarget, float maxTarget)
        {
            if (doneness >= minTarget && doneness <= maxTarget) return 1f;
            if (doneness > maxTarget && doneness < 0.9f) return 0.7f;
            if (doneness < minTarget && doneness >= 0.2f) return 0.6f;
            return 0.3f;
        }

        private void UpdateGaugeColor()
        {
            if (sliderFillImage == null) return;
            bool early = _round == 0 ? _doneness < TargetMin : _doneness > TargetMax;
            bool perfect = _doneness >= TargetMin && _doneness <= TargetMax;
            sliderFillImage.color = perfect ? Color.green : early ? Color.yellow : Color.red;
        }

        private void UpdateUI()
        {
            if (txtStatus != null) txtStatus.text = _isCompleted ? "해물꼬치 굽기 완료!"
                : $"{(_round == 0 ? "앞면 · 가는 중" : "뒷면 · 돌아오는 중")} · 완벽한 타이밍에 꼬치 3개를 빠르게 뒤집으세요. ({_flippedCount}/3)";
        }
    }
}
