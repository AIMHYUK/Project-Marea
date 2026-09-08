using System;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 재료 창고. 무엇이 몇 개인지만 안다. 그리드도 무게도 순서도 없다.
    ///
    /// 씬에 하나뿐인 물건이라 Instance로 노출한다 (+9/9). 행위자에 매달지 않는 이유는
    /// 기획이 "플레이어가 식재료를 직접 들고 이동하지 않는다"라고 못박았기 때문이다 —
    /// 창고는 플레이어 것이 아니라 가게 것이고, 요리 시스템도 같은 데이터를 본다.
    /// 그래서 IInteractor에서 뺐다. 설계 결정 10이 예고해둔 그 작업이다.
    ///
    /// 키가 id가 아니라 IngredientData 자체다. id를 키로 쓰면 "키와 값 안의 Id가 같아야
    /// 한다"는 약속이 생기는데, 어긋나도 아무 데서도 안 걸리고 증상은 "UI에 엉뚱한 이름이
    /// 뜬다"로만 나온다. 참조를 키로 쓰면 키가 곧 정체라 어긋날 수가 없다.
    /// id가 필요하면 ingredient.Id로 꺼낸다 — 표시 순서가 그 쓰임이고, 그건 UI 몫이다.
    /// </summary>
    public class Warehouse : MonoBehaviour
    {
        public static Warehouse Instance { get; private set; }

        [Tooltip("시작할 때 들고 있을 재료. 테스트용.")]
        [SerializeField] private RecipeEntry[] startingStock;

        private readonly Dictionary<IngredientData, int> _stock = new();

        /// <summary>지금 들고 있는 것 전부. 순서는 없다 — 정렬은 보여주는 쪽이 한다.</summary>
        public IReadOnlyDictionary<IngredientData, int> Stock => _stock;

        /// <summary>(재료, 바뀐 뒤 수량). UI가 구독한다.</summary>
        public event Action<IngredientData, int> OnCountChanged;

        private void Awake()
        {
            // 조용히 덮어쓰면 어느 쪽이 살아남았는지가 안 보인다. 씬에 둘을 놓는 건
            // 대개 실수이고, 그 실수의 증상은 "재료를 넣었는데 UI가 안 바뀐다"이다.
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{name}: 씬에 Warehouse가 둘 이상이다. '{Instance.name}'이 먼저 잡혔고 "
                             + "이 오브젝트는 무시된다. 하나만 남길 것.", this);
                return;
            }

            Instance = this;

            if (startingStock == null) return;
            foreach (var entry in startingStock)
                Add(entry.ingredient, entry.count);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int CountOf(IngredientData ingredient)
            => ingredient != null && _stock.TryGetValue(ingredient, out int count) ? count : 0;

        public void Add(IngredientData ingredient, int count)
        {
            if (ingredient == null || count <= 0) return;

            Set(ingredient, CountOf(ingredient) + count);
        }

        // ── 아래 둘은 계약 2다. B의 「요리 전 재료 확인·차감」이 쓴다 (2차 분담 6단계).
        //    지금 호출처가 0개인 건 B가 아직 안 짰기 때문이지 안 쓰는 게 아니다.
        //    시그니처를 바꿀 땐 B에게 먼저 말한다.
        //
        //    MenuData가 아니라 재료 묶음을 받는다 (+9/9). 창고가 "메뉴"라는 상위 개념을
        //    알 이유가 없고, 소비처가 메뉴만도 아니다 — 기획의 시설 업그레이드는 특수 자원을
        //    쓴다. 인자를 IReadOnlyList로 둔 건 MenuData.Recipe가 이미 그 타입이라
        //    B가 menu.Recipe를 변환 없이 그대로 넘길 수 있게 하려는 것이다.

        /// <summary>이만큼 다 있는가. 차감하지는 않는다.</summary>
        public bool Has(IReadOnlyList<RecipeEntry> required)
        {
            if (required == null) return false;

            // 빈 칸이 섞여 있으면 만들 수 없다고 본다. Aggregate가 null을 건너뛰기 때문에
            // 여기서 안 걸러내면 인스펙터를 덜 채운 레시피가 조용히 통과한다.
            for (int i = 0; i < required.Count; i++)
                if (required[i].ingredient == null) return false;

            foreach (var pair in Aggregate(required))
                if (CountOf(pair.Key) < pair.Value) return false;

            return true;
        }

        /// <summary>
        /// 필요한 재료를 한꺼번에 뺀다.
        /// 하나라도 모자라면 아무것도 안 빼고 false를 준다 — 반만 빠지는 일은 없다.
        /// </summary>
        public bool Consume(IReadOnlyList<RecipeEntry> required)
        {
            if (!Has(required)) return false;

            foreach (var pair in Aggregate(required))
                Set(pair.Key, CountOf(pair.Key) - pair.Value);

            return true;
        }

        /// <summary>
        /// 같은 재료가 여러 줄로 들어오면 합친다.
        ///
        /// 안 합치면 Has가 줄마다 따로 보는 바람에 "감자 1, 감자 2"를 보유 2에 대고
        /// 물었을 때 true가 나오는데, Consume은 3을 빼려 든다. 그 순간 "반만 빠지는 일은
        /// 없다"가 깨진다. 메뉴 레시피는 같은 재료를 두 번 안 적어서 지금까진 안 걸렸지만,
        /// 밖에서 묶음을 조립하기 시작하면 걸린다. (+9/9)
        /// </summary>
        private static Dictionary<IngredientData, int> Aggregate(IReadOnlyList<RecipeEntry> required)
        {
            var total = new Dictionary<IngredientData, int>();

            for (int i = 0; i < required.Count; i++)
            {
                RecipeEntry entry = required[i];
                if (entry.ingredient == null || entry.count <= 0) continue;

                total.TryGetValue(entry.ingredient, out int had);
                total[entry.ingredient] = had + entry.count;
            }

            return total;
        }

        private void Set(IngredientData ingredient, int next)
        {
            _stock[ingredient] = next;
            OnCountChanged?.Invoke(ingredient, next);
        }
    }
}
