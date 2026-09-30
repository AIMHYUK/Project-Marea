using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    [RequireComponent(typeof(RectTransform))]
    public class SequenceNoteUI : MonoBehaviour
    {
        [Header("UI 구성요소")]
        [SerializeField] private Image iconImage;

        [Header("양념 타입별 아이콘 스프라이트 (선택)")]
        [SerializeField] private Sprite saltIcon;
        [SerializeField] private Sprite pepperIcon;
        [SerializeField] private Sprite sauceIcon;

        public SeasoningKey KeyType { get; private set; }
        public RectTransform Rect { get; private set; }

        private float _speed;
        private float _missXThreshold;
        private System.Action<SequenceNoteUI> _onMissCallback;
        private bool _isInitialized;

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 노트 생성 시 레일 UI에서 호출하여 초기화
        /// </summary>
        public void Initialize(SeasoningKey keyType, float speed, float missXThreshold, System.Action<SequenceNoteUI> onMissCallback)
        {
            KeyType = keyType;
            _speed = speed;
            _missXThreshold = missXThreshold;
            _onMissCallback = onMissCallback;

            SetIconSprite(keyType);
            _isInitialized = true;
        }

        private void SetIconSprite(SeasoningKey keyType)
        {
            if (iconImage == null) return;

            switch (keyType)
            {
                case SeasoningKey.Salt:
                    if (saltIcon != null) iconImage.sprite = saltIcon;
                    break;
                case SeasoningKey.Pepper:
                    if (pepperIcon != null) iconImage.sprite = pepperIcon;
                    break;
                case SeasoningKey.Sauce:
                    if (sauceIcon != null) iconImage.sprite = sauceIcon;
                    break;
            }
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // 오른쪽에서 왼쪽(판정선 방향)으로 이동
            Rect.anchoredPosition += Vector2.left * (_speed * Time.deltaTime);

            // 판정선 위치를 완전히 지나쳤을 경우 Miss 처리
            if (Rect.anchoredPosition.x <= _missXThreshold)
            {
                _isInitialized = false;
                _onMissCallback?.Invoke(this);
            }
        }
    }
}
