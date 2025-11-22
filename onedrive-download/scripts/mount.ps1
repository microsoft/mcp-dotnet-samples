# Azure File Share 자동 마운트 스크립트 (Windows)
# azd postprovision 훅에서 자동으로 실행됩니다.

Write-Host "🔄 Azure File Share 로컬 마운트 시작..." -ForegroundColor Cyan

try {
    # 1. azd 환경변수에서 연결 문자열 가져오기
    Write-Host "📥 연결 문자열 추출 중..." -ForegroundColor Cyan

    $envValues = azd env get-values
    $connStringLine = $envValues | Where-Object { $_ -match "AZURE_STORAGE_CONNECTION_STRING" }

    if (-not $connStringLine) {
        Write-Error "❌ 스토리지 연결 문자열을 찾을 수 없습니다."
        exit 1
    }

    # "KEY=VALUE" 형태에서 VALUE만 추출 (따옴표 제거)
    $connString = $connStringLine -split "=", 2 | Select-Object -Last 1
    $connString = $connString.Trim('"').Trim()

    if ([string]::IsNullOrWhiteSpace($connString)) {
        Write-Error "❌ 연결 문자열이 비어 있습니다."
        exit 1
    }

    Write-Host "✓ 연결 문자열 추출 완료" -ForegroundColor Green

    # 2. AccountName과 AccountKey 파싱
    Write-Host "🔍 계정 정보 파싱 중..." -ForegroundColor Cyan

    $parts = $connString -split ";"
    $accountName = ($parts | Where-Object { $_ -match "AccountName=" }) -replace "AccountName=", ""
    $accountKey = ($parts | Where-Object { $_ -match "AccountKey=" }) -replace "AccountKey=", ""

    if (-not $accountName -or -not $accountKey) {
        Write-Error "❌ 계정 정보를 파싱할 수 없습니다. 연결 문자열 형식이 올바른지 확인하세요."
        exit 1
    }

    Write-Host "✓ 계정 정보 추출 완료 (Account: $accountName)" -ForegroundColor Green

    # 3. 마운트 설정
    $driveLetter = "Z:"
    $shareName = "downloads"
    $uncPath = "\\$accountName.file.core.windows.net\$shareName"

    # 4. 기존 연결 끊기
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

    # 5. 새로운 연결 실행
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
        Write-Host "  3. 계정 정보 오류" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "해결 방법:" -ForegroundColor Cyan
        Write-Host "  • Windows 방화벽 설정 확인" -ForegroundColor Cyan
        Write-Host "  • VPN 연결 시도" -ForegroundColor Cyan
        Write-Host "  • 연결 문자열 재확인" -ForegroundColor Cyan
        exit 1
    }

    # 6. 마운트 검증
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
    exit 1
}
