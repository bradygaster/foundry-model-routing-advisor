# Model Routing Advisor

A compact .NET 8 console sample that deterministically selects either a low-cost
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
az login --tenant 72f988bf-86f1-41af-91ab-2d7cd011db47
az account set --subscription 104482b7-4580-4de0-9453-0fc78df0b80e

export FOUNDRY_ENDPOINT="https://squad-imagegen-swc-1ntj32.services.ai.azure.com/api/projects/squad-imagegen-swc-1ntj32-proj"
export FOUNDRY_LOW_COST_DEPLOYMENT="gpt-5-mini"
export FOUNDRY_HIGH_CAPABILITY_DEPLOYMENT="model-router-advisor"

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

The existing `gpt-5-mini` deployment is reused for the low-cost path. Create the
high-capability Model Router deployment with the idempotent script:

```bash
./scripts/provision-model-router.sh
```

The defaults target the subscription, resource group, account, deployment name,
model version, and the service's default capacity of 10. Capacity 1 proved too
restrictive for bounded retry validation. Every value can be overridden by an
environment variable documented in the script. Model Router is usage-billed and
the deployment reserves rate-limit capacity, not dedicated compute. Remove the
sample deployment when it is no longer needed:

```bash
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
