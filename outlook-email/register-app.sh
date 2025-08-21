#!/usr/bin/env bash
set -euo pipefail

# register-app.sh
# Registers an Entra ID (Azure AD) application for the Outlook Email MCP sample.
# Features:
# - Display name: mcp-outlookemail-<random 4 digits>
# - SPA redirect URI: http://localhost
# - Adds Microsoft Graph application permission: Mail.Send
# - Creates a client secret named: default
# - Grants admin consent if caller has sufficient privileges.
#
# Requirements: Azure CLI (az)
# Notes: Application permissions (Mail.Send) require admin consent. The script
# attempts to grant consent but will not fail the whole run if that step lacks privileges.

GRAPH_APP_ID="00000003-0000-0000-c000-000000000000" # Microsoft Graph
REDIRECT_URI="http://localhost"
SECRET_NAME="default"
MAX_ATTEMPTS=5
OUT_FILE=""

msg() { echo -e "[register-app] $*"; }
err() { echo -e "[register-app][error] $*" >&2; }

print_usage() {
  cat <<USAGE
Usage: $0 [--out-file <file>] | [-o <file>]

Options:
  -o, --out-file <file>   Write JSON output to the specified file instead of stdout.
                          (The file will be overwritten if it exists.)
  -h, --help              Show this help.
USAGE
}

# Parse arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--out-file)
      [[ $# -lt 2 ]] && { err "Missing filename after $1"; exit 1; }
      OUT_FILE="$2"; shift 2 ;;
    -h|--help)
      print_usage; exit 0 ;;
    *)
      err "Unknown argument: $1"; print_usage; exit 1 ;;
  esac
done

command -v az >/dev/null 2>&1 || { err "Azure CLI (az) is required."; exit 1; }

# Ensure we're logged in
if ! az account show >/dev/null 2>&1; then
  msg "Logging into Azure..."
  az login 1>/dev/null
fi

TENANT_ID=$(az account show --query tenantId -o tsv)
msg "Using tenant: $TENANT_ID"

# Resolve the App Role ID for Microsoft Graph Mail.Send (Application permission)
MAIL_SEND_ROLE_ID=$(az ad sp show --id "$GRAPH_APP_ID" --query "appRoles[?value=='Mail.Send' && contains(allowedMemberTypes, 'Application')].id" -o tsv)
if [[ -z "$MAIL_SEND_ROLE_ID" ]]; then
  err "Could not resolve Mail.Send application permission ID from Microsoft Graph SP."
  exit 1
fi
msg "Resolved Mail.Send app role ID: $MAIL_SEND_ROLE_ID"

# Generate a unique app name
attempt=1
while :; do
  SUFFIX=$(printf "%04d" $(( RANDOM % 10000 )) )
  APP_NAME="mcp-outlookemail-${SUFFIX}"
  EXISTS=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv || true)
  if [[ -z "$EXISTS" ]]; then
    break
  fi
  if (( attempt >= MAX_ATTEMPTS )); then
    err "Failed to find unique app name after $MAX_ATTEMPTS attempts. Try again."
    exit 1
  fi
  attempt=$(( attempt + 1 ))
  sleep 1
done
msg "App display name will be: $APP_NAME"

# Create the app registration
msg "Creating app registration..."
APP_CREATE_OUTPUT=$(az ad app create \
  --display-name "$APP_NAME" \
  --sign-in-audience AzureADMyOrg \
  -o json)

APP_ID=$(echo "$APP_CREATE_OUTPUT" | az jq -r '.appId' 2>/dev/null || echo "$APP_CREATE_OUTPUT" | grep -o '"appId": *"[^"]*"' | head -n1 | cut -d '"' -f4)
APP_OBJECT_ID=$(echo "$APP_CREATE_OUTPUT" | az jq -r '.id' 2>/dev/null || echo "$APP_CREATE_OUTPUT" | grep -o '"id": *"[^"]*"' | head -n1 | cut -d '"' -f4)

if [[ -z "$APP_ID" || -z "$APP_OBJECT_ID" ]]; then
  err "Failed to parse app creation output. Raw output: $APP_CREATE_OUTPUT"
  exit 1
fi
msg "Created app. Application (client) ID: $APP_ID"
msg "App object ID: $APP_OBJECT_ID"

# Apply SPA redirect URI
msg "Applying SPA redirect URI via update..."
az ad app update --id "$APP_ID" --set "spa={'redirectUris': ['$REDIRECT_URI']}" 1>/dev/null

# Prepare requiredResourceAccess JSON dynamically
REQ_JSON=$(cat <<EOF
[
  {
    "resourceAppId": "$GRAPH_APP_ID",
    "resourceAccess": [
      { "id": "$MAIL_SEND_ROLE_ID", "type": "Role" }
    ]
  }
]
EOF
)

REQ_FILE=$(mktemp)
trap 'rm -f "$REQ_FILE"' EXIT
printf '%s' "$REQ_JSON" > "$REQ_FILE"

msg "Updating app with required resource access (Mail.Send)..."
az ad app update --id "$APP_ID" --required-resource-accesses @"$REQ_FILE" 1>/dev/null

# Create service principal so the app can be used with permissions
msg "Ensuring service principal exists..."
az ad sp create --id "$APP_ID" 1>/dev/null || true
SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv || true)
msg "Service principal object ID: ${SP_OBJECT_ID:-<unknown>}"

# Grant admin consent
msg "Granting admin consent..."
sleep 30
az ad app permission admin-consent --id "$APP_ID"

# Create client secret
msg "Creating client secret named '$SECRET_NAME'..."
SECRET_JSON=$(az ad app credential reset --id "$APP_ID" --append --display-name "$SECRET_NAME" --years 1 -o json)
CLIENT_SECRET_VALUE=$(echo "$SECRET_JSON" | az jq -r '.password' 2>/dev/null || echo "$SECRET_JSON" | grep -o '"password": *"[^"]*"' | head -n1 | cut -d '"' -f4)
if [[ -z "$CLIENT_SECRET_VALUE" ]]; then
  err "Failed to obtain client secret value. Raw: $SECRET_JSON"
  exit 1
fi

# Simple JSON escaping for quotes and backslashes
json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"   # escape backslashes
  s="${s//\"/\\\"}"   # escape quotes
  s="${s//$'\n'/ }"       # replace newlines with space (shouldn't occur)
  printf '%s' "$s"
}

JSON_OUTPUT=$(cat <<JSON
{
  "displayName": "$(json_escape "$APP_NAME")",
  "tenantId": "$(json_escape "$TENANT_ID")",
  "clientId": "$(json_escape "$APP_ID")",
  "clientSecret": "$(json_escape "$CLIENT_SECRET_VALUE")"
}
JSON
)

if [[ -n "$OUT_FILE" ]]; then
  printf '%s\n' "$JSON_OUTPUT" > "$OUT_FILE"
  msg "JSON output written to $OUT_FILE (contains clientSecret). Secure this file and consider restricting permissions (e.g. chmod 600)."
else
  printf '%s\n' "$JSON_OUTPUT"
  msg "JSON output emitted above. Store clientSecret securely; it cannot be retrieved later."
fi
