# Разработка, запуск и проверка

Версия редактора: **Unity 6000.5.7f1**, источник — [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt). Пакеты: [manifest](../Packages/manifest.json) и [lock](../Packages/packages-lock.json). Проект открывается из корня с Assets, Packages и ProjectSettings.

## Запуск

Единственная игровая сцена: [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Меню **Paper Defense → Game → Open player game**, затем Play. Конфигурация: PlayerBalance → PlayerRules → PlayerContent. `CreateConfigurationIfMissing()` повторно назначает связи каталога даже при существующих assets; не используйте его как безвредное чтение.

Windows x64: **Paper Defense → Game → Build player game**. Выход: `Builds/PlayerGame/PaperDefense.exe`, рядом build-report.txt. Для запуска без личного профиля передайте `-paper-memory-only`. Новая сборка в рамках аудита не выполнялась.

## Структура

| Каталог | Назначение |
|---|---|
| Assets/PaperDefense/Core | Правила и симуляция без Unity API |
| Application | Контроллер, профиль, конфигурация, IO |
| Presentation | Ввод, мастерская, оболочка, HUD, отрисовка |
| Editor и Validation | Импорт, сборка, редакторские сценарии |
| Tests | EditMode, Editor, PlayMode |
| Configuration | Основной баланс и отдельные сравнительные пресеты |
| ArtSource/Notebook/NewStyle | Исходные игровые PNG, нарезка, анимация |
| Tools/AlbumDesign2026 | Новый баланс, Excel и проверка галереи |
| docs/design/game-concept-art-2026-10-16 | Полная актуальная галерея: 30 экранов и 3 листа объектов, не runtime UI |
| docs/design/album-assembly-2026-10-17 | Дополнительный интерактивный прототип и листы объектов |

## Работа над переходом

Начинайте с [плана](superpowers/plans/2026-10-18-modular-album-implementation.md) и [аудита](audit/2026-10-17-album/report.md). Не меняйте число слотов одной константой: загрузчик v7 требует точного размера массива. Сначала добавить типизированные рецепты и миграцию, затем UI. Новые значения из Excel не являются уже действующими параметрами Unity.

Для графики: **Paper Defense → Art → Rebuild blue pen atlas**. Входы — sprites.source.json и animation.json в NewStyle; результат — NotebookAtlas.png, NotebookSprites.json и NotebookVisualTheme.asset. Сохраняйте .meta. [Стыки](Modular-Asset-Contract.md) обязательны для новых частей; визуальный коллаж не доказывает совместимость при вращении.

## Проверки

В Unity Test Runner запускайте EditMode и PlayMode отдельно. Сборки: `PaperDefense.Core.Tests`, `PaperDefense.Editor.Tests`, `PaperDefense.Integration.Tests`. Тесты сцен могут открывать другие сцены; сначала сохраните собственные изменения.

Подбирайте набор по изменению: миграция — сериализация и повреждённые профили; резолвер — категории, совместимость, формулы и порядок частей; бой — цикл выстрела, цели и сохранённое состояние; UI — 8 слотов, управление и ошибки записи. Финальный этап требует полного набора тестов и Windows build.

Эскизы и Excel воспроизводятся командами из [Tools/AlbumDesign2026/README](../Tools/AlbumDesign2026/README.md). Это самостоятельная проверка материалов, не тест Unity. Python-инструменты: `python -m unittest discover -s Tools/tests -p 'test_*.py'`. Перед сдачей изменений: `git diff --check`.

Ручной маршрут: меню → карта → подготовка → мастерская → альбом → бой → пауза → результат → повторное открытие. Для нового UI проверить 1024×768, 1280×720, 1280×960, 1600×900, 1920×1080, длинные подписи и клавиатурный фокус. Профилирование выполнять в фиксированных условиях устройства через Unity Profiler; результаты unit-тестов не являются FPS.

Каждый отчёт должен называть версию Unity, конфигурацию, seed, выполненные команды, результаты и ограничения. [История проверок](validation/README.md) сохраняет даты; старый успех не подтверждает новую реализацию.
