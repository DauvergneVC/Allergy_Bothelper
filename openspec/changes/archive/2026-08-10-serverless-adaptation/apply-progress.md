# Apply Progress: Serverless Adaptation

- **Change**: serverless-adaptation
- **Status**: completed
- **Date**: 2026-08-10
- **Tasks**: 25/25 complete (`openspec/changes/serverless-adaptation/tasks.md`)

## Work-Unit Commits

| Commit | Scope |
|--------|-------|
| `fd459ef1` `feat(sessions): persist chat sessions in MongoDB with TTL and CAS` | ChatSession `ChatId`/`UpdatedAt`/`Version` fields; `ISessionStore` + `MongoSessionStore` (insert-on-first-write, versioned CAS `ReplaceOneAsync`); 1h TTL index on `UpdatedAt`; `FakeSessionStore` + Mongo integration tests |
| `e123a25a` `refactor(bots): make auth handler pure with session-aware wrapper` | `BotAuthHandler` is now pure (`HandleAsync(chatId, session, text, callbackData, ct)`, no in-memory sessions/gates); new `SessionAwareHandler` wrapper owns gate → load → dirty-checked save → CAS replay (≤3 passes); test sites migrated to explicit sessions |
| `2f931bcb` `feat(config): resolve env config with process-env precedence` | `EnvConfig.Resolve` with fail-fast required vars, optional `PORT` default 8080, `NoClobberLoad` `.env` semantics; `EnvConfigTests`; `.env.example` webhook vars |
| `cf5029b8` `feat(webhook): replace polling with Telegram webhook endpoint` | `FrameworkReference Microsoft.AspNetCore.App`; `TelegramMarkup` extraction; `WebhookDispatcher`/`WebhookRequestHandler`; `IWebhookRegistrar` + `TelegramWebhookRegistrar` + `WebhookRegistrationService` (non-fatal, idempotent); `WebApplication` host with DI, `MapPost("/webhook")`, `MapGet("/healthz")`; `TelegramChannel` deleted (no polling); webhook test suite |

## Evidence

- **Build**: `0` warnings / `0` errors (`dotnet build Allergy_Bothelper.csproj`), verified after every commit.
- **Unit suite**: `133` passed, `11` skipped (Mongo-gated `MongoFact`), `0` failed — `dotnet test tests/Allergy_BotHelper.Tests` without `RUN_MONGO_TESTS`.
- **Integration suite**: `144` passed / `144` total with `RUN_MONGO_TESTS=1` against local MongoDB.
- **Live smoke**: app boots on `http://0.0.0.0:8080`; `/healthz` → 200; `POST /webhook` without token → 401, with valid token + valid body → 200. Webhook registration failed non-fatally in the smoke run (Telegram requires HTTPS URLs) — confirming non-fatal startup behavior.
- **No polling**: `rg` for `Telegram.Bot.Polling`/`GetUpdates`/`StartReceiving`/blocking reads in `src` → 0 hits.

## Notes

- Sessions TTL is 1 hour; Mongo's sweep is approximate (~60s cadence), and `UpdatedAt` is touched on every save to keep active chats alive.
- CAS semantics: `expectedVersion == 0` → insert (duplicate-key → `false`); otherwise `ReplaceOneAsync` on `ChatId + Version == expected`, success = `ModifiedCount == 1`.
- `SessionAwareHandler` dirty-checks the five mutable fields so non-mutating updates (`/help`, idle `/start`, unknown commands) never bump `Version`; CAS conflict triggers reload + full-pass replay, bounded to 3 passes (3 loads, no reload after the final attempt).
- `FrameworkReference Microsoft.AspNetCore.App` enables the `WebApplication` host without changing the SDK.
- `TelegramChannel` (long-polling) is deleted; the HTTP host is the only entry point.
- Config precedence: process environment wins, `.env` fills gaps only (`clobberExistingVars: false`).

## Deviations

None — implementation matches `proposal.md`, `design.md`, and the delta specs.

## Remaining Rollout Notes (PR description / deploy)

- Cloud Run `min-instances=1` — deployment knob, not code.
- Local development needs an HTTPS tunnel (Telegram rejects non-HTTPS webhook URLs).
- `Sessions` collection is additive — no data migration required.
