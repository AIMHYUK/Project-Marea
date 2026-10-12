using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    /// <summary>
    /// 미니게임 바의 채움 막대 모양만 맡는다. (+10/13) 채움 Image에 붙인다.
    ///
    /// 바를 얼마나 채울지는 각 미니게임 코드가 정한다(슬라이더 값, anchorMax.x, fillAmount — 미니게임마다 다르다).
    /// 여기는 그 결과(지금 채워진 비율)를 LateUpdate에서 읽어 그림과 색만 바꾼다.
    /// B 몫 미니게임(채소볶음 · 꼬치) 코드를 고치지 않으려고 이렇게 했다 — 그쪽이 Update에서 색을 칠해도
    /// LateUpdate가 뒤에 돌아서 이 모양이 남는다.
    ///
    /// 남은 비율이 lowThreshold 아래로 내려가면 lowSprite로 바꾼다(시간 바: 노랑 → 빨강).
    /// 진행 바처럼 바꿀 필요가 없으면 lowSprite를 비운다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TimeBarSkin : MonoBehaviour
    {
        [SerializeField] private Sprite normalSprite;
        [Tooltip("남은 비율이 lowThreshold 아래일 때. 비우면 바꾸지 않는다.")]
        [SerializeField] private Sprite lowSprite;
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;
        [Tooltip("그림에 곱할 색. 흰색이면 그림 그대로.")]
        [SerializeField] private Color tint = Color.white;

        private Image _image;
        private RectTransform _rect;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = (RectTransform)transform;
        }

        /// <summary>지금 채워진 비율. Filled면 fillAmount, 아니면 가로 앵커(슬라이더 · 직접 늘리는 바).</summary>
        private float Ratio => _image.type == Image.Type.Filled ? _image.fillAmount : _rect.anchorMax.x - _rect.anchorMin.x;

        private void LateUpdate()
        {
            Sprite want = lowSprite != null && Ratio < lowThreshold ? lowSprite : normalSprite;
            if (want != null && _image.sprite != want) _image.sprite = want;
            if (_image.color != tint) _image.color = tint;
        }
    }
}
