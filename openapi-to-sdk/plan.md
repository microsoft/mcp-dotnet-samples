### Epic: OpenAPI 기반 SDK 자동 생성 (MCP Server)

-   사용자가 LLM 클라이언트를 통해 OpenAPI 명세를 제공하면, 원하는 언어의 SDK를 생성하고 받을 수 있는 MCP 서버 환경을 구축한다.

---

### Feature 1: SDK 생성 및 반환

-   **설명**: OpenAPI 명세를 받아 Kiota를 실행하고 결과를 압축한다.

-   User Story 1.1: 시스템으로서, 나는 OpenAPI 명세를 받아 SDK를 생성하고 압축하고 싶다.

    -   **Tasks**:
        -   [ ] OpenAPI URL에서 명세 콘텐츠를 다운로드하거나, 전달받은 콘텐츠를 처리하는 모듈 구현.
        -   [ ] Kiota CLI 명령으로 옵션을 매핑하는 로직 구현.
        -   [ ] Process 클래스를 이용한 Kiota CLI 실행 래퍼 구현.
        -   [ ] 임시 폴더에 Kiota를 실행하여 SDK 소스 코드 생성.
        -   [ ] 생성된 SDK 폴더 전체를 하나의 ZIP 파일로 압축하는 로직 구현.

-   User Story 1.2: LLM 클라이언트로서, 나는 SDK 생성 결과를 받고 싶다.
    -   **Tasks**:
        -   [ ] LLM 클라이언트가 접근 가능한 URI ZIP 파일의 경로를 반환한다.

---

### Feature 2: 오류 처리 및 피드백

-   **설명**: SDK 생성 전 과정에서 발생하는 오류를 감지하고, 이를 구조화된 형태로 사용자에게 전달한다.

-   User Story 2.1: LLM 클라이언트로서, 나는 SDK 생성 실패 오류 메시지를 받고 싶다.

    -   **Tasks**:
        -   [ ] 표준 오류 메시지의 JSON 구조 정의 (`{"errorCode": "string", "message": ...}`).
        -   [ ] 발생한 예외를 표준 오류 메시지로 변환 후, 적절한 응답 본문으로 반환.

-   User Story 2.2: 시스템으로서, 나는 Kiota 실행 오류를 구조화하여 처리하고 싶다.
    -   **Tasks**:
        -   [ ] Kiota CLI 실행 및 프로세스 관리를 위한 서비스 구현.
        -   [ ] Kiota 실행 결과 파싱 및 오류 매핑 로직 구현.
        -   [ ] 비동기 Kiota 실행 및 타임아웃 처리 구현.

---

### Feature 3: LLM 연동 및 실행 지원

-   **설명**: LLM 클라이언트가 서버의 기능을 쉽게 사용하도록 돕고, 다양한 환경에 MCP 서버를 실행할 수 있게 한다.

-   User Story 3.1: LLM 클라이언트로서, 나는 SDK 생성 기능을 쉽게 사용할 수 있도록 Pre-defined Prompt를 받고 싶다.

    -   **Tasks**:
        -   [ ] SDK 생성 요청(Tools)을 위한 Pre-defined Prompt 정의.

-   User Story 3.2: 시스템으로서, 나는 MCP 서버를 초기화하고 서비스를 등록하고 싶다.
    -   **Tasks**:
        -   [ ] Program.cs에서 하이브리드 MCP 서버 초기화 구현 (STDIO/HTTP 모드 지원).
        -   [ ] OpenApiToSdkAppSettings 구성 및 DI 컨테이너 등록.
        -   [ ] Kiota 실행을 위한 서비스 등록 (HttpClient, 파일 처리 서비스).
        -   [ ] 프롬프트 및 도구 자동 등록을 위한 어셈블리 스캔 설정.

---

### Feature 4: 인프라 및 배포 지원

-   **설명**: 다양한 환경에서 서버를 실행하고 배포할 수 있도록 지원한다.

-   User Story 4.1: 개발자로서, 나는 로컬 개발 환경에서 서버를 실행하고 싶다.

    -   **Tasks**:
        -   [ ] .vscode/mcp.stdio.local.json 및 mcp.http.local.json 설정 파일 작성.
        -   [ ] launchSettings.json에서 개발 환경 프로필 구성.
        -   [ ] appsettings.Development.json 작성.

-   User Story 4.2: 운영자로서, 나는 컨테이너 환경에서 서버를 배포하고 싶다.

    -   **Tasks**:
        -   [ ] .vscode/mcp.stdio.container.json 및 mcp.http.container.json 설정 파일 작성.
        -   [ ] Dockerfile.openapi-to-sdk 작성.

-   User Story 4.3: 운영자로서, 나는 Azure 환경에서 서버를 배포하고 싶다.
    -   **Tasks**:
        -   [ ] .vscode/mcp.http.remote.json 설정 파일 작성.
        -   [ ] Azure Bicep 템플릿 (main.bicep, resources.bicep) 작성.
        -   [ ] azure.yaml 및 배포 스크립트 작성.
