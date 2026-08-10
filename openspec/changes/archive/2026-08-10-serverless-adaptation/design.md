# Design: Serverless Adaptation

## Technical Approach

Exploration 1A (pure handler + `SessionAwareHandler` wrapper over `ISessionStore`) + 2A (ASP.NET minimal API) + process-env-over-`.env` config, per the proposal. Order: **sessions → config → webhook**, with a build+suite gate right after adding the FrameworkReference.

## Architecture Decisions

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D1 | PORT | **Optional, default 8080** | CONFIG-2 conflicts with CONFIG-4/WEBHOOK-1; PORT never required. Required set: `TELEGRAM_API_KEY`, `MONGO_URI`, `MONGO_INITDB_DATABASE`, `WEBHOOK_URL`, `WEBHOOK_SECRET_TOKEN`. |
| D2 | Save semantics | **Dirty check in wrapper** — snapshot before delegate, compare 5 fields after; skip save when unchanged | `/share`, `/revoke`, `/help`, idle `/start`, unknown commands never bump `Version` (no spurious CAS). |
| D3 | Replay rule | On CAS fail: reload + re-run **full handler pass**, ≤3 attempts, return **last reply** | Bounded loop; `/share` replay regenerates a token — acceptable, contention-only. |
| D4 | First write | `expectedVersion == 0` → `InsertOneAsync` (dup-key → `false`); else CAS `ReplaceOneAsync` | Upsert throws E11000 on mismatch. Invariant: stored `Version >= 1`. |
| D5 | SetWebhook seam | `IWebhookRegistrar` + `WebhookRegistrationService : IHostedService` try/catch | WEBHOOK-6 "non-fatal" testable with a fake registrar. |
| D6 | Transport entry | Single `IBotAuthHandler` (session-taking); `SessionAwareHandler : IBotAuthHandler`, `session` arg **ignored** (wrapper owns load) | Decorator stays swappable (SESSION-6); placeholder `new ChatSession()` localized to dispatcher. |
| D7 | Secret compare | `CryptographicOperations.FixedTimeEquals` (UTF-8) | Constant-time per WEBHOOK-3. |
| D8 | `.env.example` | Required set + `WHATSAPP_API_KEY` + commented `PORT` | Design agent couldn't read it (permission); apply verifies contents. |

## Architecture

```
Program.cs (WebApplication, DI, /webhook, /healthz)
  └─ WebhookRequestHandler(WebhookDispatcher, secretToken)  → 200/400/401
       └─ WebhookDispatcher(ITelegramBotClient, SessionAwareHandler)
            └─ SessionAwareHandler : IBotAuthHandler   [gate·load·snapshot·delegate·save·CAS+replay]
                 ├─ BotAuthHandler (pure) ──→ IAuthService / IShareService
                 └─ ISessionStore ──→ MongoSessionStore ──→ Sessions (CAS, TTL)
  └─ WebhookRegistrationService : IHostedService ──→ IWebhookRegistrar ──→ ITelegramBotClient
```

## Interfaces / Contracts

```csharp
public interface IBotAuthHandler {
    Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct);
}
public interface ISessionStore {
    Task<ChatSession> LoadAsync(long chatId, CancellationToken ct = default);   // fresh Idle v0, ChatId set, when absent
    Task<bool> SaveAsync(long chatId, ChatSession session, long expectedVersion, CancellationToken ct = default); // false = conflict (no throw); infra errors throw
}
public interface IWebhookRegistrar {
    Task SetWebhookAsync(string url, string secretToken, CancellationToken ct);
}
```

- **`ChatSession`**: adds `[BsonId] ChatId`, `UpdatedAt` (UTC), `Version`. Fresh = `State=Idle`, `Role=None`, `Version=0`.
- **`MongoSessionStore(MongoDbContext)`** → `Sessions`; per D4: `InsertOneAsync(Version=1)` or `ReplaceOneAsync(Filter.Eq(ChatId) & Filter.Eq(Version, expected), replacement Version=expected+1, UpdatedAt=UtcNow)`; success = `ModifiedCount==1`. `EnsureIndexesAsync` adds TTL (`UpdatedAt`, `ExpireAfter=1h`).
- **`SessionAwareHandler(IBotAuthHandler inner, ISessionStore store)`**: per-chat `SemaphoreSlim` gate → `LoadAsync` → snapshot → `inner.HandleAsync` → if dirty `SaveAsync(snapshot.Version)`; `false` → reload+replay (D3).
- **`WebhookDispatcher(ITelegramBotClient, IBotAuthHandler)`**: `CallbackQuery` → `AnswerCallbackQueryAsync` + handler(`callbackData`); `Message` → handler(`text`); non-null reply → one `SendMessage` via `TelegramMarkup.Build` (moved from `TelegramChannel`, then file deleted). Passes placeholder per D6.
- **`WebhookRequestHandler`**: `Task<int> ProcessAsync(Stream body, string? secretHeader, CancellationToken)` → 401 (D7), 400 (`JsonException`/null from `DeserializeAsync<Update>(body, JsonBotAPI.Options)`), 200 after dispatch.
- **`Program.cs`**: `DotNetEnv.Env.Load()` (non-overwrite) → fail-fast required resolution naming the missing var → `UseUrls($"http://0.0.0.0:{port}")` → DI (context, services, handler, store, wrapper, client, dispatcher, registrar) → `MapPost("/webhook")`, `MapGet("/healthz")`.
- **`FakeSessionStore`**: in-memory per-chat + CAS emulation, `ConflictOnce` hook, `Lookup(chatId)`.

## Concurrency

Per-chat gate serializes in-process; cross-instance safety is `Version` CAS + full-pass replay (D3); dirty check (D2) stops non-mutating writes.

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Unit | `SessionAwareHandler` | `FakeSessionStore`: gate serialization, dirty skip, CAS conflict → replay + last reply, ≤3 cap, first-write |
| Unit | Pure handler | Migrate ~40 sites to explicit-session asserts; concurrency tests move to wrapper tests |
| Unit | Webhook | `FakeTelegramBotClient` (captures sends/answers/SetWebhook): dispatch, single reply + markup, 401/400/200, registrar non-fatal |
| Integration | `MongoSessionStore` | `MongoFact` + extended `IntegrationTestBase`: round-trip, first-write, CAS stale→false, TTL index exists |
| Gate | FrameworkReference | `dotnet build` + full suite green **before** webhook endpoint work |

## Threat Matrix

N/A — no routing (VCS/process), shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary introduced. `/webhook` is HTTP surface covered by WEBHOOK-2/3 unit tests, not a matrix boundary.

## Migration / Rollout

No migration. `Sessions` is new; TTL 1h soft-expiry, touch-on-write; flow loss equals today's restart. Rollback = revert branch (additive only).

## Task Breakdown Sketch (test-first, in order)

1. `ChatSession` fields → 2. `ISessionStore` + `FakeSessionStore` → 3. `MongoSessionStore` + TTL + extended `IntegrationTestBase` + `MongoFact` tests → 4. Pure `BotAuthHandler` (drop `_sessions`/`_gates`/`GetSession`) + migrate sites → 5. `SessionAwareHandler` + tests → **gate: suite green** → 6. Config resolution + `.env.example` (apply verifies D8) → 7. FrameworkReference → **gate: build + suite** → 8. Web host + DI + binding → 9. `WebhookDispatcher` + tests → 10. `WebhookRequestHandler` + tests → 11. `/webhook`, `/healthz`, registration service → 12. Delete `TelegramChannel.cs` → **gate: green, no polling**.

## Open Questions

None.
