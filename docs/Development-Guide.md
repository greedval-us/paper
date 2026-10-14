# Разработка, запуск и проверки

[Указатель документации](README.md) · [Архитектура](PaperDefense-Architecture.md) · [Инструменты](../Tools/README.md)

## Окружение

Проект открывается из корня, содержащего `Assets`, `Packages` и `ProjectSettings`. Версия редактора зафиксирована в [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt): **6000.5.7f1**. Для основной сборки нужен модуль Windows Build Support установленного редактора.

Зависимости заданы в [manifest.json](../Packages/manifest.json), разрешённые версии — в [packages-lock.json](../Packages/packages-lock.json). Среди используемых пакетов: URP 17.6.0, Input System 1.20.0, uGUI 2.5.0 и Test Framework 1.7.0. В manifest также есть Unity 2D, AI, Timeline и Git-пакет `com.coplaydev.unity-mcp`; первый импорт требует доступных источников пакетов. Наличие пакета само по себе не означает реализованную игровую функцию.

Python 3 нужен только для инструментов текста и арт-исходников; они используют стандартную библиотеку. PowerShell запускает вспомогательные `.ps1`. Генератор Excel использует bundled Node.js и `@oai/artifact-tool`; требования описаны [отдельно](../Tools/BalanceWorkbook/README.md). Эти инструменты не нужны для обычного запуска игры в Unity.

## Структура репозитория

| Каталог | Назначение |
|---|---|
| `Assets/PaperDefense/Core` | Правила, модели, геометрия и симуляция без Unity API |
| `Assets/PaperDefense/Application` | Контроллер, снимки конфигурации, профиль и IO |
| `Assets/PaperDefense/Presentation` | Ввод, игрок, мастерская, HUD, отрисовка и галереи |
| `Assets/PaperDefense/Validation` | Сохранённые стенды, fixtures, сценарии нагрузки и баланса |
| `Assets/PaperDefense/Editor` | Меню, создание сцен, импорт, экспорт и сборка |
| `Assets/PaperDefense/Tests` | EditMode, редакторские и PlayMode-проверки |
| `Assets/PaperDefense/Configuration` | Игровые, Target/Historical и отдельные тестовые настройки |
| `Assets/PaperDefense/Scenes`, `Prefabs`, `Resources`, `Art` | Сцены, prefab, загружаемые ресурсы и контактные SVG |
| `Assets/PaperDefense/Content/Acceptance` | Отдельный контент для проверки добавления данных без изменения кода |
| `ArtSource/Notebook` | Канонический JSON оформления, генераторы и проверки |
| `Tools` | Скрипты текста, баланса и измерений |
| `docs` | Руководства, экспорт баланса, планы и датированные доказательства |
| `Builds`, `Library`, `Temp`, `Logs`, `UserSettings` | Локальные сборки, кэш и временные результаты; исключены из Git |

Сгенерированные `.csproj` не являются источником списка зависимостей. Границы сборок задаются `.asmdef`, пакеты — manifest/lock. Файлы `.meta` Unity сохраняются вместе со своими ассетами.

## Точки входа и конфигурации

| Вход | Назначение и профиль |
|---|---|
| [Game/PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity) | Игровой маршрут; PlayerBalance → PlayerRules → PlayerContent; обычный профиль |
| [PaperDefense.unity](../Assets/PaperDefense/Scenes/PaperDefense.unity) | Полная техническая мастерская через NotebookGame prefab; TargetNotebookBalance → Target |
| [MechanicsHub.unity](../Assets/PaperDefense/Scenes/MechanicsHub.unity) | Навигация по 27 стендам |
| [Scenes/Tests](../Assets/PaperDefense/Scenes/Tests) | 23 сцены механик и 4 художественные сцены; обычные стенды не читают личный профиль |

Открывайте стенды в режиме Single. `12_ProfilePersistence` использует отдельный временный каталог; просмотр игры из `27_PlayerAssets` — профиль в памяти. [Назначение сцен](Mechanics-Test-Scenes.md), [изоляция](Content-Profile-Compatibility.md).

`PlayerGameSceneTools.CreateIfMissing()` создаёт отсутствующие игровые ассеты/сцену; существующие числовые данные баланса не копируются повторно. При этом `CreateConfigurationIfMissing()` всегда заново назначает связи `catalog.presets`, `rules.database`, `balance.dataPreset` и сохраняет assets, даже если они уже существуют. При создании новой сцены метод назначает её единственной сценой Build Settings. `Build()` сам ничего не генерирует: он собирает указанный сохранённый файл сцены. Старые меню **Открыть игру** и **Game → Open player game** ведут в разные сцены.

## Сборка и запуск Player

В Unity выполните **Paper Defense → Game → Build player game**. Метод [PlayerGameSceneTools.Build](../Assets/PaperDefense/Editor/PlayerGameSceneTools.cs) использует только игровую сцену, `StandaloneWindows64` и `BuildOptions.None`. Результат — `Builds/PlayerGame/PaperDefense.exe`; итог сборки — соседний `build-report.txt` с результатом, ошибками, предупреждениями, размером и временем.

Обычный запуск использует профиль на диске. Пример из корня проекта для проверки с профилем в памяти:

```powershell
& '.\Builds\PlayerGame\PaperDefense.exe' -paper-memory-only
```

Для Player со стендами нужен отдельный набор сцен: hub и все 27 маршрутов каталога. Основной метод сборки их не включает. В редакторе hub загружает сцены по пути без изменения Build Settings.

## Автоматические проверки

В Unity Test Runner запускайте **EditMode** и **PlayMode** отдельно. Тесты редакторских ассетов находятся на вкладке EditMode вместе с тестами ядра. Перед проверками сохранённых сцен сохраните собственные несохранённые изменения: тесты могут открывать другие сцены.

| Сборка / файлы | Что покрывают |
|---|---|
| `PaperDefense.Core.Tests` в `Tests/EditMode` | Строительство, экономика, оружие, враги, путь/толпа, полигон, рисунки, профиль, эффекты и контракты |
| `PaperDefense.Editor.Tests` в `Tests/Editor` | Сериализованные сцены, каталоги, контент, импорт графики, числовой баланс |
| `PaperDefense.Integration.Tests` в `Tests/PlayMode` | Ввод, фиксированные часы, HUD, отрисовка, IO/отказы, стенды, мастерская и игровой маршрут |
| `PlayerSceneTests`, `PlayerJourneyTests` | Привязки игрового входа, переходы экранов, подготовка, пауза и результат |
| `MechanicsSceneInventoryTests`, `MechanicsSavedSceneTests`, `MechanicsHubNavigationTests` | Состав сцен, настоящий запуск стендов, возврат и изоляция |
| `NumericBalanceTests`, `TowerDefenseBalanceTests`, `EnemyCrowdMovementTests` | Измерения сборок, правила обороны и локальное движение |
| `NotebookArtImportTests`, `NotebookArtPressureTests`, `NotebookCameraReadabilityTests` | Арт-ресурсы, нагрузка рендера и читаемость камеры |

Быстрые проверки инструментов без Unity, из корня проекта:

```powershell
python -m unittest discover -s Tools -p 'test_extract_user_text.py'
python -m unittest discover -s Tools/tests -p 'test_*.py'
python -m unittest discover -s ArtSource/Notebook -p 'test_*.py'
git diff --check
```

Это разные наборы: Python не проверяет C# или игровой маршрут. Результаты прежних запусков собраны в [указателе проверок](validation/README.md). Перечисление тестового класса означает наличие проверки, а не её успешный запуск на текущем рабочем дереве.

## Ручная и нагрузочная проверка

После изменений игрового потока пройдите меню → карта → подготовка → мастерская → бой → пауза → результат. После правок рисунка проверьте применение/отмену, сохранение страницы, повторное открытие и одинаковый вид копий в бою. Для профиля используйте временный стенд, для графики — галереи 24–27.

Нагрузочный Player активируется флагом `-paper-pressure`, параметры скорости и пути отчёта описаны в [Tools/README](../Tools/README.md). Он использует отдельный сценарий с пиком 3000 врагов, а не обычный игровой баланс. Измерения времени кадра требуют реального рендера и фиксированных условий устройства; счётчик перестроек пути или тест без графики не является измерением FPS.

Записывайте версию Unity, пресет, seed, конфигурацию, результат и пути к доказательствам в новый датированный отчёт. При изменении системы обновляйте её руководство из [общего указателя](README.md), сохраняя старые отчёты как историю.
