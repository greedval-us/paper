# Инструменты проекта

[Общая документация](../docs/README.md) · [Разработка и тесты](../docs/Development-Guide.md)

Команды ниже запускаются из корня проекта. Инструменты генерации меняют свои исходные файлы или ассеты; проверочные команды создают отчёты. Сценарии сравнения имеют собственные настройки и не представляют текущий PlayerRules. Основной маршрут ещё предстоит оформить по [эскизам](../docs/Game-Design.md).

## Меню Unity

Все пути начинаются с **Paper Defense**. Реализация находится в [Editor](../Assets/PaperDefense/Editor).

| Меню | Действие |
|---|---|
| `Game → Open player game` | Открывает сохранённую игровую сцену |
| `Game → Build player game` | Собирает Windows x64 и пишет `Builds/PlayerGame/build-report.txt` |
| `Открыть игру` | Открывает ту же основную сцену `Game/PaperDefenseGame.unity` |
| `Art → Import notebook art and current templates` | Проверяет JSON оформления, публикует ресурс/актуальные шаблоны и экспортирует SVG |
| `Art → Export notebook SVG contact sheets` | Экспортирует контактные SVG из проверенного JSON |
| `Content → Validate all databases and presets` | Проверяет базы и пресеты |
| `Content → Migrate legacy balance assets once` | Историческая миграция старой конфигурации в каталог |
| `Content → Apply modular tower preset` | Применяет модульный пресет к предусмотренным инструментом данным |
| `Content → Apply notebook tower defense rules` | Применяет правила тетрадной обороны к Target |
| `Export mechanics and balance JSON` | Экспортирует Historical/Target для книги баланса |
| `Проверить орду 3000` / `Проверить естественный пролом · 3000 врагов` | Один PressureValidation: детерминированный сценарий, отчёт `docs/validation/pressure-replay.json` |
| `Проверить историческую мастерскую и чернильные рубежи` / `Проверить целевую мастерскую и чернильные рубежи` | InkLoopValidation для Historical / Target, отчёты с префиксами `ink-loop-historical` / `ink-loop-target` |
| `Проверить все 8 волн` | Исторический CampaignValidation на `GameBalance.CreateDefault()`, отчёт `campaign-smoke.json`; не проверка PlayerRules |

Миграция, Apply и импорт изменяют данные; это не необходимые шаги перед каждым запуском. Меню генерации и открытия тестовых сцен удалены вместе со стендами. Точные источники и границы изменения данных — в [каталоге контента](../docs/Content-Architecture.md) и [оформлении](../docs/Notebook-Art.md).

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

## Запуск и нагрузочная проверка

Основная сборка находится в `Builds/PlayerGame/PaperDefense.exe`. Флаг `-paper-memory-only` запускает обычный игровой маршрут с профилем в памяти.

Runtime-измеритель `PressurePlayerProbe`, флаг `-paper-pressure` и скрипты `Measure-Pressure.ps1` / `Run-FullAlbumPressure.ps1` удалены. Детерминированные сценарии Pressure, NumericBalance и InkLoop сохранены в Editor-only сборке Validation; их вызывают редакторские команды выше и автоматические проверки. Для измерения кадров основной игры используйте Unity Profiler и фиксируйте условия устройства и разрешение окна.

## Экспорт, приёмка и исторические сравнения

| Инструмент | Назначение и ограничения |
|---|---|
| [BalanceWorkbook/Build.ps1](BalanceWorkbook/Build.ps1) и [build.mjs](BalanceWorkbook/build.mjs) | JSON из Unity → Excel; проверяют свежесть экспорта и формулы. [Инструкция](BalanceWorkbook/README.md) |
| [Validate-ContentDataOnly.ps1](Validate-ContentDataOnly.ps1) | `-Mode Freeze` фиксирует хэши кода/ассетов, `Verify` проверяет изменения только допустимых assets в `Assets/PaperDefense/Content`. Использует `rg` и Git; не заменяет Unity-тесты. Перед новым изменением данных выполните новый Freeze; старая фиксация удалена при очистке. Существующую фиксацию не перезаписывает |
| [Reconstruct-OriginalCoreBaseline.ps1](Reconstruct-OriginalCoreBaseline.ps1) | PowerShell 7, локальный .NET SDK и Unity Editor/Data; извлекает указанный Git commit (по умолчанию `b759182`) в новый каталог `Temp`, компилирует историческое ядро. Не скачивает commit и не переключает рабочее дерево |
| [OriginalCoreValidation](OriginalCoreValidation) | Явный адаптер и описание адаптации сценария для исторического baseline |
| [FullLoopValidation](../Assets/PaperDefense/Editor/FullLoopValidation.cs) | API `RunPressureJson` и `RunAuthoredCopiesJson`: полный альбом/нагрузка и копии авторской геометрии |
| [RefactorValidation](../Assets/PaperDefense/Editor/RefactorValidation.cs) | API снимков правил, испытаний, replay и измерений; исторические сравнения |
| [NumericBalanceScenario](../Assets/PaperDefense/Validation/NumericBalanceScenario.cs) | Матрица сборок, полос, бюджетов и seeds; отдельные ситуационные проверки; вызывается тестами/кодом |
| [ContentAcceptanceRunner](../Assets/PaperDefense/Validation/ContentAcceptanceRunner.cs) и [ContentAcceptanceManifestAsset](../Assets/PaperDefense/Validation/ContentAcceptanceManifestAsset.cs) | Editor-only приёмка данных `Content/Acceptance` через автоматические тесты; отдельная сцена удалена |
| [PlayerVisualCapture](../Assets/PaperDefense/Editor/PlayerVisualCapture.cs) | API `Capture`: снимки игрового интерфейса; по умолчанию `Captures/PlayerGame` |

Последняя проверка очистки и назначение сохранённых исторических JSON описаны в [указателе проверок](../docs/validation/README.md). Эти JSON остаются входами инструментов приёмки и не являются новым результатом текущего дерева. Генерация арт-данных и SVG описана в [ArtSource/Notebook](../ArtSource/Notebook/README.md); исходники и эскизы сохранены.
