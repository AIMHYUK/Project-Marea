using System.Collections;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Cooking
{
    /// <summary>
    /// 요리를 다 만들었을 때 공통으로 뜨는 "빠밤~" 팝업. (+10/2)
    ///
    /// 화면이 살짝 어두워지고 → 가운데 요리 그림이 튀어 오르고(작게 → 1.2배 → 1배) 뒤에 빛줄기가 돈다
    /// → 이름 → 랭크(도장 찍듯) → 판매가가 차례로 뜬다. 잠시 뒤 또는 클릭하면 닫힌다.
    /// CookingMenuUI가 요리 결과를 받는 곳(OnMinigameFinished)에서 <see cref="TryShow"/>를 부른다.
    ///
    /// 트윈 라이브러리 없이 코루틴으로. 시간은 기본 Time.deltaTime — 나중에 일시정지(timeScale 0)가 생기면 useUnscaledTime을 켠다.
    /// </summary>
    public class DishRevealUI : MonoBehaviour
    {
        public static DishRevealUI Instance { get; private set; }

        [Header("부품")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform rays;
        [SerializeField] private RectTransform dish;
        [SerializeField] private Image dishImage;
        [SerializeField] private RectTransform nameRoot;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI rankLabel;
        [SerializeField] private TextMeshProUGUI priceLabel;

        [Header("랭크 (판정 Miss · Bad · Good · Perfect 순)")]
        [SerializeField] private string[] rankTexts = { "F", "B", "A", "S" };
        [SerializeField] private Color[] rankColors =
        {
            new Color(0.55f, 0.55f, 0.55f), new Color(0.35f, 0.6f, 0.95f), new Color(0.35f, 0.8f, 0.35f), new Color(1f, 0.78f, 0.2f),
        };

        [Header("타이밍(초)")]
        [SerializeField, Min(0.05f)] private float popTime = 0.28f;
        [SerializeField, Range(1f, 1.6f)] private float overshoot = 1.2f;
        [SerializeField, Min(0f)] private float nameDelay = 0.35f;
        [SerializeField, Min(0f)] private float rankDelay = 0.6f;
        [SerializeField, Min(0f)] private float priceDelay = 0.8f;
        [Tooltip("다 뜬 뒤 이만큼 보여 주고 닫는다. 클릭하면 바로 닫힌다.")]
        [SerializeField, Min(0.2f)] private float holdTime = 1.6f;
        [SerializeField, Min(0f)] private float raySpeed = 25f;
        [Tooltip("timeScale을 무시하고 움직인다. 지금 게임엔 일시정지가 없어 꺼 둔다.")]
        [SerializeField] private bool useUnscaledTime;

        private float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private Coroutine _routine;

        // 평소엔 꺼져 있다(프리팹에서 끔). 처음 켜질 때 Awake가 돈다 — 여기서 다시 끄면 안 된다.
        private void Awake()
        {
            Instance = this;
            if (group == null || dish == null || nameLabel == null || rankLabel == null)
                Debug.LogError($"{name}: DishRevealUI 부품 중 비어 있는 게 있다.", this);
        }

        /// <summary>씬의 팝업을 찾아 띄운다. 꺼져 있어도 찾는다. 없으면 false.</summary>
        public static bool TryShow(CookingResult result, MenuData menu = null)
        {
            DishRevealUI ui = Instance != null ? Instance : FindFirstObjectByType<DishRevealUI>(FindObjectsInactive.Include);
            if (ui == null) return false;
            ui.Show(result, menu);
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>만든 요리를 띄운다. menu가 비면 result.menuData.</summary>
        public void Show(CookingResult result, MenuData menu = null)
        {
            if (menu == null) menu = result.menuData;
            if (_routine != null) StopCoroutine(_routine);
            gameObject.SetActive(true);
            _routine = StartCoroutine(Play(result, menu));
        }

        private IEnumerator Play(CookingResult result, MenuData menu)
        {
            // 내용 채우기
            if (dishImage != null)
            {
                dishImage.sprite = menu != null ? menu.Icon : null;
                dishImage.enabled = dishImage.sprite != null;
                dishImage.color = result.isSuccess ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            }
            nameLabel.text = menu == null ? "요리" : string.IsNullOrWhiteSpace(menu.DisplayName) ? menu.name : menu.DisplayName;
            int g = Mathf.Clamp((int)result.bestGrade, 0, rankTexts.Length - 1);
            if (!result.isSuccess) g = 0;
            rankLabel.text = rankTexts[g];
            rankLabel.color = g < rankColors.Length ? rankColors[g] : Color.white;
            if (priceLabel != null) priceLabel.text = result.isSuccess ? $"판매가 {result.finalPrice:N0} G" : "실패";

            // 처음 상태
            group.alpha = 0f;
            dish.localScale = Vector3.zero;
            SetAlpha(nameRoot, 0f); SetAlpha(rankLabel.rectTransform, 0f); if (priceLabel != null) SetAlpha(priceLabel.rectTransform, 0f);
            if (rays != null) rays.localScale = Vector3.zero;

            float t = 0f;
            bool closed = false;
            Vector2 nameHome = nameRoot != null ? nameRoot.anchoredPosition : Vector2.zero;
            float total = priceDelay + 0.25f + holdTime;
            while (t < total && !closed)
            {
                t += Dt;

                group.alpha = Mathf.Clamp01(t / 0.15f);

                // 요리: 0 → overshoot → 1 (튀어 오름)
                float p = t / popTime;
                float s = p < 1f ? overshoot * EaseOutCubic(p) : Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 1f) / 0.6f)));
                dish.localScale = Vector3.one * s;
                dish.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 9f) * 6f * Mathf.Exp(-t * 4f));   // 처음에 살짝 흔들

                if (rays != null)
                {
                    rays.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 0.3f));
                    rays.Rotate(0f, 0f, -raySpeed * Dt);
                }

                // 이름: 아래에서 올라오며 나타남
                float n = Mathf.Clamp01((t - nameDelay) / 0.2f);
                if (nameRoot != null)
                {
                    SetAlpha(nameRoot, n);
                    nameRoot.anchoredPosition = nameHome + Vector2.down * (24f * (1f - n));
                }

                // 랭크: 크게 찍혔다가 줄어든다(도장)
                float r = Mathf.Clamp01((t - rankDelay) / 0.18f);
                SetAlpha(rankLabel.rectTransform, r);
                rankLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, r);

                if (priceLabel != null) SetAlpha(priceLabel.rectTransform, Mathf.Clamp01((t - priceDelay) / 0.2f));

                // 다 뜬 뒤엔 클릭으로 닫는다
                Mouse mouse = Mouse.current;
                if (t > priceDelay && mouse != null && mouse.leftButton.wasPressedThisFrame) closed = true;
                yield return null;
            }

            if (nameRoot != null) nameRoot.anchoredPosition = nameHome;
            for (float f = 0f; f < 0.2f; f += Dt)
            {
                group.alpha = 1f - f / 0.2f;
                yield return null;
            }
            HideNow();
            _routine = null;
        }

        private void HideNow()
        {
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(false);
        }

        // 자식 그래픽 전부의 알파를 곱하지 않고 CanvasGroup 없이 바로 바꾼다 — 부품마다 CanvasGroup을 두지 않으려고.
        private static void SetAlpha(RectTransform rt, float a)
        {
            if (rt == null) return;
            foreach (Graphic gr in rt.GetComponentsInChildren<Graphic>(true))
            {
                Color c = gr.color;
                c.a = a;
                gr.color = c;
            }
        }

        private static float EaseOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x) * (1f - x);
        }
    }
}
