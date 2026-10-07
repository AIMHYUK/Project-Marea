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
    /// </summary>
    public class DriftingCrate : InteractableBase
    {
        [Tooltip("주우면 창고로 들어갈 재료와 수량.")]
        [SerializeField] private RecipeEntry[] contents;

        [Header("연출")]
        [Tooltip("다 흘러와 멈췄을 때 띄울 알림. 비우면 안 띄운다.")]
        [SerializeField] private string landedMessage = "무언가 떠내려온 것 같다…";
        [SerializeField] private VfxId pickVfx = VfxId.Sparkle;
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
            foreach (RecipeEntry entry in contents)
                warehouse.Add(entry.ingredient, entry.requiredAmount);

            Vfx.Play(pickVfx, transform.position + Vector3.up * 0.6f, 0.8f);
            SoundManager.PlayAt(pickClip, transform.position, pickVolume);

            HasLanded = false;   // 같은 프레임에 두 번 줍지 않게
            Picked?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
