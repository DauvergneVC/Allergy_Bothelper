# Allergy bot helper

Este proyecto esta pensado en dar una herramienta "simple" para poder consultar las alergias de una persona.
la idea es simplemente utilziar un bot de Whatsapp/Telegram, de momento telegram, donde pueda dar las alergias de la persona y con eso se pueda preguntar sobre cuales son o enviar una imagen sobre los componentes de un producto para saber si puede afectar de alguna manera.

## Deciciones

- **MongoDB**: Para poder utilizar archivos que pertenezcan a una persona, asi como poder almacenar imagenes y analisarlas.
- **C#**: Esta decicion es simplemente por gusto, podria haebr utilizado Python y habria sido mas sencillo, pero tube la necesidad de practicar trabajar con clases y un lenguaje estructurado como C#.
- **Arquitectura por capas**: me fui por una arquitectura simple, adaptandola para ser bien sencilla dado el caso.

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

- Chat sessions are **in-memory only** and reset on restart: active login, role, and current step are lost.
- Share tokens persist in MongoDB until they are revoked or regenerated.

### Allergy management

Para manejar las alergias:

- /Add -> Añadir alergia mediante texto, lista o fotografia. Solo owner.
- /Remove -> Quitar alergia mediante texto o lista. Solo owner.
- /Listar -> Listar las alergias. Usable por cualquiera.

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