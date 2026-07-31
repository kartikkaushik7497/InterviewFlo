# InterviewFlo

InterviewFlo is a Windows desktop AI interview platform built with:
- .NET 10
- Avalonia UI
- MongoDB Atlas with in-memory fallback
- Local Whisper transcription with Windows speech fallback
- Optional OpenAI, Gemini, Azure Speech, and ElevenLabs integrations

The source code still uses the legacy `MockUpAi` namespace and project folder names, but the product/demo branding is InterviewFlo.

## Implemented Product Features

### Dual actor architecture
- Admin and candidate flows with separate screens and navigation.

### Candidate interview flow
- Login with admin-generated credentials.
- Mandatory first-login password reset when enabled.
- Camera and microphone device selection before interview start.
- Rules page, lobby, live camera preview, answer recording, transcription preview, retry, and editable final answer.
- Per-question scoring and final result view.

### Admin control center
- Create, edit, activate, deactivate, expire, reset, and delete candidates.
- Configure role, description, category, difficulty, passing score, AI provider, and interviewer voice per candidate.
- Search, filter, sort, CSV export, PDF export, and security overview tools.
- Optional encrypted local vault for paid provider keys.

### AI and voice behavior
- Heuristic interview generation, follow-up logic, scoring, and feedback work without paid keys.
- Local Whisper is the default transcription engine when no paid speech provider is configured.
- Windows speech recognition is kept as a fallback.
- OpenAI, Gemini, Azure Speech, and ElevenLabs can be enabled locally when keys are available.

### Security and resilience
- Password policy enforcement.
- Candidate password reset enforcement.
- MongoDB startup health probe with temporary in-memory fallback.
- Global telemetry hooks for unhandled exceptions.
- Local secret overrides through `appsettings.Local.json` or environment variables.

## Project Structure

- `src/MockUpAi.App` -> Avalonia UI, view models, navigation, camera/mic capture, local transcription, voice playback, telemetry.
- `src/MockUpAi.Core` -> domain entities, DTOs, enums, service contracts.
- `src/MockUpAi.Infrastructure` -> Mongo persistence, authentication, admin services, AI routing, reports, external API clients.

## Configuration

Keep checked-in `appsettings.json` safe for source control. Put real local secrets in `src/MockUpAi.App/appsettings.Local.json`, using `appsettings.Local.example.json` as a template.

Useful settings:
- `MongoDb:ConnectionString`
- `MongoDb:DatabaseName`
- `SeedAdmin:UserId`
- `SeedAdmin:Password`
- `WhisperLocal:Enabled`
- `WhisperLocal:Model`
- `OpenAi:Enabled`
- `OpenAi:ApiKey`
- `Gemini:Enabled`
- `Gemini:ApiKey`
- `AzureSpeech:Enabled`
- `AzureSpeech:ApiKey`
- `AzureSpeech:Region`

Environment variables are also supported:
- `MongoDb__ConnectionString`
- `OPENAI_API_KEY`
- `AZURE_SPEECH_KEY`
- `MOCKUPAI_MASTER_SECRET`
- `MOCKUPAI_SEED_ADMIN_PASSWORD`

## Run

```powershell
cd C:\Users\karti\OneDrive\Desktop\mock
dotnet restore
dotnet test
dotnet run --project src\MockUpAi.App\MockUpAi.App.csproj
```

## Windows Publish

Create a self-contained Windows build:

```powershell
.\eng\publish-windows.ps1
```

The packaged output is written to `artifacts\InterviewFlo-win-x64`. Keep `appsettings.Local.json` on the target machine for real MongoDB and provider secrets.

## Default Admin

- User ID: `admin`
- Password: set `SeedAdmin:Password` in `appsettings.Local.json` or set `MOCKUPAI_SEED_ADMIN_PASSWORD`.

## Notes

- If MongoDB is not configured or unreachable, the app runs with temporary in-memory storage.
- For persistent users/interviews, configure MongoDB locally.
- The default setup does not require paid OpenAI transcription.
- The first Whisper run can take longer because the selected local model may need to download.
- Run `dotnet list .\MockUpAi.sln package --vulnerable --include-transitive` before final demo packaging.
