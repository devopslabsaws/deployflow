# DeployFlow Execution Backlog vs Dokploy/Coolify

## Goal
Deliver full parity plus advanced capabilities across all existing feature areas with an execution-ready, sprint-based plan.

## Priority Order
1. P0: Core platform blockers
2. P1: Production readiness and feature completion
3. P2: Advanced operations and reliability
4. P3: Enterprise and scale hardening

## Global Delivery Rules
- Every endpoint returns deterministic error codes and remediation messages.
- Every destructive action uses confirmation dialog and pending/disabled states.
- Every async operation has observable status lifecycle (`queued`, `running`, `completed`, `failed`).
- Every sprint closes with build, test, migration, and smoke verification gates.

## Sprint 1 (P0): Storage + Observability Foundations
### Feature Area: Volumes (missing backend)
API contracts
- `GET /api/volumes?projectId={id}`: list volumes
- `POST /api/volumes`: create volume (`name`, `driver`, `mountPath`, `projectId?`)
- `DELETE /api/volumes/{id}`: delete volume
- `POST /api/volumes/{id}/attach`: attach to service/project target
- `POST /api/volumes/{id}/detach`: detach target

Frontend tasks
- Wire existing volumes page to real backend response shape.
- Add attach/detach actions in `volumes` table card menu.
- Add conflict error UX (`409`) when volume is still attached.

Acceptance tests
- Create/list/delete volume roundtrip passes.
- Attached volume cannot be deleted and returns `409`.
- Detached volume can be deleted and disappears from list.

### Feature Area: Logs (currently simulated UI)
API contracts
- `GET /api/logs?service=&level=&from=&to=&search=&cursor=`
- `GET /api/logs/stream?service=&level=` (SSE or SignalR)
- `POST /api/logs/export` with filter payload

Frontend tasks
- Replace generated logs in `logs/page.tsx` with API-backed query.
- Replace fake streaming interval with real stream subscription.
- Implement server-side pagination/cursor loading.

Acceptance tests
- Log filtering returns deterministic result set.
- Stream reconnection resumes without duplicate records.
- Export endpoint returns file with selected filters.

### Feature Area: Monitoring (currently simulated charts)
API contracts
- `GET /api/monitoring/summary?serverId=&range=`
- `GET /api/monitoring/timeseries?metric=cpu|memory|disk|net&serverId=&range=`
- `GET /api/monitoring/network?serverId=&range=`

Frontend tasks
- Replace mock chart datasets with API data.
- Add empty-state and stale-data badges.
- Add per-server and all-servers aggregate views.

Acceptance tests
- Charts render from backend data only.
- Server filter changes query and chart traces.
- Refresh updates timestamps and values.

## Sprint 2 (P0): Pipeline Execution Engine ✅ COMPLETED
### Feature Area: Pipelines (basic only)
API contracts
- `POST /api/pipelines/{id}/runs`: start run ✅
- `GET /api/pipelines/{id}/runs`: list runs ✅
- `GET /api/pipeline-runs/{runId}`: run detail ✅
- `GET /api/pipeline-runs/{runId}/logs`: stage/step logs ✅
- `POST /api/pipeline-runs/{runId}/cancel`: cancel run ✅
- `PUT /api/pipelines/{id}`: update pipeline ✅

Frontend tasks
- Implement pipeline details page and edit page routes referenced in UI. ✅
- Add run history tab and stage-step log view. ✅
- Add disabled state and optimistic status after trigger. ✅

Acceptance tests
- Trigger creates run and transitions pipeline status. ✅
- Run logs persist and paginate. ✅
- Cancel transitions run and pipeline to terminal state. ✅

## Sprint 3 (P1): Database Production Readiness ✅ COMPLETED
### Feature Area: Backups/Restore/S3
API contracts
- `GET /api/databases/s3-destinations`: list stored destinations ✅
- `POST /api/databases/s3-destinations`: create destination ✅
- `PUT /api/databases/s3-destinations/{id}`: update destination ✅
- `DELETE /api/databases/s3-destinations/{id}`: delete destination ✅
- `POST /api/databases/s3-destinations/test`: test connectivity ✅
- `GET /api/databases/{id}/backup-policy`: get policy ✅
- `PUT /api/databases/{id}/backup-policy`: create/update policy ✅
- `GET /api/databases/{id}/restore/jobs`: list restore jobs ✅
- `POST /api/databases/{id}/restore/jobs`: start restore job ✅
- `GET /api/databases/{id}/restore/jobs/{jobId}`: restore job detail ✅

Frontend tasks (Pending)
- Add destination CRUD UX to `s3-destinations` page.
- Add destination selector in backup policy settings.
- Add restore-job history list and terminal state details.

Acceptance tests (Backend Done)
- S3 destination CRUD tested. ✅
- Restore job lifecycle tested (pending → running → terminal). ✅
- Backup policy create/update patterns verified. ✅

### Feature Area: Backup Scheduling and Retention
API contracts
- `PUT /api/databases/{id}/backup-policy` (`enabled`, `cron`, `retentionDays`, `destinationId`)
- `GET /api/databases/{id}/backup-policy`

Frontend tasks
- Add backup policy form in database settings panel.
- Add retention warning when reducing retention below existing backup age.

Acceptance tests
- Scheduler enqueues backups according to cron.
- Retention job prunes expired artifacts only.

## Sprint 4 (P1): Containers + Services Runtime Completion
### Feature Area: Containers (UI read-only today)
API contracts
- `POST /api/containers/{id}/start`
- `POST /api/containers/{id}/stop`
- `POST /api/containers/{id}/restart`
- `DELETE /api/containers/{id}`
- `GET /api/containers/{id}/logs`

Frontend tasks
- Wire action menu buttons in containers page.
- Add action result toast and refresh list after mutation.
- Add container log drawer.

Acceptance tests
- Start/stop/restart/remove actions invoke backend and mutate state.
- Failed command surfaces actionable error message.

### Feature Area: Services Autoscaling
API contracts
- `PUT /api/services/{id}/scaling-policy` (`minReplicas`, `maxReplicas`, `cpuTarget`, `memoryTarget`)
- `GET /api/services/{id}/scaling-policy`

Frontend tasks
- Add scaling policy tab in service settings.
- Add current scaling decision and trigger reason UI.

Acceptance tests
- Policy saves and is loaded correctly.
- Scale out/in executes under synthetic metric thresholds.

## Sprint 5 (P1): Team, RBAC, and Security Consistency
### Feature Area: Team API mismatch + invitation lifecycle
API contracts
- Align role update routes: support both `PUT` and `PATCH /api/team/{userId}/role`.
- Add invitation endpoints:
  - `GET /api/team/invitations`
  - `POST /api/team/invitations/{id}/resend`
  - `DELETE /api/team/invitations/{id}`

Frontend tasks
- Add invitation management table to team page.
- Add expired invitation badges and resend flow.

Acceptance tests
- Role update succeeds with current frontend call shape.
- Invitation resend/revoke updates list and audit events.

### Feature Area: RBAC Scope Expansion
API contracts
- Extend permissions resource model to include domain-specific scopes:
  - project/service/database/domain/volume scoped actions
- Add bulk grant endpoint:
  - `POST /api/permissions/grants/bulk`

Frontend tasks
- Add permission matrix editor by resource.
- Add effective permissions preview for user/resource pair.

Acceptance tests
- Explicit deny/allow precedence validated per resource.
- Non-admin cannot grant/revoke permissions.

## Sprint 6 (P2): Domains, Alerts, and Notifications
### Feature Area: Domains automation
API contracts
- `POST /api/domains/{id}/verify`
- `POST /api/domains/{id}/provision-ssl`
- `POST /api/domains/{id}/renew-ssl`
- `GET /api/domains/{id}/dns-check`

Frontend tasks
- Add DNS verification and SSL status timeline on domains page.
- Add certificate expiry badge and renewal action.

Acceptance tests
- Domain verification transitions `pending -> active` only on valid DNS checks.
- SSL renewal updates certificate expiry metadata.

### Feature Area: Alert rule engine
API contracts
- `GET /api/alert-rules`
- `POST /api/alert-rules`
- `PUT /api/alert-rules/{id}`
- `DELETE /api/alert-rules/{id}`
- `POST /api/alert-rules/{id}/test`

Frontend tasks
- Add alert rules page and rule editor.
- Link active alerts to source rule and trigger metric.

Acceptance tests
- Rule evaluation creates alerts under threshold breach.
- Rule suppression/dedup prevents alert storms.

### Feature Area: Notification channel completion
API contracts
- Extend integrations API for Teams/GitHub/GitLab/Cloudflare channels.
- Add `POST /api/notifications/channels/{channel}/test`.

Frontend tasks
- Wire settings integrations buttons that currently have no hooks.
- Add test-send UI and status feedback.

Acceptance tests
- Channel config persists encrypted and test-send works.
- Disabled channel suppresses outgoing events.

## Sprint 7 (P2): Clusters and Advanced Deployment Operations
### Feature Area: Cluster lifecycle controls
API contracts
- `POST /api/clusters/{id}/nodes/{serverId}/cordon`
- `POST /api/clusters/{id}/nodes/{serverId}/uncordon`
- `POST /api/clusters/{id}/nodes/{serverId}/drain`
- `POST /api/clusters/{id}/rebalance`

Frontend tasks
- Add cluster node action menu and maintenance state badges.
- Add rebalance progress modal.

Acceptance tests
- Drained node receives no new placements.
- Rebalance migrates workloads according to strategy.

### Feature Area: Deployment approvals and canary
API contracts
- `POST /api/deployments/{id}/approve`
- `POST /api/deployments/{id}/reject`
- `POST /api/deployments/{id}/canary/start` (`trafficPercent`, `stepDuration`)
- `POST /api/deployments/{id}/canary/promote`
- `POST /api/deployments/{id}/canary/abort`

Frontend tasks
- Add approval queue panel and canary controls in deployment detail.
- Add traffic split visualization.

Acceptance tests
- Unapproved deployment blocks production promotion.
- Canary abort rolls back traffic to stable revision.

## Sprint 8 (P3): Enterprise Hardening and Scale
### Feature Area: Audit/compliance/traceability
API contracts
- `GET /api/audit/export?from=&to=&actor=&resource=`
- `GET /api/audit` with correlation id filters
- `POST /api/security/policies` for immutable retention rules

Frontend tasks
- Add audit export filters and download workflow.
- Add correlation id search in logs/operations surfaces.

Acceptance tests
- Export includes filtered records and is immutable once generated.
- Correlation IDs link deployment, logs, alerts, and notifications.

### Feature Area: API security and throttling
API contracts
- Global throttling middleware and per-api-key limits.
- `GET /api/api-keys/{id}/usage` with rate-limit counters.

Frontend tasks
- Show API key usage chart and throttle incidents.

Acceptance tests
- Burst traffic hits configured limits and returns standard throttle response.
- Key-specific limits apply independently.

## Cross-Sprint Backlog Coverage (No Missing Existing Feature Areas)
- Dashboard
- AI assistant
- Projects
- Deployments
- Services
- Servers
- Clusters
- Containers
- Databases
- S3 destinations
- Domains
- Volumes
- Pipelines
- Logs
- Monitoring
- Alerts
- Team
- Security
- Cost monitor
- Settings and Integrations

## Release Gating and Launch Checklist
### Build/test gate (must pass every sprint)
- `dotnet build backend/DeployFlow.sln`
- `dotnet test backend/tests/DeployFlow.Tests/DeployFlow.Tests.csproj`
- `npm run lint` in frontend
- `npm run build` in frontend

### Final launch gate (after Sprint 8)
- Full regression test pass for all feature areas.
- Zero P0 and P1 open defects.
- Observability baseline in place for logs/metrics/alerts.
- Disaster recovery validation completed (backup + restore drill).
- Production deployment and smoke checks complete.

## Metrics of Completion
- Gap closure rate by sprint and priority tier.
- Deployment success rate and rollback MTTR.
- Backup/restore success rate.
- Alert precision ratio (actionable alerts / total alerts).
- API reliability and throttle event trend.
