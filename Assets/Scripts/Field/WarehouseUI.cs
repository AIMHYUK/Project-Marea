using System.Collections.Generic;
using System.Linq;
using Marea.Core;
using Marea.Data;
using Marea.Player;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 창고에 무엇이 몇 개 있는지 보여준다. id 오름차순. (+9/9)
    ///
    /// 폴더가 UI/가 아닌 이유는 UI/ 가 1차에서 B 몫으로 정해둔 자리라서다.
    /// A의 UI는 각자 자기 시스템 폴더에 둔다 (2차 분담 2절).
    ///
    /// 논블로킹이다 — 열어둔 채 걸어다녀도 되고 클릭 이동도 된다. 설계 결정 7의
    /// UIStack은 B 몫인데 아직 없어서, 여기서 임시로 만들면 나중에 두 벌이 된다.
    /// 목록형은 정보 표시라 게임플레이를 막을 이유가 없다.
    /// </summary>
    public class WarehouseUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("켜고 끄는 대상. 이 컴포넌트가 붙은 오브젝트를 끄면 Update가 안 돌아 "
               + "다시 열 수 없으니, 패널은 따로 지정한다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("셀이 쌓일 곳. VerticalLayoutGroup이 붙어 있어야 세로로 정렬된다.")]
        [SerializeField] private Transform cellParent;

        [Tooltip("한 줄 프리팹. IngredientCell이 붙어 있어야 한다.")]
        [SerializeField] private IngredientCell cellPrefab;

        [Tooltip("비워두면 같은 씬에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        [Header("동작")]
        [SerializeField] private bool openOnStart;

        // 창고와 같은 이유로 키가 id가 아니라 재료 자체다 —
        // id를 안 채운 에셋 둘이 있으면(둘 다 0) 한 줄로 합쳐진다.
        private readonly Dictionary<IngredientData, IngredientCell> _cells = new();
        private Warehouse _warehouse;

        private void Awake()
        {
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();

            if (panel == null)
                Debug.LogError($"{name}: WarehouseUI.panel이 비어 있다. 여닫을 대상이 없다.", this);
            if (cellParent == null)
                Debug.LogError($"{name}: WarehouseUI.cellParent가 비어 있다. 줄을 붙일 곳이 없다.", this);
            if (cellPrefab == null)
                Debug.LogError($"{name}: WarehouseUI.cellPrefab이 비어 있다. 줄을 만들 수 없다.", this);
            if (input == null)
                Debug.LogError($"{name}: 씬에서 PlayerInputReader를 못 찾았다. Tab으로 열 수 없다.", this);
        }

        // Warehouse.Awake가 startingStock을 넣으므로 여기서 잡으면 초기 재고를 놓친다.
        // OnEnable(Start 이후)이 아니라 Start에서 구독하고, 현재 상태를 통째로 한 번 읽는다.
        private void Start()
        {
            _warehouse = Warehouse.Instance;
            if (_warehouse == null)
            {
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 창고 UI가 아무것도 못 보여준다.", this);
                return;
            }

            _warehouse.OnCountChanged += HandleCountChanged;
            RebuildAll();

            if (panel != null) panel.SetActive(openOnStart);
        }

        private void OnDestroy()
        {
            if (_warehouse != null) _warehouse.OnCountChanged -= HandleCountChanged;
        }

        private void Update()
        {
            if (input == null || panel == null) return;
            if (!input.InventoryPressed) return;

            panel.SetActive(!panel.activeSelf);
        }

        /// <summary>구독을 걸기 전에 이미 들어와 있던 재고를 한 번에 그린다.</summary>
        private void RebuildAll()
        {
            foreach (var pair in _warehouse.Stock)
                HandleCountChanged(pair.Key, pair.Value);
        }

        private void HandleCountChanged(IngredientData ingredient, int count)
        {
            if (ingredient == null || cellParent == null || cellPrefab == null) return;

            if (_cells.TryGetValue(ingredient, out var cell))
            {
                cell.SetCount(count);
                return;
            }

            cell = Instantiate(cellPrefab, cellParent);
            cell.Bind(ingredient, count);
            _cells[ingredient] = cell;

            // 표시 순서는 창고가 아니라 여기가 정한다. 새 재료가 중간에 끼어도
            // 줄이 뒤로 밀려 붙지 않게, 줄이 늘 때만 전체를 다시 매긴다.
            ReorderCells();
        }

        /// <summary>id 오름차순으로 형제 위치를 다시 매긴다.</summary>
        private void ReorderCells()
        {
            int index = 0;
            foreach (var cell in _cells.Values.OrderBy(c => c.Ingredient.Id))
            {
                cell.transform.SetSiblingIndex(index);
                index++;
            }
        }
    }
}
