using System.Collections.Generic;
using Marea.Data;
using Marea.Field;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    /// <summary>
    /// 미니게임 중 화면 위에 뜨는 바. (+10/7, A) 기획 와이어프레임 「요리 미니게임」.
    ///
    /// 왼쪽 위: 요리 아이콘 · 이름, 그 아래 단계 목록(지난 단계 흐리게 ✓, 지금 단계 굵게).
    /// 위 가운데: 남은 영업 시간(영업 중일 때만). 지금 Main에선 비워 둔다 — B의 영업 타이머가 같은 자리에 같은 값을 이미 띄운다.
    /// 미니게임(B) 코드는 건드리지 않는다 — 돌고 있는 BaseCookingMinigame의 CurrentStepIndex를 매 프레임 읽는다.
    /// 단계 이름은 데이터에 없어 여기 표로 든다. 미니게임 단계가 바뀌면 이 표도 고친다.
    /// </summary>
    public class CookingHud : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI stepsLabel;
        [SerializeField] private GameObject timeBox;
        [SerializeField] private TextMeshProUGUI timeLabel;

        [SerializeField] private Color doneColor = new(0.29f, 0.18f, 0.11f, 0.45f);
        [SerializeField] private Color currentColor = new(0.29f, 0.18f, 0.11f, 1f);
        [SerializeField] private Color nextColor = new(0.29f, 0.18f, 0.11f, 0.6f);

        private readonly List<BaseCookingMinigame> _games = new();
        private MinigameStepIndex _shownStep = MinigameStepIndex.NotStarted;
        private MenuData _shownMenu;

        private void Awake()
        {
            if (root == null)
            {
                Debug.LogError($"{name}: CookingHud.root가 비어 있다. 미니게임 상단 바가 안 뜬다.", this);
                enabled = false;
                return;
            }
            root.SetActive(false);
        }

        private void Start()
            => _games.AddRange(FindObjectsByType<BaseCookingMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        private void Update()
        {
            BaseCookingMinigame game = Running();
            MenuData menu = CookingSelectUI.CookingMenu;
            bool show = game != null && menu != null;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show)
            {
                _shownStep = MinigameStepIndex.NotStarted;
                _shownMenu = null;
                return;
            }

            if (menu != _shownMenu)
            {
                _shownMenu = menu;
                if (nameLabel != null) nameLabel.text = menu.DisplayName;
                if (icon != null)
                {
                    icon.sprite = menu.Icon;
                    icon.enabled = menu.Icon != null;
                }
                _shownStep = MinigameStepIndex.NotStarted;
            }
            if (game.CurrentStepIndex != _shownStep)
            {
                _shownStep = game.CurrentStepIndex;
                DrawSteps(menu, _shownStep);
            }

            BusinessManager bm = BusinessManager.Instance;
            bool open = bm != null && bm.CurrentState == BusinessState.Open;
            if (timeBox != null && timeBox.activeSelf != open) timeBox.SetActive(open);
            if (open && timeLabel != null) timeLabel.text = $"남은 시간  {TimeText.Format(bm.RemainingTime)}";
        }

        private BaseCookingMinigame Running()
        {
            foreach (BaseCookingMinigame g in _games)
                if (g != null && g.CurrentStepIndex >= MinigameStepIndex.Step1 && g.CurrentStepIndex <= MinigameStepIndex.Step3) return g;
            return null;
        }

        private void DrawSteps(MenuData menu, MinigameStepIndex step)
        {
            if (stepsLabel == null) return;
            string[] names = StepNames(menu);
            int current = (int)step - (int)MinigameStepIndex.Step1;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                if (i < current) sb.Append($"<color=#{Hex(doneColor)}>✓ {i + 1}. {names[i]}</color>\n");
                else if (i == current) sb.Append($"<b><color=#{Hex(currentColor)}>▶ {i + 1}. {names[i]}</color></b>\n");
                else sb.Append($"<color=#{Hex(nextColor)}>   {i + 1}. {names[i]}</color>\n");
            }
            stepsLabel.text = sb.ToString().TrimEnd();
        }

        /// <summary>요리별 단계 이름. 각 미니게임 컨트롤러의 1 · 2 · 3단계 내용과 맞춘다.</summary>
        public static string[] StepNames(MenuData menu)
        {
            switch (menu.MiniGameId)
            {
                case MiniGameId.Skewer:
                    return menu.SkewerSubtype == SkewerSubtype.SeafoodSkewers
                        ? new[] { "재료 썰기", "꼬치에 끼우기", "굽기" }
                        : new[] { "재료 썰기", "꼬치에 끼우기", "소스 바르기" };
                case MiniGameId.Stew:
                    return menu.StewSubtype == StewSubtype.VeggieStirFry
                        ? new[] { "재료 썰기", "볶기", "간 맞추기" }
                        : new[] { "재료 넣기", "젓기", "거품 걷기" };
                case MiniGameId.FishGrill:
                    return menu.FishGrillSubtype == FishGrillSubtype.Clam
                        ? new[] { "조개 닦기", "굽기", "소스 뿌리기" }
                        : new[] { "칼집 내기", "굽기", "담기" };
                default:
                    return new[] { "1단계", "2단계", "3단계" };
            }
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);
    }
}
