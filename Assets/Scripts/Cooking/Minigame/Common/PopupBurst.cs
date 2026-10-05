using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 팝업(거품)이 눌려 터질 때의 연출. 팝업 프리팹에 붙인다. (+10/2, 이슈 84)
    ///
    /// PopupTouchGame은 누른 팝업을 바로 Destroy한다. 그 전에 <see cref="Burst"/>를 불러
    /// 자식 파티클(물방울 · 김)을 떼어 내 제자리에서 터뜨리고, 파티클이 끝나면 저절로 지운다.
    /// 이 컴포넌트가 없는 팝업은 예전처럼 그냥 사라진다.
    /// </summary>
    public class PopupBurst : MonoBehaviour
    {
        [Tooltip("터질 때 틀 파티클. 자식에 두고 Play On Awake는 끈다.")]
        [SerializeField] private ParticleSystem burst;

        public void Burst()
        {
            if (burst == null) return;

            // 팝업이 곧 지워지니 떼어 낸다. 부모 크기에 따라 줄어 있던 걸 되돌리지 않게 월드 크기를 유지한다.
            burst.transform.SetParent(null, true);
            burst.gameObject.SetActive(true);
            burst.Play(true);

            var main = burst.main;
            Destroy(burst.gameObject, main.duration + main.startLifetime.constantMax + 0.1f);
            burst = null;
        }
    }
}
