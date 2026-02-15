#!/bin/bash
# Azure File Share 자동 마운트 스크립트 (Mac/Linux)
# azd postprovision 훅에서 자동으로 실행됩니다.

set -e

echo "🔄 Azure File Share 로컬 마운트 시작..." >&2

# 1. azd 환경변수에서 연결 문자열 가져오기
echo "✓ azd 환경 변수에서 연결 문자열 추출 중..." >&2

CONN_STRING=$(azd env get-values | grep '^AZURE_STORAGE_CONNECTION_STRING=' | cut -d'=' -f2- | sed 's/^"//;s/"$//')

if [ -z "$CONN_STRING" ]; then
    echo "❌ 스토리지 연결 문자열을 찾을 수 없습니다." >&2
    exit 1
fi

echo "✓ 연결 문자열 추출 완료" >&2

# 2. AccountName과 AccountKey 파싱
ACCOUNT_NAME=$(echo "$CONN_STRING" | grep -o 'AccountName=[^;]*' | cut -d'=' -f2)
ACCOUNT_KEY=$(echo "$CONN_STRING" | grep -o 'AccountKey=[^;]*' | cut -d'=' -f2)

if [ -z "$ACCOUNT_NAME" ] || [ -z "$ACCOUNT_KEY" ]; then
    echo "❌ 계정 정보를 파싱할 수 없습니다." >&2
    echo "   AccountName: $ACCOUNT_NAME" >&2
    echo "   AccountKey: ${ACCOUNT_KEY:0:10}..." >&2
    exit 1
fi

echo "✓ 계정 정보 추출 완료 (Account: $ACCOUNT_NAME)" >&2

# 3. 마운트 설정
SHARE_NAME="downloads"
HOME_DIR=$(eval echo ~)
MOUNT_PATH="$HOME_DIR/Downloads/azure"

# 4. 마운트 폴더 생성
if [ ! -d "$MOUNT_PATH" ]; then
    echo "📁 마운트 폴더 생성 중: $MOUNT_PATH" >&2
    mkdir -p "$MOUNT_PATH" || {
        echo "❌ 마운트 폴더 생성 실패" >&2
        exit 1
    }
fi

# 5. Mac인지 Linux인지 확인
if [ "$(uname)" = "Darwin" ]; then
    # ===== Mac 마운트 =====
    echo "🍎 Mac 환경에서 마운트를 시도합니다." >&2

    SMB_URL="smb://$ACCOUNT_NAME:$ACCOUNT_KEY@$ACCOUNT_NAME.file.core.windows.net/$SHARE_NAME"

    echo "⚡ 마운트 시도: $SMB_URL -> $MOUNT_PATH" >&2

    # 기존 마운트 해제
    if mount | grep -q "$MOUNT_PATH"; then
        echo "⚠️  기존 마운트 해제 중..." >&2
        diskutil unmount "$MOUNT_PATH" 2>/dev/null || sudo umount "$MOUNT_PATH" 2>/dev/null || true
    fi

    # Mac용 mount_smbfs 사용
    if mount_smbfs "$SMB_URL" "$MOUNT_PATH" 2>/dev/null; then
        echo "✅ Mac 마운트 성공! [$MOUNT_PATH]" >&2
        open "$MOUNT_PATH" 2>/dev/null || true
        echo "🎉 Finder가 열렸습니다." >&2
    else
        echo "❌ Mac 마운트 실패" >&2
        echo "   'mount_smbfs'를 사용할 수 없습니다." >&2
        echo "   대신 Finder > 이동 > 서버에 연결에서 수동으로 연결하세요:" >&2
        echo "   $SMB_URL" >&2
        exit 1
    fi

else
    # ===== Linux 마운트 =====
    echo "🐧 Linux 환경에서 마운트를 시도합니다." >&2

    # cifs-utils 확인
    if ! command -v mount.cifs &> /dev/null; then
        echo "❌ cifs-utils가 설치되어 있지 않습니다." >&2
        echo "   다음 명령어로 설치해주세요:" >&2
        echo "   sudo apt-get install cifs-utils  (Ubuntu/Debian)" >&2
        echo "   sudo yum install cifs-utils      (CentOS/RHEL)" >&2
        exit 1
    fi

    UNC_PATH="//$ACCOUNT_NAME.file.core.windows.net/$SHARE_NAME"

    echo "⚡ 마운트 시도: $UNC_PATH -> $MOUNT_PATH" >&2

    # sudo 권한으로 마운트
    # 주의: azd가 sudo를 요청하면 패스워드 입력이 필요할 수 있습니다.
    if sudo mount -t cifs "$UNC_PATH" "$MOUNT_PATH" \
        -o username=$ACCOUNT_NAME,password=$ACCOUNT_KEY,vers=3.0,dir_mode=0755,file_mode=0755 2>/dev/null; then
        echo "✅ Linux 마운트 성공! [$MOUNT_PATH]" >&2
        echo "🎉 파일 탐색기로 폴더를 열어보세요:" >&2
        echo "   $MOUNT_PATH" >&2
    else
        echo "⚠️  Linux 마운트 실패 (sudo 권한이 필요할 수 있습니다)" >&2
        echo "   수동으로 마운트하려면 다음 명령을 실행하세요:" >&2
        echo "   sudo mount -t cifs \"$UNC_PATH\" \"$MOUNT_PATH\" \\" >&2
        echo "     -o username=$ACCOUNT_NAME,password=<PASSWORD>,vers=3.0,dir_mode=0755,file_mode=0755" >&2
        exit 1
    fi
fi

echo ""
echo "✅ Azure File Share 마운트 완료!" >&2
echo "   이제 OneDrive에서 다운로드한 파일이 $MOUNT_PATH에 저장됩니다." >&2
