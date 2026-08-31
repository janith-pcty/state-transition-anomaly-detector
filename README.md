# State Transition Anomaly Detector

A domain-agnostic library and dashboard that watches state-transition timing and flags
entities stuck in a state longer than historical norms. Implement `IStateTransitionSource`
for your own state history and get anomaly detection for free.

Ships with two synthetic demo domains — a print-job pipeline and a support-ticket
pipeline — to prove the detection engine and UI are domain-blind.

## Repo layout

- `src/StateTransitionAnomalyDetection` — core library (types, baseline calculator, detector). No dependencies.
- `src/StateTransitionAnomalyDetection.Tests` — xunit tests for the baseline/anomaly math.
- `src/StateTransitionAnomalyDetection.Adapters.Mocks` — synthetic data generator and the two demo adapters.
- `src/StateTransitionAnomalyDetection.Host` — ASP.NET Core minimal API exposing the detector over HTTP.

The UI used to live here as a standalone Vite app but has moved to `webplatform.ui` so it
can be built with Paylocity's real Citrus (CDS) components — see
`webplatform.ui/libs/sampleapp/anomaly-detector/`. Run it via
`npm run start:domains:offline --- --d=sampleapp` in that repo, then open
`http://localhost:4200/sample-app/anomaly-detector`. It talks directly to this repo's
Host (`http://localhost:5214`), so start the Host below first.

## Running locally

```shell
dotnet test                                                          # run the unit tests
dotnet run --project src/StateTransitionAnomalyDetection.Host        # API on http://localhost:5214
```

Then start the UI from `webplatform.ui` as described above. Swagger (below) also works
for manual smoke testing without the UI running at all.

## API

| Endpoint | Description |
| --- | --- |
| `GET /systems` | List registered systems and their entity types |
| `GET /anomalies?systemName=&entityType=&includeAll=` | Flagged entities, sorted by score descending |
| `GET /entities/{systemName}/{entityType}/{entityId}/history` | Transition timeline + per-state baseline data |
| `GET /entities/{systemName}/{entityType}/states` | Every valid state for that entity type (including branch terminals), for the All Jobs page's status dropdown |
| `POST /entities/{systemName}/{entityType}/{entityId}/transition` | Body `{ "toState" }`. Forces the entity into that state for demo purposes |
| `GET /entities/{systemName}/{entityType}/{entityId}/explain` | Plain-English narration of why the entity is anomalous, via the local Claude Code CLI |
| `POST /reseed` | Regenerate the mock adapters' open-entity sets |

Swagger UI is available at `/swagger` while the Host is running.
