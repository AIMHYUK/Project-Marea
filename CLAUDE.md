# Marea

농사 + 식당 경영 게임. Unity 6000.3.6f1 · URP. **2인 팀이고 사용자는 A 담당이다.**

세부는 `Docs/` 에 있다. 여기엔 매번 반복해서 말하게 되는 것만 적는다.

| 무엇을 알고 싶은가 | 어디 |
|---|---|
| 누가 뭘 맡았나, 계약이 뭔가 | `Docs/1차_구현_분담.md` |
| 왜 이렇게 짰나, 언제 뒤집히나 | `Docs/설계_결정.md` |
| 깔려 있는 것, 코드 제약 | `Docs/개발_환경.md` |

---

## 코드 제약

**레거시 `Input` 클래스를 쓰지 마라.** `activeInputHandler: 1` 이라 Input System
전용이다. 컴파일은 되고 **실행할 때** `InvalidOperationException` 이 난다.

```csharp
if (Input.GetMouseButtonDown(0))              // 안 됨
if (Mouse.current.leftButton.wasPressedThisFrame)   // 이렇게
```

**수동 이동도 `agent.Move()` 로 한다.** `transform.position` 에 직접 대입하면 NavMesh
밖으로 나가고, 그 뒤 클릭 이동이 조용히 실패한다.

**ScriptableObject에는 안 변하는 값만 넣는다.** "감자는 60초 걸린다"는 SO, "이 밭은
지금 42초째다"는 MonoBehaviour. SO는 실체가 하나라 인스턴스별 값을 넣으면 전부 공유된다.

**폴더가 소유자를 가른다.** 네임스페이스는 폴더와 같이 간다.

```
Assets/Scripts/
  Core/    IInteractable, IInteractor, InteractableBase, AgentMover   (A)
  Data/    IngredientData, MenuData, CropData                         (공유 / A)
  Player/  PlayerController, ClickSelector, CameraFollow              (A)
  Field/   Warehouse, FarmPlot, ServingStaff                          (A)
  Shop/    GameManager, Customer, Table, CookingStation…              (B)
  UI/      OrderBubbleUI, ResultPanelUI                               (B)
```

---

## 협업 경계 — 이게 제일 자주 어긋난다

**B 몫 파일을 만들지 마라.** `GameManager` `Customer` `Table` `CookingStation`
`CookingMiniGame` `SalesManager` `OrderBubbleUI` `ResultPanelUI` 는 상대가 짠다.
테스트에 필요하면 A 폴더 안에 임시 구현을 만들고 **"나중에 지운다"를 파일 맨 위에 적어라.**

**공유 지점 4개는 계약이다. 바꾸기 전에 사용자에게 먼저 말한다.**

| | 누가 주나 |
|---|---|
| `IngredientData` · `MenuData` | 같이 만든다 |
| `IInteractable` · `IInteractor` · `Warehouse` | A → B |
| `DayPhase` · `GameManager` | B → A |
| `IServeBoard` · `ServeTask` | B → A |

계약 밖(`InteractableBase` 내부, `AgentMover`, `PlayerController` private 부분)은
협의 없이 고쳐도 된다.

**서빙 직원(`ServingStaff`)에 `IInteractor`를 구현시키지 마라.** 이유는 설계 결정 10.
직원은 클릭 경로를 안 탄다 — `AgentMover` 만 플레이어와 공유한다.

---

## 작업 방식

**커밋하지 마라.** 사용자가 직접 한다. 그리고 **아직 git 저장소가 아니다.**

**지시에 `[완료]` 조건이 없으면 착수 전에 되물어라.** "무엇을 보면 됐다고 할지"를
먼저 정하지 않으면, 결과를 받고 나서 판단 기준이 그 결과에 오염된다.
길거나 애매한 작업은 시작 전에 이해한 목표·제약을 3줄로 요약해서 확인받는다.

**사실 주장은 확인하고 말한다.** 파일에 뭐가 있는지, 무엇이 이미 구현됐는지는
추측하지 말고 실제로 읽어라. 확인 못 했으면 못 했다고 말한다.

**작업 하나에 기록 하나** — `Docs/AI작업기록/NNN_<작업명>.md` 에 남긴다.
지시 원문 그대로 / 되읽기가 의도와 맞았나 / 몇 번 되돌렸나 / **사용자가 뭘 빠뜨렸었나**.
줄이거나 다듬지 않는다. 마지막 항목이 쌓여야 반복되는 구멍이 보인다.

---

## 문서 규칙

`Docs/` 에 새 `.md` 를 만들 때는 제목 바로 아래에 머리말을 넣는다.

```markdown
# 제목

> 상태: 진행중 · 갱신 2026-08-31 · 관련: [분담](1차_구현_분담.md)
> 무엇: 이 문서가 다루는 것 한 문장. 제목을 다시 쓰지 말 것.
```

상태는 `진행중` `완료` `구버전` `폐기` 중 하나. **고칠 때마다 `갱신` 날짜를 올린다.**

**바뀌거나 추가된 자리에는 `(+8/31)` 처럼 날짜를 붙인다.** 날짜 없는 줄은 최초
작성분이다. 안 붙이면 나중에 어디가 새 결정이고 어디가 원래 있던 건지 구분이 안 된다.

기존 문서를 갈아엎을 때는 **양쪽 다** 고친다 — 옛 문서는 상태를 `구버전`으로 바꾸고
`→ 대체: [새문서](새문서.md)` 를 넣는다. **옛 문서를 지우지 않는다.**
왜 그렇게 바뀌었는지는 대개 옛 문서에만 남아 있다.
