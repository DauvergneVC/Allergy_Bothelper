# Config and Environment Specification

## Purpose

Defines how the bot resolves configuration in serverless deployments: process environment overrides `.env`, required variables fail fast and clearly at startup, and secrets never live in source control.

## Requirements

### Requirement: CONFIG-1: Process env precedence over .env

The system MUST read configuration from the process environment first; `.env` MUST be loaded with non-overwrite semantics so it only fills variables not already set.

#### Scenario: Process env wins
- GIVEN a variable set both in the process environment and in `.env`
- WHEN configuration is resolved
- THEN the process environment value is used.

#### Scenario: .env fills gaps
- GIVEN a variable set only in `.env`
- WHEN configuration is resolved
- THEN the `.env` value is used.

### Requirement: CONFIG-2: Required variables at startup

The system MUST resolve `TELEGRAM_API_KEY`, `MONGO_URI`, `MONGO_INITDB_DATABASE`, `PORT`, `WEBHOOK_URL`, and `WEBHOOK_SECRET_TOKEN` at startup, failing with a clear error naming any missing variable (`PORT` defaults to `8080` when unset).

#### Scenario: All variables present
- GIVEN every required variable is set
- WHEN the app starts
- THEN startup proceeds.

#### Scenario: Missing variable
- GIVEN a required variable is unset
- WHEN the app starts
- THEN startup fails with an error naming the variable.

### Requirement: CONFIG-3: Mongo wiring from environment

The system MUST derive the MongoDB client and database from `MONGO_URI` and `MONGO_INITDB_DATABASE`.

#### Scenario: Mongo configured
- GIVEN `MONGO_URI` and `MONGO_INITDB_DATABASE` are set
- WHEN the app starts
- THEN the Mongo context targets that URI and database.

### Requirement: CONFIG-4: PORT binding

The system MUST bind the web host using `PORT` and SHOULD default to `8080` when `PORT` is unset.

#### Scenario: Explicit port
- GIVEN `PORT=9090`
- WHEN the host starts
- THEN it binds port 9090.

#### Scenario: Default port
- GIVEN `PORT` unset
- WHEN the host starts
- THEN it binds port 8080.

### Requirement: CONFIG-5: Webhook configuration

The system MUST use `WEBHOOK_URL` for `SetWebhook` and `WEBHOOK_SECRET_TOKEN` for both `SetWebhook` and webhook request validation.

#### Scenario: Values flow to webhook
- GIVEN both variables set
- WHEN the app starts and a webhook request arrives
- THEN registration uses the URL and token
- AND request validation matches the token.

### Requirement: CONFIG-6: .env.example documents variables

`.env.example` MUST document every required variable with a placeholder and MUST NOT contain real secrets.

#### Scenario: Template completeness
- GIVEN `.env.example`
- THEN every required variable appears with a non-secret placeholder.

#### Scenario: No secrets in source
- GIVEN the repository
- THEN no real tokens, passwords, or URIs are committed.
