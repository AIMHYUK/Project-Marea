using System.Collections;
using UnityEngine;

namespace Marea.Cooking
{
    public class FishGrillVisual : MonoBehaviour
    {
        [Header("렌더러 바인딩")]
        [SerializeField] private Renderer fishRenderer;

        [Header("굽기 단계별 색상")]
        [SerializeField] private Color rawColor = new Color(1f, 0.6f, 0.6f);      // 날것: 연분홍
        [SerializeField] private Color goodColor = new Color(1f, 0.9f, 0.4f);     // 익어감: 밝은 노랑
        [SerializeField] private Color perfectColor = new Color(0.85f, 0.45f, 0.1f); // 완벽: 노릇한 갈색
        [SerializeField] private Color burntColor = new Color(0.15f, 0.1f, 0.1f);   // 탐: 짙은 흑색

        [Header("뒤집기 애니메이션 설정")]
        [SerializeField] private float flipDuration = 0.45f;
        [SerializeField] private float flipJumpHeight = 0.3f;

        private Vector3 _originalLocalPos;
        private Quaternion _originalLocalRot;
        private Coroutine _flipRoutine;
        private Material _runtimeMat;

        private void Awake()
        {
            _originalLocalPos = transform.localPosition;
            _originalLocalRot = transform.localRotation;

            if (fishRenderer == null)
            {
                fishRenderer = GetComponent<Renderer>();
            }

            if (fishRenderer != null)
            {
                // 인스턴스 머티리얼 캐싱
                _runtimeMat = fishRenderer.material;
            }
        }

        public void InitializeVisual()
        {
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
                _flipRoutine = null;
            }

            transform.localPosition = _originalLocalPos;
            transform.localRotation = _originalLocalRot;
            UpdateGrillColor(0f);
            gameObject.SetActive(true);
        }

        public void UpdateGrillColor(float progress01)
        {
            if (_runtimeMat == null && fishRenderer != null)
            {
                _runtimeMat = fishRenderer.material;
            }

            if (_runtimeMat == null) return;

            Color targetColor;
            if (progress01 < 0.45f)
            {
                float t = progress01 / 0.45f;
                targetColor = Color.Lerp(rawColor, goodColor, t);
            }
            else if (progress01 < 0.80f)
            {
                float t = (progress01 - 0.45f) / 0.35f;
                targetColor = Color.Lerp(goodColor, perfectColor, t);
            }
            else
            {
                float t = (progress01 - 0.80f) / 0.20f;
                targetColor = Color.Lerp(perfectColor, burntColor, t);
            }

            // 머티리얼 컬러 직접 갱신 (URP _BaseColor 및 Standard _Color 동시 적용)
            _runtimeMat.color = targetColor;
            if (_runtimeMat.HasProperty("_BaseColor"))
            {
                _runtimeMat.SetColor("_BaseColor", targetColor);
            }
        }

        public void PlayFlipAnimation()
        {
            if (_flipRoutine != null) StopCoroutine(_flipRoutine);
            _flipRoutine = StartCoroutine(FlipRoutine());
        }

        private IEnumerator FlipRoutine()
        {
            Vector3 startPos = _originalLocalPos;
            Quaternion startRot = transform.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, 180f);

            float elapsed = 0f;
            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flipDuration);

                float heightOffset = Mathf.Sin(t * Mathf.PI) * flipJumpHeight;
                transform.localPosition = startPos + new Vector3(0f, heightOffset, 0f);
                transform.localRotation = Quaternion.Slerp(startRot, endRot, t);

                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = endRot;
            _flipRoutine = null;
        }

        public void ResetVisual()
        {
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
                _flipRoutine = null;
            }

            transform.localPosition = _originalLocalPos;
            transform.localRotation = _originalLocalRot;
            UpdateGrillColor(0f);
        }
    }
}
