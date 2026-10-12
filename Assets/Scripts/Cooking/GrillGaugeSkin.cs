using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    /// <summary>
    /// 해물꼬치 3단계(뒤집기) 게이지를 1단계 재료 썰기 게이지와 같은 모양으로 보여 준다. (+10/13)
    ///
    /// 굽기 정도는 SeafoodSkewersGrill이 슬라이더 값(앞면 0→1, 뒷면 1→0)으로 넣는다.
    /// 여기는 그 값을 LateUpdate에서 읽어 뒤집개(indicator)를 바 위에서 옮기기만 한다.
    /// B 몫 코드(SeafoodSkewersGrill)를 고치지 않으려고 TimeBarSkin과 같은 방식으로 했다.
    ///
    /// 주황 판정 구간은 SeafoodSkewersGrill.targetZoneRect에 연결해 두면 그쪽이 targetMin~Max로 위치·폭을 정한다.
    /// 이 오브젝트는 슬라이더와 같은 폭으로 늘려 둔다 — 그래야 뒤집개와 판정 구간이 같은 자를 쓴다.
    /// </summary>
    public class GrillGaugeSkin : MonoBehaviour
    {
        [Tooltip("SeafoodSkewersGrill.donenessSlider와 같은 슬라이더")]
        [SerializeField] private Slider source;
        [Tooltip("바 위를 오가는 표시기(뒤집개 그림의 부모)")]
        [SerializeField] private RectTransform indicator;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (source == null || indicator == null) return;
            float half = _rect.rect.width * 0.5f;
            Vector2 position = indicator.anchoredPosition;
            position.x = Mathf.Lerp(-half, half, source.normalizedValue);
            indicator.anchoredPosition = position;
        }
    }
}
