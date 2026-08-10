# Proposal: Serverless Adaptation

## Intent
In-memory sessions, long-polling, `.env` config - all break under Cloud Run multi-instance. Implements steps 1-3 of the `readme.md` plan: sessions -> MongoDB, polling -> webhook, production config.

## Scope
### In Scope
- **Sessions (1A)**: `ISessionStore` + `MongoSessionStore` (`Sessions`, TTL, CAS); `BotAuthHandler` pure; `SessionAwareHandler` wrapper (gate, load/save, replay).
- **Webhook (2A)**: ASP.NET FrameworkReference; `POST /webhook`; secret-token validation; startup `SetWebhook`; `PORT` binding; polling removed.
- **Config**: process env > `.env`; required vars; `.env.example` update.
- **Tests**: `FakeSessionStore`; `MongoFact` integration; ~40 `GetSession` sites.

### Out of Scope
Non-goals: OCR/Vision, translation, Dockerfile, allergy matching. Untouched: `AuthService`, token lifecycle, `BotCopy`, `/help`, `AllergyService`, `Users`+indexes, `readme.md`, env naming.

## Key Decisions
| Decision | Choice | Tradeoff |
|---|---|---|
| Ownership | 1A pure handler + wrapper | one path; churn accepted |
| Cross-instance | version CAS + replay | gate per-instance only |
| TTL | 1h soft | +/-1 min sweep; loss = restart today |
| Transport | 2A minimal API | DI + native secret-token |
| Env naming | keep `TELEGRAM_API_KEY` | zero churn |
| Cold start | `min-instances=1` knob | deploy config, not code |
| Order | sessions -> config -> webhook | validate Mongo/CAS first |

## Approach
Web host + DI; store + wrapper; handler refactor; `/webhook`; `DotNetEnv.Env.Load()` (non-overwrite) + env resolution. Exploration: 1A + 2A, keep `TELEGRAM_API_KEY`.

## Capabilities
### New Capabilities
- `session-persistence`: persistence (TTL + CAS), wrapper, replay.
- `telegram-webhook`: endpoint, secret validation, registration, polling removal.
- `config-env`: precedence + required-variable resolution.
**Modified**: None (no `openspec/specs/`).

## Affected Areas
| Area | Impact |
|---|---|
| `src/Program.cs`, `src/bots/BotAuthHandler.cs`, `src/bots/ChatSession.cs` | Modified |
| `src/interfaces/ISessionStore.cs`, `src/data/SessionStore.cs` | New |
| `src/channels/TelegramChannel.cs`, `src/data/MongoDbContext.cs`, `Allergy_BotHelper.csproj`, `tests/`, `.env.example` | Modified |

## Risks
| Risk | Likelihood | Mitigation |
|---|---|---|
| Concurrent chat updates across instances | Med | CAS + replay |
| Framework ref breaks build | Low | `dotnet build` + suite |
| `GetSession` churn (~40 sites) | High | mechanical; suite gates webhook work |
| Single-pr 800-line budget | Med | tasks forecasts |

## Rollback Plan
Revert branch - additive only, no migration (`Sessions` new); polling restore = revert `Program.cs`/`TelegramChannel`.

## Dependencies
Cloud Run + HTTPS `WEBHOOK_URL`; Mongo Atlas URI (driver unchanged); tunnel for local dev.

## Success Criteria
- [ ] Sessions persist: Mongo load/save, TTL index, CAS++, replay - unit + `MongoFact`.
- [ ] `/webhook`: 401 on secret mismatch, 200 after reply; delegates Message/CallbackQuery; polling gone.
- [ ] Env precedence process-env > `.env`; required vars at startup.
- [ ] Full suite green; no untouched-file changes (`git diff`).

## Open Questions
None - exploration settled all forks.
