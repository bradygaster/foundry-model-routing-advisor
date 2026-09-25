#!/usr/bin/env bash
set -euo pipefail

subscription_id="${AZURE_SUBSCRIPTION_ID:-104482b7-4580-4de0-9453-0fc78df0b80e}"
resource_group="${AZURE_RESOURCE_GROUP:-rg-squad-imagegen}"
account_name="${AZURE_AI_ACCOUNT:-squad-imagegen-swc-1ntj32}"
project_name="${AZURE_AI_PROJECT:-squad-imagegen-swc-1ntj32-proj}"
deployment_name="${MODEL_ROUTER_DEPLOYMENT:-model-router-advisor}"
model_version="${MODEL_ROUTER_VERSION:-2025-11-18}"
sku_name="${MODEL_ROUTER_SKU:-GlobalStandard}"
sku_capacity="${MODEL_ROUTER_CAPACITY:-10}"

az account set --subscription "$subscription_id"

existing_model="$(
  az cognitiveservices account deployment show \
    --resource-group "$resource_group" \
    --name "$account_name" \
    --deployment-name "$deployment_name" \
    --query properties.model.name \
    --output tsv 2>/dev/null || true
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
echo "export FOUNDRY_LOW_COST_DEPLOYMENT=\"gpt-5-mini\""
echo "export FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT=\"${deployment_name}\""
