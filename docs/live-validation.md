# Authenticated Azure validation

Validation was performed against the existing Microsoft Foundry account without
using or persisting API keys. Commands acquired short-lived Microsoft Entra ID
tokens in process and did not print them.

## Target

| Item | Value |
|---|---|
| Subscription | `104482b7-4580-4de0-9453-0fc78df0b80e` (`BradyG Private Test Sub`) |
| Tenant | `72f988bf-86f1-41af-91ab-2d7cd011db47` |
| Resource group | `rg-squad-imagegen` |
| Foundry account | `squad-imagegen-swc-1ntj32` in `swedencentral` |
| Account resource ID | `/subscriptions/104482b7-4580-4de0-9453-0fc78df0b80e/resourceGroups/rg-squad-imagegen/providers/Microsoft.CognitiveServices/accounts/squad-imagegen-swc-1ntj32` |
| Supplied project endpoint | `https://squad-imagegen-swc-1ntj32.services.ai.azure.com/api/projects/squad-imagegen-swc-1ntj32-proj` |
| Verified inference endpoint family | `https://<account>.services.ai.azure.com/api/projects/<project>/openai/v1/responses` |
| Foundry token audience | `https://ai.azure.com/` (`https://ai.azure.com/.default` SDK scope) |

Microsoft's current authentication documentation identifies
`https://ai.azure.com/.default` as the Foundry data-plane scope. Live probes
confirmed the project-scoped Responses API accepts that token and both deployment
names.

## Provisioning evidence

At `2026-09-25T08:18Z`, the account advertised `model-router` version
`2025-11-18` with the `GlobalStandard` SKU in Sweden Central. The following
command created the sample's high-capability deployment:

```bash
az cognitiveservices account deployment create \
  --resource-group rg-squad-imagegen \
  --name squad-imagegen-swc-1ntj32 \
  --deployment-name model-router-advisor \
  --model-format OpenAI \
  --model-name model-router \
  --model-version 2025-11-18 \
  --sku-name GlobalStandard \
  --sku-capacity 10
```

Result:

```text
provisioningState=Succeeded
sku=GlobalStandard
capacity=10
deploymentResourceId=/subscriptions/104482b7-4580-4de0-9453-0fc78df0b80e/resourceGroups/rg-squad-imagegen/providers/Microsoft.CognitiveServices/accounts/squad-imagegen-swc-1ntj32/deployments/model-router-advisor
```

## Direct authenticated inference evidence

At `2026-09-25T08:22Z`, authenticated Responses API calls completed for both
deployments using a bearer token requested for `https://ai.azure.com/`. HTTP 429
responses observed while exercising Model Router were transient; bounded retries
subsequently completed:

```bash
TOKEN="$(az account get-access-token \
  --resource https://ai.azure.com/ \
  --query accessToken --output tsv)"

curl --request POST \
  "https://squad-imagegen-swc-1ntj32.services.ai.azure.com/api/projects/squad-imagegen-swc-1ntj32-proj/openai/v1/responses" \
  --header "Authorization: Bearer $TOKEN" \
  --header "Content-Type: application/json" \
  --data '{"model":"<deployment>","input":"Reply with exactly: route-ok","max_output_tokens":256}'
```

| Deployment path | HTTP | Service-returned model | Result |
|---|---:|---|---|
| `gpt-5-mini` | 200 | `gpt-5-mini` | `route-ok`; response ID `resp_03c98e3b253c8e57006ab62f07b6088195b2e418b1a24651c2` |
| `model-router-advisor` | 200 after transient 429s | `gpt-5.4-mini-2026-03-17` | Output began `ROUTER_E2E_OK:`; response ID `resp_0a4e38fd1e313880006ab62f034da88195923980757cf7156e` |

The Model Router result demonstrates that the deployment name and responding
model are distinct and should both be recorded. The exact underlying model can
change between requests as router policy and availability evolve.

## Compiled CLI acceptance matrix

At `2026-09-25T08:20Z`, the session-local .NET 8.0.425 SDK ran 12 offline tests
with 0 failures and both fake routing paths completed in one attempt.

At `2026-09-25T08:23Z`, the compiled authenticated CLI completed the low-cost
path:

```text
mode=foundry
route=LowCost
attempts=1
deployment=gpt-5-mini
model=gpt-5-mini
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
deployment=model-router-advisor
error.category=RateLimited
error.message=Foundry returned HTTP 429 (TooManyRequests). Service request ID: ee4fea4a-5c35-457a-b652-f13b268d1ae0.
```

This was a transient account rate-window condition rather than an authentication,
endpoint, deployment, or parsing failure. At `2026-09-25T08:32Z`, an independent
retry of the compiled high-capability CLI completed in one attempt:

```text
mode=foundry
route=HighCapability
score=6
attempts=1
deployment=model-router-advisor
model=grok-4-1-fast-reasoning
response=HIGH_ROUTE_OK
```

The two successful router calls returned different backing models, as expected
for managed routing: `gpt-5.4-mini-2026-03-17` in the earlier direct Responses
proof and `grok-4-1-fast-reasoning` in the later compiled CLI proof.

## Cost and cleanup

Capacity `1` was accepted but repeatedly exhausted its request window during
bounded live validation, including after two 30-second waits. The final script
uses the service's default capacity `10`, which is the minimum practical setting
verified for this end-to-end sample. Global Standard model inference is
consumption billed; deployment capacity sets a rate-limit allocation rather than
provisioning dedicated compute. Delete the sample deployment when validation is
complete:

```bash
./scripts/delete-model-router.sh
```
