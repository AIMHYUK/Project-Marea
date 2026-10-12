using System;
using Marea.Core;
using Marea.Data;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 바다에서 떠내려오는 보급품 상자. (+10/7)
    ///
    /// DriftingCrateSpawner가 바다에 띄워 Launch로 데크 가장자리까지 흘려 보낸다. 다 흘러와 멈추면
    /// 주울 수 있다([E] 줍기 / 클릭) — 떠내려오는 중엔 못 줍는다(플레이어가 물을 따라갈 수 없다).
    /// 주우면 재료가 창고로 들어가고 상자는 사라진다. 한 번 줍고 끝이라 SupplyCrate(재충전)와 따로 둔다.
    ///
    /// 출렁임은 모델 자식의 FloatBob이 맡는다. 이건 위치만 옮긴다.
    ///
    /// (+10/12) 다 흘러와 멈추면 줍기 전까지 이펙트(landedLoopVfx — 물 뿌리기, (+10/13) 불꽃놀이 Firework 1 반복)를 계속 튼다. 자식으로 붙여 주우면 상자와 같이 사라진다.
    /// </summary>
    public class DriftingCrate : InteractableBase
    {
        [Tooltip("주우면 창고로 들어갈 재료. 수량은 amountRange가 정한다(0이면 여기 적힌 수량).")]
        [SerializeField] private RecipeEntry[] contents;
        [Tooltip("(+10/13) 재료마다 이 범위(최소~최대, 둘 다 포함)에서 무작위로 준다. x가 0이면 contents의 수량 그대로.")]
        [SerializeField] private Vector2Int amountRange = new(5, 10);
        [Tooltip("(+10/13) contents 중 이 범위(최소~최대 종류)만 무작위로 골라 준다. x가 0이면 전부.")]
        [SerializeField] private Vector2Int kindRange = new(1, 2);

        [Header("연출")]
        [Tooltip("다 흘러와 멈췄을 때 띄울 알림. 비우면 안 띄운다.")]
        [SerializeField] private string landedMessage = "무언가 떠내려온 것 같다…";
        [SerializeField] private VfxId pickVfx = VfxId.Sparkle;
        [Tooltip("(+10/12) 다 흘러와 멈춘 뒤 주울 때까지 계속 트는 이펙트(반복 재생 파티클 프리팹). 비우면 안 튼다.")]
        [SerializeField] private GameObject landedLoopVfx;
        [Tooltip("상자 기준 이펙트 자리. 통 윗면이 0.76m쯤이라 그 위.")]
        [SerializeField] private Vector3 landedVfxOffset = new(0f, 0.8f, 0f);
        [Tooltip("이펙트 크기 배율. 1이면 통에 비해 작아 잘 안 보였고 2.5는 너무 컸다(+10/13). 바꾸면 PF_CrateSprinkler의 물결 고리 높이(-0.8m ÷ 배율)도 맞출 것.")]
        [SerializeField, Min(0.01f)] private float landedVfxScale = 1.5f;
        [SerializeField] private AudioClip pickClip;
        [SerializeField, Range(0f, 1f)] private float pickVolume = 0.8f;

        private Vector3 _from;
        private Vector3 _to;
        private Vector3 _standPoint;
        private float _duration;
        private float _elapsed;
        private bool _launched;

        /// <summary>다 흘러와 멈췄는가.</summary>
        public bool HasLanded { get; private set; }

        /// <summary>주워졌다. 스포너가 자리를 비운다.</summary>
        public event Action<DriftingCrate> Picked;

        // 클릭하면 플레이어가 물속이 아니라 데크 가장자리로 걸어온다.
        public override Vector3 InteractPoint => _launched ? _standPoint : base.InteractPoint;

        private void Awake()
        {
            if (contents == null || contents.Length == 0)
                Debug.LogError($"{name}: DriftingCrate.contents가 비어 있다. 주워도 아무것도 안 들어온다.", this);
        }

        /// <param name="from">바다 쪽 출발점(수면 높이).</param>
        /// <param name="to">멈출 곳 — 데크 가장자리 바로 앞 물.</param>
        /// <param name="standPoint">주우러 온 플레이어가 설 데크 위 자리.</param>
        public void Launch(Vector3 from, Vector3 to, Vector3 standPoint, float seconds)
        {
            _from = from;
            _to = to;
            _standPoint = standPoint;
            _duration = Mathf.Max(0.1f, seconds);
            _elapsed = 0f;
            _launched = true;
            HasLanded = false;

            Vector3 dir = to - from;
            dir.y = 0f;
            transform.SetPositionAndRotation(from, dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, UnityEngine.Random.Range(-40f, 40f), 0f)
                : transform.rotation);
        }

        private void Update()
        {
            if (!_launched || HasLanded) return;

            _elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);   // 물살에 실려 오다 천천히 멎는다
            transform.position = Vector3.Lerp(_from, _to, eased);
            if (k >= 1f)
            {
                HasLanded = true;
                if (!string.IsNullOrEmpty(landedMessage)) Toast.Show(landedMessage);
                if (landedLoopVfx != null)
                {
                    GameObject fx = Instantiate(landedLoopVfx, transform.position + landedVfxOffset, Quaternion.identity, transform);
                    fx.transform.localScale = Vector3.one * (landedVfxScale / Mathf.Max(0.0001f, transform.lossyScale.x));
                }
            }
        }

        public override bool CanInteract(IInteractor actor) => HasLanded;

        public override string InteractLabel(IInteractor actor) => "줍기";

        public override void Interact(IInteractor actor)
        {
            if (!HasLanded) return;

            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 주운 재료가 갈 곳이 없다.", this);
                return;
            }
            // (+10/13) contents를 섞어 앞에서 kindRange개만 준다
            var pool = new System.Collections.Generic.List<RecipeEntry>(contents);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int kinds = kindRange.x > 0 ? Mathf.Min(pool.Count, UnityEngine.Random.Range(kindRange.x, Mathf.Max(kindRange.x, kindRange.y) + 1)) : pool.Count;

            var got = new System.Collections.Generic.List<string>();
            for (int k = 0; k < kinds; k++)
            {
                RecipeEntry entry = pool[k];
                int amount = amountRange.x > 0 ? UnityEngine.Random.Range(amountRange.x, Mathf.Max(amountRange.x, amountRange.y) + 1) : entry.requiredAmount;
                warehouse.Add(entry.ingredient, amount);
                if (entry.ingredient != null && amount > 0)
                    got.Add($"<b>{entry.ingredient.DisplayName}</b> {amount}개");
            }
            // (+10/8) 알림 — "고기 2개를 획득했습니다!" (늘 "개"로 끝나 조사는 "를")
            if (got.Count > 0) Toast.Show(string.Join(", ", got) + "를 획득했습니다!");

            Vfx.Play(pickVfx, transform.position + Vector3.up * 0.6f, 0.8f);
            SoundManager.PlayAt(pickClip, transform.position, pickVolume);

            HasLanded = false;   // 같은 프레임에 두 번 줍지 않게
            Picked?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
