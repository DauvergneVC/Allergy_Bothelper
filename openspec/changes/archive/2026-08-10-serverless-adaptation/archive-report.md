# Archive Report: serverless-adaptation

- **Change**: serverless-adaptation
- **Status**: CLOSED — completed and verified PASS
- **Archive date**: 2026-08-10
- **Artifact store**: openspec (FILES)
- **Archived to**: `openspec/changes/archive/2026-08-10-serverless-adaptation/`

## Scope

Adapts the bot for Cloud Run serverless: sessions persisted in MongoDB, Telegram long-polling replaced by an ASP.NET Core webhook endpoint, and production configuration resolved from environment (process env over `.env`). Implements steps 1–3 of the `readme.md` plan.

## Commits

Range on local `main`, baseline → HEAD:

- **Baseline**: `45b9d25` (`feat(bot): scope /help command list by session state`)
- **HEAD**: `cf5029b8` (`feat(webhook): replace polling with Telegram webhook endpoint`)

| Commit | Scope |
|--------|-------|
| `fd459ef1` `feat(sessions): persist chat sessions in MongoDB with TTL and CAS` | `ChatSession` `ChatId`/`UpdatedAt`/`Version`; `ISessionStore` + `MongoSessionStore` (insert-on-first-write, versioned CAS); 1h TTL index; `FakeSessionStore` + Mongo integration tests |
| `e123a25a` `refactor(bots): make auth handler pure with session-aware wrapper` | Pure `BotAuthHandler`; `SessionAwareHandler` wrapper (gate → load → dirty-checked save → CAS replay ≤3); test sites migrated to explicit sessions |
| `2f931bcb` `feat(config): resolve env config with process-env precedence` | `EnvConfig.Resolve` fail-fast required vars, optional `PORT` default 8080, non-clobber `.env`; `EnvConfigTests`; `.env.example` webhook vars |
| `cf5029b8` `feat(webhook): replace polling with Telegram webhook endpoint` | `FrameworkReference Microsoft.AspNetCore.App`; `TelegramMarkup`; `WebhookDispatcher`/`WebhookRequestHandler`; `IWebhookRegistrar` + `WebhookRegistrationService` (non-fatal, idempotent); `WebApplication` host + DI, `/webhook`, `/healthz`; `TelegramChannel` deleted |

> The commit hashes in `apply-progress.md` were corrected from the stale prefixes `fd459ef6`/`e123a25d`/`2f931bc6`/`cf5029b6` (which did not resolve) to the real prefixes above, verified against `git log --format=%H`. This was verify-report SUGGESTION 1.

## Capabilities (now in `openspec/specs/`)

### session-persistence — SESSION-1..8
`ISessionStore` contract; `MongoSessionStore` on `Sessions` keyed by `ChatId`; document shape (`State`, `Role`, `UserId`, `GuestToken`, `PendingEmail`, `UpdatedAt`, `Version`); TTL index on `UpdatedAt` (1h, touch-on-write); compare-and-swap writes (filter `ChatId + Version == expected`, store `Version+1`, success = `ModifiedCount==1`; first write inserts, dup-key → conflict); `SessionAwareHandler` wrapper (per-chat gate, load, dirty-check save, CAS conflict → reload + full-pass replay ≤3, last reply); pure handler with explicit session parameter; `FakeSessionStore` with CAS emulation + `ConflictOnce` hook.

### telegram-webhook — WEBHOOK-1..8
ASP.NET Core minimal API on `http://0.0.0.0:{PORT}` (default 8080); `POST /webhook` deserializing a Telegram `Update` (400 on unreadable payload); constant-time secret-token validation on `X-Telegram-Bot-Api-Secret-Token` (401 without dispatch); Message → handler(text) and CallbackQuery → `AnswerCallbackQuery` + handler(data); non-null `BotReply` → one `SendMessage` with markup, endpoint returns 200; startup `SetWebhook(URL, secret, allowedUpdates: Message + CallbackQuery)` via `IWebhookRegistrar` seam, idempotent and non-fatal; polling code path removed (`TelegramChannel.cs` deleted); `GET /healthz` → 200.

### config-env — CONFIG-1..6
Process env precedence over `.env` (non-overwrite load); fail-fast startup resolution of `TELEGRAM_API_KEY`, `MONGO_URI`, `MONGO_INITDB_DATABASE`, `WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN` (naming any missing variable), `PORT` optional defaulting to 8080; Mongo client/database derived from `MONGO_URI` + `MONGO_INITDB_DATABASE`; `WEBHOOK_URL`/`WEBHOOK_SECRET_TOKEN` flow to registration and validation; `.env.example` documents every required variable with non-secret placeholders, no secrets committed.

## Tests / Evidence (final state)

- **Build**: `0` warnings / `0` errors — `dotnet build Allergy_Bothelper.csproj --nologo -v minimal` (exit 0).
- **Unit suite**: `133` passed / `11` skipped / `0` failed / `144` total — `dotnet test tests/Allergy_BotHelper.Tests` without `RUN_MONGO_TESTS` (the 11 skipped are `MongoFact`/integration tests gated on `RUN_MONGO_TESTS`).
- **Integration suite (Mongo live)**: `144` passed / `0` skipped / `144` total with `RUN_MONGO_TESTS=1` against local MongoDB.
- **Requirements**: `22/22`; **Scenarios**: `36/36` (33 via committed automated tests; 3 — host binding, `/healthz`, and one webhook status path — via code path plus the documented apply live smoke: `/healthz` → 200, bad-token `/webhook` → 401, valid → 200; startup webhook registration failed non-fatally against Telegram in the smoke run, confirming WEBHOOK-6).
- **Polling removal**: grep across `src/*.cs` for `Telegram.Bot.Polling|GetUpdates|StartReceiving|Console.ReadLine` → 0 hits.
- **Verdict**: `gentle-ai.verify-result/v1` — `pass`, `blockers: 0`, `critical_findings: 0`. Design decisions D1–D8 all followed.
- Verify-report SUGGESTION 1 (non-resolving commit hashes) is fixed by the apply-progress correction above; the remaining suggestions are recorded as follow-ups.

## Follow-ups (SUGGESTION-level, non-blocking)

1. **No automated host-level test** — host binding (WEBHOOK-1), `/healthz` (WEBHOOK-8), and the `/webhook` HTTP-status mapping are covered by unit tests + code inspection + apply live smoke, but no committed test boots the `WebApplication`. Suggested fix: integration test that boots the host on a free port and asserts `/healthz` → 200, bad-token `/webhook` → 401, malformed body → 400.
2. **`SessionAwareHandler._gates` never evicted** — the per-chat `SemaphoreSlim` dictionary grows with distinct chat ids for process lifetime. Bounded by chat ids seen and per-instance only (Cloud Run), chats TTL out after ~1h, so risk is low. Suggested fix (optional): remove the gate after an idle window.
3. **Null-forgiving callback message** — `WebhookDispatcher` uses `query.Message!` on the CallbackQuery path, which would NRE if a callback arrives without a `Message`. Telegram always supplies the originating message for inline-keyboard callbacks, so this is safe in practice. Suggested fix (optional): guard `query.Message is not null` and skip/answer without dispatch otherwise.
4. **`readme.md` uncommitted working-tree change** — the Deployment architecture section + session note added by the orchestrator remains UNCOMMITTED by design (orchestrator-requested docs, pending user decision on whether/how to commit). Do not treat as a defect of this change.

## Deviations

- **Archive-date note**: the launch prompt asked for "the 3 follow-ups above" while enumerating four items; all four enumerated follow-ups are recorded above (all SUGGESTION-level).
- No other deviations. No commit hashes were left stale; delivery (push/PR) was NOT performed — the four commits exist on local `main` only.
- Working tree at close: `readme.md` modified (untouched by archive), `.atl/` and `openspec/` untracked. No commits made by the archive phase.

## Next Recommended

None — pipeline complete for `serverless-adaptation`. The next change is image analysis (OCR) + allergy matcher.
