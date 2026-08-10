# Exploration: serverless-adaptation

Migrate the bot from a single-process in-memory + long-polling design to a serverless multi-instance Cloud Run deployment (GCP). This change covers steps 1–3 of the deployment plan already documented in `readme.md`: sessions out of memory into MongoDB, Telegram long-polling → webhook, and production-ready config/env handling.

## Current State

- **Entry point (`src/Program.cs`)**: console app (`OutputType Exe`, net10.0). `Main` calls `DotNetEnv.Env.Load()`, reads `TELEGRAM_API_KEY` / `WHATSAPP_API_KEY` / `MONGO_URI` / `MONGO_INITDB_DATABASE`, pings Mongo, ensures indexes, hand-wires `MongoDbContext → UserRepository → AuthService / AllergyService / ShareService → BotAuthHandler`, then blocks in `TelegramChannel.StartAsync` on `Console.ReadLine()`. No DI container, no web host.
- **`BotAuthHandler` (src/bots/BotAuthHandler.cs)**: owns per-chat state entirely in memory via `ConcurrentDictionary<long, ChatSession> _sessions` + `ConcurrentDictionary<long, SemaphoreSlim> _gates`. `HandleAsync(chatId, text, callbackData, ct)` acquires a per-chat gate, gets-or-creates the session, mutates it in place inside `HandleCoreAsync`, and returns `BotReply?`. Every flow transition mutates the session: `State`, `Role`, `UserId`, `GuestToken`, `PendingEmail` (see `HandleRegisterEmail`, `HandleRegisterPasswordAsync`, `HandleLoginEmailAsync`, `HandleLoginPasswordAsync`, `HandleLogout`, `HandlePendingCommand`, `StartLoginFlow`, `StartRegisterFlow`). `/share` and `/revoke` do **not** mutate the session. `GetSession(long)` is `internal` (used heavily by tests). Sessions reset on restart — this is the core multi-instance problem.
- **`ChatSession` (src/bots/ChatSession.cs)**: POCO with `State`, `Role`, `UserId (ObjectId?)`, `GuestToken`, `PendingEmail`. No `ChatId`, no timestamps, no version. BSON-serializable as-is.
- **`TelegramChannel` (src/channels/TelegramChannel.cs)**: long-polling via Telegram.Bot events (`OnError`, `OnMessage`, `OnUpdate`). Builds `InlineKeyboardMarkup` from `BotReply.Buttons`, `SendMessage` for replies, `AnswerCallbackQuery` for callbacks. This is the code the webhook replaces.
- **Data layer (src/data/MongoDbContext.cs, UserRepository.cs)**: `MongoDbContext` holds `IMongoClient`/`IMongoDatabase`, exposes `GetCollection<T>(name)` + `EnsureIndexesAsync()` (unique `Email`, non-unique `ShareToken`) + `PingAsync()`. `UserRepository` takes the context in its ctor and grabs collection `"Users"`. A `MongoSessionStore` fits this exact pattern.
- **Tests (tests/Allergy_BotHelper.Tests/)**: unit tests build the handler as `new BotAuthHandler(auth, share)` and assert via `handler.GetSession(chatId)` (~40 call sites). `FakeUserRepository` is an in-memory `IUserRepository` (with call counters, duplicate-key emulation). Integration tests use `[MongoFact]` gated on `RUN_MONGO_TESTS=1` and `IntegrationTestBase : IAsyncLifetime` that cleans the `Users` collection pre/post; `AssemblyInfo.cs` disables parallelization (shared DB).
- **Telegram.Bot 22.10.2.1 (pinned, verified from package)**: request-object client. Webhook surface exists on `TelegramBotClientExtensions`: `SetWebhook(client, url, certificate, ipAddress, maxConnections, allowedUpdates, dropPendingUpdates, secretToken, ct)` — **native secret-token support** — plus `DeleteWebhook`, `GetWebhookInfo`, `GetMe`, `SendMessage`, `AnswerCallbackQuery`. `JsonBotAPI.Options` (`JsonSerializerOptions`) is the serializer used to deserialize `Update` JSON in a webhook. `Update` exposes `Id`, `Message`, `CallbackQuery`, `Type`, etc.

## Affected Areas

- `src/Program.cs` — startup rewrite: web host + DI + config precedence + webhook endpoint registration.
- `src/bots/BotAuthHandler.cs` — signature change so the handler takes a session; sessions/gates leave the handler (moved to a wrapper/owner).
- `src/bots/ChatSession.cs` — gains persistence fields (`ChatId`, `UpdatedAt` for TTL, `Version` for CAS). Field additions only; state machine untouched.
- `src/interfaces/ISessionStore.cs` (new) — `Load`/`Save` by chatId, plus `FakeSessionStore` in tests.
- `src/data/SessionStore.cs` (new) — Mongo implementation: TTL index + CAS `ReplaceOneAsync`.
- `src/channels/TelegramChannel.cs` — polling wiring replaced by a webhook endpoint + `SetWebhook` registration; reply/markup logic is preserved.
- `src/data/MongoDbContext.cs` — add `Sessions` collection TTL index creation in `EnsureIndexesAsync`.
- `Allergy_Bothelper.csproj` — add `FrameworkReference Microsoft.AspNetCore.App` (or switch SDK to `Microsoft.NET.Sdk.Web`) for the minimal API.
- `tests/Allergy_BotHelper.Tests/` — handler construction/`GetSession` assertions adjust; new `FakeSessionStore` unit tests; new `MongoFact`-gated `SessionStore` integration tests; possibly a new `IntegrationTestBase` (or extend it) to clean the `Sessions` collection.
- `.env.example` — new vars (`WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN`, optionally `PORT`); `.env` stays local-only.
- `readme.md` — already documents this plan (working tree has uncommitted edits; do not touch).

## Workstream 1 — Sessions out of memory (ISessionStore + Mongo implementation)

**Approach 1A — Pure handler + wrapper/owner (recommended, matches decided scope).**
`IBotAuthHandler.HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, ct)` — the handler receives a session (defaults to a fresh `ChatSession` when absent), mutates it, and returns `BotReply?`. The handler drops `_sessions` and `_gates`. A new `SessionAwareHandler : IBotAuthHandler` wrapper owns:
1. per-chat in-memory `SemaphoreSlim` gate (serializes within an instance),
2. `ISessionStore.LoadAsync(chatId)` → session (or `new ChatSession()`),
3. delegate to the pure handler,
4. `ISessionStore.SaveAsync(chatId, session)` with CAS on the version read at load,
5. retry on CAS conflict: reload and replay the whole delegate call, returning the reply of the last successful attempt (the state machine is deterministic given session+input, so replay is safe; `/start`, `/cancel`, login/register transitions are all idempotent re-applications).

- Pros: single session-ownership path; the handler stays a pure function of (session, input) — the design the scope demands; cross-instance safety via CAS+replay instead of locks.
- Cons: mechanical churn in `BotAuthHandlerTests` (constructor changes; `GetSession` assertions re-point at `FakeSessionStore`/wrapper).
- Effort: Medium.

**Approach 1B — Handler keeps store (inject `ISessionStore` into the handler).**
- Pros: least test churn; `GetSession` keeps meaning.
- Cons: couples the pure handler to persistence, violating the decided wrapper/owner split; two ownership modes linger.
- Effort: Low-Medium. Rejected — contradicts the decided scope.

**Mongo implementation.** `MongoSessionStore(MongoDbContext)` → collection `"Sessions"`. Document shape mirrors `ChatSession` plus `ChatId` (long, `_id`), `UpdatedAt` (DateTime, refreshed on every save), `Version` (int64, incremented per save). TTL index: `CreateOneAsync(new CreateIndexModel<ChatSessionRecord>(IndexKeys.Ascending(x => x.UpdatedAt), new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(1) }))`. CAS write: `ReplaceOneAsync(Filter = ChatId == id && Version == expected, replacement with Version+1)`; success = `ModifiedCount == 1`. Note Mongo TTL runs ~every 60s, so a 1h TTL is a soft bound (±1 min). Reset TTL by bumping `UpdatedAt` on each save (touch-on-write keeps active chats alive).

**Gotcha — CAS vs. gate layering**: the in-memory gate only serializes within one instance. Telegram webhook delivery is ordered per chat, but Cloud Run can route to different instances; the version check is the real cross-instance safety net. A failed CAS must replay the handler pass — never return the stale reply.

## Workstream 2 — Webhook (replace long-polling)

**Approach 2A — ASP.NET minimal API in-process (recommended).**
- `csproj`: add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (keeps the `Exe` + single-project layout; the test project reference keeps working). Switching the SDK to `Microsoft.NET.Sdk.Web` is equivalent but changes default build behavior more.
- `Program.cs`: `WebApplication.CreateBuilder(args)`; register `TelegramBotClient`, `BotAuthHandler`, `SessionAwareHandler`, repositories/services in DI; bind `app.MapPost("/webhook", ...)`.
- Endpoint: read body, `await JsonSerializer.DeserializeAsync<Update>(stream, JsonBotAPI.Options)`; validate `X-Telegram-Bot-Api-Secret-Token` equals `WEBHOOK_SECRET_TOKEN` (constant-time compare; 401 on mismatch); dispatch: `Message` → handler with `text`, `CallbackQuery` → `AnswerCallbackQuery` + handler with `callbackData`; if `reply != null` → `SendMessage` (reuse the exact markup-building logic from `TelegramChannel`); always return `Results.Ok()` after the reply is sent (fast: one handler pass + one outbound call).
- Registration: at startup call `client.SetWebhook(webhookUrl, secretToken: ..., allowedUpdates: [Message, CallbackQuery])` (idempotent; tolerate already-set). `WEBHOOK_URL` env supplies the public HTTPS URL.
- Host binding for Cloud Run: `builder.WebHost.UseUrls($"http://0.0.0.0:{PORT}")` where `PORT` defaults to `8080`.
- Polling removal: `TelegramChannel.StartAsync` (and its blocking `Console.ReadLine()`) is deleted. One code path — webhook only. Local dev works via a tunnel (ngrok/cloudflared) or localhost webhook (Telegram requires HTTPS).

- Pros: one host, DI, dead-simple wiring, `200 fast` shape matches Cloud Run; secret-token support is native in the pinned Telegram.Bot.
- Cons: requires the ASP.NET framework reference; `SendMessage` is awaited inside the request (adds latency but stays well under Cloud Run's default request timeout; required to preserve per-chat ordering).
- Effort: Medium.

**Approach 2B — Separate web server / standalone HttpListener.** Rejected: no DI, manual HTTP, reinvents ASP.NET.

**Gotchas.** Cold start absorbed by `min-instances=1` (deployment-time, not in code — note for propose/design). Every instance calling `SetWebhook` at startup is safe but races; tolerate and move on. Do not return Telegram errors for invalid payloads without a body that matters — keep 400/401/200 semantics simple.

## Workstream 3 — Config/env

- **Precedence**: process env wins, `.env` only as local fallback. `DotNetEnv.Env.Load()` does **not** overwrite existing variables by default (DotNetEnv 3.x default `overwrite: false`), so it can be called unconditionally; on Cloud Run there is no `.env` in the image and the platform env + Secret Manager values win.
- **Required env vars**: `TELEGRAM_API_KEY` (see naming fork below), `MONGO_URI`, `MONGO_INITDB_DATABASE`, `PORT` (Cloud Run-provided), `WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN`.
- **Mongo → Atlas M0**: same `MongoDB.Driver 3.10.0` and `MongoClient`; Atlas URI carries credentials; no driver code change. Note: M0 is a shared sandbox — TTL index and the existing `Users` indexes work unchanged.
- **Env var naming fork (real decision for proposal)**: current code reads `TELEGRAM_API_KEY`. Scope text names `TELEGRAM_BOT_TOKEN`. Keep `TELEGRAM_API_KEY` (zero churn, `WHATSAPP_API_KEY` sibling stays consistent) or standardize on `TELEGRAM_BOT_TOKEN` (matches plan, requires `.env.example` + docs + one-line reads). Recommendation: keep `TELEGRAM_API_KEY`; it is not worth churn inside this change.

## Options Summary

| Approach | Pros | Cons | Effort |
|----------|------|------|--------|
| 1A Pure handler + wrapper (recommended) | single ownership path, matches scope, CAS+replay safety | mechanical test churn | Medium |
| 1B Inject store into handler | minimal churn | couples handler to storage, contradicts scope | Low-Medium |
| 2A Minimal API in-process (recommended) | DI, simple, native secret-token support | needs ASP.NET framework ref | Medium |
| 2B Standalone HTTP server | none over 2A | no DI, manual HTTP | High |
| Keep `TELEGRAM_API_KEY` (recommended) | zero churn | name ≠ plan wording | Low |
| Rename to `TELEGRAM_BOT_TOKEN` | matches plan | env/docs/read churn | Low |

## Recommendation

Adopt 1A + 2A + keep `TELEGRAM_API_KEY`. Order of work: **(1) sessions** (ISessionStore + FakeSessionStore + MongoSessionStore + handler signature refactor + wrapper + tests) → **(2) config/env** (startup service resolution, precedence) → **(3) webhook** (csproj framework ref, minimal API, SetWebhook, PORT binding, polling removal). Untouched: `AuthService`, token lifecycle (`ShareService`/`UserRepository.GenerateTokenAsync`/`RevokeTokenAsync`), `BotCopy` copy, `/help` logic, `AllergyService`, the `Users` collection + its indexes.

## Risks

- **Multi-instance concurrency**: concurrent updates for one chat across instances; last-write-wins would drop a transition. Mitigated by version-CAS + replay-on-conflict. Never apply a CAS without replaying the handler pass.
- **TTL choice**: 1h idle expiry silently resets long-paused flows (today restart already loses them — acceptable; document). Mongo TTL sweep runs ~every 60s → expiry is soft.
- **Cold start**: `min-instances=1` is a Cloud Run deployment knob, not code; must be recorded in the proposal. First webhook call per instance pays Mongo connection + handler warm-up.
- **Framework switch**: adding the ASP.NET Core framework reference to a console `Exe` project is low-risk but must be validated by `dotnet build` + the full test suite (tests reference the main project).
- **Test churn**: `GetSession` removal touches ~40 assertion sites; keep it mechanical and run the suite before moving to workstream 2.
- **SetWebhook startup race**: benign; make it idempotent and non-fatal if another instance already set it.
- **Atlas connectivity**: first-time Atlas URI/certs must be validated in the integration suite (`RUN_MONGO_TESTS=1`) against the same driver version before Cloud Run work starts.

## Ready for Proposal

Yes — scope (3 workstreams), order, and the untouched list are settled. The proposal should surface: the handler-signature refactor (1A), the env naming decision (`TELEGRAM_API_KEY`), TTL=1h, CAS+replay semantics, `min-instances=1` as a deployment knob, and that `readme.md` already documents this plan (has uncommitted edits — leave alone).
