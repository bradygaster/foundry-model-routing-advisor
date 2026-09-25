# Authenticated Azure validation

> [!IMPORTANT]
> **This document is sanitized historical evidence. Every Azure name, ID,
> endpoint, deployment, model/output identifier, request/response/correlation
> identifier, timestamp, and command result shown below is fictional or
> generalized.** It does not identify or provide access to a live Azure resource,
> subscription, tenant, user, or operator.

Validation used Microsoft Entra ID without persisting API keys. The commands
below are generic setup patterns and require explicit values from your own Azure
environment.

## Target

| Item | Value |
|---|---|
| Subscription | `00000000-0000-4000-8000-000000000001` (fictional, not live) |
| Tenant | `00000000-0000-4000-8000-000000000002` (fictional, not live) |
| Resource group | `<your-resource-group>` (fictional placeholder) |
| Foundry account | `<your-account-name>` in `<supported-region>` (fictional placeholders) |
| Account resource ID | `/subscriptions/00000000-0000-4000-8000-000000000001/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-account-name>` (fictional, not live) |
| Supplied project endpoint | `https://<your-account-name>.services.ai.azure.com/api/projects/<your-project-name>` (fictional template, not live) |
| Verified inference endpoint family | `https://<account>.services.ai.azure.com/api/projects/<project>/openai/v1/responses` |
| Foundry token audience | `https://ai.azure.com/` (`https://ai.azure.com/.default` SDK scope) |

Microsoft's current authentication documentation identifies
`https://ai.azure.com/.default` as the Foundry data-plane scope. Live probes
confirmed the project-scoped Responses API accepts that token and both deployment
names.

## Provisioning evidence

The account advertised an available `model-router` version and supported SKU.
The following generic command shape creates a high-capability deployment after
the variables are populated explicitly:

```bash
export AZURE_RESOURCE_GROUP="<your-resource-group>"
export AZURE_AI_ACCOUNT="<your-account-name>"
export MODEL_ROUTER_DEPLOYMENT="<your-model-router-deployment>"
export MODEL_ROUTER_VERSION="<available-model-router-version>"
export MODEL_ROUTER_SKU="<supported-sku>"
export MODEL_ROUTER_CAPACITY="<positive-capacity>"

az cognitiveservices account deployment create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_AI_ACCOUNT" \
  --deployment-name "$MODEL_ROUTER_DEPLOYMENT" \
  --model-format OpenAI \
  --model-name model-router \
  --model-version "$MODEL_ROUTER_VERSION" \
  --sku-name "$MODEL_ROUTER_SKU" \
  --sku-capacity "$MODEL_ROUTER_CAPACITY"
```

Sanitized fictional result shape (not live):

```text
provisioningState=Succeeded
sku=<supported-sku>
capacity=<positive-capacity>
deploymentResourceId=/subscriptions/00000000-0000-4000-8000-000000000001/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-account-name>/deployments/<your-model-router-deployment>
```

## Direct authenticated inference evidence

Authenticated Responses API calls completed for both example deployment paths
using a bearer token requested for `https://ai.azure.com/`. HTTP 429 responses
observed while exercising Model Router were transient; bounded retries
subsequently completed. Use an explicitly configured endpoint:

```bash
TOKEN="$(az account get-access-token \
  --resource https://ai.azure.com/ \
  --query accessToken --output tsv)"

curl --request POST \
  "${FOUNDRY_ENDPOINT%/}/openai/v1/responses" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data '{"model":"<deployment>","input":"Reply with exactly: route-ok","max_output_tokens":256}'
```

| Deployment path | HTTP | Service-returned model | Result |
|---|---:|---|---|
| `<your-low-cost-deployment>` | 200 | `<low-cost-model>` | `route-ok`; response ID `<fictional-response-id>` |
| `<your-model-router-deployment>` | 200 after transient 429s | `<service-selected-model>` | Output began `ROUTER_E2E_OK:`; response ID `<fictional-response-id>` |

The Model Router result demonstrates that the deployment name and responding
model are distinct and should both be recorded. The exact underlying model can
change between requests as router policy and availability evolve.

## Compiled CLI acceptance matrix

The .NET 10 SDK ran 12 offline tests with 0 failures and both fake
routing paths completed in one attempt.

The compiled authenticated CLI completed the low-cost path:

```text
mode=foundry
route=LowCost
attempts=1
deployment=<your-low-cost-deployment>
model=<low-cost-model>
response=LOW_ROUTE_OK
```

An initial CLI command for the high-capability path was rate limited after three
attempts, including the bounded 30-second fallback between attempts. The final
failure from that run was:

```text
mode=foundry
route=HighCapability
score=6
reasons=capability keywords: architecture, compare (+4); explicit multi-step reasoning (+2)
attempts=3
deployment=<your-model-router-deployment>
error.category=RateLimited
error.message=Foundry returned HTTP 429 (TooManyRequests). Service request ID: 00000000-0000-4000-8000-000000000003 (fictional, not live).
```

This was a transient account rate-window condition rather than an authentication,
endpoint, deployment, or parsing failure. An independent retry of the compiled
high-capability CLI completed in one attempt:

```text
mode=foundry
route=HighCapability
score=6
attempts=1
deployment=<your-model-router-deployment>
model=<service-selected-model>
response=HIGH_ROUTE_OK
```

The successful router calls returned different backing models, as expected for
managed routing. Exact model identifiers are intentionally omitted.

## Cost and cleanup

Model inference is consumption billed; deployment capacity sets a rate-limit
allocation rather than provisioning dedicated compute. Choose capacity based on
current service guidance and available quota. Delete the sample deployment when
validation is complete:

```bash
export CONFIRM_DELETE_DEPLOYMENT="$MODEL_ROUTER_DEPLOYMENT"
./scripts/delete-model-router.sh
```
