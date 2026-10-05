using System;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 공용 효과 이름. 기획 「파티클 이펙트 리스트」의 공용 효과 ID(VFX_01~13)를 그대로 옮겼다. (+10/6, 이슈 117)
    /// 숫자는 기획 ID와 같다 — 표를 보고 찾기 쉽게. 02와 같은 등급 변형은 100번대로 뺐다.
    /// </summary>
    public enum VfxId
    {
        None = 0,
        Flame = 1,          // VFX_01 작은 불꽃 — 화구 · 그릴
        Sparkle = 2,        // VFX_02 완성 · 보상 반짝임 (일반)
        Steam = 3,          // VFX_03 흰 증기
        Splash = 4,         // VFX_04 액체 튐 — 색 · 크기를 바꿔 재사용
        Pour = 5,           // VFX_05 액체 줄기
        Bubble = 6,         // VFX_06 기포
        Seasoning = 7,      // VFX_07 양념 알갱이
        Ping = 8,           // VFX_08 입력 · 판정 강조(링)
        Slash = 9,          // VFX_09 얇은 궤적
        Dust = 10,          // VFX_10 먼지 · 파편
        Wake = 11,          // VFX_11 물거품 · 항적
        HitSpark = 12,      // VFX_12 입력 반짝임 — 성공 판정
        Fail = 13,          // VFX_13 충돌 폭발 — 실패 · 떨어뜨림

        SparkleHigh = 102,  // VFX_02 고급 — 높은 등급 · 금빛 (정리 시트 02-2)
    }

    /// <summary>
    /// 공용 효과 ID → 파티클 프리팹 표. 에셋 하나만 만들어 Vfx(PF_Systems)에 꽂는다. (+10/6, 이슈 117)
    ///
    /// 바뀌지 않는 값만 둔다(어떤 프리팹을 몇 배로 · 무슨 색으로). 어디서 몇 번 터지는지는 부르는 쪽이 정한다.
    /// 에셋을 바꾸려면 이 표만 고친다 — 코드는 VfxId로만 부른다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public VfxId id;
            public GameObject prefab;
            [Tooltip("프리팹 크기에 곱한다. 기획 메모 「사이즈 줄여서」 같은 것.")]
            [Min(0.01f)] public float scale;
            [Tooltip("시작 색에 곱한다. 흰색이면 그대로. 기획 메모 「색 바꿔서」 · 「불투명도 낮추고」는 여기서(알파).")]
            public Color tint;
            [TextArea] public string note;
        }

        [SerializeField] private Entry[] entries;

        public bool TryGet(VfxId id, out Entry entry)
        {
            if (entries != null)
                foreach (Entry e in entries)
                    if (e.id == id && e.prefab != null) { entry = e; return true; }
            entry = default;
            return false;
        }
    }
}
