# Azure File Share 자동 마운트 스크립트 (Windows)
# azd postprovision 훅에서 자동으로 실행됩니다.

Write-Host "🔄 Azure File Share 로컬 마운트 시작..." -ForegroundColor Cyan

# 1. azd 환경변수에서 연결 문자열 가져오기
# azd env get-values는 현재 환경의 모든 변수를 가져옵니다.
try {
    $envValues = azd env get-values
    $connStringLine = $envValues | Where-Object { $_ -match "AZURE_STORAGE_CONNECTION_STRING" }

    if (-not $connStringLine) {
        Write-Error "❌ 스토리지 연결 문자열을 찾을 수 없습니다."
        exit 1
    }

    # "KEY=VALUE" 형태에서 VALUE만 추출 (따옴표 제거)
    $connString = $connStringLine -split "=", 2 | Select-Object -Last 1
    $connString = $connString.Trim('"')

    Write-Host "✓ 연결 문자열 추출 완료" -ForegroundColor Green

    # 2. AccountName과 AccountKey 파싱
    $parts = $connString -split ";"
    $accountName = ($parts | Where-Object { $_ -match "AccountName=" }) -replace "AccountName=", ""
    $accountKey = ($parts | Where-Object { $_ -match "AccountKey=" }) -replace "AccountKey=", ""

    if (-not $accountName -or -not $accountKey) {
        Write-Error "❌ 계정 정보를 파싱할 수 없습니다."
        Write-Error "AccountName: $accountName, AccountKey: $($accountKey.Substring(0, 10))..."
        exit 1
    }

    Write-Host "✓ 계정 정보 추출 완료 (Account: $accountName)" -ForegroundColor Green

    # 3. 마운트 설정
    $driveLetter = "Z:"
    $shareName = "downloads"
    $uncPath = "\\$accountName.file.core.windows.net\$shareName"

    # 4. 기존 연결 끊기 (재연결)
    if (Test-Path $driveLetter) {
        Write-Host "⚠️  기존 $driveLetter 연결 해제 중..." -ForegroundColor Yellow
        try {
            net use $driveLetter /delete /y 2>&1 | Out-Null
            Start-Sleep -Seconds 2
        }
        catch {
            Write-Host "기존 연결 제거 실패 (무시)" -ForegroundColor Yellow
        }
    }

    # 5. 새로운 연결 실행 (net use)
    Write-Host "⚡ 마운트 시도: $uncPath" -ForegroundColor Cyan
    Write-Host "드라이브: $driveLetter" -ForegroundColor Cyan
    Write-Host "명령: net use $driveLetter $uncPath /u:AZURE\$accountName ***" -ForegroundColor Gray

    $mountOutput = cmd /c "net use $driveLetter $uncPath /u:AZURE\$accountName $accountKey 2>&1"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ 마운트 명령 실패"
        Write-Error $mountOutput
        Write-Error ""
        Write-Error "⚠️  포트 445가 차단되었을 수 있습니다."
        Write-Error "   Windows 방화벽 설정을 확인하거나 VPN을 사용해보세요."
        exit 1
    }

    # 6. 마운트 검증
    Start-Sleep -Seconds 1
    if (Test-Path $driveLetter) {
        Write-Host ""
        Write-Host "✅ 마운트 성공! [Z:] 드라이브가 연결되었습니다." -ForegroundColor Green
        Write-Host ""
        Write-Host "📂 탐색기 자동 오픈 중..." -ForegroundColor Cyan

        # 탐색기 열기
        try {
            Invoke-Item $driveLetter
            Write-Host "✅ 탐색기가 열렸습니다." -ForegroundColor Green
        }
        catch {
            Write-Host "⚠️  탐색기 오픈 실패 (수동으로 열어주세요)" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "🎉 모든 설정이 완료되었습니다!" -ForegroundColor Green
        Write-Host "   이제 OneDrive에서 다운로드한 파일이 Z: 드라이브에 저장됩니다." -ForegroundColor Green
    }
    else {
        Write-Error "❌ 마운트 검증 실패"
        Write-Error "   $driveLetter 드라이브를 찾을 수 없습니다."
        exit 1
    }
}
catch {
    Write-Error "❌ 스크립트 실행 중 오류 발생"
    Write-Error $_
    exit 1
}
