# Mechanics Boundaries Implementation Plan

**Goal:** Рефакторинг независимых механик без изменения текущего режима по умолчанию, затем Excel по runtime-балансу.

**Architecture:** Developer config в Core; Application адаптирует Inspector; immutable availability фиксируется на старте. Потребители используют отдельные возможности, а общие статусы и базовые фазы остаются независимыми.

**Tech Stack:** Unity 6000.5.7f1, C#, NUnit EditMode/PlayMode, artifact-tool XLSX.

**Spec:** `docs/superpowers/specs/2026-10-12-mechanics-boundaries-design.md`

## Constraints

- Никакого выбора механик игроком и hot-toggle во время партии.
- Не менять формат профиля, устойчивые ID и текущие числовые настройки.
- Сохранить старые master gates и владение GameSession.Balance.
- Excel экспортирует эффективные настройки после Target-преобразования.

## Tasks

- [x] Аудит механик/баланса и полный исходный EditMode-прогон.
- [x] Добавить MechanicsConfiguration / MechanicsAvailability, adapter и read model; добавить тесты отключений.
- [x] Наблюдать ожидаемые failures для неподключённых выключателей.
- [x] Подключить границы спавна, клякс, целей, цвета, качества и развития; разделить смешанные этапы.
- [x] Полный EditMode/PlayMode, независимый обзор и исправления: 608 + 222, ошибок 0.
- [x] Экспортировать runtime JSON; создать и проверить Excel: 8 листов, 387 параметров/пресет, 104 расчёта, ошибок 0.
- [x] Обновить архитектуру/гайд расширения и verification report.

## Review focus

- Отключение клякс не выключает обычное замедление оружия.
- Отключение улучшений не оставляет PendingUpgrade и остановку волн.
- При отсутствии чернильниц полигон всё ещё допускает свои явно заданные цвета.
- Копирование и Target сохраняют developer configuration.
- Представление UI не сообщает о доступности отключённых команд.
