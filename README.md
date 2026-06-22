# LLMGateway

Локальный ASP.NET Core gateway для отправки чат-запросов к настроенным языковым моделям.

`Model` хранит стабильный ключ, по которому вызывающие проекты обращаются к модели, например `mistral:7b`.
Одна модель может иметь несколько deployment-ов: локальный Ollama и удаленный OpenAI-совместимый API.
Gateway выбирает доступный deployment с наименьшим значением `priority`.

## Запуск

Настройте подключение к PostgreSQL через User Secrets, не добавляя пароль в Git:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=llm_infos;Username=postgres;Password=your-password" --project LLMGateway
dotnet ef database update --project LLMGateway
dotnet run --project LLMGateway --launch-profile http
```

Схема старой базы несовместима с текущей начальной миграцией. Для базы разработки,
созданной предыдущей версией проекта, удалите и создайте базу заново перед запуском
`database update`.

## API

Получить ключи доступных моделей:

```http
GET /api/models
```

```json
[
  "gpt-5.4-mini",
  "mistral-large-latest",
  "mistral:7b"
]
```

Создать логическую модель:

```http
POST /api/models
Content-Type: application/json

{
  "key": "mistral:7b",
  "displayName": "Mistral 7B",
  "description": "Mistral 7B с локальным и удаленным deployment-ами"
}
```

Добавить локальный deployment через Ollama:

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

Добавить ограничение запросов для deployment-а:

```http
POST /api/models/deployments/1/rate-limits
Content-Type: application/json

{
  "windowSeconds": 60,
  "maxRequests": 20
}
```

Отправить чат-запрос:

```http
POST /api/chat
Content-Type: application/json

{
  "model": "mistral:7b",
  "temperature": 0.7,
  "messages": [
    { "role": "system", "content": "Отвечай кратко." },
    { "role": "user", "content": "Кто ты?" }
  ]
}
```
