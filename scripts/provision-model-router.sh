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
  AZURE_AI_PROJECT
  MODEL_ROUTER_DEPLOYMENT
  MODEL_ROUTER_VERSION
  MODEL_ROUTER_SKU
  MODEL_ROUTER_CAPACITY
)

for variable in "${required_variables[@]}"; do
  require_env "$variable"
done

if [[ ! "$MODEL_ROUTER_CAPACITY" =~ ^[1-9][0-9]*$ ]]; then
  echo "MODEL_ROUTER_CAPACITY must be a positive integer." >&2
  exit 2
fi

if [[ ! "$MODEL_ROUTER_DEPLOYMENT" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$ ]]; then
  echo "MODEL_ROUTER_DEPLOYMENT contains unsupported characters." >&2
  exit 2
fi

subscription_id="$AZURE_SUBSCRIPTION_ID"
resource_group="$AZURE_RESOURCE_GROUP"
account_name="$AZURE_AI_ACCOUNT"
project_name="$AZURE_AI_PROJECT"
deployment_name="$MODEL_ROUTER_DEPLOYMENT"
model_version="$MODEL_ROUTER_VERSION"
sku_name="$MODEL_ROUTER_SKU"
sku_capacity="$MODEL_ROUTER_CAPACITY"

az account set --subscription "$subscription_id"

existing_model="$(
  az cognitiveservices account deployment list \
    --resource-group "$resource_group" \
    --name "$account_name" \
    --query "[?name=='${deployment_name}'].properties.model.name | [0]" \
    --output tsv
)"

if [[ -n "$existing_model" ]]; then
  if [[ "$existing_model" != "model-router" ]]; then
    echo "Deployment '$deployment_name' already exists with model '$existing_model'." >&2
    exit 1
  fi

  echo "Deployment '$deployment_name' already exists and uses model-router; applying requested settings."
fi

az cognitiveservices account deployment create \
  --resource-group "$resource_group" \
  --name "$account_name" \
  --deployment-name "$deployment_name" \
  --model-format OpenAI \
  --model-name model-router \
  --model-version "$model_version" \
  --sku-name "$sku_name" \
  --sku-capacity "$sku_capacity" \
  --output none

az cognitiveservices account deployment show \
  --resource-group "$resource_group" \
  --name "$account_name" \
  --deployment-name "$deployment_name" \
  --query '{id:id,model:properties.model,sku:sku,provisioningState:properties.provisioningState}' \
  --output json

echo
echo "Configure the sample with:"
echo "export FOUNDRY_ENDPOINT=\"https://${account_name}.services.ai.azure.com/api/projects/${project_name}\""
echo "export FOUNDRY_LOW_COST_DEPLOYMENT=\"<your-existing-low-cost-deployment>\""
echo "export FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT=\"${deployment_name}\""
