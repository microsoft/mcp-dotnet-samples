# scripts/mount.ps1
# Azure File Share 자동 마운트 스크립트 (Windows)
# azd postprovision 훅에서 자동으로 실행됩니다.

# 1. 에러 발생 시 즉시 중단 (Strict Mode)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "🔄 [Post-Provision] Azure File Share 로컬 마운트 시작..."

# ---------------------------------------------------------
# 2. 환경 변수 파일 찾기
# ---------------------------------------------------------
$EnvName = $env:AZURE_ENV_NAME
if (-not $EnvName) { $EnvName = "default" }

$ScriptPath = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $ScriptPath

# 우선순위 1: azd 환경 (.azure/...)
$EnvFilePath = Join-Path $ProjectRoot ".azure" $EnvName ".env"

if (-not (Test-Path $EnvFilePath)) {
    # 우선순위 2: 로컬 개발용 (.env.local)
    $EnvFilePath = Join-Path $ProjectRoot ".env.local"
}

if (-not (Test-Path $EnvFilePath)) {
    Write-Host "❌ [Error] 환경 변수 파일을 찾을 수 없습니다."
    Write-Host "   경로: $EnvFilePath"
    exit 1
}

Write-Host "✓ 파일 발견: $EnvFilePath"

# ---------------------------------------------------------
# 3. 파일 읽기 & 연결 문자열 추출
# ---------------------------------------------------------
$Content = Get-Content -Path $EnvFilePath -Raw -Encoding UTF8
$ConnString = ""

if ($Content -match 'AZURE_STORAGE_CONNECTION_STRING="?([^"\r\n]+)"?') {
    $ConnString = $matches[1]
}

if (-not $ConnString) {
    Write-Host "❌ [Error] 파일에 'AZURE_STORAGE_CONNECTION_STRING'이 없습니다."
    exit 1
}

Write-Host "✓ 연결 문자열 추출 완료"

# ---------------------------------------------------------
# 4. 계정 정보 파싱
# ---------------------------------------------------------
$AccountName = ""
$AccountKey = ""

if ($ConnString -match 'AccountName=([^;]+)') { $AccountName = $matches[1] }
if ($ConnString -match 'AccountKey=([^;]+)') { $AccountKey = $matches[1] }

Write-Host "   - Account: $AccountName"
if ($AccountKey) { Write-Host "   - Key: [설정됨]" } else { Write-Host "   - Key: [누락]" }

if (-not $AccountName -or -not $AccountKey) {
    Write-Host "❌ [Error] 연결 문자열 형식이 잘못되었습니다. (AccountName 또는 AccountKey 누락)"
    exit 1
}

# ---------------------------------------------------------
# 5. 마운트 실행 (엄격한 검사)
# ---------------------------------------------------------
$DriveLetter = "Z:"
$ShareName = "downloads"
$UncPath = "\\$AccountName.file.core.windows.net\$ShareName"

# 기존 연결 해제 (실패해도 무시 - 정리 작업)
if (Test-Path $DriveLetter) {
    Write-Host "⚠️  기존 $DriveLetter 연결 해제 중..."
    cmd /c "net use $DriveLetter /delete /y" 2>$null | Out-Null
}

Write-Host "⚡ 마운트 시도: $UncPath"

# net use 실행 및 종료 코드 캡처
$process = Start-Process -FilePath "net" `
    -ArgumentList "use $DriveLetter $UncPath /u:AZURE\$AccountName $AccountKey" `
    -Wait -NoNewWindow -PassThru

if ($process.ExitCode -ne 0) {
    Write-Host "❌ [Fatal Error] 'net use' 명령어 실패 (Exit Code: $($process.ExitCode))"
    Write-Host ""
    Write-Host "   [원인 추정]"
    Write-Host "   1. 인터넷 통신사(ISP)가 SMB 포트(445)를 차단했을 가능성"
    Write-Host "   2. 스토리지 계정 방화벽 설정 확인 필요"
    Write-Host "   3. 연결 문자열의 계정 정보가 올바른지 확인"
    Write-Host ""
    Write-Host "   [해결 방법]"
    Write-Host "   • VPN을 켠 후 다시 시도"
    Write-Host "   • 모바일 핫스팟으로 테스트"
    Write-Host "   • 스토리지 방화벽 설정 확인"
    exit 1
}

# ---------------------------------------------------------
# 6. 결과 검증
# ---------------------------------------------------------
if (-not (Test-Path $DriveLetter)) {
    Write-Host "❌ [Fatal Error] 마운트 명령은 성공했지만 드라이브에 접근할 수 없습니다."
    Write-Host "   상태: $DriveLetter 드라이브를 찾을 수 없음"
    exit 1
}

Write-Host "✅ 마운트 성공! [$DriveLetter] 드라이브 연결됨"

# 탐색기 자동 오픈
try {
    Invoke-Item $DriveLetter
    Write-Host "✅ 파일 탐색기가 열렸습니다."
}
catch {
    Write-Host "⚠️  탐색기 오픈 실패 (수동으로 $DriveLetter 열기)"
}

Write-Host "🎉 완료! OneDrive 파일이 $DriveLetter 드라이브에 저장됩니다."
exit 0
