using System;
using System.Collections.Generic;
using Marea.Player;
using TMPro;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// U로 여는 시설 업그레이드 목록. (+9/10)
    ///
    /// 폴더가 UI/가 아닌 이유는 UI/ 가 B 몫이라서다. A의 UI는 각자 자기 시스템 폴더에
    /// 둔다 — WarehouseUI가 Field/에 있는 것과 같은 규칙이다 (2차 분담 2절).
    ///
    /// 논블로킹이다. 열어둔 채 걸어다녀도 되고 클릭 이동도 된다. 설계 결정 7의 UIStack은
    /// B 몫인데 아직 없어서, 여기서 임시로 만들면 나중에 두 벌이 된다. 창고 UI와 같은 판단.
    ///
    /// 진입은 전역 키다. 시설이 월드 오브젝트가 아니라 가게 전체 스탯이라 다가갈 대상이
    /// 없기 때문이다. 나중에 탐사정처럼 실물이 있는 시설이 생기면 그때 그 오브젝트가
    /// 이 패널을 여는 창구가 된다 — 목록 자체는 그대로 쓴다.
    /// </summary>
    public class UpgradeUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("여닫을 대상. 이 컴포넌트가 붙은 오브젝트를 끄면 Update가 안 돌아 "
               + "다시 열 수 없으니, 패널은 따로 지정한다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("줄이 쌓일 곳. VerticalLayoutGroup이 붙어 있어야 세로로 정렬된다.")]
        [SerializeField] private Transform cellParent;

        [Tooltip("한 줄 프리팹. FacilityCell이 붙어 있어야 한다.")]
        [SerializeField] private FacilityCell cellPrefab;

        [Tooltip("보유 골드를 찍을 곳. 비워둬도 목록은 돈다.")]
        [SerializeField] private TextMeshProUGUI goldLabel;

        [Tooltip("비워두면 같은 씬에서 찾는다.")]
        [SerializeField] private PlayerInputReader input;

        [Header("동작")]
        [SerializeField] private bool openOnStart;

        private readonly List<FacilityCell> _cells = new();
        private FacilityLevels _levels;
        private Wallet _wallet;

        private void Awake()
        {
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>();

            if (panel == null)
                Debug.LogError($"{name}: UpgradeUI.panel이 비어 있다. 여닫을 대상이 없다.", this);
            if (cellParent == null)
                Debug.LogError($"{name}: UpgradeUI.cellParent가 비어 있다. 줄을 붙일 곳이 없다.", this);
            if (cellPrefab == null)
                Debug.LogError($"{name}: UpgradeUI.cellPrefab이 비어 있다. 줄을 만들 수 없다.", this);
            if (input == null)
                Debug.LogError($"{name}: 씬에서 PlayerInputReader를 못 찾았다. U로 열 수 없다.", this);
        }

        // FacilityLevels.Awake가 에셋 표를 만든 뒤라야 DataOf가 값을 준다. Awake에서
        // 줄을 만들면 실행 순서에 따라 빈 목록이 나온다.
        private void Start()
        {
            _levels = FacilityLevels.Instance;
            _wallet = Wallet.Instance;

            if (_levels == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. 업그레이드 목록이 비어 있게 된다.", this);
                return;
            }

            if (_wallet == null)
                Debug.LogError($"{name}: 씬에 Wallet이 없다. 비용을 치를 수 없다.", this);

            BuildCells();

            _levels.OnLevelChanged += HandleLevelChanged;
            if (_wallet != null) _wallet.OnGoldChanged += HandleGoldChanged;

            RefreshAll();

            if (panel != null) panel.SetActive(openOnStart);
        }

        private void OnDestroy()
        {
            if (_levels != null) _levels.OnLevelChanged -= HandleLevelChanged;
            if (_wallet != null) _wallet.OnGoldChanged -= HandleGoldChanged;
        }

        private void Update()
        {
            if (input == null || panel == null) return;
            if (!input.UpgradePressed) return;

            panel.SetActive(!panel.activeSelf);

            // 닫혀 있는 동안에도 골드는 변한다(정산). 열 때 한 번 맞춰준다.
            if (panel.activeSelf) RefreshAll();
        }

        /// <summary>enum에 선언된 순서대로 줄을 만든다. 에셋이 빠진 시설은 건너뛴다.</summary>
        private void BuildCells()
        {
            if (cellParent == null || cellPrefab == null) return;

            foreach (FacilityKind kind in Enum.GetValues(typeof(FacilityKind)))
            {
                // 에셋이 없으면 FacilityLevels.Awake가 이미 에러를 냈다. 여기서 빈 줄을
                // 만들면 "이름 없는 줄이 하나 있다"로 증상이 바뀌어 원인이 더 멀어진다.
                if (_levels.DataOf(kind) == null) continue;

                FacilityCell cell = Instantiate(cellPrefab, cellParent);
                cell.Bind(kind, HandleUpgradeClicked);
                _cells.Add(cell);
            }
        }

        private void HandleUpgradeClicked(FacilityKind kind)
        {
            // 실패해도 조용하다 — 버튼이 이미 비활성이라 여기 오는 실패는 드물고,
            // 온다면 그 사이에 골드가 줄어든 경우다. 갱신하면 버튼이 알아서 꺼진다.
            FacilityUpgrade.TryUpgrade(kind);
            RefreshAll();
        }

        private void HandleLevelChanged(FacilityKind kind, int level) => RefreshAll();

        private void HandleGoldChanged(int gold) => RefreshAll();

        /// <summary>
        /// 줄 전체를 다시 그린다. 한 시설을 올리면 골드가 줄어 <b>다른 줄의 버튼도</b>
        /// 꺼져야 하므로, 바뀐 줄만 고치지 않는다.
        /// </summary>
        private void RefreshAll()
        {
            if (goldLabel != null && _wallet != null)
                goldLabel.text = $"{_wallet.Gold:N0} G";

            foreach (FacilityCell cell in _cells)
                cell.Refresh();
        }
    }
}
