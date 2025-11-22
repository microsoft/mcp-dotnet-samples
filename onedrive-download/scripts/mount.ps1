# Azure File Share 자동 마운트 스크립트 (Windows)
# azd postprovision 훅에서 자동으로 실행됩니다.

Write-Host "🔄 Azure File Share 로컬 마운트 시작..." -ForegroundColor Cyan

try {
    # 1. .env 파일 경로 찾기
    Write-Host "📂 .env 파일 위치 탐색 중..." -ForegroundColor Cyan

    $envFilePath = ""
    $azdEnvName = $env:AZURE_ENV_NAME

    if ($azdEnvName) {
        # .azure/{env_name}/.env 경로
        $envFilePath = ".\.azure\$azdEnvName\.env"
    }
    else {
        Write-Host "⚠️  AZURE_ENV_NAME 환경변수가 설정되지 않았습니다." -ForegroundColor Yellow
        Write-Host "   기본 경로 사용: .\.azure\main\.env" -ForegroundColor Yellow
        $envFilePath = ".\.azure\main\.env"
    }

    if (-not (Test-Path $envFilePath)) {
        Write-Error "❌ .env 파일을 찾을 수 없습니다."
        Write-Error "   경로: $envFilePath"
        Write-Host ""
        Write-Host "👉 팁: infra/main.bicep 파일에서 appSettings 설정을 확인하세요." -ForegroundColor Cyan
        exit 1
    }

    Write-Host "✓ .env 파일 발견: $envFilePath" -ForegroundColor Green

    # 2. .env 파일에서 AZURE_STORAGE_CONNECTION_STRING 추출 (정규식)
    Write-Host "📥 연결 문자열 추출 중..." -ForegroundColor Cyan

    $content = Get-Content -Path $envFilePath -Raw -Encoding UTF8
    $connString = ""

    # 정규식으로 AZURE_STORAGE_CONNECTION_STRING="값" 또는 AZURE_STORAGE_CONNECTION_STRING=값 찾기
    if ($content -match 'AZURE_STORAGE_CONNECTION_STRING\s*=\s*"?([^"\n\r]+)"?') {
        $connString = $matches[1].Trim()
    }

    if (-not $connString) {
        Write-Error "❌ .env 파일에 'AZURE_STORAGE_CONNECTION_STRING'이 없거나 형식이 잘못되었습니다."
        Write-Host ""
        Write-Host "파일 내용 (처음 10줄):" -ForegroundColor Gray
        Get-Content -Path $envFilePath | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        Write-Host ""
        Write-Host "👉 팁: infra/ 폴더의 bicep 파일에 appSettings 설정을 추가했는지 확인하세요." -ForegroundColor Cyan
        exit 1
    }

    Write-Host "✓ 연결 문자열 추출 완료" -ForegroundColor Green

    # 3. AccountName과 AccountKey 추출 (정규식)
    Write-Host "🔍 계정 정보 파싱 중..." -ForegroundColor Cyan

    $accountName = ""
    $accountKey = ""

    if ($connString -match 'AccountName=([^;]+)') {
        $accountName = $matches[1].Trim()
    }

    if ($connString -match 'AccountKey=([^;]+)') {
        $accountKey = $matches[1].Trim()
    }

    if (-not $accountName -or -not $accountKey) {
        Write-Error "❌ 연결 문자열 형식이 올바르지 않습니다."
        Write-Error "   AccountName: $($accountName -or '(없음)')"
        Write-Error "   AccountKey: $(if ($accountKey) { '(설정됨)' } else { '(없음)' })"
        Write-Host ""
        Write-Host "연결 문자열 형식 예:" -ForegroundColor Cyan
        Write-Host "  DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=xxx;EndpointSuffix=core.windows.net" -ForegroundColor Gray
        exit 1
    }

    Write-Host "✓ 계정 정보 추출 완료 (Account: $accountName)" -ForegroundColor Green

    # 4. 마운트 설정
    $driveLetter = "Z:"
    $shareName = "downloads"
    $uncPath = "\\$accountName.file.core.windows.net\$shareName"

    # 5. 기존 연결 끊기
    Write-Host "🔌 기존 연결 정리 중..." -ForegroundColor Cyan

    if (Test-Path $driveLetter) {
        Write-Host "⚠️  기존 $driveLetter 연결 해제 중..."
        try {
            net use $driveLetter /delete /y 2>&1 | Out-Null
            Start-Sleep -Seconds 1
        }
        catch {
            Write-Host "기존 연결 제거 시도 (무시)" -ForegroundColor Yellow
        }
    }

    # 6. 새로운 연결 실행
    Write-Host ""
    Write-Host "⚡ 새로운 연결 시도 중..." -ForegroundColor Cyan
    Write-Host "  UNC 경로: $uncPath" -ForegroundColor Gray
    Write-Host "  드라이브: $driveLetter" -ForegroundColor Gray
    Write-Host "  사용자: AZURE\$accountName" -ForegroundColor Gray

    $mountOutput = cmd /c "net use $driveLetter $uncPath /u:AZURE\$accountName $accountKey 2>&1"
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host ""
        Write-Error "❌ 마운트 명령 실패 (종료 코드: $exitCode)"
        Write-Error "출력: $mountOutput"
        Write-Host ""
        Write-Host "⚠️  가능한 원인:" -ForegroundColor Yellow
        Write-Host "  1. 포트 445가 방화벽으로 차단됨" -ForegroundColor Yellow
        Write-Host "  2. VPN 또는 네트워크 설정 문제" -ForegroundColor Yellow
        Write-Host "  3. 계정 정보 오류 (자격 증명 불일치)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "해결 방법:" -ForegroundColor Cyan
        Write-Host "  • Windows 방화벽 설정에서 포트 445 활성화" -ForegroundColor Cyan
        Write-Host "  • VPN 연결 시도" -ForegroundColor Cyan
        Write-Host "  • .env 파일의 AZURE_STORAGE_CONNECTION_STRING 재확인" -ForegroundColor Cyan
        Write-Host "  • 'net use * /delete /y'로 모든 연결 초기화 후 재시도" -ForegroundColor Cyan
        exit 1
    }

    # 7. 마운트 검증
    Start-Sleep -Seconds 1
    if (Test-Path $driveLetter) {
        Write-Host ""
        Write-Host "✅ 마운트 성공! [$driveLetter] 드라이브가 연결되었습니다." -ForegroundColor Green
        Write-Host ""
        Write-Host "📂 파일 탐색기 자동 오픈 중..." -ForegroundColor Cyan

        try {
            Invoke-Item $driveLetter
            Start-Sleep -Seconds 1
            Write-Host "✅ 파일 탐색기가 열렸습니다." -ForegroundColor Green
        }
        catch {
            Write-Host "⚠️  파일 탐색기 자동 오픈 실패 (수동으로 $driveLetter를 열어주세요)" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "🎉 모든 설정이 완료되었습니다!" -ForegroundColor Green
        Write-Host "   이제 OneDrive에서 다운로드한 파일이 $driveLetter 드라이브에 저장됩니다." -ForegroundColor Green
        Write-Host ""
    }
    else {
        Write-Error "❌ 마운트 검증 실패"
        Write-Error "   $driveLetter 드라이브를 찾을 수 없습니다."
        Write-Error "   마운트 명령은 성공했으나 드라이브 접근 실패 상태입니다."
        exit 1
    }
}
catch {
    Write-Error "❌ 스크립트 실행 중 오류 발생"
    Write-Error $_.Exception.Message
    Write-Error "스택 추적: $($_.ScriptStackTrace)"
    exit 1
}
