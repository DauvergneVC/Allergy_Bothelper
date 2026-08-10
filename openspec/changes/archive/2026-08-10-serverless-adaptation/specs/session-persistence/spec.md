# Session Persistence Specification

## Purpose

Persists per-chat bot sessions in MongoDB so auth flows survive process restarts and multi-instance Cloud Run routing. A pure handler plus a session-aware wrapper isolates business logic from storage; compare-and-swap (CAS) with full replay guarantees no transition is lost and no stale reply is returned.

## Requirements

### Requirement: SESSION-1: ISessionStore contract

The system MUST expose `ISessionStore` with `LoadAsync(chatId)` returning the persisted session (or a fresh `ChatSession` in `Idle` state, version 0, when absent) and `SaveAsync(chatId, session, expectedVersion)` returning whether the write succeeded.

#### Scenario: Load existing session
- GIVEN a session was previously saved for a chat
- WHEN `LoadAsync(chatId)` is called
- THEN the stored session fields and version are returned.

#### Scenario: Load absent session
- GIVEN no session exists for the chat
- WHEN `LoadAsync(chatId)` is called
- THEN a fresh `ChatSession` in `Idle` state with version 0 is returned.

### Requirement: SESSION-2: MongoSessionStore on "Sessions"

The system MUST provide a `MongoSessionStore` implementing `ISessionStore` that stores one document per chat in the `Sessions` collection, keyed by `ChatId`.

#### Scenario: Save then load round-trip
- GIVEN a `MongoSessionStore` connected to MongoDB
- WHEN a session is saved and loaded by the same `chatId`
- THEN the loaded session matches the saved fields.

### Requirement: SESSION-3: Session document shape

A session document MUST contain `ChatId`, the session fields (`State`, `Role`, `UserId`, `GuestToken`, `PendingEmail`), `UpdatedAt` refreshed on every save, and `Version` incremented on every save.

#### Scenario: Full round-trip
- GIVEN a session with non-default state, role, user id, guest token and pending email
- WHEN saved then reloaded
- THEN every field is preserved exactly.

### Requirement: SESSION-4: TTL index, 1-hour soft bound

The system MUST create a TTL index on `UpdatedAt` with a 1-hour `ExpireAfter` for `Sessions`, and MUST refresh `UpdatedAt` on every save (touch-on-write).

#### Scenario: Index created at startup
- GIVEN `EnsureIndexesAsync` has run
- THEN a 1-hour TTL index on `Sessions.UpdatedAt` exists.

#### Scenario: Touch-on-write keeps active chats alive
- GIVEN a chat saved, then saved again within the hour
- WHEN the TTL sweep runs
- THEN the session remains because `UpdatedAt` was refreshed.

### Requirement: SESSION-5: Compare-and-swap write

`SaveAsync` MUST replace via a filter on `ChatId` plus `Version == expectedVersion`, store `Version + 1`, and report success only when exactly one document was modified.

#### Scenario: Matching version
- GIVEN a session stored at version V
- WHEN saved with expected version V
- THEN the write succeeds
- AND the stored version becomes V+1.

#### Scenario: Stale version
- GIVEN the stored session is now at version V+1
- WHEN saved with expected version V
- THEN the write fails
- AND the stored document is unchanged.

### Requirement: SESSION-6: SessionAwareHandler wrapper

The system MUST wrap the pure handler in `SessionAwareHandler` which, per chat: acquires an in-process gate, loads the session, delegates to the pure handler, saves with the version read at load, and on CAS conflict reloads and replays the full handler pass, returning the reply of the last successful pass.

#### Scenario: Conflict triggers replay
- GIVEN a concurrent update advanced the version between load and save
- WHEN the save fails CAS
- THEN the handler pass replays against the reloaded session
- AND the successful pass's reply is returned, never a stale reply.

#### Scenario: Gate serializes per chat
- GIVEN two simultaneous updates for one chat
- WHEN both reach the wrapper
- THEN they run one after another, each seeing the prior result.

### Requirement: SESSION-7: Pure handler

`IBotAuthHandler.HandleAsync` MUST take the session as a parameter and MUST NOT keep per-chat session or gate state; identical session plus input MUST yield an identical reply and mutation.

#### Scenario: Deterministic pass
- GIVEN a session and an input
- WHEN handled twice with identical inputs
- THEN both passes produce identical replies and session state.

### Requirement: SESSION-8: FakeSessionStore in tests

Tests MUST provide an in-memory `FakeSessionStore` implementing `ISessionStore` with CAS emulation and configurable conflict injection.

#### Scenario: Unit test exercises replay
- GIVEN a `FakeSessionStore` seeded with conflict injection enabled
- WHEN the wrapper saves and CAS fails
- THEN the test observes the replay and the final reply.
