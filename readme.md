# Allergy Bot Helper

Bot de Telegram para gestionar alergias alimentarias. Permite a los usuarios registrar sus alergenos y consultar rápidamente si un producto o ingrediente es seguro, mediante comandos de texto o escaneo de fotos con OCR.

## Características

- **Gestión de alergias**: registra, lista y elimina alergenos con comandos simples (`/add`, `/listar`, `/remove`)
- **Consulta de ingredientes**: envía una lista de ingredientes (texto o foto) y el bot te dice si contiene alergenos
- **OCR con Google Vision**: escanea fotos de productos para extraer ingredientes automáticamente
- **Sesiones persistentes**: las conversaciones se guardan en MongoDB, sobreviven a reinicios
- **Compartir acceso**: genera tokens para que otros usuarios consulten tus alergias (solo lectura)
- **Bilingüe ES/EN**: detecta automáticamente el idioma de tus mensajes

## Instalación

### Requisitos

- .NET 10 SDK
- MongoDB (local o Atlas)
- (Opcional) Docker para contenerización
- (Opcional) Cuenta de Google Cloud para OCR real

### Desarrollo local

1. **Clona el repositorio**:
   ```bash
   git clone <repo-url>
   cd Allergy_BotHelper
   ```

2. **Configura las variables de entorno**:
   ```bash
   cp .env.example .env
   # Edita .env con tus valores
   ```

3. **Inicia MongoDB** (con Docker):
   ```bash
   docker run -d -p 27017:27017 \
     -e MONGO_INITDB_ROOT_USERNAME=root \
     -e MONGO_INITDB_ROOT_PASSWORD=password \
     -e MONGO_INITDB_DATABASE=Allergy_helper_db \
     --name mongodb mongo:7
   ```

4. **Ejecuta el bot**:
   ```bash
   dotnet run
   ```

### Docker

```bash
docker build -t allergy-bot .
docker run -d -p 8080:8080 --env-file .env allergy-bot
```

### Cloud Run (GCP)

```bash
# Build y push a Artifact Registry
gcloud builds submit --tag gcr.io/PROJECT_ID/allergy-bot
gcloud run deploy allergy-bot \
  --image gcr.io/PROJECT_ID/allergy-bot \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars TELEGRAM_API_KEY=...,WEBHOOK_URL=...,WEBHOOK_SECRET_TOKEN=...,MONGO_URI=...
```

## Configuración

Variables de entorno (ver `.env.example`):

| Variable | Descripción | Default |
|----------|-------------|---------|
| `TELEGRAM_API_KEY` | Token del bot de Telegram (obtenido de @BotFather) | Requerido |
| `WEBHOOK_URL` | URL pública del webhook (ej: `https://tu-dominio.com/webhook`) | Requerido |
| `WEBHOOK_SECRET_TOKEN` | Token secreto para validar requests de Telegram | Requerido |
| `MONGO_URI` | Connection string de MongoDB | `mongodb://localhost:27017` |
| `MONGO_INITDB_DATABASE` | Nombre de la base de datos | `Allergy_helper_db` |
| `PORT` | Puerto HTTP (Cloud Run inyecta esto automáticamente) | `8080` |
| `OCR_MODE` | Modo OCR: `stub` (texto fijo) o `google` (Google Vision) | `stub` |
| `GOOGLE_APPLICATION_CREDENTIALS` | Path al archivo JSON de credenciales de GCP (solo para `OCR_MODE=google`) | Opcional |

## Uso

### Comandos del bot

#### Autenticación
- `/start` — muestra el menú principal
- `/login` — inicia sesión como owner (email + password) o guest (token compartido)
- `/register` — crea una cuenta de owner
- `/logout` — cierra la sesión del chat actual
- `/cancel` — cancela el paso actual

#### Gestión de alergias (solo owner)
- `/add <ingredientes>` — agrega alergenos. Acepta un item o lista separada por comas:
  ```
  /add maní
  /add maní, trigo, leche
  ```
  Los sinónimos se canonicalizan (ej: `cacahuete` → `peanut`), duplicados se ignoran.

- `/listar` — muestra tus alergenos registrados con sus nombres canónicos:
  ```
  Tus alergias:
  • maní (peanut)
  • trigo (gluten)
  ```

- `/remove <ingredientes>` — elimina alergenos:
  ```
  /remove maní
  /remove maní, trigo
  ```

#### Compartir acceso (solo owner)
- `/share` — genera un token para que otros usuarios consulten tus alergias (solo lectura)
- `/revoke` — revoca el token actual

#### Consulta de ingredientes
- **Texto**: envía cualquier mensaje sin comando con ingredientes:
  ```
  ingredientes: leche, huevo, harina
  ```
  El bot responde si detecta alergenos o si es seguro.

- **Foto**: envía una foto de un producto:
  - Sin caption → consulta los ingredientes detectados
  - Con caption `/add` → agrega los ingredientes detectados a tus alergias
  - Con otro caption → combina tu texto + OCR para consultar

### Ejemplos

**Registrar alergias**:
```
Usuario: /add maní, trigo, leche
Bot: Alergias agregadas: maní, trigo, leche
```

**Consultar un producto**:
```
Usuario: ingredientes: leche, azúcar, cacao
Bot: Alérgeno detectado: leche (leche)
```

**Escanear una foto**:
```
[Envía foto de un producto]
Bot: Alérgeno detectado: maní (cacahuete, peanut)
```

## Arquitectura

### Stack técnico
- **Lenguaje**: C# / .NET 10
- **Framework web**: ASP.NET Core Minimal API
- **Base de datos**: MongoDB (sesiones, usuarios, tokens)
- **OCR**: Google Cloud Vision API (opcional, stub por defecto)
- **Bot**: Telegram.Bot library
- **Deploy**: Docker + Cloud Run (serverless)

### Estructura del proyecto
```
src/
├─ Program.cs              → Entry point, DI, webhook endpoints
├─ Models/                 → Entidades (User, ChatSession)
├─ Data/
│  ├─ MongoDbContext.cs    → Conexión MongoDB
│  └─ Repositories/       → IUserRepository, UserRepository
├─ Services/
│  ├─ AuthService.cs       → Login/register/autorización
│  ├─ AllergyService.cs    → Gestión de alergenos
│  ├─ ShareService.cs      → Tokens de compartir
│  └─ OcrService.cs        → OCR (stub/Google Vision)
├─ Bots/
│  ├─ BotAuthHandler.cs    → Lógica de comandos
│  ├─ SessionAwareHandler.cs → Decorador de sesiones
│  └─ BotCopy.cs           → Strings bilingües
└─ Webhook/
   └─ WebhookDispatcher.cs → Routing de updates de Telegram
```

### Decisiones de diseño
- **MongoDB**: persistencia de sesiones, usuarios y tokens. Permite escalar a múltiples instancias.
- **Webhook vs long-polling**: el bot expone un endpoint HTTPS (`/webhook`) en vez de hacer polling. Más eficiente para Cloud Run.
- **Sesiones por chat**: cada chat tiene su propia sesión persistente, permite flujos conversacionales complejos.
- **Canonicalización de alergenos**: un vocabulario mapea sinónimos a claves canónicas (ej: `maní`, `cacahuete`, `peanut` → `peanut`).
- **OCR pluggable**: `OCR_MODE=stub` para desarrollo (sin credenciales), `OCR_MODE=google` para producción.

## Desarrollo

### Tests
```bash
# Unit tests
dotnet test

# Integration tests (requiere MongoDB corriendo)
RUN_MONGO_TESTS=1 dotnet test
```

### Estructura de tests
```
tests/
├─ Unit/                   → Tests de servicios y lógica
├─ Integration/            → Tests con MongoDB real
└─ Fakes/                  → Mocks y fakes para tests
```

## Licencia
