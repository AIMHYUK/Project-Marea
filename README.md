# Project Marea

농사 + 식당 경영 게임. **Unity 6000.3.6f1 · URP 17.3.0**
**4인 팀 — 프로그래머 2(A·B) · 아트 1 · 기획 1.**

플레이 구조: 재료 준비 → 영업 시작 → 주문 → 요리 → 판매 → 정산 → 성장

## 처음 받았다면

1. **`git lfs install` 을 먼저 한 번 한다.** 텍스처·모델이 LFS로 올라가 있어서, 안 하면
   텍스처 대신 포인터 텍스트가 받아진다 — [아트 에셋 — Git LFS](#아트-에셋--git-lfs-915) (+9/15)
2. Unity Hub에서 **6000.3.6f1** 로 연다. 버전이 다르면 프로젝트가 조용히 업그레이드되고 되돌리기 어렵다
3. 첫 임포트가 오래 걸린다 — `Library/` 를 커밋하지 않기 때문이다 (수 GB짜리 로컬 캐시)
4. 씬은 `Assets/Scenes/SampleScene.unity` 하나다

`Assets/MCPForUnity/` 는 **커밋에서 빠져 있다.** 유니티 에디터를 AI 클라이언트에 붙이는
에디터 도구라 게임 코드가 참조하는 게 없다. 쓸 사람만 각자 넣는다 —
절차는 [개발 환경](Docs/개발_환경.md) 문서에.

## 작업 전에 알아둘 것

- **브랜치를 판다.** `main` 에 직접 커밋하지 않는다. `feature/<이슈번호>-<짧은이름>` 으로
  작업하고 PR로 머지한다. 본문에 `Closes #N` 을 넣으면 이슈가 자동으로 닫힌다
- ⚠️ **씬 파일(`.unity`)을 건드리는 작업은 시작 전에 말한다.** UnityYAMLMerge를 아직
  안 켰기 때문에 충돌하면 손으로 푼다. **브랜치를 나눠도 이건 안 막아준다** —
  머지 시점에 똑같이 충돌한다
- `Docs/설계_결정.md` 는 결정마다 **"언제 뒤집히나"** 를 달아뒀다. 조건이 바뀌었다 싶으면
  코드를 고치기 전에 그걸 먼저 본다

## 아트 에셋 — Git LFS (+9/15)

텍스처·모델·사운드(png, psd, tga, exr, fbx, obj, glb, wav 등)는 **Git LFS로 올라간다.**
git에는 133바이트짜리 포인터만 들어가고 실제 파일은 LFS 저장소에 따로 올라간다.
규칙이 `.gitattributes` 에 있어서 **add · commit · push 는 평소대로 하면 된다.**

- **PC마다 한 번 `git lfs install`.** 안 하면 **경고 없이** 일반 파일로 커밋된다 —
  4K 텍스처 한 장에 20MB씩 히스토리에 영구히 쌓이고 지워도 안 줄어든다. GitHub Desktop은 LFS가 내장돼 있다
- 올린 게 LFS로 갔는지: `git lfs ls-files` 에 새 파일이 나오면 된다
- 유니티에서 텍스처가 핑크색이거나 파일이 133바이트면 LFS를 안 받은 것이다 → `git lfs pull`
- 같은 텍스처를 둘이 동시에 고치면 병합이 안 된다. 하나를 골라야 하니 고치기 전에 말한다

### `art-test` 에서 작업하던 경우 — 한 번만

`art-test` 의 커밋 4개(BO · 물 높이 · 식품 에셋 · 머티리얼 이름)를 **LFS로 옮겨
`feature/art-lfs` 로 다시 올렸다** (#38). 파일 내용은 같다.
**`art-test` 에는 더 커밋하지 않는다** — 거기엔 텍스처 262MB가 일반 파일로 들어 있어서,
이어서 쓰면 그게 그대로 main까지 따라온다.

```bash
git lfs install
git fetch origin
git status                    # 커밋 안 한 변경이 있는지 먼저 본다
git switch feature/art-lfs    # 커밋 안 한 변경은 그대로 따라온다
git lfs pull
```

GitHub Desktop이면 `Fetch origin` → `Current Branch` 에서 `feature/art-lfs` 선택 →
변경이 있으면 **"Bring my changes to feature/art-lfs"** 를 고른다.

- ⚠️ **`art-test` 에 푸시 안 한 커밋이 있거나 switch가 거부되면 멈추고 A에게 말한다.**
  그대로 옮기면 텍스처가 일반 파일로 딸려온다
- 옮긴 뒤 유니티로 열어 텍스처가 멀쩡한지 본다
- 확인되면 로컬 옛 브랜치를 지운다: `git branch -D origin/feature/art-test`
  (이름이 잘못 만들어져 원격엔 `origin/origin/feature/art-test` 로 올라가 있다. 원격 쪽은 A가 지운다)
- #38 이 머지된 뒤 새 작업은 `main` 에서 브랜치를 판다

## 문서부터 읽는다

| 무엇을 알고 싶은가 | 어디 |
|---|---|
| 누가 뭘 맡았나, 넘어가는 지점의 계약 | [Docs/1차_구현_분담.md](Docs/1차_구현_분담.md) |
| 왜 이렇게 짰나, **언제 뒤집히나** | [Docs/설계_결정.md](Docs/설계_결정.md) |
| 깔려 있는 것, 코드 제약 | [Docs/개발_환경.md](Docs/개발_환경.md) |

## 분담

- **A** — 플레이어 조작 · 상호작용 뼈대 · 창고 · 농사 · 서빙 직원
- **B** — 영업 루프 · 손님 · 주문 · 요리(미니게임 포함) · 매출 · 정산

**서로의 파일을 열지 않는다.** 넘어가는 지점은 네 개뿐이고,
[분담 문서](Docs/1차_구현_분담.md)의 "공유 지점" 절에 시그니처까지 적혀 있다.
그 넷을 바꿀 때는 상대에게 먼저 말한다.

## 코드 규칙

- **레거시 `Input` 클래스를 쓰지 않는다.** `activeInputHandler: 1` 이라 Input System 전용이다.
  컴파일은 되고 **실행할 때** `InvalidOperationException` 이 난다
- **`Keyboard.current` / `Mouse.current` 를 직접 폴링하지 않는다.** 입력은 액션 에셋을 통해
  읽고, 읽는 자리는 리더 컴포넌트 한 곳에 모은다 (A는 `PlayerInputReader`)
- **남의 `.inputactions` 를 열지 않는다.** 새 입력이 필요하면 **자기 에셋을 새로 만든다.**
  `.inputactions` 는 JSON이라 둘이 건드리면 머지 충돌을 손으로 풀어야 한다.
  미니게임 입력이 여기 해당한다 — [설계 결정 11](Docs/설계_결정.md) 참고
- **`IInteractable` 을 직접 구현하지 않는다.** 반드시 `InteractableBase` 를 상속한다 —
  `ClickSelector` 가 base 타입으로 대상을 찾기 때문에 직접 구현하면 클릭이 아예 안 잡힌다
- **여러 프레임 이어지는 상호작용은 `actor.BeginBusy()` / `EndBusy()` 로 감싼다** (미니게임 등).
  안 감싸면 진행 중에 WASD로 걸어나갈 수 있고, `EndBusy()` 를 빠뜨리면 **플레이어가 영구히 잠긴다**
- **ScriptableObject에는 안 변하는 값만** 넣는다. 런타임에 변하는 건 MonoBehaviour 쪽
- **폴더가 소유자를 가른다** — `Core/` `Data/` `Player/` `Field/` 는 A,
  `Shop/` `UI/` 는 B

## 아직 안 정한 것

- 브랜치 전략 (지금은 `main` 하나)
- 대표 메뉴 미니게임 1종이 무엇인지
- "오늘의 메뉴"를 무엇이 정하는지 (1차는 고정 목록)
