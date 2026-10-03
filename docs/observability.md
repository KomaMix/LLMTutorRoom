# Наблюдаемость

Все шесть серверных процессов отправляют логи, метрики и распределённые трассы
по OTLP. Общая конфигурация находится в `Shared.Observability`, поэтому набор
инструментации и resource-атрибутов одинаков для всех сервисов.

## Что попадает в телеметрию

- входящие ASP.NET Core запросы, кроме `/health/*` в трассах;
- исходящие HTTP-запросы между сервисами и к LLM provider-ам;
- операции Npgsql;
- публикация и получение RabbitMQ-сообщений с переносом W3C trace context;
- вызовы LLM через `Microsoft.Extensions.AI` с GenAI span-ами и метриками;
- runtime, ASP.NET Core и HttpClient метрики;
- структурированные `ILogger`-логи с `TraceId` и `SpanId`;
- прикладные метрики outbox, задержки доставки RabbitMQ и обработки review job.

Тексты LLM-запросов и ответов в телеметрию не записываются. Для
`Microsoft.Extensions.AI` параметр `EnableSensitiveData` явно выключен.

## Локальный просмотр

В `docker-compose.yml` и `docker-compose.infra.yml` добавлен standalone Aspire
Dashboard. После запуска Compose интерфейс доступен по адресу
`http://localhost:18888`. Данные сохраняются в volume
`aspire-dashboard-data` и восстанавливаются после перезапуска контейнера.

Полный Compose передаёт телеметрию по gRPC на
`http://aspire-dashboard:18889`. При локальном запуске сервисов в Development
используется стандартный OTLP gRPC endpoint `http://localhost:4317`, который
опубликован контейнером dashboard.

Анонимный доступ включён только для локального development-стека. Такой режим
не следует переносить в окружение, доступное извне.

## Конфигурация экспортёра

В Development OTLP exporter включается автоматически. В другом окружении он
включается, когда задан `OTEL_EXPORTER_OTLP_ENDPOINT`. Остальные стандартные
переменные OpenTelemetry, включая `OTEL_EXPORTER_OTLP_PROTOCOL` и
`OTEL_EXPORTER_OTLP_HEADERS`, обрабатываются SDK.

Пример для внешнего collector-а:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://otel.example.test:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

## Сквозная трассировка outbox

HTTP Activity завершается раньше, чем фоновый publisher читает outbox. Поэтому
`AttemptSubmissionOutboxMessages` и `IntegrationOutboxMessages` сохраняют
`TraceParent` и `TraceState` вместе с событием. Publisher восстанавливает
родительский контекст, создаёт outbox span, а RabbitMQ instrumentation переносит
его в заголовках сообщения. В результате в одной трассе видны исходный HTTP
запрос, запись БД, отложенная публикация, доставка сообщения, обработка review и
вызов LLM.

Baggage в outbox не сохраняется, чтобы случайные пользовательские данные не
попадали в базу телеметрии. Изменены начальные миграции тестового проекта, поэтому
существующие development-базы `AttemptService` и `TeachingService` нужно
пересоздать.

## Прикладные метрики

| Метрика | Смысл | Основные теги |
| --- | --- | --- |
| `llmtutorroom.outbox.publish.attempts` | попытки публикации outbox | `outbox.name`, `outcome` |
| `llmtutorroom.outbox.publish.delay` | время от создания события до попытки публикации | `outbox.name`, `outcome` |
| `llmtutorroom.messaging.delivery.delay` | возраст RabbitMQ-сообщения при получении | `messaging.destination.name` |
| `llmtutorroom.review.jobs` | обработанные review job | `outcome` |
| `llmtutorroom.review.job.duration` | длительность обработки review job | `outcome` |

В метриках нет идентификаторов попыток, проверок, пользователей или сообщений.
Эти значения доступны в конкретных трассах и логах, но не создают
высококардинальные временные ряды.
