# MockUpAi

MockUpAi is a cross-platform desktop AI interview platform built with:
- .NET 10
- Avalonia UI
- MongoDB Atlas (with resilient fallback)
- OpenAI interview + transcription integration

## Implemented Product Features

### 1. Dual actor architecture
- **Admin** and **Candidate** have separate flows and separate UI.

### 2. Candidate interview flow
- Login with admin-generated credentials.
- Mandatory password reset flow (when flagged).
- Real permission checks for:
  - microphone device access
  - camera device access
  - transcription key readiness
- Rules page and timed lobby.
- Live interview with:
  - role-based AI question generation
  - real microphone recording
  - speech-to-text transcription via OpenAI
  - live camera preview
  - per-question AI scoring
- Final interview result card with score + verdict.

### 3. Admin control center
- Create candidate credentials with role + job description.
- Candidate lifecycle management:
  - activate/deactivate
  - expire account by date
  - reset candidate password
  - delete candidate
  - edit role and description
- Dashboard with search/filter/sort and selected candidate actions.
- Export reports:
  - CSV
  - PDF
- Security tools:
  - admin password rotation
  - secure OpenAI API key save in encrypted local vault.

### 4. AI scoring quality
- OpenAI rubric-driven evaluation with weighted dimensions:
  - technical correctness
  - role relevance
  - depth/reasoning
  - communication clarity
- Role-fit score calculation from full interview evidence.
- Automatic heuristic fallback if OpenAI is unavailable.

### 5. Security and resilience
- Password policy enforcement (length + upper/lower/digit/special).
- Candidate first-login password reset enforcement.
- Encrypted key vault storage for OpenAI API keys.
- Mongo retry strategy for transient faults.
- Mongo health probe on startup with in-memory fallback if unreachable.
- App telemetry logging with global exception capture.

## Project Structure

- `src/MockUpAi.App` -> Avalonia UI, navigation, media capture, telemetry
- `src/MockUpAi.Core` -> domain models, DTOs, contracts
- `src/MockUpAi.Infrastructure` -> Mongo persistence, AI services, security, exports

## Configuration

Edit `src/MockUpAi.App/appsettings.json`:

- `MongoDb:ConnectionString`
- `MongoDb:DatabaseName`
- `OpenAi:ApiKey` (optional)
- `OpenAi:Model`
- `OpenAi:TranscriptionModel`
- `SeedAdmin:UserId`
- `SeedAdmin:Password`

You can also set:
- `OPENAI_API_KEY` environment variable
- `MOCKUPAI_MASTER_SECRET` environment variable (for stronger local vault encryption key derivation)

## Run

```powershell
cd C:\Users\karti\OneDrive\Desktop\mock
dotnet restore
dotnet run --project src\MockUpAi.App\MockUpAi.App.csproj
```

## Default Admin

- User ID: `admin`
- Password: `Admin@123`

## Notes

- If MongoDB is not configured or unreachable, the app runs with in-memory repositories.
- If OpenAI key is unavailable, interview generation/evaluation falls back to heuristic mode.
- Speech transcription requires an available OpenAI key.
