using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using Marea.Restaurant;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// 특별 이벤트 — 선장과 선원들. (+10/9, 이슈 128)
    ///
    /// eventDay(2일차) 영업 첫 배로 big_ship이 선장 + 선원(모두 partySize명)을 태우고 온다(GuestBoatArrival이 이 컴포넌트를 보고
    /// 첫 배를 바꾼다). 빈자리만큼 앉고(선장이 첫 번째) 나머지 선원은 선착장 근처에 서서 기다린다.
    /// - 선장은 오늘의 메뉴 중 하나를 시키고 최우선(IsPriority) — 머리 위 "최우선!" 라벨, 완성 요리 · 서빙 직원이 먼저 간다.
    /// - 그 요리를 S(Perfect)로 받으면 성공: 그날 정산 ×revenueMultiplier, 가운데 위 알림.
    /// - A 이하로 받거나 · 다른 요리를 받거나 · patienceSeconds 안에 못 받으면 실패: 식당 바닥 빈 곳 hazardCount군데에
    ///   파손 장판(TripHazard)이 생기고, 알림, 일행이 모두 화내며 떠난다.
    /// 선원도 말한다(도착 · 성공 · 실패). 일행이 다 타면 배가 떠나고 그다음 일반 배가 온다.
    /// </summary>
    public class CaptainEvent : MonoBehaviour
    {
        [Header("언제 · 누가")]
        [Tooltip("이 일차 영업의 첫 배로 온다. 하루 한 번.")]
        [SerializeField, Min(1)] private int eventDay = 2;
        [Tooltip("GuestBoatArrival.boatTypes 중 몇 번 배로 오나. 1 = 중간 배(big_ship).")]
        [SerializeField, Min(0)] private int boatTypeIndex = 1;
        [Tooltip("선장 포함 일행 수. 빈자리만큼 앉고 나머지는 서 있는다.")]
        [SerializeField, Min(1)] private int partySize = 11;

        [Header("판정")]
        [Tooltip("선장이 주문하고 기다리는 시간(초). 지나면 실패.")]
        [SerializeField, Min(5f)] private float patienceSeconds = 90f;
        [Tooltip("성공하면 그날 정산에 더 곱할 배율.")]
        [SerializeField, Min(1f)] private float revenueMultiplier = 2f;

        [Header("실패 — 데크 파손")]
        [SerializeField] private GameObject hazardPrefab;
        [SerializeField, Min(0)] private int hazardCount = 2;
        [Tooltip("파손 장판끼리 · 기존 장판 · 좌석에서 이만큼 떨어진 곳만.")]
        [SerializeField, Min(0.5f)] private float hazardSpacing = 2f;
        [SerializeField] private VfxId breakVfx = VfxId.Dust;
        [SerializeField] private AudioClip breakClip;

        [Header("서 있는 선원")]
        [Tooltip("입구(손님 스폰 지점)에서 이 반경 안 빈 곳에 선다.")]
        [SerializeField, Min(1f)] private float crewStandRadius = 4.5f;

        [Header("글 — {0}은 선장 주문 메뉴(강조), {1}은 이/가")]
        [SerializeField] private string priorityLabel = "최우선!";
        [Tooltip("(+10/9) 메인 화면 왼쪽 알람(HudAlarm — 탐사정 귀환 알람과 같은 칸). 선장이 주문하면 뜨고 판정이 나면 내린다. {0}은 메뉴 이름.")]
        [SerializeField] private string alarmText = "선장의 요리를 최우선으로 만들어야 한다! ({0})";
        [SerializeField] private string successToast = "선장이 크게 만족했다! 오늘 수익이 두 배가 된다";
        [SerializeField] private string failToast = "선원들이 데크를 부수고 떠났다…";
        [SerializeField] private string[] captainOrderLines = { "{0}. 이 가게 최고의 솜씨로 가져와라.", "{0}. 최고의 솜씨로 부탁하지." };
        [SerializeField] private string[] crewArriveLines = { "우리 선장님은 최고의 요리만 드신다!", "S급이 아니면 각오해라!", "선장님을 기다리게 하지 마라!" };
        [SerializeField] private string[] captainSuccessLines = { "훌륭하군! 소문대로야." };
        [SerializeField] private string[] crewSuccessLines = { "역시 소문대로군!", "선장님이 웃으셨다!", "오늘은 실컷 먹자!" };
        [Tooltip("요리를 받았는데 S가 아닐 때.")]
        [SerializeField] private string[] captainFailLines = { "이걸 요리라고 내놓은 건가!" };
        [Tooltip("시간 안에 못 받았거나 다른 요리를 받았을 때.")]
        [SerializeField] private string[] captainNoFoodLines = { "감히 선장을 기다리게 해?", "이 가게는 손님을 굶기는군!" };
        [SerializeField] private string[] crewFailLines = { "이딴 걸 선장님께!", "부숴 버려!", "가자, 이런 가게는 필요 없다!" };

        private int _doneDay = -1;
        private WorldLabelUI _labels;
        private Marea.Hud.HudAlarm _alarm;

        public int BoatTypeIndex => boatTypeIndex;
        public int PartySize => partySize;

        /// <summary>오늘이 이벤트 날이고 아직 안 했는가.</summary>
        public bool IsToday => BusinessManager.Instance != null && BusinessManager.Instance.Day == eventDay && _doneDay != eventDay;

        private static bool IsOpen => BusinessManager.Instance != null && BusinessManager.Instance.CurrentState == BusinessState.Open;

        private void OnDisable()
        {
            if (_alarm != null) _alarm.Clear(this);
        }

        private void Awake()
        {
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);
            _alarm = FindAnyObjectByType<Marea.Hud.HudAlarm>(FindObjectsInactive.Include);
            if (_alarm == null)
                Debug.LogError($"{name}: 씬에 HudAlarm이 없다. '선장의 요리를 최우선으로' 알람이 안 뜬다.", this);
            if (hazardPrefab == null)
                Debug.LogError($"{name}: CaptainEvent.hazardPrefab이 비어 있다. 실패해도 데크가 안 부서진다.", this);
        }

        /// <summary>
        /// 배가 일행을 다 내린 뒤 GuestBoatArrival이 부른다. 판정이 나고 서 있는 선원이 배로 돌아갈 때까지 돈다.
        /// 앉은 일행(seated)은 그 뒤 GuestBoatArrival이 평소처럼 다 탈 때까지 기다린다.
        /// </summary>
        public IEnumerator Run(IReadOnlyList<CustomerController> seated, List<GameObject> extras, Vector3 entrance)
        {
            _doneDay = eventDay;
            CustomerController captain = seated.Count > 0 ? seated[0] : null;
            if (captain == null)
            {
                Debug.LogError($"{name}: 선장이 앉을 자리가 없었다. 이벤트를 건너뛴다.", this);
                yield return ReturnExtras(extras, entrance);
                yield break;
            }
            captain.IsPriority = true;
            captain.SetPatience(patienceSeconds);

            // 서 있는 선원은 입구 근처 빈 곳으로.
            List<Vector3> spots = StandSpots(entrance, extras.Count);
            for (int i = 0; i < extras.Count; i++) MoveExtra(extras[i], spots[i], null);

            // 선장이 앉아 주문하면 대사 — 선장 한마디, 서 있는 선원 몇이 거든다.
            while (IsOpen && captain != null && captain.State == CustomerState.WalkingToSeat) yield return null;
            if (captain != null && captain.State == CustomerState.WaitingOrder)
            {
                // 앉는 순간 말풍선(CustomerSpeech)이 일반 주문 대사를 먼저 한다 — 그 뒤에 덮어써야 선장 대사가 남는다.
                yield return new WaitForSeconds(0.3f);
                if (captain != null)
                {
                    Say(captain.gameObject, captainOrderLines);
                    string menu = captain.OrderedMenu != null ? captain.OrderedMenu.DisplayName : "";
                    if (_alarm != null) _alarm.Post(this, string.Format(alarmText, menu));
                }
                yield return CrewSay(extras, seated, crewArriveLines, 0.8f);
            }

            // 판정.
            bool resolved = false, success = false;
            while (IsOpen && !resolved)
            {
                if (captain == null) resolved = true;
                else if (captain.HasReceivedFood) { resolved = true; success = captain.ReceivedGrade == HitGrade.Perfect; }
                else if (captain.State == CustomerState.Leaving || captain.State == CustomerState.Finished) resolved = true;
                else if (_labels != null && captain.State == CustomerState.WaitingOrder)
                    _labels.Set(captain, captain.transform.position + Vector3.up * 3.4f, priorityLabel);
                yield return null;
            }
            if (_alarm != null) _alarm.Clear(this);   // 판정이 났거나 영업이 끝났다
            if (!IsOpen)
            {
                yield return ReturnExtras(extras, entrance);
                yield break;
            }

            if (success)
            {
                Settlement.SetEventMultiplier(eventDay, revenueMultiplier);
                Toast.Show(successToast);
                if (captain != null) Say(captain.gameObject, captainSuccessLines);
                yield return CrewSay(extras, seated, crewSuccessLines, 0.5f);
            }
            else
            {
                if (captain != null) Say(captain.gameObject, captain.HasReceivedFood ? captainFailLines : captainNoFoodLines);
                yield return CrewSay(extras, seated, crewFailLines, 0.4f);
                BreakDeck(captain != null ? captain.transform.position : entrance);
                Toast.Show(failToast);
                foreach (CustomerController c in seated)
                    if (c != null) c.LeaveAngry();
            }

            yield return new WaitForSeconds(1.5f);   // 대사를 보여 주고 돌아간다
            yield return ReturnExtras(extras, entrance);
        }

        // --- 서 있는 선원 ---

        private List<Vector3> StandSpots(Vector3 entrance, int count)
        {
            var spots = new List<Vector3>();
            for (int attempt = 0; attempt < count * 30 && spots.Count < count; attempt++)
            {
                Vector2 r = Random.insideUnitCircle * crewStandRadius;
                if (!NavMesh.SamplePosition(entrance + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                if (spots.Exists(p => (p - hit.position).sqrMagnitude < 0.8f * 0.8f)) continue;
                spots.Add(hit.position);
            }
            while (spots.Count < count) spots.Add(entrance);   // 자리가 모자라면 입구에 겹쳐 선다
            return spots;
        }

        private static void MoveExtra(GameObject extra, Vector3 to, System.Action onDone)
        {
            if (extra == null) { onDone?.Invoke(); return; }
            AgentMover mover = extra.GetComponent<AgentMover>();
            Animator anim = extra.GetComponentInChildren<Animator>();
            SetMoving(anim, true);
            if (mover == null) { extra.transform.position = to; SetMoving(anim, false); onDone?.Invoke(); return; }
            mover.GoTo(to,
                onArrived: () => { SetMoving(anim, false); onDone?.Invoke(); },
                onFailed: () => { SetMoving(anim, false); onDone?.Invoke(); },
                allowPartialPath: true);
        }

        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");

        private static void SetMoving(Animator anim, bool moving)
        {
            if (anim != null && anim.runtimeAnimatorController != null) anim.SetBool(ParamIsMoving, moving);
        }

        /// <summary>서 있는 선원을 입구로 걸려 보내고, 닿으면(= 배에 탐) 지운다. 다 탈 때까지 기다린다.</summary>
        private IEnumerator ReturnExtras(List<GameObject> extras, Vector3 entrance)
        {
            int left = 0;
            foreach (GameObject x in extras)
            {
                if (x == null) continue;
                left++;
                GameObject captured = x;
                MoveExtra(x, entrance, () => { if (captured != null) Destroy(captured); left--; });
            }
            for (float t = 0f; left > 0 && t < 20f; t += Time.deltaTime) yield return null;
            foreach (GameObject x in extras) if (x != null) Destroy(x);   // 길이 막혀 못 닿은 선원도 정리
            extras.Clear();
        }

        // --- 대사 ---

        private IEnumerator CrewSay(List<GameObject> extras, IReadOnlyList<CustomerController> seated, string[] lines, float gap)
        {
            // 서 있는 선원 먼저, 모자라면 앉은 선원(선장 빼고)에서 — 최대 대사 수만큼.
            var speakers = new List<GameObject>();
            foreach (GameObject x in extras) if (x != null && speakers.Count < lines.Length) speakers.Add(x);
            for (int i = 1; i < seated.Count && speakers.Count < lines.Length; i++)
                if (seated[i] != null) speakers.Add(seated[i].gameObject);

            for (int i = 0; i < speakers.Count; i++)
            {
                yield return new WaitForSeconds(gap);
                if (speakers[i] != null) Say(speakers[i], new[] { lines[i % lines.Length] });
            }
        }

        private static void Say(GameObject who, string[] lines)
        {
            if (who == null || lines == null || lines.Length == 0) return;
            CustomerSpeech speech = who.GetComponent<CustomerSpeech>();
            if (speech != null) speech.SayLine(lines[Random.Range(0, lines.Length)]);
        }

        // --- 실패: 데크 파손 ---

        private void BreakDeck(Vector3 near)
        {
            if (hazardPrefab == null || hazardCount <= 0) return;

            // 식당 바닥 = 켜진 좌석들을 감싼 상자(+2m). 좌석 · 기존 장판 · 새 장판 · 물체에서 떨어진 걸을 수 있는 곳.
            var seats = new List<Vector3>();
            foreach (Seat s in FindObjectsByType<Seat>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled && s.SitPoint != null) seats.Add(s.SitPoint.position);
            if (seats.Count == 0) { Debug.LogError($"{name}: 켜진 좌석이 없어 식당 바닥을 못 찾았다. 데크를 안 부순다.", this); return; }

            Bounds floor = new Bounds(seats[0], Vector3.zero);
            foreach (Vector3 p in seats) floor.Encapsulate(p);
            floor.Expand(new Vector3(4f, 0f, 4f));

            var taken = new List<Vector3>();
            foreach (TripHazard h in FindObjectsByType<TripHazard>(FindObjectsSortMode.None)) taken.Add(h.transform.position);

            int made = 0;
            for (int attempt = 0; attempt < 200 && made < hazardCount; attempt++)
            {
                var guess = new Vector3(Random.Range(floor.min.x, floor.max.x), floor.center.y, Random.Range(floor.min.z, floor.max.z));
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (Mathf.Abs(p.y - floor.center.y) > 1.5f) continue;   // 다른 층 · 물가 계단 말고 식당 바닥
                if (seats.Exists(s => Flat(s - p) < hazardSpacing * 0.8f)) continue;
                if (taken.Exists(t => Flat(t - p) < hazardSpacing)) continue;
                if (Blocked(p)) continue;

                GameObject hazard = Instantiate(hazardPrefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                hazard.name = $"TripHazard_Captain_{made + 1}";
                Vfx.Play(breakVfx, p + Vector3.up * 0.3f, 1.2f);
                SoundManager.PlayAt(breakClip, p, 0.9f);
                taken.Add(p);
                made++;
            }
            if (made < hazardCount)
                Debug.LogWarning($"{name}: 데크 파손 자리를 {hazardCount}곳 중 {made}곳만 찾았다 — 식당이 좁거나 물건이 많다.", this);
        }

        private static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        // 탁자 · 의자 · 기둥 같은 단단한 물체가 그 자리에 있으면 안 된다(바닥 자체는 아래라 안 걸린다).
        private static bool Blocked(Vector3 p)
        {
            foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 0.6f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<CustomerController>() != null) continue;   // 손님은 움직인다
                if (c.GetComponentInParent<Marea.Player.PlayerController>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
