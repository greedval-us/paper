# Документация проекта

Точка входа — [README проекта](../README.md). Здесь собраны действующие руководства. Числа берутся из назначенного пресета: игровая сцена использует PlayerRules, техническая — Target. Эти настройки нельзя считать взаимозаменяемыми.

## Играть и проверять механику

| Документ | Содержание |
|---|---|
| [Игровая оболочка](Player-Game.md) | Меню, карта, подготовка, управление боем, пауза и результат |
| [Действующие механики](Current-Mechanics.md) | Реестр реализованных систем, ресурсы, оружие, враги и ограничения |
| [Оборона тетради](Tower-Defense.md) | Карта, волны, строительство, поддержка и баланс пресетов |
| [Мастерская](Workshop-Guide.md) | Детали, альбом, открытия, набор чертежей и полигон |
| [Пошаговая мастерская](Guided-Workshop.md) | Вариант A: четыре шага, подсказки, подробности и расположение всех редакторов |
| [Редактор конструкций](Drawing-Workbench.md) | Инструменты рисования, заготовки, предпросмотр и сохранение |
| [Свободная форма](Freeform-Towers.md) | Как геометрия корпуса и стволов влияет на характеристики |
| [Модульные башни](Modular-Towers.md) | Основания, крепления, оборудование, подача и формы мортиры |
| [Движение толпы](Crowd-Movement.md) | Общие маршруты, локальное движение, очереди и проходы |
| [Общий экран стендов](Mechanics-Hub.md) | Навигация и фильтры 27 сцен |
| [Сцены механик](Mechanics-Test-Scenes.md) | Назначение каждого стенда, шаг симуляции и изоляция профиля |
| [Тетрадное оформление](Notebook-Art.md) | Векторные ресурсы, анимация, эффекты, импорт и камера |
| [Ассеты игровой оболочки](Player-Assets.md) | Галерея, фоны, значки и просмотр нового оформления |

## Разрабатывать и менять данные

| Документ | Содержание |
|---|---|
| [Разработка и проверки](Development-Guide.md) | Окружение, структура репозитория, запуск, сборка и тесты |
| [Архитектура](PaperDefense-Architecture.md) | Слои, жизненный цикл, снимки данных, время и отрисовка |
| [Контракты механик](Mechanics-Contracts.md) | Границы правил и developer configuration |
| [Каталог контента](Content-Architecture.md) | Базы, пресеты, overrides и проверка данных |
| [Словарь баланса](Balance-Parameters.md) | Единицы, формулы, источники и потребители параметров |
| [Совместимость профиля](Content-Profile-Compatibility.md) | Версии, миграция, неизвестные ID, ошибки и атомарная запись |
| [Расширение механик](Extending-Mechanics.md) | Добавление деталей, эффектов и поведения |
| [Инструменты проекта](../Tools/README.md) | Меню Unity, текстовые ресурсы, CLI, нагрузка и экспорт |
| [Книга баланса](../Tools/BalanceWorkbook/README.md) | Экспорт Historical/Target в JSON и Excel; PlayerRules в книгу не включён |
| [Исходники оформления](../ArtSource/Notebook/README.md) | Генерация канонического JSON и семи SVG-листов |

## Покрытие реализованных подсистем

Это карта ответственности документации, а не отчёт об успешном запуске тестов. Пути ведут к реализации; тестовые группы перечислены в [руководстве разработчика](Development-Guide.md).

| Подсистема и источник | Где описана |
|---|---|
| [Игровые экраны](../Assets/PaperDefense/Presentation/Player/PlayerGameRoot.cs), подготовка, пауза, улучшения, результат | Player-Game |
| [Мастерская, команды и полигон](../Assets/PaperDefense/Application/RunController.cs), постоянные открытия и альбом | Workshop-Guide, Content-Profile-Compatibility |
| [Редактор линий](../Assets/PaperDefense/Core/DrawingEditSession.cs), [геометрия](../Assets/PaperDefense/Core/FreeformTowerDesign.cs), заготовки | Drawing-Workbench, Freeform-Towers |
| [Состав башни](../Assets/PaperDefense/Core/TowerBlueprint.cs), крепления, оборудование, цены и подача | Modular-Towers, Balance-Parameters |
| [Строительство](../Assets/PaperDefense/Core/GameSession.Construction.cs), [смола и ремонт](../Assets/PaperDefense/Core/GameSession.Support.cs) | Current-Mechanics, Tower-Defense |
| [Оружие](../Assets/PaperDefense/Core/WeaponSystem.cs), [мортирные формы](../Assets/PaperDefense/Core/MortarFootprint.cs), цвет и статусы | Current-Mechanics, Modular-Towers, Mechanics-Contracts |
| [Враги](../Assets/PaperDefense/Core/GameSession.Enemies.cs), [карта](../Assets/PaperDefense/Core/BattlefieldDefinition.cs), [толпа](../Assets/PaperDefense/Core/EnemyCrowdMotion.cs) | Tower-Defense, Crowd-Movement |
| [Чернильницы](../Assets/PaperDefense/Core/GameSession.Objectives.cs), [кляксы](../Assets/PaperDefense/Core/CorpseBlotSystem.cs), [экономика](../Assets/PaperDefense/Core/GameSession.Progression.cs) | Current-Mechanics, Mechanics-Contracts |
| [Контент](../Assets/PaperDefense/Application/ContentDatabaseAsset.cs), пресеты и валидация | Content-Architecture, Extending-Mechanics |
| [Профиль v7](../Assets/PaperDefense/Core/WorkshopProfile.cs), [IO](../Assets/PaperDefense/Application/WorkshopProfileStore.cs), повтор записи | Content-Profile-Compatibility, PaperDefense-Architecture |
| [Фиксированный шаг](../Assets/PaperDefense/Application/FixedStepClock.cs), [жесты ввода](../Assets/PaperDefense/Application/ConstructionGesture.cs), read model | PaperDefense-Architecture, Mechanics-Contracts |
| [Рендерер поля](../Assets/PaperDefense/Presentation/Rendering/NotebookWorldView.cs), анимация, эффекты, камера и повтор полигона | Notebook-Art, PaperDefense-Architecture |
| [Галерея игрока](../Assets/PaperDefense/Presentation/Art/PlayerAssetStand.cs), экспериментальные панели | Player-Assets |
| [Сцены механик](../Assets/PaperDefense/Validation/MechanicsTestScene.cs), fixtures, давление 3000 и числовые прогоны | Mechanics-Test-Scenes, Development-Guide, Tools/README |
| [Русские тексты](../Assets/PaperDefense/Core/UserText.cs), [генератор](../Tools/Extract-UserText.py), отдельные тексты оболочки | Tools/README |

## История и границы актуальности

- [docs/validation](validation/README.md) — датированные результаты проверок, снимки и измерения. Успех старого прогона относится только к зафиксированному там срезу.
- `docs/superpowers/specs` и `docs/superpowers/plans` — проектные решения и планы реализации. Незавершённый пункт плана не доказывает отсутствие функции, а описание будущего поведения не доказывает её наличие.
- Остальные отчёты в `docs/superpowers` сохраняют историю ревью и рефакторинга.
- `docs/balance` — экспортированные данные и книга баланса; свежесть проверяет генератор, источник истины остаётся в коде и ассетах Unity.

При добавлении системы обновляйте тематическое руководство и строку покрытия здесь; при изменении запуска — также корневой README. Исторические отчёты сохраняйте с датой, новые результаты записывайте отдельно. Сверка документации не является новым прогоном Unity Test Runner или измерением FPS.
