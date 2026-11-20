# Разработка, запуск и проверка

Версия редактора: **Unity 6000.5.7f1**, источник — [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt). Пакеты: [manifest](../Packages/manifest.json) и [lock](../Packages/packages-lock.json). Проект открывается из корня с Assets, Packages и ProjectSettings.

## Запуск

Единственная игровая сцена: [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Меню **Paper Defense → Game → Open player game**, затем Play. Конфигурация: PlayerBalance → PlayerRules → PlayerContent. `CreateConfigurationIfMissing()` повторно назначает связи каталога даже при существующих assets; не используйте его как безвредное чтение.

Windows x64: **Paper Defense → Game → Build player game** (`PaperDefense.Editor.PlayerGameSceneTools.Build`). Выход обычной команды: `Builds/PlayerGame/PaperDefense.exe`, рядом `build-report.txt`. Отдельные проверки могут сохранять сборки в собственные каталоги; фактические результаты и ограничения собраны в [журнале проверок](validation/README.md). Для запуска без личного профиля передайте `-paper-memory-only`.

Для отдельного тестового сохранения поддерживается `-paper-profile-path <абсолютный путь внутри системной временной папки>`. Некорректный путь отклоняется до открытия хранилища; автоматического перехода к личному профилю при ошибке нет. Флаг памяти имеет приоритет над временным store. Приёмочная инструментализация включается только явно через `-paper-release-probe <отчёт.json>` вместе с изоляцией профиля и не создаётся при обычном запуске игры.

Перед прогоном в открытом редакторе вызовите `PaperDefense.Tests.TestRunResults.Enable("EditMode")` или `Enable("PlayMode")`: нативный TestRunnerApi сохраняет окончательный XML в `Temp/TestResults`. Для датированного отчёта передайте каталог вторым аргументом. Запись однократная и отключается после завершения; для отмены вызовите `Disable()`. Обычные прогоны не создают исторические каталоги документации. Сборка остаётся Windows x64, без Development-флага; frame timing включён для проверки на устройстве. При скрытом окне GPU-измерение может отсутствовать и должно быть обозначено как недоступное.

## Структура

| Каталог | Назначение |
|---|---|
| Assets/PaperDefense/Core | Правила и симуляция без Unity API |
| Application | Контроллер, профиль, конфигурация, IO |
| Presentation | Ввод, мастерская, оболочка, HUD, отрисовка |
| Editor и Validation | Импорт, сборка, редакторские сценарии |
| Tests | EditMode, Editor, PlayMode |
| Configuration | Основной баланс и отдельные сравнительные пресеты |
| Assets/PaperDefense/Graphics | Внешние ресурсы текущего оформления; фон карты меню и provenance |
| Presentation/Art и Presentation/Rendering | Нативные модульные рисунки, боевая тема и общий renderer |
| Tools/AlbumDesign2026 | Числовой каталог, независимая проверка баланса и Excel |
| docs/validation | Итоги запусков по датам и входные данные для миграции и сравнительной книги |

## Изменения игры

Начинайте с [текущего состояния](Current-Mechanics.md), [мастерской](Workshop-Assembly.md), [интерфейса](Interface.md) и [источников параметров](Balance-Parameters.md). Writer v9 использует восемь слотов; readers v1–v8 сохраняют исторические данные и переносят назначения без потери страниц. Сохраняйте этот контракт и тесты миграции. Изменения баланса вносятся в build-data.mjs, импортируются через AlbumBalanceImport, затем сверяются с экспортом Player; сама правка Excel игровые assets не меняет.

Для графики используйте [действующий стандарт](../Assets/PaperDefense/Graphics/README.md): `NightNotebookArt` (`paper-mechanisms-v4`), `AssemblyVisualComposer.Compose/ComposePart/ComposeFoundation/ComposeWeapon`, боевую тему `InkSwarmArt` (`ink-swarm-v1`) и `FieldMenuSkin`. `FieldMenuTheme` и `BattleHudTheme` независимо оформляют меню и бой. Единый UI-предпросмотр `StandardAssemblyIllustration` получает страницу альбома и сохраняет точный состав; семантический значок не подменяет известный рецепт. Авторские тёмные линии прежних сохранений должны оставаться читаемыми на защищённом светлом листе; оформление не меняет сохранённые штрихи. Физические ограничения загружаются через `AssemblyFitResources` из `Resources/Configuration/AssemblyFits.json`. [Контракт графики](Modular-Asset-Contract.md) обязателен; визуальный коллаж не доказывает совместимость при вращении.

## Проверки

В Unity Test Runner запускайте EditMode и PlayMode отдельно. Сборки: `PaperDefense.Core.Tests`, `PaperDefense.Editor.Tests`, `PaperDefense.Integration.Tests`. [Назначение действующих наборов](Testing.md) отделяет текущие сценарии от необходимой совместимости сохранений. Тесты сцен могут открывать другие сцены; сначала сохраните собственные изменения.

Подбирайте набор по изменению: миграция — сериализация и повреждённые профили; резолвер — категории, совместимость, формулы и порядок частей; бой — цикл выстрела, цели и сохранённое состояние; UI — 8 слотов, управление и ошибки записи. Финальный этап требует полного набора тестов и Windows build.

Числовые JSON и Excel воспроизводятся по [инструкции AlbumDesign2026](../Tools/AlbumDesign2026/README.md): генерация авторских данных → импорт в Unity → свежий runtime-экспорт → проверка. После импорта и экспорта из корня проекта:

```powershell
node --test Tools/AlbumDesign2026/verify-balance.test.mjs
node Tools/AlbumDesign2026/verify-balance.mjs
python -m unittest discover -s Tools/tests -p 'test_*.py'
git diff --check
```

Для batch-сборки через установленный редактор Unity используйте `-batchmode -quit -projectPath <корень проекта> -executeMethod PaperDefense.Editor.PlayerGameSceneTools.Build -logFile <путь лога>`. Для тестов: `-batchmode -projectPath <корень проекта> -runTests -testPlatform EditMode -testResults <путь.xml> -logFile <путь лога>`; PlayMode запускается отдельно с `-testPlatform PlayMode`. Не добавляйте `-quit` к запуску тестов. Закройте открытый экземпляр этого проекта перед отдельным batch-запуском.

Ручной маршрут: меню → карта → подготовка → мастерская башни и каждого семейства сооружений → библиотека компонентов → альбом и выбор страницы → полигон → бой и инспектор → пауза/настройки/справка → победа и поражение → повторное открытие. Проверяйте также окна покупки, сохранения и назначения, пустые/недоступные состояния, длинные названия и клавиатурный фокус. Контрольные размеры: 800×600, 1024×768, 1280×720, 1280×960, 1600×900, 1920×1080. Кадры с искусственно заданным исходом боя обозначайте проверкой представления, а не прохождением кампании. Профилирование выполняйте через Unity Profiler в фиксированных условиях; unit-тесты не измеряют FPS.

Каждый отчёт должен называть версию Unity, конфигурацию, seed, выполненные команды, результаты и ограничения. [Результаты проверок](validation/README.md) сохраняют даты; успешный прогон не подтверждает изменения, внесённые после него.
