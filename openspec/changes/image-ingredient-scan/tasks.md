# Tasks: Image & Text Ingredient Scan for Allergies

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1400 (range 1200–1600) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | single PR only with `size:exception` (delivery strategy: single-pr) |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

Forecast exceeds the 800-line review budget; orchestrator must resolve `size:exception` or a chain strategy before apply.

### Suggested Work Units (commit boundaries — tests ship with code)

| Unit | Goal | Conventional commit | Focused test command | Runtime harness | Rollback boundary |
|------|------|---------------------|----------------------|-----------------|-------------------|
| U1 | Pure domain: normalizer, vocabulary, parser, matcher, reply language | `feat(allergy): add canonical vocabulary, normalizer, parser, and matcher` | `dotnet test --filter "FullyQualifiedName~TextNormalizer|Vocabulary|IngredientParser|IngredientMatcher|ReplyLanguage"` | N/A — pure logic, no I/O boundary | Delete the 5 new src files + their tests; nothing else references them |
| U2 | /add storage: AllergyService over IUserRepository + User.AllergyDisplay | `fix(allergy): implement AllergyService over IUserRepository` | `dotnet test --filter "FullyQualifiedName~Allergy"` (`RUN_MONGO_TESTS=1` for MongoFact) | N/A — repo layer exercised via handler fakes + MongoFact | Revert AllergyService/IAllergyService/User.cs hunks |
| U3 | /add command + commandless text consultation + ES/EN copy | `feat(bots): add /add command and commandless ingredient consultation` | `dotnet test --filter "FullyQualifiedName~BotAuthHandler"` | N/A — handler unit tests over fakes | Revert BotAuthHandler branches + BotCopy allergy strings |
| U4 | Photo seam: dispatcher download, OCR adapter, OCR_MODE | `feat(webhook): route photos through OCR adapter with OCR_MODE` | `dotnet test` (full suite) | N/A — stub OCR only; Google path requires creds, untested by design (OCR-4) | Revert dispatcher photo branch + OCR files; `OCR_MODE=stub` removes GCP dependency |
| U5 | Docs: readme `/add`, deferred commands, `.env.example` note | `docs(readme): document /add and defer /remove /listar` | N/A — docs only | N/A — docs | Revert readme/.env.example hunks |

## Untouched (zero diff)

AuthService, token lifecycle, BotCopy EXCEPT new ES/EN allergy strings, session-persistence specs/behavior, `SessionAwareHandler.IsDirty` (flows are stateless), Users collection indexes.

## Phase 1 — Slice 1: vocabulary + normalization + matcher (pure)

- [x] 1.1 Create `src/services/TextNormalizer.cs`: `Normalize`/`Tokenize` (case-fold, NFD accent-fold, punctuation-strip). Tests: ES accented pairs. REQ: ADD-4, CONSULT-5.
- [x] 1.2 Create `src/services/Vocabulary.cs`: static EN-key→ES/EN/Latin/brand synonyms + reverse normalized→canonical index; `Canonicalize(term)`. **ADD-4 fallback: unmapped item → stored under its own normalized form.** Tests: `maní`/`cacahuete`→`peanut`, `trigo`→gluten, unmapped fallback. REQ: ADD-4.
- [x] 1.3 Create `src/services/IngredientParser.cs`: `StripPrefix` (ES/EN phrases, accent/case-insensitive, trailing colon/newline stripped) + `SplitItems` (newlines, commas, semicolons, bullets, numbered). REQ: ADD-2, CONSULT-3, CONSULT-4.
- [x] 1.4 Create `src/services/IngredientMatcher.cs`: `AllergenMatch`/`MatchResult` records; canonical token containment. Tests: direct, containment, no-match, **CONSULT-8: unknown/unrecognized tokens still get a "no allergen match" reply (non-blocking)**. REQ: CONSULT-5, CONSULT-8.
- [x] 1.5 Create `src/bots/ReplyLanguage.cs`: `Detect(text)` — ES prefix or ES diacritic → ES, else EN; determinism test. REQ: ADD-6, CONSULT-6/7/9.
- **Gate G1**: slice-1 pure-domain unit tests green (`dotnet test`).

## Phase 2 — Slice 2: /add text + storage + reply copy

- [x] 2.1 Enabling fix: `src/services/AllergyService.cs` ctor → `IUserRepository` (latent DI bug); `src/interfaces/IAllergyService.cs` → `AddAsync(userId, canonical, display)` / `GetAllergiesAsync` returns canonical keys; `src/models/User.cs` additive `List<string>? AllergyDisplay` (index-aligned). REQ: ADD-5.
- [x] 2.2 Implement `AllergyService`: canonicalize via Vocabulary, dedupe (idempotent; synonym duplicates stored once), persist canonical + display via existing full-doc replace (no migration). MongoFact round-trip both fields + legacy-doc upgrade. REQ: ADD-4, ADD-5, ADD-9.
- [x] 2.3 `src/bots/BotCopy.cs`: add ES/EN strings only — add echo, usage, owner-only, log-in prompt, match/safe verdicts, OCR failure. REQ: ADD-6/7/8, CONSULT-6/7.
- [x] 2.4 `src/bots/BotAuthHandler.cs` `/add` branch: exact lowercase `/add` (case-sensitive); **ADD-7: bare `/add` (no argument, no photo) → usage instructions reply, nothing persisted**; **ADD-8 three-way role gating: Owner → proceed; Guest → owner-only message; None → log-in prompt**; parse list/single, persist, echo reply. Tests over `FakeUserRepository`. REQ: ADD-1, ADD-2, ADD-6, ADD-7, ADD-8, ADD-9.

## Phase 3 — Slice 3: commandless text consultation

- [x] 3.1 `BotAuthHandler` Idle non-command text: Owner/Guest → strip prefix → parse → match vs owner's canonical keys → ES/EN verdict (match list w/ offending tokens, or safe); replaces `UnknownCommand`; CONSULT-8 unknown tokens non-blocking at handler level; determinism test. REQ: CONSULT-1, CONSULT-3..8.
- [x] 3.2 **CONSULT-2: Role == None → log-in prompt, NEVER OCR (guard before any download/OCR)**; test asserts zero `FakeOcrService` invocations. REQ: CONSULT-2.
- **Gate G2** (after slices 2–3): all handler flows green.

## Phase 4 — Slice 4: photo path + OCR

- [ ] 4.1 Handler seam: `IBotAuthHandler.HandleAsync` (declared in `src/bots/BotAuthHandler.cs`) gains optional `byte[]? photoBytes = null` **after `ct`**; `SessionAwareHandler` passes through (IsDirty unchanged); existing positional call sites compile unchanged. REQ: WEBHOOK-9.
- [ ] 4.2 Create `src/interfaces/IOcrService.cs` (`Task<string?> RecognizeAsync(byte[], CancellationToken)`; `null` = no text), `src/services/StubOcrService.cs` (canned, deterministic, credential-free), `src/services/OcrFailureException.cs`, `src/services/GoogleVisionOcrService.cs` (`DetectDocumentTextAsync`, ADC, lazy `ImageAnnotatorClient.CreateAsync()`, gRPC/network errors → typed failure); `Allergy_Bothelper.csproj` adds `Google.Cloud.Vision.V1`. REQ: OCR-1, OCR-2, OCR-3, OCR-5.
- [ ] 4.3 `src/config/EnvConfig.cs`: optional `OCR_MODE` (default `stub`, unknown → stub, NOT required); `src/Program.cs` DI selects impl from `OCR_MODE`; `.env.example` documents `OCR_MODE` + `GOOGLE_APPLICATION_CREDENTIALS` (optional, local-only, no secrets). EnvConfig tests. REQ: OCR-4, CONFIG-6, CONFIG-7, CONFIG-8.
- [ ] 4.4 `src/webhook/WebhookDispatcher.cs` photo branch: pick largest `PhotoSize` ≤ 20 MB, `DownloadFile` → bytes; tri-state (`null` no-photo/tolerated failure, empty = oversize/rejected, non-empty = OCR input); download failure → handler still invoked, endpoint 200. Extend `FakeTelegramBotClient.DownloadFile` (canned bytes) + `RecordingBotHandler` (records bytes). REQ: WEBHOOK-4, WEBHOOK-9, OCR-6.
- [ ] 4.5 `BotAuthHandler` photo flows: caption `/add` → OCR → same parser → persist + echo; non-command/no-caption photo → OCR → consult verdict; oversize (empty bytes) → friendly failure without Vision call; `OcrFailureException` → friendly ES/EN reply, webhook never crashes; CONSULT-2 guard applies before OCR. REQ: ADD-3, CONSULT-4, CONSULT-9, OCR-5, OCR-6.
- **Gate G3**: full suite + MongoFact integration green; zero GCP-dependent tests.

## Phase 5 — Rollout / Documentation

- [ ] 5.1 `readme.md`: document `/add` (lowercase, owner-only, text/list/photo); replace `/Add /Remove /Listar` wording — note `/remove` and `/listar` as deferred follow-ups (resolves gatekeeper naming ambiguity). REQ: proposal risk row 5.
