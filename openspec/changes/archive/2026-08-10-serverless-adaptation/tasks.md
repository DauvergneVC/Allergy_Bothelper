# Tasks: Serverless Adaptation

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,400–1,600 (WS1 ~700 · WS2 ~120 · WS3 ~700) |
| Review budget (this session) | 800 lines |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

> Single PR is viable only via `size:exception` (budget is 800 this session); otherwise slice WS1 → WS2 → WS3.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| U1 | Store contract + Mongo impl + TTL | PR 1 | `dotnet test tests/Allergy_BotHelper.Tests --filter "FullyQualifiedName~MongoSessionStore"` | `$env:RUN_MONGO_TESTS=1` + local/Atlas Mongo | Revert `ChatSession` fields, `ISessionStore.cs`, `SessionStore.cs`, `MongoDbContext.cs` TTL |
| U2 | Pure handler refactor + test-site migration | PR 2 | `dotnet test tests/Allergy_BotHelper.Tests --filter "FullyQualifiedName~BotAuthHandler\|FullyQualifiedName~Login\|FullyQualifiedName~Register"` | N/A (pure unit, no runtime boundary) | Revert `BotAuthHandler.cs` signature + test asserts |
| U3 | `SessionAwareHandler` wrapper + replay | PR 3 | `dotnet test tests/Allergy_BotHelper.Tests --filter "FullyQualifiedName~SessionAwareHandler"` | N/A (FakeSessionStore in-memory) | Remove `SessionAwareHandler.cs` + wrapper tests |
| U4 | Config resolution + `.env.example` | PR 4 | `dotnet test tests/Allergy_BotHelper.Tests --filter "FullyQualifiedName~EnvConfig"` | `dotnet run` with/without vars | Revert `Program.cs` config block + `.env.example` |
| U5 | FrameworkReference + web host + DI + PORT | PR 5 | `dotnet build` + full suite | `dotnet run --project .` + `curl localhost:8080/healthz` | Revert csproj `FrameworkReference` + host code |
| U6 | Dispatcher + request handler + markup | PR 6 | `dotnet test tests/Allergy_BotHelper.Tests --filter "FullyQualifiedName~Webhook"` | N/A (FakeTelegramBotClient) | Remove dispatcher/handler/markup files + tests |
| U7 | Startup registration + `/healthz` + polling removal | PR 7 | full suite + `rg "StartAsync\|Console.ReadLine" src` | `dotnet run` + `curl -X POST localhost:8080/webhook` (200/401) | Restore `TelegramChannel.cs` polling, drop endpoints |

## Phase 1: Sessions out of memory (workstream 1)

- [x] 1.1 Add `ChatId` (`[BsonId]`), `UpdatedAt` (UTC), `Version` to `src/bots/ChatSession.cs`; fresh session = `State=Idle`, `Role=None`, `Version=0` (SESSION-3).
- [x] 1.2 Create `src/interfaces/ISessionStore.cs`: `LoadAsync(chatId)` (fresh Idle v0 + ChatId set when absent), `SaveAsync(chatId, session, expectedVersion)` → `bool` (SESSION-1).
- [x] 1.3 Create `tests/Allergy_BotHelper.Tests/Fakes/FakeSessionStore.cs`: in-memory per-chat store, CAS emulation, `ConflictOnce` hook, `Lookup(chatId)` (SESSION-8).
- [x] 1.4 Create `src/data/SessionStore.cs` `MongoSessionStore(MongoDbContext)`: `expectedVersion==0` → `InsertOneAsync` (dup-key → `false`), else CAS `ReplaceOneAsync(ChatId & Version==expected, Version+1, UpdatedAt=UtcNow)`; success=`ModifiedCount==1`; conflict → `false`, infra errors throw (SESSION-2, SESSION-5).
- [x] 1.5 Add `Sessions` TTL index on `UpdatedAt` (`ExpireAfter=1h`) to `MongoDbContext.EnsureIndexesAsync` (SESSION-4).
- [x] 1.6 Extend `tests/.../Integration/IntegrationTestBase.cs` to clean `Sessions`; add `MongoSessionStoreIntegrationTests` (`MongoFact`): round-trip, first-write version 1, stale CAS → `false` + doc unchanged, TTL index exists (SESSION-2/4/5).
- [x] 1.7 Pure `BotAuthHandler`: signature → `HandleAsync(chatId, session, text, callbackData, ct)`; drop `_sessions`, `_gates`, `GetSession`; migrate ~42 `GetSession` sites in `BotAuthHandlerTests.cs`, `LoginTests.cs`, `RegisterTests.cs`, `GuardBindingTests.cs` to explicit-session asserts; move concurrency tests to wrapper (SESSION-7).
- [x] 1.8 Create `SessionAwareHandler(IBotAuthHandler inner, ISessionStore store)`: per-chat `SemaphoreSlim` gate → load → snapshot → delegate → dirty-check save (skip unchanged so `/share`, `/revoke`, `/help`, idle `/start` never bump `Version`); CAS `false` → reload + full-pass replay, ≤3 attempts, return last reply. RED first via `FakeSessionStore` (SESSION-6, SESSION-5, D2/D3).
- [x] 1.9 GATE G1: full suite green — `dotnet test tests/Allergy_BotHelper.Tests` — before workstream 2.

## Phase 2: Config and env (workstream 2)

- [x] 2.1 Config resolution in `src/Program.cs`: `DotNetEnv.Env.Load()` (non-overwrite, process env wins); fail-fast required set `TELEGRAM_API_KEY`, `MONGO_URI`, `MONGO_INITDB_DATABASE`, `WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN` naming the missing var; `PORT` optional default `8080` (CONFIG-1/2/3/4, D1).
- [x] 2.2 Add `EnvConfigTests` (pure resolution helper): process-env-wins, `.env` fills gaps, missing var names itself, PORT default (CONFIG-1/2/4).
- [x] 2.3 Update `.env.example`: add `WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN`, commented `PORT`; PRESERVE existing vars (`WHATSAPP_API_KEY`, `WHATSAPP_PHONE_NUMBER`, `TELEGRAM_API_KEY`, MONGO set) (CONFIG-6, D8; apply must verify preservation).
- [x] 2.4 GATE: suite still green after config change.

## Phase 3: Webhook (workstream 3)

- [x] 3.1 Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `Allergy_Bothelper.csproj` (WEBHOOK-1).
- [x] 3.2 GATE G2: `dotnet build` + full suite green BEFORE any webhook endpoint work.
- [x] 3.3 Extract markup builder from `src/channels/TelegramChannel.cs` into `src/channels/TelegramMarkup.cs` preserving button-to-markup logic (WEBHOOK-5).
- [x] 3.4 Create `tests/.../Fakes/FakeTelegramBotClient.cs`: captures `SendMessage`, `AnswerCallbackQuery`, `SetWebhook` (WEBHOOK-4/6).
- [x] 3.5 Create `src/webhook/WebhookDispatcher.cs`: `Message` → handler(text); `CallbackQuery` → `AnswerCallbackQueryAsync` + handler(callbackData); non-null reply → one `SendMessage` via `TelegramMarkup`; placeholder `new ChatSession()` per D6 (WEBHOOK-4/5).
- [x] 3.6 Create `src/webhook/WebhookRequestHandler.cs`: `DeserializeAsync<Update>(JsonBotAPI.Options)` → 400 on `JsonException`/null; `CryptographicOperations.FixedTimeEquals` on `X-Telegram-Bot-Api-Secret-Token` → 401 without dispatch; else dispatch → 200 (WEBHOOK-2/3, D7).
- [x] 3.7 Rewrite `src/Program.cs`: `WebApplication` builder + DI (context, services, handler, store, wrapper, client, dispatcher, registrar); `UseUrls($"http://0.0.0.0:{port}")`; `MapPost("/webhook")`, `MapGet("/healthz")` (WEBHOOK-1/8, CONFIG-4/5).
- [x] 3.8 Create `WebhookRegistrationService : IHostedService` over `IWebhookRegistrar` (default `TelegramBotClientExtensions.SetWebhook`): URL + secret + hardcoded `allowedUpdates: [Message, CallbackQuery]`; idempotent; try/catch non-fatal (WEBHOOK-6, D5).
- [x] 3.9 Add `WebhookTests`: dispatch Message/CallbackQuery, single reply + markup, null reply → no send, 401 wrong/missing token, 400 malformed body, 200, registrar failure non-fatal (WEBHOOK-2..6,8).
- [x] 3.10 Delete `src/channels/TelegramChannel.cs`; verify no polling start or blocking read remains (WEBHOOK-7).
- [x] 3.11 GATE G3: full suite green + no polling usage + local smoke (`/healthz` 200; `/webhook` 401 on bad token, 200 on valid).

## Phase 4: Rollout notes (non-code)

- [x] 4.1 Record in PR description: Cloud Run `min-instances=1` (deployment knob, not code), local dev via tunnel, `Sessions` additive/no migration. Do not edit `readme.md`.

## Archive

- **Status**: CLOSED — archived `2026-08-10`. All 25/25 tasks complete; verified PASS (build 0/0, unit 133 passed / 11 skipped, integration 144/144 live, 22/22 REQs, 36/36 scenarios, 0 blockers). See `archive-report.md` for the terminal record.
