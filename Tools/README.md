# Инструменты проекта

[Общая документация](../docs/README.md) · [Разработка и тесты](../docs/Development-Guide.md)

Команды ниже запускаются из корня проекта. Инструменты генерации меняют свои исходные файлы или ассеты; проверочные команды создают отчёты. Старые сценарии сравнения имеют собственные настройки и не представляют текущий PlayerRules.

## Меню Unity

Все пути начинаются с **Paper Defense**. Реализация находится в [Editor](../Assets/PaperDefense/Editor).

| Меню | Действие |
|---|---|
| `Game → Open player game` | Открывает сохранённую игровую сцену |
| `Game → Build player game` | Собирает Windows x64 и пишет `Builds/PlayerGame/build-report.txt` |
| `Открыть игру` | Открывает прежнюю техническую сцену PaperDefense |
| `Validation → Open mechanics hub` | Открывает общий экран или возвращает в него из Play |
| `Validation → Generate missing mechanics scenes` | Создаёт отсутствующие механические стенды и их данные |
| `Validation → Generate mechanics hub` | Создаёт отсутствующий hub и обновляет каталог существующих стендов |
| `Art → Generate art test scenes` | Создаёт отсутствующие художественные стенды |
| `Art → Import notebook art and current templates` | Проверяет JSON оформления, публикует ресурс/актуальные шаблоны и экспортирует SVG |
| `Art → Export notebook SVG contact sheets` | Экспортирует контактные SVG из проверенного JSON |
| `Content → Validate all databases and presets` | Проверяет базы и пресеты |
| `Content → Migrate legacy balance assets once` | Историческая миграция старой конфигурации в каталог |
| `Content → Apply modular tower preset` | Применяет модульный пресет к предусмотренным инструментом данным |
| `Content → Apply notebook tower defense rules` | Применяет правила тетрадной обороны к Target |
| `Content → Prepare development acceptance scene` | Готовит отдельную сцену для приёмки контента |
| `Export mechanics and balance JSON` | Экспортирует Historical/Target для книги баланса |
| `Проверить орду 3000` / `Проверить естественный пролом · 3000 врагов` | Один PressureValidation: детерминированный сценарий, отчёт `docs/validation/pressure-replay.json` |
| `Проверить историческую мастерскую и чернильные рубежи` / `Проверить целевую мастерскую и чернильные рубежи` | InkLoopValidation для Historical / Target, отчёты с префиксами `ink-loop-historical` / `ink-loop-target` |
| `Проверить все 8 волн` | Исторический CampaignValidation на `GameBalance.CreateDefault()`, отчёт `campaign-smoke.json`; не проверка PlayerRules |

Миграция, Apply и импорт изменяют данные; это не необходимые шаги перед каждым запуском. Генераторы отсутствующих стендов не обновляют уже сохранённые сцены. Точные источники и границы изменения — в [каталоге контента](../docs/Content-Architecture.md) и [оформлении](../docs/Notebook-Art.md).

## Русские тексты

[user-texts.json](user-texts.json) — канонический словарь общих текстов. [Extract-UserText.py](Extract-UserText.py) извлекает видимые строки C#, сохраняет устойчивые ключи и генерирует `Resources/Text/ru.asset` и `Core/UserTextContracts.Generated.cs`. Комментарии, атрибуты и техническая диагностика по умолчанию исключены; английские строки требуют отдельной проверки.

```powershell
# Только аудит; код возврата 1 означает найденные неизвлечённые строки.
python Tools/Extract-UserText.py --audit Assets/PaperDefense/Presentation/UI

# После проверки текста: заменить выбранные литералы и обновить ресурсы.
python Tools/Extract-UserText.py --write Assets/PaperDefense/Presentation/UI

# Перегенерировать ресурсы из существующего словаря без обхода C#.
python Tools/Extract-UserText.py --write
```

`--merge <путь.json>` объединяет проверенный словарь и **пишет** сгенерированные файлы даже без `--write`. `--include-diagnostics` явно включает диагностические строки. Пути разрешаются относительно корня проекта.

Игровая оболочка использует отдельный [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset): `PlayerUi.T` добавляет префикс `player.`. Генератор общего словаря не создаёт этот файл. Названия/описания деталей также живут в каталоге контента. Не добавляйте текст прямо в сгенерированный файл контрактов.

## Нагрузочный Player

Параметры [PressurePlayerProbe](../Assets/PaperDefense/Validation/PressurePlayerProbe.cs):

| Параметр | Значение |
|---|---|
| `-paper-memory-only` | Обычный запуск с профилем в памяти |
| `-paper-pressure` | Устанавливает измеритель и сам изолирует профиль до инициализации мастерской |
| `-pressure-speed 1` / `2` | Скорость сценария; по умолчанию 1 |
| `-pressure-report <полный путь.json>` | Путь JSON; по умолчанию `pressure-player.json` относительно рабочего каталога |

Нужна предварительно собранная сцена с `PaperDefenseBootstrap`. Сценарий выполняется с реальной отрисовкой, записывает время кадров/симуляции/mesh, GC, пик врагов, отпечаток и состояние изоляции профиля. Прогон ограничен 600 секундами или 36 000 образцами кадров; после отчёта Player завершается. Это отдельный нагрузочный fixture.

| Скрипт | Вход и выход |
|---|---|
| [Measure-Pressure.ps1](Measure-Pressure.ps1) | Ожидает `Builds/Windows/PaperDefense.exe`; `-Speed 0` запускает ×1 и ×2, либо укажите 1/2. Пишет фиксированные `player-pressure-xN.json/.log`, повтор заменяет эти отчёты |
| [Run-FullAlbumPressure.ps1](Run-FullAlbumPressure.ps1) | `-PlayerRelativePath`, `-ReportTag`, `-Speeds`; по умолчанию `Builds/FullAlbumTarget/PaperDefense.exe`. Создаёт датированные отчёты и не заменяет существующие |

Пример выбора основной сборки вместо исторического пути:

```powershell
./Tools/Run-FullAlbumPressure.ps1 -PlayerRelativePath 'Builds/PlayerGame/PaperDefense.exe' -ReportTag 'player-game' -Speeds 1,2
```

Скрипты запускают отдельный процесс и ожидают отчёт; они не собирают Player. Сохраняйте условия устройства и разрешение окна при сравнении результатов.

## Экспорт, приёмка и исторические сравнения

| Инструмент | Назначение и ограничения |
|---|---|
| [BalanceWorkbook/Build.ps1](BalanceWorkbook/Build.ps1) и [build.mjs](BalanceWorkbook/build.mjs) | JSON из Unity → Excel; проверяют свежесть экспорта и формулы. [Инструкция](BalanceWorkbook/README.md) |
| [Validate-ContentDataOnly.ps1](Validate-ContentDataOnly.ps1) | `-Mode Freeze` фиксирует хэши кода/ассетов, `Verify` проверяет изменения только допустимых assets в `Assets/PaperDefense/Content`. Использует `rg` и Git; не заменяет Unity-тесты. Существующую фиксацию не перезаписывает |
| [Reconstruct-OriginalCoreBaseline.ps1](Reconstruct-OriginalCoreBaseline.ps1) | PowerShell 7, локальный .NET SDK и Unity Editor/Data; извлекает указанный Git commit (по умолчанию `b759182`) в новый каталог `Temp`, компилирует историческое ядро. Не скачивает commit и не переключает рабочее дерево |
| [OriginalCoreValidation](OriginalCoreValidation) | Явный адаптер и описание адаптации сценария для исторического baseline |
| [FullLoopValidation](../Assets/PaperDefense/Editor/FullLoopValidation.cs) | API `RunPressureJson` и `RunAuthoredCopiesJson`: полный альбом/нагрузка и копии авторской геометрии |
| [RefactorValidation](../Assets/PaperDefense/Editor/RefactorValidation.cs) | API снимков правил, испытаний, replay и измерений; исторические сравнения |
| [NumericBalanceScenario](../Assets/PaperDefense/Validation/NumericBalanceScenario.cs) | Матрица сборок, полос, бюджетов и seeds; отдельные ситуационные проверки; вызывается тестами/кодом |
| [PlayerVisualCapture](../Assets/PaperDefense/Editor/PlayerVisualCapture.cs) | API `Capture`: снимки игрового интерфейса; по умолчанию `Captures/PlayerGame` |

Результаты и ограничения прежних прогонов — в [истории проверок](../docs/validation/README.md). Генерация арт-данных и SVG описана в [ArtSource/Notebook](../ArtSource/Notebook/README.md).
