# Model Routing Advisor

> [!IMPORTANT]
> **All Azure names, IDs, endpoints, deployment names, model/output identifiers,
> and command results in this repository are fictional or sanitized examples.**
> They are not live, do not identify real Azure resources or users, and must be
> replaced with values from your own environment before authenticated use.

A compact .NET 10 console sample that deterministically selects either a low-cost
or high-capability model path. The default experience is fully local: it uses a
fake transport, requires no cloud account, and makes no network calls at runtime.

## Architecture

1. `DeterministicRoutingPolicy` scores only visible prompt characteristics.
2. `ResilientModelClient` applies a per-attempt timeout and at most three attempts.
3. `IModelTransport` separates policy and resilience from model access.
4. `FakeModelTransport` provides deterministic offline behavior.
5. `FoundryModelTransport` optionally calls the project-scoped Microsoft Foundry
   Responses API with the `https://ai.azure.com/.default` audience and a token
   acquired by `DefaultAzureCredential`.

The policy routes prompts with a score of four or more to the high-capability
path. Long prompts, code or diagnostic context, capability keywords, and explicit
multi-step requests add fixed points. It does not use a model to choose a model,
so routing is repeatable, inspectable, and free in the local path.

## Local validation

From this sample directory:

```bash
dotnet run --project src/ModelRoutingAdvisor -- \
  --prompt "Summarize why deterministic routing is useful."

dotnet run --project src/ModelRoutingAdvisor -- \
  --prompt "Compare architecture and security trade-offs step-by-step."

dotnet test tests/ModelRoutingAdvisor.Tests
```

These commands validate policy selection, fake transport injection, bounded
retries, timeout categorization, and permanent-error behavior. They do **not**
prove that a deployment exists, the signed-in identity has data-plane access,
or the selected models are available in a particular subscription and region.

## Authenticated Foundry runtime validation

Real mode is opt-in. Set these variables in your shell or secret manager; do not
commit their values:

| Variable | Purpose |
|---|---|
| `FOUNDRY_ENDPOINT` | HTTPS Microsoft Foundry project endpoint |
| `FOUNDRY_LOW_COST_DEPLOYMENT` | Existing deployment used for the low-cost route |
| `FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT` | Existing deployment used for the high-capability route |

Authenticate using any supported `DefaultAzureCredential` source, such as Azure
CLI login for development or managed identity when hosted. Interactive browser
authentication is intentionally disabled.

```bash
export AZURE_TENANT_ID="00000000-0000-4000-8000-000000000002" # FICTIONAL; replace
export AZURE_SUBSCRIPTION_ID="00000000-0000-4000-8000-000000000001" # FICTIONAL; replace

az login --tenant "$AZURE_TENANT_ID"
az account set --subscription "$AZURE_SUBSCRIPTION_ID"

export FOUNDRY_ENDPOINT="https://<your-account-name>.services.ai.azure.com/api/projects/<your-project-name>"
export FOUNDRY_LOW_COST_DEPLOYMENT="<your-low-cost-deployment>"
export FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT="<your-model-router-deployment>"

dotnet run --project src/ModelRoutingAdvisor -- --real \
  --prompt "Summarize deterministic routing in one sentence."

dotnet run --project src/ModelRoutingAdvisor -- --real \
  --prompt "Compare architecture and security trade-offs step-by-step."
```

The app posts to
`https://<account>.services.ai.azure.com/api/projects/<project>/openai/v1/responses`.
The output includes both `deployment=` and the service-returned `model=`; for
Model Router these values differ, which makes the selected underlying model
visible. HTTP 429 responses are categorized as transient and retried with bounded
exponential backoff.

## Reproducible Azure deployment

Provide every target value explicitly, then create the high-capability Model
Router deployment with the idempotent script:

```bash
export AZURE_SUBSCRIPTION_ID="00000000-0000-4000-8000-000000000001" # FICTIONAL; replace
export AZURE_RESOURCE_GROUP="<your-resource-group>"
export AZURE_AI_ACCOUNT="<your-foundry-account>"
export AZURE_AI_PROJECT="<your-foundry-project>"
export MODEL_ROUTER_DEPLOYMENT="<your-model-router-deployment>"
export MODEL_ROUTER_VERSION="<available-model-router-version>"
export MODEL_ROUTER_SKU="<supported-sku>"
export MODEL_ROUTER_CAPACITY="<positive-capacity>"

./scripts/provision-model-router.sh
```

The script has no Azure defaults and exits before calling Azure CLI if any input
is missing. Model Router is usage-billed and deployment capacity represents a
rate-limit allocation rather than dedicated compute. Remove a deployment only
after explicitly confirming its name:

```bash
export CONFIRM_DELETE_DEPLOYMENT="$MODEL_ROUTER_DEPLOYMENT"
./scripts/delete-model-router.sh
```

See `docs/live-validation.md` for sanitized commands and authenticated evidence.

Treat prompts and model output as untrusted data. Do not place production PII,
credentials, or regulated content in this demonstration. Applications that turn
the response into a consequential action need explicit validation, prompt-
injection defenses, deterministic escalation rules, and human approval.

## Error categories

Failures are reported as `InvalidConfiguration`, `Authentication`,
`Authorization`, `RateLimited`, `Timeout`, `Transport`, `Service`,
`InvalidResponse`, or `Cancelled`. Only transient rate-limit, timeout, transport,
and service failures are retried, and all retries are bounded.
