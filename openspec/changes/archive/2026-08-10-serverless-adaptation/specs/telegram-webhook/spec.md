# Telegram Webhook Specification

## Purpose

Replaces Telegram long-polling with an ASP.NET Core minimal API that receives Telegram updates via webhook, validates them, delegates to the bot handler, and returns fast 2xx responses — the transport shape Cloud Run expects.

## Requirements

### Requirement: WEBHOOK-1: ASP.NET Core hosting

The project MUST reference the `Microsoft.AspNetCore.App` framework and host a web application binding `http://0.0.0.0:{PORT}` with `PORT` defaulting to `8080`.

#### Scenario: Host binds configured port
- GIVEN `PORT` is set
- WHEN the app starts
- THEN it listens on `http://0.0.0.0:{PORT}`.

#### Scenario: Default port
- GIVEN `PORT` is unset
- WHEN the app starts
- THEN it listens on `http://0.0.0.0:8080`.

### Requirement: WEBHOOK-2: POST /webhook deserialization

The system MUST accept `POST /webhook`, deserialize the body into a Telegram `Update` using Telegram's JSON options, and return `400` for an unreadable payload.

#### Scenario: Valid update
- GIVEN a well-formed Telegram update JSON
- WHEN POSTed to `/webhook`
- THEN the update is processed.

#### Scenario: Malformed payload
- GIVEN an unparseable body
- WHEN POSTed to `/webhook`
- THEN a `400` response is returned.

### Requirement: WEBHOOK-3: Secret-token validation

The endpoint MUST require header `X-Telegram-Bot-Api-Secret-Token` to match `WEBHOOK_SECRET_TOKEN` using constant-time comparison, returning `401` on mismatch without processing.

#### Scenario: Correct token
- GIVEN the header equals `WEBHOOK_SECRET_TOKEN`
- WHEN a request arrives
- THEN the update is processed.

#### Scenario: Wrong or missing token
- GIVEN the header is absent or different
- WHEN a request arrives
- THEN `401` is returned
- AND no handler dispatch occurs.

### Requirement: WEBHOOK-4: Message and CallbackQuery dispatch

A `Message` update MUST invoke the handler with its text; a `CallbackQuery` update MUST first call `AnswerCallbackQuery` and then invoke the handler with `callbackData`.

#### Scenario: Message update
- GIVEN an update containing a Message
- WHEN processed
- THEN the handler runs with the message text and no callback data.

#### Scenario: Callback query update
- GIVEN an update containing a CallbackQuery
- WHEN processed
- THEN `AnswerCallbackQuery` is called
- AND the handler runs with the callback data.

### Requirement: WEBHOOK-5: Reply sending and response

A non-null `BotReply` MUST produce one `SendMessage` preserving the existing button-to-markup logic; after processing, the endpoint MUST return `200`.

#### Scenario: Reply with buttons
- GIVEN the handler returns a reply with buttons
- WHEN processed
- THEN one `SendMessage` with the markup is sent
- AND the endpoint returns 200.

#### Scenario: Null reply
- GIVEN the handler returns null
- WHEN processed
- THEN no `SendMessage` occurs
- AND the endpoint returns 200.

### Requirement: WEBHOOK-6: Startup webhook registration

At startup the system MUST call `SetWebhook(WEBHOOK_URL, secretToken, allowedUpdates: Message and CallbackQuery)`; registration MUST be idempotent and non-fatal if another instance already set the webhook.

#### Scenario: Successful registration
- GIVEN the app starts
- WHEN registration runs
- THEN Telegram is configured with the webhook URL, secret token, and the allowed updates.

#### Scenario: Registration race tolerated
- GIVEN another instance already set the webhook
- WHEN startup registration fails
- THEN the app still starts and serves requests.

### Requirement: WEBHOOK-7: Polling removed

The system MUST remove the polling code path, including `TelegramChannel.StartAsync` and its blocking `Console.ReadLine`; webhook MUST be the only update delivery path.

#### Scenario: Single transport path
- GIVEN the codebase after the change
- WHEN searching for polling usage
- THEN no polling start or blocking read remains.

### Requirement: WEBHOOK-8: Health endpoint

The system MUST expose `GET /healthz` returning `200` for startup probes.

#### Scenario: Health probe
- GIVEN the app is running
- WHEN `GET /healthz` is requested
- THEN a 200 response is returned.
