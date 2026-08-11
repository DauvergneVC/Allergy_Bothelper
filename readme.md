# Allergy bot helper

Este proyecto esta pensado en dar una herramienta "simple" para poder consultar las alergias de una persona.
la idea es simplemente utilziar un bot de Whatsapp/Telegram, de momento telegram, donde pueda dar las alergias de la persona y con eso se pueda preguntar sobre cuales son o enviar una imagen sobre los componentes de un producto para saber si puede afectar de alguna manera.

## Deciciones

- **MongoDB**: Para poder utilizar archivos que pertenezcan a una persona, asi como poder almacenar imagenes y analisarlas.
- **C#**: Esta decicion es simplemente por gusto, podria haebr utilizado Python y habria sido mas sencillo, pero tube la necesidad de practicar trabajar con clases y un lenguaje estructurado como C#.
- **Arquitectura por capas**: me fui por una arquitectura simple, adaptandola para ser bien sencilla dado el caso.

## Arquitectura de despliegue (plan)

- **Cloud Run (GCP), serverless multi-instance**: el bot corre como una imagen de contenedor única; las instancias escalan elásticamente y no tienen almacenamiento local persistente. El `docker-compose` local queda solo para desarrollo (MongoDB); Cloud Run no ejecuta compose.
- **Telegram por webhook**: en vez de long-polling, el bot expone un endpoint HTTPS que recibe los updates. Implica validar el secreto del webhook y responder rápido; los cold starts se absorben con `min-instances=1`.
- **Sesiones por chat en MongoDB**: los flujos conversacionales se persisten por `chatId` con TTL, para que un update pueda caer en cualquier instancia sin perder el paso actual. Hoy están en memoria y se pierden al reiniciar; este cambio es requisito del multi-instance.
- **MongoDB en producción**: Atlas free tier (M0); el driver `MongoDB.Driver` no cambia.
- **Configuración**: env vars / Secret Manager en producción; `.env` solo local.
- **OCR**: Google Vision API (cuota gratuita) — sin sidecar Python en Cloud Run.
- **Traducción ES↔EN**: Google Cloud Translation API como fallback del matcher de alergias — sin Argos local.
- **Empaquetado**: Dockerfile multi-stage (.NET 10 SDK → runtime), la app escucha en `$PORT`, usuario no-root.

## Bot commands

The implemented surface is conversational authentication. Each command walks you through a step-by-step flow, and `/start` shows a menu with buttons.

### Start

- `/start` — shows the main menu with **Login** and **Register** buttons.

### Authentication

- `/login` — owner login (email + password) or guest login (share token).
  1. Enter your email or a share token.
  2. If you entered an email, enter your password to finish.
- `/register` — creates a new owner account.
  1. Enter a new email.
  2. Enter a password to finish.
- `/logout` — clears the session for this chat.
- `/cancel` — aborts the current step and returns to the idle menu.

### Token sharing (owner only)

- `/share` — generates a new share token. Anyone holding it can log in as a guest and view the owner's allergies read-only. A new token invalidates the previous one.
- `/revoke` — revokes the current token, so guests can no longer log in with it.

Only the owner can generate or revoke tokens; guests log in read-only with a token.

### Session and storage

- Chat sessions are **in-memory only** and reset on restart: active login, role, and current step are lost. (Target: persisted in MongoDB with TTL so flows survive across Cloud Run instances — see Deployment architecture.)
- Share tokens persist in MongoDB until they are revoked or regenerated.

### Allergy management

- `/add <ingredients>` — adds allergens for the chat owner. Accepts a single item or a comma-separated list (`/add maní, trigo`), items separated by newlines, semicolons, bullets, or numbered markers. Items are canonicalized, so duplicates and synonyms are stored once (adding `cacahuete` after `maní` keeps a single `peanut` entry). The reply echoes what was added. Only the owner can add: logged-out chats get a log-in prompt and guests get an owner-only message. `/remove` and `/listar` are planned follow-ups and are not implemented yet.

### Ingredient consultation

Any non-command message is treated as an ingredient scan against the owner's allergies — there is no command to run. Send `ingredientes: ...`, `mira esta salsa ...`, `check this ...`, or just a list, and the bot replies with the allergens detected (and the triggering ingredients) or a "safe" verdict, in the language of the message.

### Photo scan

- Send a product photo with an `/add` caption and the OCR'd ingredients are added to your allergens.
- Send a photo without a caption and the OCR'd ingredients are consulted against your allergens.
- A photo with any other caption text combines both: the caption is your message and the OCR text is appended before consulting.

OCR runs in `stub` mode by default (it returns fixed text, enough for local development and tests). Set `OCR_MODE=google` to use Google Cloud Vision — locally that also needs `GOOGLE_APPLICATION_CREDENTIALS` pointing to a service-account key; see `.env.example`.

## Estructura

```
    src/
    ├─ Program.cs → arranca el host, registra DI
    ├─ Models/ → solo entidades (User)
    ├─ Data/
    │ ├─ MongoDbContext.cs → conexión (único punto que conoce Mongo)
    │ └─ Repositories/
    │   ├─ IUserRepository.cs
    │   ├─ UserRepository.cs
    ├─ Services/
    │ ├─ AuthService.cs → lógica de login/register/autorización
    │ ├─ AllergyService.cs → Add/Remove/Listar
    │ └─ ShareService.cs → generar/revocar tokens
    ├─ Commands/ → comandos del bot (usan Services)
    └─ Channels/ → adapters Telegram/WhatsApp
```