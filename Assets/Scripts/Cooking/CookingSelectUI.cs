using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using Marea.Field;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Cooking
{
    /// <summary>
    /// 조리할 요리 선택 창. (+10/7, A) 기획 와이어프레임 「요리 — 조리할 요리 선택」.
    ///
    /// 조리대를 누르면 B의 CookingMenuUI.Open이 이 창으로 넘긴다(분류 단계 없이 바로 목록).
    /// 왼쪽: 오늘의 메뉴(고르기 전이면 열린 메뉴 전부) — "생선구이 · 주문 2" / "야채볶음 · 재료 부족".
    /// 오른쪽: 고른 요리의 이름 · 그림 · 재료 필요량/보유량 · 조리 가능 여부, 부족하면 무엇이 부족한지.
    /// [조리 시작]은 B의 분기(CookingMenuUI.StartCookingMenu)를 그대로 부른다 — 미니게임 · 재료 차감 · 조리대 거치는 B 그대로.
    /// </summary>
    public class CookingSelectUI : UiPanel
    {
        [Header("목록")]
        [SerializeField] private Transform rowParent;
        [Tooltip("복제할 줄. 버튼 + 자식 TMP 하나. 꺼 둔다.")]
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Sprite rowNormal;
        [SerializeField] private Sprite rowSelected;

        [Header("상세")]
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image picture;
        [SerializeField] private TextMeshProUGUI recipeLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private TextMeshProUGUI shortageLabel;
        [SerializeField] private TextMeshProUGUI emptyLabel;

        [Header("기타")]
        [SerializeField] private TextMeshProUGUI timeLabel;
        [SerializeField] private Button startButton;

        [SerializeField] private Color okColor = new(0.20f, 0.45f, 0.18f, 1f);
        [SerializeField] private Color badColor = new(0.62f, 0.20f, 0.14f, 1f);

        private CookingMenuUI _owner;
        private CookingCounter _counter;
        private readonly List<MenuData> _menus = new();
        private readonly List<Button> _rows = new();
        private MenuData _selected;
        private bool _starting;   // 조리 시작으로 닫힐 땐 B의 Close(플레이어 해제)를 부르지 않는다

        /// <summary>지금(또는 마지막으로) 조리를 시작한 요리. 미니게임 상단 바가 읽는다.</summary>
        public static MenuData CookingMenu { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (rowParent == null || rowTemplate == null)
                Debug.LogError($"{name}: CookingSelectUI.rowParent · rowTemplate이 비어 있다. 요리 목록이 안 나온다.", this);
            else
                rowTemplate.gameObject.SetActive(false);
            if (startButton == null)
                Debug.LogError($"{name}: CookingSelectUI.startButton이 비어 있다. 조리를 시작할 수 없다.", this);
            else
                startButton.onClick.AddListener(StartCooking);
        }

        /// <summary>B의 CookingMenuUI.Open이 부른다. 닫히면 owner.Close로 플레이어 · 손님 스폰을 풀어 준다.</summary>
        public void Open(CookingMenuUI owner)
        {
            _owner = owner;
            _counter = FindAnyObjectByType<CookingCounter>();
            _starting = false;
            Open();
        }

        protected override void OnOpened()
        {
            Collect();
            BuildRows();
            Select(FirstCookable());
        }

        protected override void OnClosed()
        {
            if (!_starting && _owner != null) _owner.Close();
            _owner = null;
        }

        private void Update()
        {
            if (!IsVisible) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }

            BusinessManager bm = BusinessManager.Instance;
            if (timeLabel != null)
            {
                bool open = bm != null && bm.CurrentState == BusinessState.Open;
                timeLabel.gameObject.SetActive(open);
                if (open) timeLabel.text = $"남은 영업 시간  {TimeText.Format(bm.RemainingTime)}";
            }
        }

        // --- 목록 ---

        private void Collect()
        {
            _menus.Clear();
            TodayMenu today = TodayMenu.Instance;
            if (today != null && today.Selected.Count > 0)
            {
                foreach (MenuData m in today.Selected) if (m != null) _menus.Add(m);
            }
            else if (today != null)
            {
                foreach (MenuData m in today.Candidates)
                    if (m != null && m.IsActive && today.IsMenuUnlocked(m, out _)) _menus.Add(m);
            }
            else
            {
                Debug.LogError($"{name}: 씬에 TodayMenu가 없다. 조리할 요리 목록을 못 만든다.", this);
            }

            // 만들 수 있는 것 먼저, 그 안에선 주문 많은 순.
            _menus.Sort((a, b) =>
            {
                int c = CanCook(b).CompareTo(CanCook(a));
                return c != 0 ? c : Orders(b).CompareTo(Orders(a));
            });
        }

        private void BuildRows()
        {
            foreach (Button row in _rows) if (row != null) Destroy(row.gameObject);
            _rows.Clear();
            if (rowTemplate == null) return;

            foreach (MenuData menu in _menus)
            {
                Button row = Instantiate(rowTemplate, rowParent);
                row.gameObject.SetActive(true);
                MenuData captured = menu;
                row.onClick.AddListener(() => Select(captured));
                TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = RowText(menu);
                _rows.Add(row);
            }
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(_menus.Count == 0);
        }

        private string RowText(MenuData menu)
        {
            if (!HasIngredients(menu)) return $"{menu.DisplayName}  ·  <color=#{ColorUtility.ToHtmlStringRGB(badColor)}>재료 부족</color>";
            int n = Orders(menu);
            return n > 0 ? $"{menu.DisplayName}  ·  주문 {n}" : menu.DisplayName;
        }

        private MenuData FirstCookable()
        {
            foreach (MenuData m in _menus) if (CanCook(m)) return m;
            return _menus.Count > 0 ? _menus[0] : null;
        }

        // --- 상세 ---

        private void Select(MenuData menu)
        {
            _selected = menu;
            for (int i = 0; i < _rows.Count; i++)
            {
                Image bg = _rows[i] != null ? _rows[i].GetComponent<Image>() : null;
                if (bg != null && rowNormal != null) bg.sprite = _menus[i] == menu && rowSelected != null ? rowSelected : rowNormal;
            }

            bool has = menu != null;
            SetActive(nameLabel, has);
            SetActive(recipeLabel, has);
            SetActive(statusLabel, has);
            if (picture != null) picture.gameObject.SetActive(has && menu.Icon != null);
            if (startButton != null) startButton.interactable = has && CanCook(menu);
            if (!has)
            {
                SetActive(shortageLabel, false);
                return;
            }

            nameLabel.text = menu.DisplayName;
            if (picture != null && menu.Icon != null) picture.sprite = menu.Icon;

            var lines = new System.Text.StringBuilder("재료 필요량 / 보유량\n");
            var missing = new List<string>();
            foreach (var (ingredient, need) in Needs(menu))
            {
                int own = Warehouse.Instance != null ? Warehouse.Instance.CountOf(ingredient) : 0;
                bool ok = own >= need;
                string color = ColorUtility.ToHtmlStringRGB(ok ? okColor : badColor);
                lines.Append($"{Name(ingredient)}  <color=#{color}>{own}</color> / {need}\n");
                if (!ok) missing.Add($"{Name(ingredient)} {need - own}개");
            }
            recipeLabel.text = lines.ToString().TrimEnd();

            bool counterFull = _counter != null && _counter.IsFull;
            if (missing.Count > 0) SetStatus("조리 불가 — 재료 부족", false);
            else if (counterFull) SetStatus("조리 불가 — 조리대가 가득 찼다", false);
            else SetStatus("조리 가능", true);

            SetActive(shortageLabel, missing.Count > 0);
            if (shortageLabel != null && missing.Count > 0) shortageLabel.text = "부족: " + string.Join(", ", missing);
        }

        private void SetStatus(string text, bool ok)
        {
            statusLabel.text = text;
            statusLabel.color = ok ? okColor : badColor;
        }

        private void StartCooking()
        {
            if (_selected == null || !CanCook(_selected) || _owner == null) return;
            CookingMenu = _selected;
            CookingMenuUI owner = _owner;
            _starting = true;
            Close();
            owner.StartCookingMenu(_selected);
        }

        // --- 계산 ---

        /// <summary>재료별 필요량 합 — 하와이안처럼 같은 재료가 레시피에 두 번 나오면 더한다.</summary>
        private static List<(IngredientData, int)> Needs(MenuData menu)
        {
            var list = new List<(IngredientData, int)>();
            foreach (RecipeEntry e in menu.Recipe)
            {
                if (e.ingredient == null) continue;
                int i = list.FindIndex(x => x.Item1 == e.ingredient);
                if (i >= 0) list[i] = (e.ingredient, list[i].Item2 + e.requiredAmount);
                else list.Add((e.ingredient, e.requiredAmount));
            }
            return list;
        }

        private static bool HasIngredients(MenuData menu)
            => Warehouse.Instance == null || Warehouse.Instance.Has(menu.Recipe);

        private bool CanCook(MenuData menu)
            => menu != null && HasIngredients(menu) && (_counter == null || !_counter.IsFull);

        /// <summary>아직 음식을 못 받은 손님 중 이 요리를 주문한 수.</summary>
        private static int Orders(MenuData menu)
        {
            int n = 0;
            foreach (CustomerController c in FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
                if (c.OrderedMenu == menu && (c.State == CustomerState.WaitingOrder || c.State == CustomerState.WalkingToSeat)) n++;
            return n;
        }

        private static string Name(IngredientData d)
            => string.IsNullOrEmpty(d.DisplayName) ? d.name : d.DisplayName;

        private static void SetActive(Component c, bool on)
        {
            if (c != null) c.gameObject.SetActive(on);
        }
    }
}
