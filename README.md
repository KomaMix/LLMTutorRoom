# LLMGateway

Local ASP.NET Core gateway for chat requests to a configured language model.

`Model` stores the stable key used by callers, for example `mistral:7b`.
One model can have multiple deployments, such as a local Ollama instance and a
remote OpenAI-compatible API. Deployments are selected by ascending `priority`.

## Setup

Configure the local PostgreSQL connection without putting its password in Git:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=llm_infos;Username=postgres;Password=your-password" --project LLMGateway
dotnet ef database update --project LLMGateway
dotnet run --project LLMGateway --launch-profile http
```

The old database schema is incompatible with the new initial migration. For a
development database created by the prior version, drop and recreate the
database before running `database update`.

## API

Create the logical model:

```http
POST /api/models
Content-Type: application/json

{
  "key": "mistral:7b",
  "displayName": "mistral-7b-local",
  "description": "Mistral 7B deployment group"
}
```

Add a local Ollama deployment:

```http
POST /api/models/mistral:7b/deployments
Content-Type: application/json

{
  "providerType": "Ollama",
  "endpoint": "http://localhost:11434",
  "providerModelId": "mistral:7b",
  "priority": 0,
  "maxConcurrentRequests": 1
}
```

Add a rate-limit rule for a deployment:

```http
POST /api/models/deployments/1/rate-limits
Content-Type: application/json

{
  "windowSeconds": 60,
  "maxRequests": 20
}
```

Send a chat request:

```http
POST /api/chat
Content-Type: application/json

{
  "model": "mistral:7b",
  "temperature": 0.7,
  "messages": [
    { "role": "system", "content": "Answer briefly." },
    { "role": "user", "content": "Who are you?" }
  ]
}
```
