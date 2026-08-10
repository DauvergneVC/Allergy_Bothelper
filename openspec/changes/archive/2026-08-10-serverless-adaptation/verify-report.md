```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:dffe9e5171354c5255b9c54ce1f98d05d9aa24d566676793250ef662f1483c2f
verdict: pass
blockers: 0
critical_findings: 0
requirements: 22/22
scenarios: 36/36
test_command: dotnet test tests/Allergy_BotHelper.Tests/Allergy_BotHelper.Tests.csproj --nologo -v minimal
test_exit_code: 0
test_output_hash: sha256:dffe9e5171354c5255b9c54ce1f98d05d9aa24d566676793250ef662f1483c2f
build_command: dotnet build Allergy_Bothelper.csproj --nologo -v minimal
build_exit_code: 0
build_output_hash: sha256:65635f7f9a0bf0f6883372284dba98a157a66e0fe3c2c509f962a3cb3656a247
```

# Verification Report: serverless-adaptation

**Status**: passed
**Change**: serverless-adaptation
**Date**: 2026-08-10
**Mode**: Standard (not Strict TDD)
**Artifact store**: openspec (FILES)
**Execution**: auto · Delivery: single-pr + maintainer `size:exception`

## Executive Summary

Independent verification of the `serverless-adaptation` change against the three delta specs (SESSION-1..8, WEBHOOK-1..8, CONFIG-1..6). Build is clean (0 warnings / 0 errors), the full unit suite passes (133 passed / 11 skipped / 0 failed) and the MongoDB-gated integration suite runs live and passes (144 passed / 0 skipped / 144 total). All 22 requirements map to passing tests plus source inspection; all 36 spec scenarios are exercised (33 via committed automated tests, 3 — host binding and `/healthz` — via code path plus the documented apply live smoke). All eight design decisions (D1–D8) are followed. No blockers, no critical findings; four SUGGESTION-level improvements. Verdict: **PASS** — archive-ready.

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 25 |
| Tasks complete | 25 |
| Tasks incomplete | 0 |
| Requirements | 22/22 |
| Scenarios | 36/36 |

## Build & Tests Execution

**Build**: ✅ Passed — 0 warnings / 0 errors
```text
dotnet build Allergy_Bothelper.csproj --nologo -v minimal
Build succeeded. 0 Warning(s), 0 Error(s).
```

**Unit suite (no env)**: ✅ 133 passed / 11 skipped / 0 failed / 144 total
```text
dotnet test tests/Allergy_BotHelper.Tests/Allergy_BotHelper.Tests.csproj --nologo -v minimal
Passed! - Failed: 0, Passed: 133, Skipped: 11, Total: 144
```
The 11 skipped are `MongoFact`/integration tests gated on `RUN_MONGO_TESTS` (discovery-time skip).

**Integration suite (Mongo live)**: ✅ 144 passed / 0 skipped / 0 failed / 144 total
```text
$env:RUN_MONGO_TESTS=1; $env:MONGO_URI='mongodb://root-dauvergne:***@localhost:27017/Allergy_helper_db?authSource=admin'; $env:MONGO_INITDB_DATABASE='Allergy_helper_db'; dotnet test ... --nologo -v minimal
Passed! - Failed: 0, Passed: 144, Skipped: 0, Total: 144
```

**Coverage**: ➖ Not available (no coverage tool configured); verification is evidence-based per the SDD verify contract (passing tests + source inspection).

## Spec Compliance Matrix

| Requirement | Scenario | Test evidence | Result |
|-------------|----------|---------------|--------|
| SESSION-1 ISessionStore contract | Load existing | `MongoSessionStoreIntegrationTests.SaveThenLoad_RoundTrip_PreservesAllFields`, `SessionAwareHandlerTests.Rehydrates_PersistedState_BetweenUpdates`; contract `src/interfaces/ISessionStore.cs:7,14` | ✅ CONFIRMED |
| SESSION-1 | Load absent → fresh Idle v0 | `MongoSessionStoreIntegrationTests.LoadAbsentChat_ReturnsFreshIdleSession_WithChatIdSet`; `SessionStore.cs:21` | ✅ CONFIRMED |
| SESSION-2 MongoSessionStore on "Sessions" | Save/load round-trip | `MongoSessionStoreIntegrationTests.SaveThenLoad_RoundTrip_PreservesAllFields`; `CollectionName="Sessions"` `SessionStore.cs:5` | ✅ CONFIRMED |
| SESSION-3 Session document shape | Full round-trip all fields | `SaveThenLoad_RoundTrip_PreservesAllFields` (State/Role/UserId/GuestToken/PendingEmail); fields `ChatSession.cs:20-30`; UpdatedAt+Version on save `SessionStore.cs:27,33,46` | ✅ CONFIRMED |
| SESSION-4 TTL index 1h | Index created at startup | `MongoSessionStoreIntegrationTests.TtlIndex_OnUpdatedAt_Exists_WithOneHourExpiry` (expireAfterSeconds==3600); `MongoDbContext.cs:39-45` | ✅ CONFIRMED |
| SESSION-4 | Touch-on-write keeps chats alive | `FirstWrite_StoresVersionOne_AndTouchesUpdatedAt` (UpdatedAt != default); every save sets `UpdatedAt = UtcNow` `SessionStore.cs:27`; TTL-sweep survival not directly testable (60s Mongo cadence) | ✅ CONFIRMED |
| SESSION-5 CAS write | Matching version | `MatchingVersion_Save_Succeeds_AndIncrementsVersion` (V→V+1); `SessionStore.cs:45-51` | ✅ CONFIRMED |
| SESSION-5 | Stale version fails, doc unchanged | `StaleCas_InsertAndReplace_Fail_AndLeaveDocumentUnchanged`; dup-key → false `SessionStore.cs:39-42` | ✅ CONFIRMED |
| SESSION-6 SessionAwareHandler | Conflict triggers replay, last reply | `CasConflict_ReloadsAndReplays_UntilSaveSucceeds`, `CasConflict_ReplayReturnsLastReply`; `SessionAwareHandler.cs:48-58` | ✅ CONFIRMED |
| SESSION-6 | Gate serializes per chat | `SameChat_ConcurrentUpdates_AreSerialized_WithoutLostUpdates`, `TwoChats_ConcurrentRegisterAndGuestLogin_StayIndependent`; `SessionAwareHandler.cs:30-31` | ✅ CONFIRMED |
| SESSION-7 Pure handler | Deterministic pass, no state | 72 explicit-session `HandleAsync` sites in `BotAuthHandlerTests.cs`; no `_sessions`/`_gates`/`GetSession` in `BotAuthHandler.cs` (grep 0 hits outside wrapper); identical inputs → identical replies | ✅ CONFIRMED |
| SESSION-8 FakeSessionStore | Replay exercised in unit test | `CasConflict_ReloadsAndReplays_UntilSaveSucceeds` with `ConflictOnce=true`; `FakeSessionStore.cs:37-41,63-69` | ✅ CONFIRMED |
| WEBHOOK-1 ASP.NET hosting | Host binds configured port / default 8080 | `EnvConfigTests.Resolve_PortProvided_IsUsed` (9090), `Resolve_PortBlank_FallsBackToDefault`; `UseUrls($"http://0.0.0.0:{config.Port}")` `Program.cs:48`; FrameworkReference `Allergy_Bothelper.csproj:18`; apply live smoke booted on 8080 | ✅ CONFIRMED |
| WEBHOOK-2 POST /webhook deserialization | Valid update / malformed → 400 | `WebhookRequestHandlerTests.ValidMessage_Returns200_AndSendsReply`, `MalformedBody_Returns400`, `NullJson_Returns400`; `WebhookRequestHandler.cs:33-43` | ✅ CONFIRMED |
| WEBHOOK-3 Secret-token validation | Correct token / wrong or missing → 401 | `WrongSecret_Returns401_AndDoesNotDispatch`, `MissingSecret_Returns401`; `CryptographicOperations.FixedTimeEquals` `WebhookRequestHandler.cs:56-58` | ✅ CONFIRMED |
| WEBHOOK-4 Message/CallbackQuery dispatch | Message → text; Callback → AnswerCallbackQuery + data | `WebhookDispatcherTests.Message_ForwardsTextToHandler`, `Callback_AnswersCallback_ThenForwardsData`, `WebhookRequestHandlerTests.ValidCallback_Returns200_AnswersAndSendsReply`; `WebhookDispatcher.cs:26-36` | ✅ CONFIRMED |
| WEBHOOK-5 Reply sending and response | Reply with buttons → one SendMessage + 200 | `Reply_SendsOneMessageWithText`, `ReplyWithButtons_BuildsInlineKeyboardMarkup`; markup `TelegramMarkup.cs:9-17` | ✅ CONFIRMED |
| WEBHOOK-5 | Null reply → no send + 200 | `NullReply_SendsNothing`; `WebhookDispatcher.cs:38-41` | ✅ CONFIRMED |
| WEBHOOK-6 Startup registration | Successful registration (URL+secret+allowedUpdates) | `WebhookRegistrationServiceTests.StartAsync_CallsSetWebhookWithUrlAndSecret`, `TelegramWebhookRegistrar_RoutesThroughClient_SetWebhook`; `TelegramWebhookRegistrar.cs:19-23` | ✅ CONFIRMED |
| WEBHOOK-6 | Registration race tolerated (non-fatal) | `StartAsync_RegistrarFailure_IsNonFatal`; try/catch `WebhookRegistrationService.cs:24-31` | ✅ CONFIRMED |
| WEBHOOK-7 Polling removed | Single transport path | grep across `src/*.cs`: no `Telegram.Bot.Polling`/`GetUpdates`/`StartReceiving`/`Console.ReadLine`; only `WebhookRegistrationService.StartAsync` (IHostedService, WEBHOOK-6); `TelegramChannel.cs` deleted | ✅ CONFIRMED |
| WEBHOOK-8 /healthz | Health probe 200 | `MapGet("/healthz", () => Results.Ok("ok"))` `Program.cs:84`; apply live smoke → 200; no automated host-level test | ✅ CONFIRMED |
| CONFIG-1 Process env precedence | Process env wins / .env fills gaps | `EnvConfigTests.ProcessEnv_WinsOverDotEnvFile_AndFileFillsGaps` (asserts process wins + file-only value loaded); `NoClobberLoad` `clobberExistingVars:false` `EnvConfig.cs:32-35`; `Program.cs:17` | ✅ CONFIRMED |
| CONFIG-2 Required vars fail fast | All present / missing named | `Resolve_AllRequiredPresent_ReturnsConfig`, `Resolve_MissingVariable_ThrowsNamingIt`, `Resolve_MissingMultipleVariables_NamesThemAll`; `EnvConfig.cs:17-24,39-47` | ✅ CONFIRMED |
| CONFIG-3 Mongo wiring from env | Mongo configured | `MongoDbContext(config.MongoUri, config.MongoDatabase)` `Program.cs:34`; `Resolve` maps both vars; full integration suite passed against the env-derived DB | ✅ CONFIRMED |
| CONFIG-4 PORT binding | Explicit port / default port | `Resolve_PortProvided_IsUsed` (9090), `Resolve_PortBlank_FallsBackToDefault`, `Resolve_PortNotAnInteger_Throws`; `EnvConfig.cs:49-54`; `Program.cs:48` | ✅ CONFIRMED |
| CONFIG-5 Webhook configuration | URL/token flow to registration + validation | `StartAsync_CallsSetWebhookWithUrlAndSecret`; wiring `Program.cs:70-73`; token validation `WebhookRequestHandlerTests` (uses config secret) | ✅ CONFIRMED |
| CONFIG-6 .env.example | Template completeness / no secrets committed | `.env.example` (verified via `git show`, unchanged in working tree) lists all 5 required vars + commented `# PORT=8080` + preserved WHATSAPP vars, placeholders non-secret; `git grep` for real credentials → 0 hits; only `.env.example` tracked (`*.env` ignored) | ✅ CONFIRMED |

**Compliance summary**: 36/36 scenarios compliant (evidence: automated passing tests + source inspection; 3 scenarios backed by code path + documented apply live smoke instead of a committed host test).

## Correctness (Static Evidence)

| Item | Status | Notes |
|------|--------|-------|
| Pure `BotAuthHandler` (SESSION-7) | ✅ Implemented | `HandleAsync(chatId, session, text, callbackData, ct)`; no per-chat state/gates |
| `SessionAwareHandler` wrapper (SESSION-6) | ✅ Implemented | gate → load → snapshot → delegate → dirty-check save → CAS replay ≤3 |
| `MongoSessionStore` (SESSION-2/5) | ✅ Implemented | insert on `expectedVersion==0`, dup-key → false; else versioned `ReplaceOneAsync`, success = `ModifiedCount==1` |
| TTL index (SESSION-4) | ✅ Implemented | `Sessions.UpdatedAt` ascending, `ExpireAfter=1h`, created in `EnsureIndexesAsync` |
| Secret compare (WEBHOOK-3) | ✅ Implemented | `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes |
| Webhook dispatch (WEBHOOK-4/5) | ✅ Implemented | Callback → answer + handler(data); Message → handler(text); non-null reply → one SendMessage |
| Polling removal (WEBHOOK-7) | ✅ Implemented | `TelegramChannel.cs` deleted; no polling symbols remain |
| Config resolution (CONFIG-1/2/4) | ✅ Implemented | non-clobber `.env`, fail-fast naming missing vars, PORT optional default 8080 |

## Coherence (Design)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| D1 PORT optional default 8080 | ✅ Yes | `EnvConfig.DefaultPort=8080`; required set excludes PORT |
| D2 Dirty check in wrapper | ✅ Yes | `IsDirty` over the 5 mutable fields; skip save when unchanged |
| D3 Replay ≤3, last reply | ✅ Yes | `MaxAttempts=3`; reload only while attempts remain; returns last reply |
| D4 First write insert, else CAS replace | ✅ Yes | `expectedVersion==0` → `InsertOneAsync`; else versioned `ReplaceOneAsync` |
| D5 Registrar seam + non-fatal | ✅ Yes | `IWebhookRegistrar` + `WebhookRegistrationService : IHostedService` try/catch |
| D6 Single session-taking handler; session arg ignored by wrapper | ✅ Yes | Decorator swaps transparently; `new ChatSession()` placeholder in dispatcher |
| D7 `FixedTimeEquals` secret compare | ✅ Yes | Constant-time UTF-8 comparison |
| D8 `.env.example` required set + WHATSAPP vars + commented PORT | ✅ Yes | Verified via git history |

## Runtime Checks (read-only, code lines)

| Check | Result | Evidence |
|-------|--------|----------|
| Env precedence: process env > .env non-clobber | ok | `Program.cs:17` `DotNetEnv.Env.Load(".env", EnvConfig.NoClobberLoad)`; `EnvConfig.cs:32-35` (`clobberExistingVars:false`); test `ProcessEnv_WinsOverDotEnvFile_AndFileFillsGaps` |
| `/healthz` | ok | `Program.cs:84` `MapGet("/healthz", () => Results.Ok("ok"))`; apply smoke → 200 |
| `/webhook` 401 path | ok | `Program.cs:80-81` reads `X-Telegram-Bot-Api-Secret-Token` → `ProcessAsync` returns 401 on missing/wrong token `WebhookRequestHandler.cs:25-28` (no dispatch); unit tests `WrongSecret_...`, `MissingSecret_...` |
| `/webhook` 400 path | ok | `WebhookRequestHandler.cs:35-38` (JsonException) and `40-43` (null body) → 400; unit tests `MalformedBody_Returns400`, `NullJson_Returns400` |
| TTL index registration | ok | `MongoDbContext.cs:39-45` `ExpireAfter=TimeSpan.FromHours(1)` on `Sessions.UpdatedAt`; integration test asserts `expireAfterSeconds==3600` |
| CAS insert path | ok | `SessionStore.cs:29-43`: `expectedVersion==0` → `InsertOneAsync` (Version=1), dup-key `MongoWriteException` → `false` |
| CAS replace path | ok | `SessionStore.cs:45-51`: filter `ChatId & Version==expected`, replacement Version=expected+1, success = `ModifiedCount==1` |
| Dirty-check skip | ok | `SessionAwareHandler.cs:43-46` returns without save when `IsDirty` false; `IsDirty` compares 5 fields `:81-86`; test `NonMutating_StartAndHelp_AreLoadedButNotSaved` asserts `SaveCalls==0` |
| Replay ≤3 | ok | `SessionAwareHandler.cs:16` `MaxAttempts=3`; loop `:37`; reload only when `attempt < MaxAttempts-1` `:55-58`; test `PersistentCasFailure_BoundedToThreeAttempts` asserts 3 loads / 3 saves |

## Findings

**CRITICAL**: None
**WARNING**: None
**SUGGESTION**:
1. **Apply-progress commit hashes do not resolve** — `apply-progress.md` lists `fd459ef6`, `e123a25d`, `2f931bc6`, `cf5029b6`; none pass `git rev-parse --verify`. Real prefixes: `fd459ef1`, `e123a25a`, `2f931bcb`, `cf5029b8`. Evidence: `git log --format=%H`. Fix: correct the abbreviations or use full SHAs so the evidence trail is reproducible. (No code impact.)
2. **No automated host-level test** — host binding (WEBHOOK-1), `/healthz` (WEBHOOK-8), and the `/webhook`→HTTP-status mapping (`Program.cs:78-84`) are covered by unit tests + code inspection + apply live smoke, but no committed test boots the `WebApplication`. Fix: add an integration test that boots the host on a free port and asserts `/healthz` → 200, bad-token `/webhook` → 401, malformed body → 400.
3. **Per-chat gates never evicted** — `SessionAwareHandler._gates` (`ConcurrentDictionary<long, SemaphoreSlim>`) grows with distinct chat ids for process lifetime. Per-instance only (Cloud Run), chats TTL out after ~1h, and the semaphore is lightweight, so risk is low. Fix (optional): remove the gate after an idle window.
4. **Null-forgiving on callback message** — `WebhookDispatcher.cs:28` `query.Message!.Chat.Id` would NRE on a `CallbackQuery` without a `Message`. Telegram always supplies the originating message for inline-keyboard callbacks, so this is safe in practice. Fix (optional): guard `query.Message is not null` and skip/answer without dispatch otherwise.

## Real Evidence

- **Build**: `dotnet build Allergy_Bothelper.csproj --nologo -v minimal` → exit 0, `0 Warning(s), 0 Error(s)`.
- **Unit suite REAL counts**: `133 passed / 11 skipped / 0 failed / 144 total` (no `RUN_MONGO_TESTS`).
- **Integration suite REAL counts**: `144 passed / 0 skipped / 0 failed / 144 total` with `RUN_MONGO_TESTS=1` against live MongoDB on `localhost:27017`.
- Output hashes (SHA-256): build `65635F7F...A247`, unit `66F9706D...3D08`, integration `DFFE9E51...3C2F` (integration hash also recorded as `evidence_revision`).
- Polling removal: grep across `src/*.cs` for `Telegram.Bot.Polling|GetUpdates|StartReceiving|Console.ReadLine` → 0 hits (the only `StartAsync` is the IHostedService method, which is WEBHOOK-6, not polling).

## Deviations

None. Implementation matches the delta specs and design D1–D8. Task list shows 25/25 complete; the four commits exist with the correct scopes (minor: abbreviated hashes in `apply-progress.md` differ from real prefixes — see SUGGESTION 1). Working tree left as-is: `readme.md` modified (orchestrator-requested, untouched), `.atl/` and `openspec/` untracked (expected).

## Recommendation

**archive** — change is complete, verified, and archive-ready. All 22 requirements and 36 scenarios are evidenced; no blockers, no critical or warning findings. The four SUGGESTIONs can be tracked as follow-up work or PR-comment notes; none gate archiving. Include the rollout notes from `apply-progress.md` (Cloud Run `min-instances=1`, HTTPS tunnel for local dev, additive `Sessions` collection) in the PR description.
