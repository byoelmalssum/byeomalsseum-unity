# 별말씀 · Unity 합본 (팀 오롯)

순우리말 한 단어를 하나의 공간으로 체험하는 인터랙티브 미디어 작품 **「별말씀」**의 Unity 프로젝트예요.
4명이 이 저장소 하나를 **GitHub Desktop**으로 같이 씁니다. 명령어는 몰라도 돼요.

| 항목 | 값 |
|---|---|
| Unity | **6000.6.0f1** (꼭 이 버전으로!) |
| 렌더 파이프라인 | URP (Universal 3D) |
| 협업 도구 | GitHub Desktop · 브랜치는 `main` 하나만 |

📘 자세한 설명서: `별말씀_GitHub협업가이드.pdf` · `별말씀_단어공간_작업가이드.pdf`

---

## 🗂 담당 단어와 폴더

| 단어 | 내 작업 폴더 | 공간에 넣을 자모 |
|---|---|---|
| 길차다 | `Assets/Words/길차다` | 7 |
| 논틀밭틀 | `Assets/Words/논틀밭틀` | 12 |
| 살천스럽다 | `Assets/Words/살천스럽다` | 13 |
| 퍼르퍼르 | `Assets/Words/퍼르퍼르` | 8 |

`Assets/Byeolmalsseum` = 공용 작업 도구 코드 (고치기 전에 단톡방 공지!)

---

## 🛠 처음 한 번 세팅

1. Unity Hub에서 **6000.6.0f1** 설치
2. [GitHub Desktop](https://desktop.github.com) 설치
3. GitHub Desktop → **Sign in to GitHub.com** → 팀 공용 계정으로 로그인 → **Authorize desktop**
   (계정 정보는 단톡방 / 버튼이 흐리면 3~5초 기다리기)
4. **Configure Git** 화면 → **Configure manually** → **Name을 내 이름으로** 바꾸고 **Finish**
   (같은 계정이어도 기록에 각자 이름이 남아요. 나중엔 `File > Options > Git`)
5. `File > Clone repository` → **GitHub.com** 탭 → `byeomalsseum-unity` → Local path는 `C:\Unity` 처럼 짧게 → **Clone**
   (OneDrive·바탕화면 같은 동기화 폴더는 피하기)
6. Unity Hub → 프로젝트 → **추가 ▼ → 디스크에서 프로젝트 추가** → 받은 `byeomalsseum-unity` 폴더 선택
   (⚠️ "저장소에서 프로젝트 추가"는 쓰지 마세요)

---

## 🔁 매일 작업 흐름

```
① 받기      Fetch origin → Pull origin       (Unity는 저장했거나 닫은 상태)
② 작업      Unity에서 Tools > 별말씀 > 공간 씬 만들기 · 열기 > 내 단어 → 작업 → Ctrl+S
③ 확인      GitHub Desktop 왼쪽 Changes: 경로가 Assets/Words/내 단어/… 인지 확인
④ 기록      Summary 한 줄 쓰고 → Commit to main
⑤ 올리기    Push origin   ← 하루 끝엔 꼭!
```

**Summary 예시:** `[길차다] ㅣ를 기둥으로 배치` · `[퍼르퍼르] 카메라 경로 수정`

---

## ✅ 규칙

1. **시작 전 Pull, 끝나면 Commit → Push** — 며칠씩 쌓아두지 않기
2. **내 `Assets/Words/내 단어` 폴더만 수정** — 남의 씬은 구경만, 저장(Ctrl+S) 금지
3. **Commit 전에 목록 훑어보기** — 내 폴더 밖 파일은 체크 해제하고 물어보기
4. **공용 코드(`Assets/Byeolmalsseum`) 수정 전엔 단톡방 공지** — "나 ○○ 고칠게"
5. **`ProjectSettings`, `Packages`(패키지 설치)는 담당자만**
6. **에셋 스토어 등 외부 에셋은 Import 후 내 폴더 안으로 옮기기**
7. **Unity 버전은 6000.6.0f1만**
8. **Pull 전엔 저장, 가능하면 Unity 닫기**
9. **`.meta` 파일도 같이 올리기** (체크된 그대로 두면 OK)
10. **100MB 넘는 파일은 미리 말하기** (GitHub 업로드 한도)

🚫 Branch 만들기·바꾸기, 저장소 Settings 건드리기, 모르는 팝업 아무거나 누르기 → **캡처해서 단톡방에!**

---

## 🧭 버튼 사전 (GitHub Desktop은 영어만 지원)

| 버튼 | 뜻 |
|---|---|
| **Fetch origin** | 새로 올라온 거 있는지 확인 |
| **Pull origin** | 받기 |
| **Commit to main** | 내 작업 기록하기 |
| **Push origin** | 올리기 |
| **Changes / History** | 바뀐 파일 / 지금까지의 기록 |
| **Discard changes** (파일 우클릭) | 내 변경 취소 (휴지통으로 감) |

---

## 🆘 문제가 생겼을 때

| 증상 | 해결 |
|---|---|
| Push 때 `rejected` / `newer commits` | **Pull origin** 먼저 → 다시 **Push origin** |
| **Resolve conflicts** 창 | 아무것도 고르지 말고 캡처해서 단톡방 |
| 내 폴더 밖 파일이 Changes에 뜸 | 체크 해제 후 Commit (계속 뜨면 Discard changes) |
| 남의 씬을 실수로 저장 | Commit 전: 우클릭 → Discard changes / Push 후: 단톡방에 알리기 (기록에서 복구 가능) |
| Changes에 파일 수만 개, `Library` 보임 | **Commit 하지 말고** 바로 알리기 |
| 분홍색 물체 / Missing | Pull 다시 → 그래도 그러면 어느 씬·물체인지 알리기 |
| Pull 후 Unity에 "Reload" 창 | 내 작업 저장했으면 **Reload** |

> 덮어써도 괜찮아요 — GitHub는 올릴 때마다 전체를 기록해 둬서 이전 상태로 되돌릴 수 있어요.

---
글꼴: [Pretendard](https://github.com/orioncactus/pretendard) — SIL Open Font License 1.1
