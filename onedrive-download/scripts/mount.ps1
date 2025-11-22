# scripts/mount.ps1
# Azure File Share 자동 마운트 스크립트 (Windows)
# azd postprovision 훅에서 자동으로 실행됩니다.

# 1. 콘솔 인코딩 설정 (한글 깨짐 방지)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "🔄 [Post-Provision] Azure File Share 로컬 마운트 시작..."

# 2. 환경 변수 파일 찾기
$EnvName = $env:AZURE_ENV_NAME
if (-not $EnvName) { $EnvName = "default" }

$ScriptPath = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ScriptPath
$EnvFilePath = Join-Path $ProjectRoot ".azure" $EnvName ".env"

Write-Host "📂 환경 설정 파일 읽기: $EnvFilePath"

if (-not (Test-Path $EnvFilePath)) {
    # 혹시 .azure 폴더에 없으면 .env.local (로컬 개발용) 확인
    $EnvFilePath = Join-Path $ProjectRoot ".env.local"
    if (-not (Test-Path $EnvFilePath)) {
        Write-Warning "⚠️  환경 변수 파일을 찾을 수 없습니다. 마운트를 건너뜁니다."
        exit 0
    }
    Write-Host "📂 .env.local 파일로 대체합니다."
}

# 3. 파일 내용 읽어서 연결 문자열 찾기
$Content = Get-Content -Path $EnvFilePath -Raw -Encoding UTF8
$ConnString = ""

# 정규식으로 값 추출 (AZURE_STORAGE_CONNECTION_STRING)
if ($Content -match 'AZURE_STORAGE_CONNECTION_STRING="?([^"\r\n]+)"?') {
    $ConnString = $matches[1]
}

if (-not $ConnString) {
    Write-Warning "⚠️  파일에서 'AZURE_STORAGE_CONNECTION_STRING'을 찾을 수 없습니다."
    exit 0
}

# 4. AccountName과 AccountKey 파싱 (Regex 사용)
$AccountName = ""
$AccountKey = ""

if ($ConnString -match 'AccountName=([^;]+)') { $AccountName = $matches[1] }
if ($ConnString -match 'AccountKey=([^;]+)') { $AccountKey = $matches[1] }

# 디버깅용 출력 (키 값은 보안상 숨김)
Write-Host "   - Account: $AccountName"
if ($AccountKey) { Write-Host "   - Key: [확인됨]" } else { Write-Host "   - Key: [없음]" }

if (-not $AccountName -or -not $AccountKey) {
    Write-Error "❌ 연결 문자열 파싱 실패. (AccountName 또는 AccountKey 누락)"
    exit 0
}

# 5. 마운트 설정
$DriveLetter = "Z:"
$ShareName = "downloads"
$UncPath = "\\$AccountName.file.core.windows.net\$ShareName"

# 6. 기존 연결 끊기
if (Test-Path $DriveLetter) {
    Write-Host "⚠️  기존 $DriveLetter 연결 해제 중..."
    net use $DriveLetter /delete /y 2>&1 | Out-Null
}

# 7. 연결 실행
Write-Host "⚡ 연결 시도: $UncPath"
cmd /c "net use $DriveLetter $UncPath /u:AZURE\$AccountName $AccountKey"

# 8. 결과 확인
if (Test-Path $DriveLetter) {
    Write-Host "✅ 마운트 성공! 탐색기를 엽니다."
    try {
        Invoke-Item $DriveLetter
    }
    catch {
        Write-Host "⚠️  탐색기 오픈 실패 (수동으로 $DriveLetter를 열어주세요)"
    }
    Write-Host "🎉 완료! OneDrive 파일이 $DriveLetter 드라이브에 저장됩니다."
}
else {
    Write-Host "❌ 마운트 실패. 다음을 확인하세요:"
    Write-Host "   [원인 추정]"
    Write-Host "   1. 인터넷 통신사(ISP)가 445 포트를 차단했을 수 있습니다. (VPN 필요)"
    Write-Host "   2. 스토리지 계정 방화벽 설정 확인 필요"
    Write-Host "   3. 연결 문자열 (AZURE_STORAGE_CONNECTION_STRING) 확인"
    Write-Host ""
    Write-Host "   [수동 마운트]"
    Write-Host "   net use $DriveLetter $UncPath /u:AZURE\$AccountName <PASSWORD>"
}

# 마운트 실패해도 배포 프로세스는 성공으로 끝내기 (exit 0)
exit 0
