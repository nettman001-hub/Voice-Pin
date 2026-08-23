# 다들려 (Voice-Pin)

틱톡 라이브 판매자의 음성을 실시간 STT로 텍스트 변환해 판매 내역을 자동 저장하고,
트리거 단어가 나오면 지정 영역을 화면 캡처하는 Windows 데스크톱 앱.

- 클라우드 STT: Deepgram Nova-3 (WebSocket 스트리밍, keyterm 바이어싱)
- 오디오: WASAPI 루프백 캡처 -> 16kHz/16bit/mono PCM
- 저장소: 로컬 SQLite (%APPDATA%\VoicePin\voicepin.db)

## 빌드 / 실행 / 테스트

```powershell
dotnet build .\VoicePin.slnx
dotnet run --project src\VoicePin.App
dotnet test  tests\VoicePin.Core.Tests
```

요구 사항: Windows 10+ (x64), .NET 8 SDK

## 시작하기 (필수 설정)

1. 앱 실행 -> 마이페이지 -> STT 설정(Deepgram)
2. Deepgram API 키 입력 후 저장 (모델 기본값 nova-3, 언어 ko)
   - 키는 Windows DPAPI(CurrentUser)로 암호화되어 %APPDATA%\VoicePin\settings.json 에만 저장되며
     소스 코드/저장소에는 절대 커밋되지 않음
3. 인식 단오 규칙 화면에서 캡처 영역(프리셋)과 규칙 확인
4. 틱톡 라이브를 켠 뒤 라이브 청취 홈 -> 청취 시작

## 핵심 흐름

WASAPI 루프백 -> Deepgram Nova-3 스트리밍(keyterms)
  -> TranscriptAnalyzer(규칙 매칭)
     - 판매확정 멘트: 닉네임/금액 추출 -> SQLite 저장 (누락 시 '보류', 중복 차단)
     - '캡처' 등 트리거 단어: 지정 영역 PNG 캡처 -> 내역 연결
음성 명령: "수정 시작" -> "닉네임은 OOO" / "금액은 N만원" -> "수정 완료" (무발화 10초 복귀)

## 구현 화면 (IA 경로)

| 경로 | 화면 | 상태 |
|---|---|---|
| /live | 라이브 청취 홈 | 실동작 |
| /voice-training | 음성 학습 녹음/반복 훈련 | 실동작 |
| /recognition-rules | 인식 단어/동작 규칙, 캡처 영역 | 실동작 |
| /sales, /sales/{id} | 판매 내역 목록/상세(+캡처 뷰어 모달) | 실동작 |
| /sales/review | 방송 후 일괄 확인/수정/확정 | 실동작 |
| /settlement | 기간별 집계 + CSV 내보내기(UTF-8 BOM) | 실동작 |
| /onboarding, /login, /signup, /password/reset | 인증 플로우 | 목업 |
| /pricing, /subscription/* | 요금제/결제/구독 관리 | 목업 |
| /notifications/settings, /my | 알림 설정, 마이페이지(STT 키 관리) | 목업/로컬 |
| /admin* | 관리자 대시보드 | 목업 |

목업 = 백엔드 서버 연동 전 단계로 UI와 로컬 상태만 동작.

## 프로젝트 구조

src/VoicePin.Core            - 도메인: Models, Services(계약), Rules, Listening, Export
src/VoicePin.Infrastructure  - Data(SQLite), Audio(NAudio), Stt(Deepgram), Capture(GDI), Security(DPAPI), Settings
src/VoicePin.App             - WPF 셸, RouteTable 네비게이션, Views
tests/VoicePin.Core.Tests    - 단위 테스트 (30개)
