# Проверка каталога и баланса через данные

Отчёт относится к рефакторингу каталога «ДЕРЖИ ЛИНИЮ». Исходный Core для сравнения взят из локального коммита `b7591827ccb64ebbc0f0960fedeef0babd43a669` (`b759182`). Это исходный HEAD, а не утверждение, что рефакторинг уже закоммичен: состав итоговых исходников зафиксирован отдельно в [code-freeze.json](code-freeze.json).

Проверки разделены на сохранение прежнего поведения и добавление контента после фиксации кода. Рабочие источники и ограничения расширения описаны в [Content-Architecture.md](../../Content-Architecture.md), правила сохранений — в [Content-Profile-Compatibility.md](../../Content-Profile-Compatibility.md).

## Состояние проверки

| Проверка | Результат | Доказательство |
| --- | --- | --- |
| Два рабочих пресета, 168 сборок | Совпадение эффективных старых параметров и расчётов | [rules.json до](before/rules.json), [после](after/rules.json) |
| Исторический набор испытаний из фабрики | 28 из 28 совпадают побайтно | [trials.txt до](before/trials.txt), [после](after/trials.txt) |
| Испытания с фактическим Target asset и исходным Core | 28 из 28 совпадают побайтно | [target-asset-trials.txt до](before/target-asset-trials.txt), [после](after/target-asset-trials.txt) |
| Пять полных прогонов | Хеши состояния и событий совпадают | [baseline-comparison.json](after/baseline-comparison.json) |
| Добавление A–E только через assets | 207 файлов кода неизменны; шесть изменений assets | [data-only-diff.json](data-only-diff.json) |
| Приёмка A–E в Editor | Все пять проверок PASS; временный профиль удалён | [editor-acceptance.json](after/editor-acceptance.json) |
| Полный EditMode | 685 / 685 PASS, 0 ошибок, 0 пропусков | [editmode-tests.json](after/editmode-tests.json) |
| Полный PlayMode | 256 / 256 PASS, 0 ошибок, 0 пропусков | [playmode-tests.json](after/playmode-tests.json) |
| Development Player: сборка | `Succeeded`, 0 ошибок / 485 предупреждений, 23.76 с, 198.72 МБ | [development-build.json](after/development-build.json) |
| Development Player: запуск A–E | Все пять проверок PASS, код выхода `0`; временный профиль удалён | [player-acceptance.json](after/player-acceptance.json) |

Среда зафиксированной Editor-приёмки — Unity `6000.5.7f1`, seed `1337`. Время тестов и полный список результатов находятся в JSON; длительность запуска не является критерием равенства симуляции.

## Что именно сравнивалось

В `before/` сохранены данные до замены поведения; в `after/` — результаты нового кода. `rules.json` включает два эффективных пресета (`NotebookBalance`, `TargetNotebookBalance`) и 168 сочетаний: семь прежних видов оружия × три материала × четыре набора оборудования × два пресета. Сравниваются цена, нагрузка, вместимость, расход цвета, здоровье, защита, строительство, дальность и интервал.

Два набора испытаний используют семь прежних видов оружия и четыре сценария, бюджет `200`, seed `1337`, синий цвет. Набор `trials.txt` сохраняет исходную фабричную фикстуру. `target-asset-trials.txt` дополнительно проверяет реальные текущие параметры Target asset на реконструированном исходном Core.

| Полный прогон | Файл до / после | Итоговый digest |
| --- | --- | --- |
| Историческая фабричная нагрузочная фикстура | [before/historical-replay.json](before/historical-replay.json) / [after/historical-replay.json](after/historical-replay.json) | `9c008656c9223382` |
| Target campaign, сохранённый прямой эталон | [before/target-replay.json](before/target-replay.json) / [after/target-replay.json](after/target-replay.json) | `1c7897be45940a81` |
| Исторический asset, pressure | [before/historical-asset-pressure.json](before/historical-asset-pressure.json) / [after/historical-asset-pressure.json](after/historical-asset-pressure.json) | `3a042ccfeb445fdb` |
| Исторический asset, campaign | [before/historical-asset-campaign.json](before/historical-asset-campaign.json) / [after/historical-asset-campaign.json](after/historical-asset-campaign.json) | `85b5b5bc10491e52` |
| Target asset, campaign | [before/target-asset-campaign.json](before/target-asset-campaign.json) / [after/target-asset-campaign.json](after/target-asset-campaign.json) | `1c7897be45940a81` |

Первые два JSON, оба файла испытаний и `rules.json` совпадают побайтно. Для трёх прогонов исходного Core с реальными assets сравниваются хеш состояния и событий каждого тика, контрольные точки, количество тиков/событий и все доступные исходные поля итогового отчёта. Результат: pressure — `5693` тика / `1872` события, историческая campaign — `20299` / `9016`, Target campaign — `21063` / `6912`; расхождений сравниваемых полей нет.

У изолированно собранной старой DLL `JsonUtility` не выводит некоторые вложенные поля диагностического отчёта: `FirstBreachCell` у pressure и `WaveTuning`, `Commands`, `Waves` у campaign. Они перечислены в `OriginalDllJsonOmittedFields` внутри [baseline-comparison.json](after/baseline-comparison.json). Это ограничение сериализации отчёта, поэтому три таких JSON не объявлены побайтно равными. Все хеши симуляции и исходные сериализованные скалярные поля совпали; прямой Target-эталон совпал и с вложенными записями.

Происхождение исходной DLL, хеши экспортированных Core-файлов, компилятора и Unity-ссылок записаны в [original-core-provenance.json](original-core-provenance.json). Исходные Core-файлы извлекались через локальный `git show` и компилировались без исправлений. Мост переносит каждое старое публичное поле эффективных настроек в старый тип; отсутствие поля вызывает ошибку, подстановка defaults запрещена. Архив старого валидатора — [before/RefactorValidation.cs.txt](before/RefactorValidation.cs.txt); исходные контуры и прежний экспорт — [templates.json](before/templates.json), [balance-data.json](before/balance-data.json).

## Приёмка A–E после фиксации кода

Финальная фиксация содержит `207` файлов кода и выполнена `2026-10-12T17:58:43.5024196Z`. Затем тестовый каталог снова расширен с базовых `13` деталей до `17`: два оружия, оборудование и основание добавлены данными, существующее оружие и общие правила изменены данными. Их экземпляры и ожидаемые числа заданы в [ContentAcceptanceManifest.asset](../../../Assets/PaperDefense/Content/Acceptance/ContentAcceptanceManifest.asset).

| Случай | Стабильный ID / источник | Ожидаемые параметры |
| --- | --- | --- |
| A: новое оружие | `weapon.acceptance.precision` | Цена сборки `52`, дальность `13.5`, интервал `0.55` с, нагрузка `5`, открытие `23` золота |
| B: вариант существующего поведения | `weapon.acceptance.wide-line` | Три мишени `(5, 0)`, `(8, 0.7)`, `(8, 1.1)`; фактический урон `[17, 17, 0]` соответствует заданной маске попаданий |
| C: новое оборудование | `module.acceptance.long-range` | Открытие `17` золота; сборка A + C стоит `63`, дальность `16.2`, интервал `0.605` с |
| D: новое основание | `chassis.acceptance.field` | Цена основания `22`, сборка с двумя A — `94`, здоровье `73`, вместимость `16`; оба крепления стреляют |
| E: изменённые прежние данные | `weapon.gun`, `effect.color.blue`, правила волн Acceptance preset | Цена оружия `47`, точный урон по изолированной цели `19`, длительность эффекта `4.75` с, выдача чернил после волны `123` |

Файл [editor-acceptance.json](after/editor-acceptance.json) подтверждает каталог и оплату открытия, рисунок, сохранение/перезапуск, предпросмотр, полигон и реальные атаки. Для E предпросмотр и оплаченная цена равны `47`, четыре копии на полигоне стоят `188`, изолированное попадание наносит `19`, фактическая выдача после волны — `123`. На боевом участке выполнены шесть атак; суммарный боевой урон `66` учитывает условия боя и не подменяет проверку одного попадания. Модуль сохраняется по строковому ID и ID экземпляра; новые enum или битовые флаги для него не добавлялись.

Собранный Development Player повторил все A–E и числа Editor-приёмки: [player-acceptance.json](after/player-acceptance.json). Первый запуск внутри Windows sandbox не смог записать изолированный временный профиль. Повторный запуск с разрешением на выполнение вне sandbox завершился кодом `0`; код и assets между запусками не менялись. Все 485 предупреждений сборки относятся к шейдерам пакета `com.unity.ai.inference`: 25 PixelShaders и 460 ConvGeneric.compute. Группы и примеры находятся в [development-build.json](after/development-build.json); ошибок сборки нет.

Изменились ровно шесть asset-файлов: `AcceptanceContent.asset`, `AcceptancePreset.asset`; добавлены `ContentAcceptanceManifest.asset` и его `.meta`; удалены промежуточные `Stage1Manifest.asset` и его `.meta`. Сцена `ContentAcceptance.unity` уже существовала и в финальной стадии не менялась. Полный список и хеши находятся в [data-only-diff.json](data-only-diff.json). Экспериментальные параметры относятся к отдельному Acceptance preset; они не заменяют рабочий баланс двух основных пресетов.

Промежуточная стадия сохранена в [stage1/](stage1/): A–E прошли, но полный PlayMode обнаружил ошибку селектора в тесте интерфейса — тест искал stable ID там, где имя старой кнопки намеренно сохранено. Исправлен только тестовый селектор; изменений runtime-кода не было. После прохождения всех `256` PlayMode каталог сброшен до базовых `13` деталей, выполнена новая фиксация и повторено добавление assets. Причина и границы перезапуска записаны в [stage1/restart-note.json](stage1/restart-note.json); финальные доказательства находятся в корне этого каталога и `after/`.

## Как повторить

1. Открыть проект той же версией Unity и выполнить `Paper Defense → Content → Validate all databases and presets`. Проверка должна разрешить оба рабочих пресета и Acceptance preset без ошибок. Запускать полные EditMode и PlayMode из Test Runner; сохранённые отчёты содержат результаты всех тестов, включая динамический каталог и persisted manifest.
2. В Editor вызвать `ContentAcceptanceRunner.Run(manifest)` с asset `Assets/PaperDefense/Content/Acceptance/ContentAcceptanceManifest.asset`; сохранить результат отдельно от эталона. Приёмка создаёт изолированный временный профиль и удаляет его. Профиль игрока не используется. API и запуск находятся в `Assets/PaperDefense/Application/ContentAcceptanceRunner.cs` и `ContentAcceptanceProbe.cs`.
3. Для Development Player включить готовую сцену `Assets/PaperDefense/Content/Acceptance/ContentAcceptance.unity` первой, а игровую сцену `PaperDefense` — следующей. Компонент `ContentAcceptanceProbe` читает тот же manifest. Запустить собранный Player командой ниже; код выхода `0` и `Success: true` в JSON означают успешную приёмку. Без флага smoke сцена переходит в игру.

```powershell
& '<путь к Development Player.exe>' -paper-content-smoke -paper-content-report '<абсолютный путь к report.json>' -logFile '<абсолютный путь к player.log>'
```

Проверить неизменность кода относительно уже сохранённой финальной фиксации:

```powershell
pwsh -File Tools/Validate-ContentDataOnly.ps1 -Mode Verify
```

`Verify` заново пишет отчёт diff. Режим `Freeze` предназначен для начала нового эксперимента в отдельной копии/стадии; текущую фиксацию доказательств перезаписывать не следует. Скрипт проверяет SHA-256 кода и разрешает изменения только assets/scenes/meta внутри `Assets/PaperDefense/Content`.

Для независимого повторения исходного Core нужен локальный коммит `b759182`, установленный .NET SDK и Unity Editor с reference assemblies; загрузка зависимостей не требуется:

```powershell
pwsh -File Tools/Reconstruct-OriginalCoreBaseline.ps1 -BaselineCommit b759182 -UnityDataPath '<путь к Unity Editor/Data>' -DotnetPath '<путь к dotnet.exe>'
```

Скрипт создаёт DLL и provenance в уникальном каталоге `Temp/OriginalCoreValidation-b759182/run-*`. В Editor загрузить полученную сборку отдельно и вызвать `PaperDefense.OldBaselineBridge.CanonicalSettings(balance)`, `TrialsJson(balance)` и `StartReplay("pressure"|"campaign", balance, absoluteOutputPath)`, передав эффективный balance соответствующего asset. Мост не содержит новых правил Core. Для текущей реализации эквивалентные точки входа — `PaperDefense.Editor.RefactorValidation.RulesJson()`, `TrialsJson(legacyFixture)` и `StartReplay(target, outputPath, legacyFixture, campaignReplay)`. Фоновый replay завершён после появления JSON; файл `.error` означает ошибку. Сравнивать результаты нужно с соответствующей фабричной или asset-фикстурой, не смешивая их настройки.

Для отчёта Excel выполнить `Paper Defense → Export mechanics and balance JSON`, затем:

```powershell
pwsh -File Tools/BalanceWorkbook/Build.ps1
```

Это односторонний экспорт рабочих пресетов, а не импорт Excel в игру. Источник — `docs/balance/balance-data.json`, результат — `docs/balance/PaperDefense-Mechanics-Balance.xlsx`, проверка — `docs/balance/workbook-validation.json`. Книга содержит девять листов: «Механики», «Расчеты», «Оружие», «Детали», «Материалы и враги», «Волны», «Параметры», «Каталог данных», «Источники». Генератор проверяет хеши исходников и exports; после правок кода/assets JSON следует экспортировать заново. Эксперимент Acceptance A–E описан в этом отчёте отдельно от основного баланса.

Финальная книга содержит 478 глобальных параметров и 2020 полей каталога. Все 104 формулы сверены с runtime-эталонами; поиск ошибок формул дал ноль совпадений. Просмотрены 15 PNG-фрагментов всех девяти листов, обрезанного текста и видимых ошибок нет. Пересчёт выполнен `@oai/artifact-tool`; запуск книги в Microsoft Excel отдельно не выполнялся. [JSON проверки книги](../../balance/workbook-validation.json) также хранит SHA256 семи отчётов приёмки и регрессии.
