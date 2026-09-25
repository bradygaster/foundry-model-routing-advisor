# Model routing advisor experiment journal

## Outcome and acceptance criteria

Implemented `samples/model-routing-advisor/` as a .NET 8 console sample with:

- deterministic selection between low-cost and high-capability paths;
- an injectable transport boundary and fake offline transport;
- optional Microsoft Foundry invocation configured only by environment variables
  and authenticated with `DefaultAzureCredential`;
- a maximum of three attempts, per-attempt timeout, bounded backoff, and explicit
  error categories;
- targeted tests that require neither cloud credentials nor a live endpoint;
- documentation that separates local proof from authenticated runtime proof.

Acceptance is local build success, all targeted tests passing, both local routes
executing, no committed credential values, and explicit documentation of the
remaining authenticated validation.

## Architecture decision

Use a deterministic, inspectable scoring policy before model invocation and keep
all network access behind `IModelTransport`. This is smaller and more testable
than using an LLM as the router, and it makes the low-cost control path truly
local. A raw Azure OpenAI-compatible HTTP transport was chosen for the optional
Foundry path so the sample needs only `Azure.Identity`; the transport selects one
of two configured deployments and obtains a Cognitive Services bearer token with
`DefaultAzureCredential`.

The local policy demonstrates application-owned routing. It does not claim to
reproduce the managed Model Router service's policy. A real deployment can point
one or both configured routes at a Model Router deployment, but subscription,
region, quota, and service behavior require authenticated validation.

## Squad activity

A separate Squad architecture session was launched but exceeded four minutes
without producing a durable artifact. This implementation-owner recovery was
started to preserve delivery momentum. The recovery owner completed the narrow
vertical slice directly: architecture, implementation, tests, local validation,
documentation, and evidence capture.

The direct owner could cover a compact application boundary and deterministic
offline evidence. It could not independently establish team consensus, compare
all current Foundry routing products, verify tenant-specific availability, obtain
reviewer approval, or produce authenticated subscription evidence. Those are the
meaningful differences from the parallel Squad path, not missing local code.

## Evidence and assumptions

| Item | Evidence or assumption |
|---|---|
| Offline behavior | `FakeModelTransport` makes no runtime network calls and is the default mode. |
| Deterministic routing | Fixed scoring inputs and threshold in `DeterministicRoutingPolicy`; repeated-input test included. |
| Bounded resilience | `ResilienceOptions` caps attempts at five and defaults to three; each attempt has its own timeout. |
| Retry safety | Only transient transport, timeout, rate-limit, and service failures retry. |
| Secretless authentication | Real mode uses `DefaultAzureCredential`; configuration contains endpoint and deployment names, not keys. |
| Runtime API shape | Uses a Microsoft Foundry project endpoint with the `https://ai.azure.com/.default` audience; the exact API version still requires target-environment validation. |
| Deployment availability | Unverified locally; must be proven in the target authenticated environment. |
| Managed Model Router equivalence | Explicitly not assumed; this sample demonstrates application-owned deterministic routing. |
| Rejected Squad branch | `7e7485bedfc57ae26d208b57596351986a6ff2a4` was not merged after pre-ship review found a likely wrong token audience, generic live diagnostics, permanently skipped live testing, duplicate JSON handling, missing CLI coverage, optimistic pre-review scores, and conflated resource/RBAC guidance. |

## Validation log

| Validation | Expected evidence | Result |
|---|---|---|
| Restore/build | .NET 8 projects restore and compile without credentials | Passed with .NET SDK 8.0.425; no cloud credentials supplied |
| Targeted tests | Routing, token audience, retry, timeout, and error behavior pass offline | Passed: 9 tests, 0 failed, 0 skipped |
| Low-cost local route | Short prompt selects `LowCost` and fake model | Passed: score 0, one attempt, `offline-low-cost` |
| High-capability local route | Complex prompt selects `HighCapability` and fake model | Passed: score 6, one attempt, `offline-high-capability` |
| Authenticated runtime | Both configured deployments respond using `DefaultAzureCredential` | Not run; requires target environment |

## Friction and recovery

The architecture session did not return a durable artifact within four minutes.
Rather than wait indefinitely or invent its conclusions, the implementation
owner recovered with the smallest complete control: a deterministic policy,
transport abstraction, offline fake, narrow credential-based real transport,
tests, and explicit uncertainty boundaries.

The worktree initially had only .NET 10 SDK and runtime installed. Restore and
compilation succeeded, but the .NET 8 testhost could not start. A session-local
.NET 8.0.425 SDK/runtime was installed outside the repository, after which all
tests and both local routes passed on the declared target. An attempted parallel
validation caused competing builds to lock the shared `obj` output; recovery was
to clean once, build/test sequentially, then execute runtime checks with
`--no-build`.

## What Squad did well

| Dimension | Score (1-5) | Evidence |
|---|---:|---|
| Requirements clarity | 5 | The parent contract specified path, runtime, identity, offline default, resilience, tests, journal headings, and reporting. |
| Routing and ownership | 3 | A parallel architecture path was attempted, and implementation recovery had a clear owner; the handoff lacked a durable intermediate artifact. |
| Architecture quality | 4 | The resulting boundary is small, injectable, deterministic, and explicit about managed Model Router non-equivalence. |
| Implementation completeness | 5 | Console app, fake and real transports, resilience, configuration, errors, docs, and tests are included. |
| Test quality | 4 | Targeted offline tests cover core policy and failure behavior; live Foundry behavior remains environment-gated. |
| Security and secret handling | 5 | No API-key path or committed secret values; real mode uses `DefaultAzureCredential` and HTTPS validation. |
| Evidence discipline | 4 | Local versus authenticated claims are separated; authenticated evidence is intentionally still pending. |
| Recovery behavior | 5 | Work resumed after the four-minute stall with a bounded, durable implementation rather than another open-ended delegation. |
| Delivery efficiency | 4 | One owner completed the vertical slice; absence of early architecture output caused duplicated architecture effort. |

## Core FoundrySquad improvements

| Improvement | Core surface | Evidence | Impact (1-5) | Effort (1-5) | Confidence (1-3) |
|---|---|---|---:|---:|---:|
| Require a timeout handoff with a minimal decision artifact. | coordinator response mode | Separate session exceeded four minutes with no durable artifact, forcing downstream owners to restart architecture work. | 5 | 2 | 3 |
| Publish a durable artifact heartbeat during long work. | coordinator prompt and handoff contract | The implementation owner knew a session existed but had no partial requirements or decision record. | 4 | 2 | 3 |
| Standardize authenticated evidence fields. | evidence template and quality gate | Local code can only mark deployment and availability claims as unverified without endpoint, tenant, region, timestamp, route, and result evidence. | 5 | 3 | 3 |
| Ship a control-versus-Squad comparison rubric. | experiment template | Direct-owner capability limits and scoring dimensions otherwise vary between experiments. | 3 | 1 | 3 |
| Maintain an offline-first .NET sample baseline. | sample template | The recovery owner recreated routing, transport, resilience, and test conventions from scratch. | 4 | 3 | 2 |
| Finalize experiment scores only after pre-ship verdicts. | ceremonies and journal template | The rejected Squad branch had optimistic usefulness, quality, and RAI scores before Reviewer/Rai/Fact Checker findings landed. | 5 | 2 | 3 |
| Record endpoint family, token audience, and data-plane RBAC together. | model and platform evidence | The rejected branch used a likely incorrect audience and conflated resource families. | 5 | 2 | 3 |

## Comparison score

| Dimension | Score (1-5) | Evidence |
|---|---:|---|
| Architecture economy | 5 | The control uses one deterministic routing policy and one transport abstraction without an agent framework. |
| Routing accuracy | 3 | The initial Squad route selected the right domains but did not produce a reusable artifact before recovery. |
| Handoff quality | 2 | The implementation owner received intent but no durable architecture decision from the parallel Squad session. |
| Evidence discipline | 5 | Local tests, documentation evidence, and authenticated Foundry runtime evidence are explicitly separated. |
| Implementation usefulness | 5 | The .NET 8 sample includes offline defaults, an opt-in Foundry transport, resilience, CLI examples, and tests. |
| Quality coverage | 5 | Nine tests cover routing thresholds, token audience, transport injection, retries, timeout behavior, and error categories. |
| Security and RAI | 5 | The sample uses `DefaultAzureCredential`, validates configuration, avoids secrets, bounds retries, and performs advisory triage only. |
| Ceremony efficiency | 2 | More than four minutes elapsed without a durable Squad artifact before the direct-owner recovery. |
| Recovery behavior | 5 | The control delivered a complete tested vertical slice and recorded the missing specialist evidence honestly. |
