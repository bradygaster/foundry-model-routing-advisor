#!/usr/bin/env bash
set -euo pipefail

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Required environment variable '$name' is not set." >&2
    exit 2
  fi
}

required_variables=(
  AZURE_SUBSCRIPTION_ID
  AZURE_RESOURCE_GROUP
  AZURE_AI_ACCOUNT
  MODEL_ROUTER_DEPLOYMENT
  CONFIRM_DELETE_DEPLOYMENT
)

for variable in "${required_variables[@]}"; do
  require_env "$variable"
done

if [[ "$CONFIRM_DELETE_DEPLOYMENT" != "$MODEL_ROUTER_DEPLOYMENT" ]]; then
  echo "CONFIRM_DELETE_DEPLOYMENT must exactly match MODEL_ROUTER_DEPLOYMENT." >&2
  exit 2
fi

subscription_id="$AZURE_SUBSCRIPTION_ID"
resource_group="$AZURE_RESOURCE_GROUP"
account_name="$AZURE_AI_ACCOUNT"
deployment_name="$MODEL_ROUTER_DEPLOYMENT"

az account set --subscription "$subscription_id"
az cognitiveservices account deployment delete \
  --resource-group "$resource_group" \
  --name "$account_name" \
  --deployment-name "$deployment_name"

echo "Deleted deployment '$deployment_name'."
