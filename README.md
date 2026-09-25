# Model Routing Advisor

A compact .NET 8 console sample that deterministically selects either a low-cost
or high-capability model path. The default experience is fully local: it uses a
fake transport, requires no cloud account, and makes no network calls at runtime.

## Architecture

1. `DeterministicRoutingPolicy` scores only visible prompt characteristics.
2. `ResilientModelClient` applies a per-attempt timeout and at most three attempts.
3. `IModelTransport` separates policy and resilience from model access.
4. `FakeModelTransport` provides deterministic offline behavior.
5. `FoundryModelTransport` optionally calls a Microsoft Foundry project endpoint
   with the `https://ai.azure.com/.default` audience and a token acquired by
   `DefaultAzureCredential`.

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
| `FOUNDRY_API_VERSION` | Optional API version; defaults to `2024-10-21` |

Authenticate using any supported `DefaultAzureCredential` source, such as Azure
CLI login for development or managed identity when hosted. Interactive browser
authentication is intentionally disabled.

```bash
dotnet run --project src/ModelRoutingAdvisor -- --real \
  --prompt "Compare architecture and security trade-offs step-by-step."
```

Successful authenticated validation proves that the configured identity can
acquire a Foundry data-plane token and invoke the named deployment at that
endpoint. Validate both routes separately. Model Router-specific behavior,
regional availability, quota, content filtering, latency, and cost must be
verified in the target environment; local tests intentionally make no claims
about them.

Treat prompts and model output as untrusted data. Do not place production PII,
credentials, or regulated content in this demonstration. Applications that turn
the response into a consequential action need explicit validation, prompt-
injection defenses, deterministic escalation rules, and human approval.

## Error categories

Failures are reported as `InvalidConfiguration`, `Authentication`,
`Authorization`, `RateLimited`, `Timeout`, `Transport`, `Service`,
`InvalidResponse`, or `Cancelled`. Only transient rate-limit, timeout, transport,
and service failures are retried, and all retries are bounded.
