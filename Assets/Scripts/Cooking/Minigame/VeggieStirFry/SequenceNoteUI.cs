using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public class SequenceNoteUI : MonoBehaviour
    {
        [Header("UI 아이콘 연결")]
        [SerializeField] private Image noteIconImage;
        [SerializeField] private Sprite saltIcon;
        [SerializeField] private Sprite pepperIcon;

        public RectTransform Rect { get; private set; }
        public SeasoningKey KeyType { get; private set; }

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
        }

        // 💡 SequenceInputRailUI에서 호출하는 초기화 메서드
        public void SetupNote(SeasoningKey keyType, Vector2 startPosition)
        {
            if (Rect == null) Rect = GetComponent<RectTransform>();

            KeyType = keyType;
            Rect.anchoredPosition = startPosition;

            // 소금/후추 타입에 따라 스프라이트 변경
            if (noteIconImage != null)
            {
                if (keyType == SeasoningKey.Salt && saltIcon != null)
                {
                    noteIconImage.sprite = saltIcon;
                }
                else if (keyType == SeasoningKey.Pepper && pepperIcon != null)
                {
                    noteIconImage.sprite = pepperIcon;
                }
            }
        }
    }
}
