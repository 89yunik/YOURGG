# YOURGG 프로젝트

YOURGG는 Riot API를 사용하여 소환사의 최신 협곡 매치 데이터를 조회하고, 사용자에게 해당 정보를 제공하는 웹 애플리케이션입니다.  
이 프로젝트는 **ASP.NET Core MVC**를 기반으로 개발되었으며, Riot Games의 게임 데이터를 API를 통해 불러와 사용자에게 보여주는 기능을 제공합니다.

---

## 기능

- **소환사 검색**  
  사용자가 Riot ID (`소환사명#태그`)를 입력하면, 해당 소환사의 최신 매치 정보를 조회합니다.

- **매치 상세 정보**
- 최근 매치의 아래 정보를 제공합니다:

  - 게임일시, 게임시간
  - 게임 유형 (솔로랭크, 자유랭크, 일반 등)
  - 플레이어 이름, 챔피언, 챔피언 레벨
  - K/D/A, CS 수
  - 사용한 스펠 이미지, 아이템 이미지
  - 10인 전원 챔피언 이미지 및 소환사명

- **예외 및 캐싱 처리**
  - 소환사명을 잘못 입력하거나 데이터가 존재하지 않을 경우 오류 메시지를 출력합니다.
  - 동일한 소환사 요청은 메모리 캐시에 5분간 저장하여 API 호출을 줄입니다.

---

## 프로젝트 구성

### 1. `SummonerController.cs`

Riot API와의 연동을 처리하며, 사용자의 입력에 따라 소환사의 최신 협곡 매치 데이터를 조회하고 이를 뷰에 전달하는 역할을 합니다.

- `Search()`

  - **HTTP Method**: GET
  - 사용자가 소환사 이름을 입력할 수 있는 검색 페이지를 렌더링합니다.
    ![image](https://github.com/user-attachments/assets/8853f19a-2481-4228-9717-2485186a11f2)

- `MatchDetail(string summonerName)`

  - **HTTP Method**: POST
  - 사용자가 입력한 소환사 이름을 기반으로 최신 매치 데이터를 조회하고 뷰에 전달합니다.
  - 소환사 또는 매치 데이터를 찾지 못할 경우 에러 메시지를 ViewBag에 담아 전달합니다.
    ![image](https://github.com/user-attachments/assets/7fa126fb-7af8-42a4-b191-4aefdaf29081)

- `Error()`
  - **HTTP Method**: GET
  - 에러 발생 시 기본 오류 페이지를 반환합니다.
    ![image](https://github.com/user-attachments/assets/eab84ebd-ee1b-461f-a410-7654d7701a2c)

---

### 2. `RiotApiService.cs`

Riot API와 통신을 담당하는 서비스로, 주어진 소환사 이름에 대한 최신 협곡 매치 데이터를 가져오는 기능을 제공합니다.

- `GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)`

  - 전체 데이터 흐름을 조율하는 메인 메서드
  - 캐시 확인 → 소환사명 유효성 검사 → puuid 조회 → matchId 조회 → match 정보 조회 → ViewModel 생성 → 캐시 저장

- `TryGetFromCache(string summonerName, out MatchDetailViewModel? cachedMatch)`

  - 캐시에 소환사 전적 데이터가 존재하는지 확인
  - 존재할 경우 cachedMatch로 반환

- `GetPuuidAsync(string summonerName)`

  - Riot ID (게임이름#태그) → puuid 조회

- `GetLatestMatchIdAsync(string puuid)`

  - puuid를 기준으로 queue 420, 440, 400(솔랭, 자랭, 일반) 각각의 최근 게임 ID 1개씩 병렬 조회
  - 결과를 합쳐 가장 최근 matchId 반환

- `GetMatchInfoAsync(string matchId)`

  - matchId로부터 해당 경기의 info JSON 정보 조회

- `BuildMatchDetailViewModelAsync(JsonElement matchInfo, string puuid, string version)`

  - 경기 정보(JSON)에서 puuid에 해당하는 소환사의 정보 추출
  - 챔피언, 스펠, 아이템, KDA, CS, 게임 시간 등의 정보로 MatchDetailViewModel 생성

- `SetCache(string summonerName, MatchDetailViewModel matchDetail)`
  - 소환사 전적 데이터를 캐시에 5분간 저장

---

### 3. `MatchDetailViewModel.cs`

매치의 상세 정보를 표현하는 뷰 모델입니다. 이 모델은 사용자에게 매치 데이터를 제공하는 데 사용됩니다.

#### 주요 속성

| 속성명                       | 설명                                 |
| ---------------------------- | ------------------------------------ |
| `GameDate`                   | 게임 날짜                            |
| `GameDuration`               | 게임 시간                            |
| `SummonerName`               | 소환사 이름                          |
| `ChampionImgUrl`             | 챔피언 이미지 URL                    |
| `ChampLevel`                 | 챔피언 레벨                          |
| `Result`                     | 승리/패배 여부                       |
| `Participants`               | 플레이어 리스트                      |
| `Spells`                     | 사용한 소환사 주문 이미지 URL 리스트 |
| `Items`                      | 아이템 이미지 URL 리스트             |
| `Kills`, `Deaths`, `Assists` | K/D/A                                |
| `TotalCS`                    | 총 CS                                |
| `GameType`                   | 게임 타입(솔로랭크/자유랭크/일반)    |

---

### 4. `Views`

#### `Search.cshtml`

- **역할**: 사용자가 소환사 이름을 입력할 수 있는 검색 폼을 제공
- **경로**: `/Views/Summoner/Search.cshtml`

#### `MatchDetail.cshtml`

- **역할**: 매치 데이터를 시각적으로 표시 (챔피언 이미지, KDA, 아이템, 소환사 주문 등)
- **경로**: `/Views/Summoner/MatchDetail.cshtml`

#### `Error.cshtml`

- **역할**: 오류 발생 시 사용자에게 알리는 메시지를 포함한 기본 오류 페이지
- **경로**: `/Views/Summoner/Error.cshtml`

---

## 실행 방법

### 1. **프로젝트 클론**

```bash
git clone https://github.com/yourgg/yourgg.git
```

### 2. 필수 구성 요소 설치

- Visual Studio (ASP.NET Core 개발 가능) 또는 .NET CLI 설치
- Riot API 키 필요
  - `appsettings.json`에 다음과 같이 추가:
    ```json
    {
      "RiotApiKey": "YOUR_RIOT_API_KEY"
    }
    ```

### 3. 프로젝트 실행

```bash
dotnet run
```

또는 Visual Studio에서 IIS Express로 실행

### 4. 웹 브라우저에서 접속

```bash
http://localhost:{port}/summoner/search
```

## 개발 환경

- .NET 9.0 SDK 이상이 필요합니다.
- Visual Studio 2022 이상 버전에서 .NET 9.0을 지원합니다. 최신 버전의 Visual Studio가 설치되어 있어야 합니다.
- Visual Studio에서 .NET 9.0 프로젝트를 여는 경우, 필요한 SDK가 자동으로 감지됩니다. 만약 SDK가 감지되지 않는다면 [공식 .NET 사이트](https://dotnet.microsoft.com/download)에서 최신 SDK를 설치해 주세요.

## 라이센스

이 프로젝트는 MIT License를 따릅니다.
