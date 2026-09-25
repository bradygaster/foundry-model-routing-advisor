#!/usr/bin/env bash
set -euo pipefail

subscription_id="${AZURE_SUBSCRIPTION_ID:-104482b7-4580-4de0-9453-0fc78df0b80e}"
resource_group="${AZURE_RESOURCE_GROUP:-rg-squad-imagegen}"
account_name="${AZURE_AI_ACCOUNT:-squad-imagegen-swc-1ntj32}"
deployment_name="${MODEL_ROUTER_DEPLOYMENT:-model-router-advisor}"

az account set --subscription "$subscription_id"
az cognitiveservices account deployment delete \
  --resource-group "$resource_group" \
  --name "$account_name" \
  --deployment-name "$deployment_name"

echo "Deleted deployment '$deployment_name'."
