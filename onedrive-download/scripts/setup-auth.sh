#!/bin/bash

# setup-auth.sh - Automatic OAuth app registration script

echo "[INFO] Starting authentication setup automation..."

# 1. Load environment variables
APP_NAME=$AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME
RESOURCE_GROUP="rg-$AZURE_ENV_NAME"
FUNCTION_APP_URL=$AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN
TENANT_ID=$AZURE_TENANT_ID

echo "[DEBUG] AppName: $APP_NAME"
echo "[DEBUG] ResourceGroup: $RESOURCE_GROUP"
echo "[DEBUG] FunctionAppUrl: $FUNCTION_APP_URL"

if [ -z "$APP_NAME" ] || [ -z "$RESOURCE_GROUP" ] || [ -z "$FUNCTION_APP_URL" ]; then
    echo "[ERROR] Required environment variables are missing"
    echo "[ERROR] AppName: $APP_NAME"
    echo "[ERROR] ResourceGroup: $RESOURCE_GROUP"
    echo "[ERROR] FunctionAppUrl: $FUNCTION_APP_URL"
    exit 1
fi

echo "[INFO] App Name: $APP_NAME"
echo "[INFO] Resource Group: $RESOURCE_GROUP"
echo "[INFO] Function App URL: $FUNCTION_APP_URL"

# 2. Create Redirect URI
REDIRECT_URI="https://$FUNCTION_APP_URL/auth/callback"
echo "[INFO] Redirect URI: $REDIRECT_URI"

# 3. Create App Registration
echo "[INFO] Creating App Registration..."
APP_REG_NAME="mcp-oauth-$AZURE_ENV_NAME-$RANDOM"

APP_OUTPUT=$(az ad app create \
    --display-name "$APP_REG_NAME" \
    --public-client-redirect-uris "$REDIRECT_URI" \
    --sign-in-audience "AzureADandPersonalMicrosoftAccount" \
    --query "appId" -o tsv 2>&1)

if [ -z "$APP_OUTPUT" ]; then
    echo "[ERROR] Failed to create App Registration"
    echo "[ERROR] Output: $APP_OUTPUT"
    exit 1
fi

APP_ID=$APP_OUTPUT
echo "[SUCCESS] App Registration created (ID: $APP_ID)"

# 4. Add API permissions (Microsoft Graph)
echo "[INFO] Adding API permissions..."
GRAPH_API_ID="00000003-0000-0000-c000-000000000000"

# Delegated permissions
PERMISSIONS=(
    "88d21fd4-8e52-4522-a9d3-8732c3a2f881"  # User.Read
    "5447fe39-cb82-4c18-9a2e-6271dcc5b4a7"  # Files.Read
    "df85f4d6-205c-4ac5-a5ea-6bf408dba38d"  # Files.Read.All
    "37f7f235-527c-4136-accd-4a02d197296e"  # offline_access
)

for PERM_ID in "${PERMISSIONS[@]}"; do
    az ad app permission add \
        --id "$APP_ID" \
        --api "$GRAPH_API_ID" \
        --api-permissions "$PERM_ID=Scope" > /dev/null 2>&1
done

echo "[SUCCESS] API permissions added (User.Read, Files.Read, Files.Read.All, offline_access)"

# 5. Create Service Principal
echo "[INFO] Creating Service Principal..."
az ad sp create --id "$APP_ID" > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "[SUCCESS] Service Principal created"
else
    echo "[WARNING] Service Principal may already exist (ignoring)"
fi

# 6. Output App ID for bicep (AZD will capture this)
echo "[INFO] App Registration ID: $APP_ID"

# 7. Update Function App environment variables (FORCE UPDATE)
echo "[INFO] Updating Function App settings..."
az functionapp config appsettings set \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings "EntraId__ClientId=$APP_ID" 2>&1

if [ $? -eq 0 ]; then
    echo "[SUCCESS] Function App settings updated with new App ID: $APP_ID"
else
    echo "[ERROR] Failed to update Function App settings"
    exit 1
fi

echo "[SUCCESS] Authentication setup complete!"
echo "[INFO] App ID: $APP_ID"
echo "[INFO] Users can now login at: https://$FUNCTION_APP_URL/login"
