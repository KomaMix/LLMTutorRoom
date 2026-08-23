# Документация проекта

Документы рассчитаны на разработчика, который впервые открыл репозиторий и
хочет понять границы сервисов до чтения реализации.

Рекомендуемый порядок:

1. [Архитектура системы](architecture.md) — владельцы данных, связи и два
   сквозных события.
2. [AuthService](auth-service.md) — пользователи, роли и JWT.
3. [TeachingService](teaching-service.md) — каталог, задания и версии тестов.
4. [AttemptService](attempt-service.md) — попытки, таймер, ответы и отправка.
5. [LLMTutorRoom](llm-tutor-room.md) — stateless web-фасад и React-приложение.
6. [ReviewService](review-service.md) — создание и выполнение проверок.
7. [LLMGateway](llm-gateway.md) — модели, deployment-ы и вызов provider-а.

Дополнительные технические документы:

- [Frontend LLMTutorRoom](../LLMTutorRoom/ClientApp/README.md)
- [Конфигурация nginx](../infra/nginx/README.md)

Эти файлы описывают текущее состояние кода. При изменении владельца данных,
межсервисного контракта или основного бизнесового сценария документацию нужно
обновлять в том же изменении.
