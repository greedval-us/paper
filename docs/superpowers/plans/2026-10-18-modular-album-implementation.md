# Составной альбом — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Выполнение начато по запросу пользователя 18.10.2026. Задачи 1–4 завершены; следующая — задача 5. [Проверки задачи 1](../../validation/modular-album/task1-report.md), [задачи 2](../../validation/modular-album/task2-report.md), [задачи 3](../../validation/modular-album/task3-report.md), [задачи 4](../../validation/modular-album/task4-report.md).

**Goal:** перенести существующую игру на альбом составных башен и сооружений, восемь общих слотов и открытия деталей за уровень и золото, включая все обновлённые экраны.

**Architecture:** сохранить Core / Application / Presentation и существующую симуляцию обороны. Core разрешает рецепт в неизменяемый снимок характеристик; мастерская, полигон и бой используют этот расчёт. Application отвечает за транзакции профиля и миграцию; Presentation показывает готовые данные и отправляет команды. Чтение старых рисунков остаётся отдельным адаптером совместимости.

**Tech Stack:** Unity **6000.5.7f1**, C#, uGUI, Input System, существующие asmdef, NUnit / Unity Test Runner; текущий конвейер атласа и Node.js-инструменты баланса. Новые внешние зависимости и смена UI-фреймворка не требуются.

**Spec:** [дизайн игры](../../Game-Design.md), [мастерская](../../Workshop-Assembly.md), [формулы и способности](../../Component-Stats-and-Abilities.md), [миграция](../../Modular-Transition.md), [контракт графики](../../Modular-Asset-Contract.md), [JSON баланса](../../balance/album-balance-v1.json), [Excel](../../balance/PaperDefense-Album-Balance-v1.xlsx), [полная галерея](../../design/game-concept-art-2026-10-16/index.html).

Дата: **18.10.2026**. Проверенная база исходников при подготовке: commit `1f57553`; до подготовки плана рабочее дерево чистое. Галочки ниже отражают проверенное выполнение реализации, а не готовность эскизов или документации.

## Global Constraints

- До **8 разных рисунков** в общем наборе; пустые слоты разрешены. Для старта нужна хотя бы одна атакующая башня; ремонт и промокашка атакой не считаются. Начатый бой использует замороженный набор.
- **64 страницы**, имя до **48 символов**, **54 определения деталей**, **8 бесплатных стартовых рецептов**. Покупка детали однократная, после достижения уровня; сохранение рисунка бесплатно.
- Основа: рамка / корпус / броня / подача / поворот. Оружие: механизм / ствол или головка / прицел или датчик / магазин или кассета / спуск. Сооружение: основание / функция / оболочка / оснащение.
- Семь семейств оружия и четыре семейства сооружений: баррикада, замедлитель, ремонтный пост, генератор штрихов. Шипы не входят в новый стартовый каталог.
- Существующая карта и **8 волн**, начальные **640 штрихов**. Начальное золото **0**. За завершённую волну **8 золота + 10 XP**; за победу дополнительно **30 золота + 40 XP**. Полная победа: **94 золота / 120 XP**.
- Пороги XP уровней 1–8: **0 / 100 / 230 / 390 / 580 / 800 / 1050 / 1330**. Уже купленные права при миграции не блокируются новым уровнем.
- Генераторов одновременно максимум **2**, возврат **0**, материал только **карандаш**. Выплата только за полностью пережитую волну; не выдаёт золото или XP. Ремонт — одна цель на пост, сильнейшая скорость на цели, без сложения.
- Свободное рисование, оценка штрихов, редактирование контура мортиры, лучей веера и распределения брони исключены из нового маршрута. Зигзаг, волна и возврат снаряда не входят в v1.
- Стиль: синяя ручка, тёплая клетчатая бумага. Текст и числа — живой UI. Эскизы являются визуальными образцами, их надписи не запекаются в игровой интерфейс.
- Сохранять `.meta`, GUID сцены и действующих ресурсов. Не перенумеровывать старые enum и не удалять reader v1–v7 вместе с редактором. Проверки миграции выполняются на копиях, без личного профиля.
- Базовые точки баланса фиксируются тестами. Корректировки после игровых прогонов разрешены, но меняются вместе в исходнике данных, JSON, Excel, runtime-каталоге, тестах и документации с новой ревизией.

## Review Focus

1. **Старый или будущий профиль:** 4→8 без потери золота, страниц и прав; неизвестный ID/версия не заменяется молча. Тесты задачи 4.
2. **Ошибка записи и повторная команда:** покупка, сохранение, удаление и награда не публикуются частично и не оплачиваются дважды. Тесты задач 5 и 8.
3. **Последний патрон, пропажа цели, пауза и изменение подачи:** магазин не восстанавливается от смены цели, время не учитывается дважды. Тесты задачи 6.
4. **Удалённая библиотечная основа или новая ревизия баланса:** сохранённая башня и начатый бой сохраняют свой состав. Тесты задач 2, 4 и 5.
5. **Два независимо поворачивающихся ствола и узкий экран:** разрешённая сборка не пересекается сама с собой, все 8 слотов и действия доступны при 1024×768. Геометрические тесты задачи 3, функциональные и визуальные проверки задач 10–13.

---

## От чего начинаем

| Область | Проверено в текущем коде | Что предстоит |
|---|---|---|
| Профиль | `WorkshopProfile.CurrentVersion = 7`, `LoadoutSlotCount = 4`; строгий reader проверяет именно эту длину | v8, восемь слотов, смешанный альбом, библиотеки, XP |
| Мастерская | `WorkshopWizard.StepCount = 3`: основа → оружие → проверка | Вложенная сборка основы/оружия, отдельный маршрут сооружений |
| Контент | Готовые основы, оружие, модули через `ContentDatabaseAsset` | Типизированные детали и перенос 54 определений из проектных данных |
| Бой | Семейства попаданий, общая подача, cooldown и захват цели | Полноценная обойма, смена магазина, очередь, параметры составных объектов |
| Поддержка | Отдельные `ConstructionToolRules`; ремонт сейчас лечит все подходящие цели в зоне | Строительство из альбома, ремонт одной цели, генератор |
| Ввод | `DrawingInput`: 1–4 — башни; 5–8 — стена, шипы, замедление, ремонт | 1–8 — общие слоты, отдельные действия материала/цвета/сноса |
| Графика | Общая композиция и split-gun пилот; свободный ход соседних стволов не принят | Проверка стыков и движения, затем весь модульный набор |
| Оболочка | Меню, карта, подготовка, бой, настройки и результат уже существуют | Перенос новых состояний и композиций всех экранов в ту же игровую сцену |

[Аудит 17–18 октября](../../audit/2026-10-17-album/report.md) содержит **939 EditMode** и **66 выбранных PlayMode** успешных тестов. Это историческая база, а не прогон этого плана; полный PlayMode и свежий Player тогда не проверялись. При подготовке плана код прочитан повторно, Unity-тесты не запускались.

Эскизы уже обновлены: **30 экранов + 3 листа объектов**. [Интерактивный прототип](../../design/album-assembly-2026-10-17/index.html) — дополнительный материал. Папка полной галереи с датой `2026-10-16` актуальна: её содержимое обновлено 18 октября, удалять её как «старую» нельзя.

## Порядок и контрольные результаты

| Задачи | Зависимость | Проверяемый результат |
|---|---|---|
| 1 → 2 | Текущий Core и данные баланса | Каталог и единый расчёт всех допустимых составов |
| 3 | 2 | Принятый графический пилот обычной основы, двух пулемётов и брони |
| 4 → 5 | 2; публикация сборок также требует 3 | Сохранение v8, восемь слотов, библиотеки и покупки |
| 6, 7 | 2 | Новые циклы оружия и строительство составных объектов |
| 8 | 5, 7 | Генератор, XP и однократные награды |
| 9 → 10 | 3, 5; запуск партии требует 6–8 | Мастерская, альбом, главное меню, карта и подготовка |
| 11, 12 | 6–10 | Боевой HUD, пауза, результат, помощь и общий полигон |
| 13 | 3, 9–12 | Все детали и все 33 визуальных образца перенесены и сверены |
| 14 → 15 | 11–13 | Измеренный баланс, удалённый старый редактор, проверенная сборка |

Выполнять по номерам. Независимость некоторых backend-задач указана для понимания зависимостей, а не как требование запускать нескольких исполнителей. После задачи 11 должен проходить полный игровой маршрут; после задачи 13 — визуальная приёмка всего комплекта. Готовность не определяется только количеством написанных классов.

## Карта файлов и правила проверки

Пути далее относительны корню проекта. `Core/...`, `Application/...`, `Presentation/...`, `Editor/...`, `Validation/...`, `Tests/...`, `Configuration/...`, `Resources/...` означают соответствующие каталоги внутри `Assets/PaperDefense/`. Пометка **новый** означает будущий файл, а не уже существующий API.

| Ответственность | Место изменений |
|---|---|
| Каталог, рецепты, разрешённые снимки | Новые небольшие классы в `Core/Assembly/`; существующие `ContentCatalog.cs`, `ContentDefinitions.cs`, `TowerCatalog.cs` подключаются через явный переходный адаптер |
| Профиль и транзакции | `Core/WorkshopProfile.cs`, `ProfileLimits.cs`, `BlueprintData.cs`; `Application/WorkshopProfileStore.cs`, `ProfileSaveCoordinator.cs`, `WorkshopReadModel.cs` |
| Сборки и команда игрока | Новый `Application/AssemblyCommands.cs`; `RunController.cs` делегирует ему операции, не разрастается в новый монолит |
| Симуляция | `WeaponSystem.cs`, `GameTypes.cs`, `GameSession.Construction.cs`, `.Support.cs`, `.Progression.cs`, `.Objectives.cs`, `ReadModel.cs`, `TrialRunner.cs` |
| Мастерская | `WorkshopWizard.cs`, `WorkshopHud.cs`, `WizardAssemblyPanel.cs`, `WizardFoundationPanel.cs`, `WizardWeaponPanel.cs`, `WizardAlbumPanel.cs`, `WizardTrialPanel.cs`; новые панели состава |
| Оболочка и ввод | `PlayerGameRoot.cs`, части `PlayerShell.*.cs`, `PlayerBattleHud.cs`, `Presentation/Input/DrawingInput.cs`, `Resources/Text/ru.asset` |
| Графика и импорт | `NotebookMountLayout.cs`, `NotebookSpriteCatalog.cs`, `NotebookTowerComposition.cs`, `WeaponAssemblyGraphic.cs`, `Editor/NotebookSpriteImportTools.cs`, `ArtSource/Notebook/NewStyle/` |
| Проверки | `Tests/EditMode/` — чистые правила; `Tests/Editor/` — импорт и графика; `Tests/PlayMode/` — профиль, UI, интеграция |

Для каждого нового файла Unity создать и сохранить его `.meta`. Не создавать отдельные сцены для каждого меню: основной маршрут остаётся в `Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity` и существующей оболочке. Фрагменты `Assert` ниже фиксируют обязательные ожидания названных тестов; подготовка fixture строится на интерфейсах соответствующей задачи.

**Как выполнять тестовый шаг:** в Unity Test Runner выбрать EditMode или PlayMode, отфильтровать указанное имя класса, выполнить Run Selected. Editor-тесты также находятся в EditMode. На красном шаге требуется содержательный FAIL или ожидаемая ошибка компиляции отсутствующего API; существующие регрессии не считать ожидаемым красным результатом. Если условие уже обеспечено предыдущей задачей, записать его успешную проверку как регрессионную — не ломать production искусственно. После реализации — 0 failed, затем перечисленные смежные регрессии. Сохранять XML и контекст запуска в новом `docs/validation/modular-album/`.

Для автоматизации после определения реального пути Unity 6000.5.7f1 и закрытия редактора, удерживающего этот проект:

```powershell
# $unityEditor — проверенный путь к Unity.exe нужной версии, не путь к Hub.
# $testFilter — имя класса из шага; для полного прогона убрать -testFilter.
& $unityEditor -batchmode -projectPath 'D:\Projekt Unity\paper' -runTests -testPlatform EditMode -testFilter $testFilter -testResults 'Temp/album-editmode.xml' -logFile 'Temp/album-editmode.log'
# Для интеграционных тестов: -testPlatform PlayMode и отдельные файлы результатов.
```

Не добавлять `-quit` к команде тестов. Успех подтверждается XML, а не одним кодом завершения. Не запускать второй Unity на том же checkout при открытом редакторе: использовать Test Runner текущего редактора. Ни одна задача не требует личного профиля.

Каждая задача завершается отдельным коммитом после проверки: `git diff --check`, затем выборочно `git add -- <только файлы задачи и их .meta>` и `git commit -m "<сообщение задачи>"`. Не использовать `git add .`; уже существовавшие чужие изменения в коммит не включать.

## Задача 1. Каталог деталей и рецепты

**Новые:** `Core/Assembly/ComponentDefinition.cs`, `ComponentCatalog.cs`, `AssemblyRecipe.cs`, `AlbumEntry.cs`, `AssemblyDiagnostic.cs`; `Application/AssemblyCatalogAsset.cs`; `Editor/AlbumBalanceImport.cs`; `Configuration/Game/PlayerAssemblyCatalog.asset`; `Tests/EditMode/AssemblyCatalogTests.cs`, `Tests/Editor/AlbumBalanceImportTests.cs`.

**Изменить:** `Application/ContentDatabaseAsset.cs`, `Configuration/Game/PlayerContent.asset` — явная ссылка на новый каталог, без перезаписи существующих боевых настроек.

**Интерфейсы и данные:**

- `ComponentDefinition`: `Id`, `Revision`, типизированная `Category`, `RequiredLevel`, `UnlockGold`, совместимые семейства/разъёмы и числовые модификаторы. Никаких игровых правил из русской строки `compatible` во время боя.
- `PartRef(string id, int revision)`; `FoundationRecipe` и `WeaponRecipe` содержат по пять именованных `PartRef`; `StructureRecipe` — четыре. `TowerRecipe` содержит основу и `WeaponMountRecipe[]` со стабильным `SlotIndex` 0–2.
- `AssemblyRecipe` — ровно один вариант `TowerRecipe` / `StructureRecipe`. `AlbumEntry` имеет ID, имя, ревизию, `Kind` (Tower / Structure / Legacy), `RecipeSnapshot` или `TowerBlueprint LegacyBlueprint`; смешивать два payload нельзя.
- `RecipeSnapshot`: рецепт, ревизия баланса, копии использованных определений. Массивы копируются, наружу выдаются read-only представления.
- `bool ComponentCatalog.TryGet(string id, int revision, out ComponentDefinition definition)` возвращает только точную пару ID/revision; false преобразуется валидатором в диагностику, без подстановки другой версии. `AssemblyCatalogAsset.CreateSnapshot()` возвращает независимый `ComponentCatalog`. `AlbumBalanceImport.Validate(string json)` возвращает `IReadOnlyList<AssemblyDiagnostic>`, `Import(string json, string assetPath)` — сохранённый `AssemblyCatalogAsset`; импорт блокируется при ошибках. Для первого импорта всех частей revision=1, версия баланса — `album-v1.0`; ID и ревизия графики не используются вместо версии баланса.
- `AssemblyDiagnostic`: стабильный `Code`, путь к части/креплению, аргументы для локализации; ошибки не привязаны к английскому/русскому UI-тексту.

- [x] Написать `AssemblyCatalogTests.ImportV1_HasCompleteFreeStarterSet`: `Assert.That(catalog.Definitions.Count, Is.EqualTo(54)); Assert.That(catalog.Starters.Count, Is.EqualTo(8));`. Каждая стартовая часть имеет цену открытия 0; категории 14/7/18/15. Duplicate ID+revision, пропущенная категория, отрицательная цена, NaN и неизвестная совместимость дают конкретную диагностику.
- [x] Запустить `AssemblyCatalogTests` и `AlbumBalanceImportTests` в EditMode, зафиксировать красный результат.
- [x] Реализовать модели и импорт JSON. В импортёре явно преобразовать текущие поля JSON в типы; ID не переименовывать. Предпросмотр импорта показывает добавления и изменения; повторный импорт тех же данных сохраняет GUID и результат. Неполный импорт не меняет asset.
- [x] Повторить оба класса: 0 failed; изменение исходного массива после `CreateSnapshot()` не меняет каталог. Восемь рецептов полностью ссылаются на существующие детали. Каталог остаётся выключенным для пользовательского маршрута до готовности зависимостей.
- [x] Зафиксировать: `feat: add versioned assembly catalog and balance import`.

## Задача 2. Единый расчёт и неизменяемый состав

**Новые:** `Core/Assembly/AssemblyResolver.cs`, `ResolvedEntry.cs`, `ResolvedWeapon.cs`, `AssemblyValidation.cs`, `LegacyAlbumAdapter.cs`; `Tests/EditMode/AssemblyResolverTests.cs`, `AssemblySnapshotTests.cs`.

**Изменить:** `Core/TowerCatalog.cs`, `ResolvedTowerAssembly.cs` — сохранить прежний путь для legacy; не менять его порядок модификаторов. `Application/WorkshopReadModel.cs` — подготовить показ результата нового расчёта.

**Интерфейсы:** `AssemblyResolution AssemblyResolver.Resolve(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance, MaterialTier material)`. Результат: `Valid`, `Diagnostics`, `Value: ResolvedEntry` (null при ошибке). `AssemblyResolution` определяется рядом с `ResolvedEntry`. `ResolvedEntry` хранит Kind, Cost, Health, Load, Capacity, BuildSeconds, Defense, Weapons и Support; `ResolvedWeapon` — точные параметры семейства, Damage, Range, MinimumRange, Interval, MagazineSize, ReloadSeconds, AcquisitionSeconds и лимиты эффекта. `Support: ResolvedSupport` хранит вид, радиус, скорость ремонта/замедления, доход, возврат и разрешённые материалы; этот небольшой тип находится в `ResolvedEntry.cs`. `LegacyAlbumAdapter.Resolve(AlbumEntry entry, GameBalance balance)` оставляет старые страницы на старом расчёте.

- [x] Написать `AssemblyResolverTests.StarterRecipes_MatchBalanceV1` с карандашом, без цвета/усилений: `Assert.That(towerCosts, Is.EqualTo(new[] {40,64,60,99}));`, HP каждой `55`, нагрузка `[4,8,6,9]`; сооружения cost/HP `[16/200,22/55,65/70,60/80]`. Для дробных значений `Within(0.0001f)`.
- [x] Добавить проверки: у базового пулемёта `Damage=12, Range=8, Interval=.42, MagazineSize=6, ReloadSeconds=.42`; короткий вариант `Cost=36, Damage=10.8, Range=6.4, Interval=.357`; длинный `Cost=52, Damage=13.2, Range=10, Interval=.504`. Очередь + барабан недопустимы. Перегрузка, отсутствующий первый ствол, неверная семья головки и четвёртое крепление отклоняются.
- [x] Запустить оба класса в EditMode, увидеть красный результат.
- [x] Реализовать порядок из спецификации: проверка → исходные значения → прибавки → произведение множителей → лимиты → материал/цвет/усиления. Кредит 16 уже учтён в цене механизма и повторно не вычитается. Расчёт геометрического clearance подключается задачей 3 отдельно от числового resolver.
- [x] Проверить перестановку модификаторов, повторное применение материала, смешанные оружия и `N=1`. `AssemblySnapshotTests`: изменение/удаление библиотечного рецепта либо каталога после создания snapshot не меняет сохранённый состав и цену. Все новые тесты и `SharedTowerProductionTests`, `CombatContractTests`, `BalanceBoundaryTests` проходят.
- [x] Зафиксировать: `feat: resolve composite recipes with stable snapshots`.

## Задача 3. Графический пилот и проверка стыков

**Новые:** `Core/Assembly/AssemblyFitCatalog.cs`, `AssemblyFitRules.cs`; `Presentation/Art/AssemblyVisualComposer.cs`; `ArtSource/Notebook/NewStyle/assembly-parts.source.json`; `Tests/Editor/AssemblyVisualPilotTests.cs`, `Tests/EditMode/AssemblyFitTests.cs`.

**Изменить:** `Presentation/Art/NotebookMountLayout.cs`, `NotebookSpriteCatalog.cs`, `Presentation/Rendering/NotebookTowerComposition.cs`, `Presentation/UI/WeaponAssemblyGraphic.cs`, `Editor/NotebookSpriteImportTools.cs`; `ArtSource/Notebook/NewStyle/sprites.source.json`, `animation.json`; сгенерированные `Resources/Art/NotebookMounts.json`, атлас и каталог спрайтов.

**Интерфейсы:** `AssemblyFitRules.Validate(AssemblyRecipe recipe, AssemblyFitCatalog fits)` возвращает `IReadOnlyList<AssemblyDiagnostic>`. Каталог хранит числовые sockets, bounds, clearance и диапазоны углов без Unity API. `AssemblyVisualComposer.Compose(RecipeSnapshot recipe, AssemblyVisualPose pose)` возвращает `AssemblyVisualComposition`: упорядоченные фрагменты, трансформации и anchors. Pose содержит углы каждого mount и отдачу; все UI/world-адаптеры используют один результат. Импорт метаданных выдаёт согласованные визуальные и Core-данные посадок.

- [x] Написать `AssemblyFitTests.IntersectingPair_ReportsClearance`: `Assert.That(issues.Select(x => x.Code), Does.Contain("assembly.clearance"));`. В `AssemblyVisualPilotTests` неизвестный socket/версия отклоняются; после trim и перепаковки точки attach/muzzle совпадают с исходными с допуском 0,5 исходного пикселя; раздельные углы двух стволов сохраняются. Текущая пересекающаяся пара должна быть воспроизводимо отклонена.
- [x] Запустить новые тесты в EditMode, зафиксировать красный результат.
- [x] Собрать пилот: обычная рамка, корпус front/back, бронепластины, штатные подача/поворот, два пулемёта со всеми пятью функциональными категориями. Согласовать опоры и зоны движения по контракту. Исправить исходную геометрию так, чтобы бесплатный «Двойной дозор» оставался допустимым; не скрывать пересечения масштабированием только в UI или изменением слоя.
- [x] Проверить весь разрешённый диапазон двух независимых поворотов и максимальную отдачу: консервативные swept bounds автоматически, пограничные позы визуально. Пройти `AssemblyVisualPilotTests`, `AssemblyFitTests`, `StandardMountLayoutTests`, `SplitMountMetadataTests`, `SplitGunMountTests`. Пригодность сборки включает и числовую, и геометрическую валидацию.
- [x] Сохранить сравнение одной сборки в карточке, крупном preview, альбоме, полигоне и бою через существующие визуальные адаптеры и тестовый snapshot; новые экраны для этой проверки ещё не требуются. Внешняя длина ствола не меняет симуляционную дальность; VFX начинается в согласованном muzzle. До прохождения этого шага не размножать графический подход на остальные семейства. Для последующих backend/UI-тестов использовать явные fixtures посадок; полное производственное покрытие fit/графики принимается в задаче 13.
- [x] Зафиксировать: `feat: validate assembly sockets and finish modular gun pilot`.

## Задача 4. Профиль v8 и миграция без потери страниц

**Новые:** `Core/Assembly/AssemblyProfileData.cs`, `ComponentLibraryEntry.cs`, `Core/ProfileMigrationV8.cs`; `Tests/EditMode/ProfileMigrationV8Tests.cs`; `Tests/PlayMode/AssemblyProfilePersistenceTests.cs`.

**Изменить:** `Core/WorkshopProfile.cs`, `ProfileLimits.cs`, `BlueprintData.cs`; `Application/WorkshopProfileStore.cs`, `WorkshopPersistence.cs`, `ProfileSaveCoordinator.cs`, `WorkshopReadModel.cs`; `Core/ContentCatalog.cs` / `ContentDefinitions.cs` в местах проверки startup slots.

**Интерфейсы:** расширить существующий `WorkshopProfileData` полями v8: XP, `AlbumEntryData[] Entries`, `ComponentLibraryEntryData[] Library`, `RunRewardReceiptData[] Rewards`, `OperationReceiptData[] Operations` и 8 строк Loadout; DTO определить в `AssemblyProfileData.cs` уже на этом этапе. GUID в JSON хранится строкой формата N. Receipt награды содержит runId, число выданных волн и флаг бонуса; receipt команды — operationId, тип и ID результата. `ProfileMigrationResult ProfileMigrationV8.Migrate(WorkshopProfileData source, ComponentCatalog catalog)` возвращает Success, Candidate типа WorkshopProfile и Diagnostics; тип результата определить в `ProfileMigrationV8.cs`. В `WorkshopProfile` добавить read-only `Entries`, `Library`, `TotalXp`; старый tower-only API временно оставить адаптером для ещё не перенесённых экранов. `ComponentLibraryEntry` хранит ID, revision, kind Foundation/Weapon и `ComponentRecipeSnapshot`: ровно один FoundationRecipe/WeaponRecipe плюс копии определений и ревизия баланса. Этот snapshot определяется в том же файле и отличается от snapshot целой страницы задачи 1.

- [x] Написать `ProfileMigrationV8Tests.V7_PreservesFourSlotsAndAddsFourEmpty`: `Assert.That(candidate.Loadout.Count, Is.EqualTo(8)); Assert.That(candidate.Loadout.Skip(4), Is.All.Null);`. Первые четыре ID, Gold и права сохраняются, XP=0. v1–v7 читаются историческими правилами; прежняя длина 4 не берётся из новой константы 8.
- [x] Добавить случаи: 64 страницы сохраняются без вытеснения; новые бесплатные сооружения можно импортировать после освобождения места. Стандартные страницы переходят в штатные рецепты только при совпадении их прежних параметров; при несовпадении сохраняются legacy. Пользовательские формы — read-only legacy, без распознавания линий. Неизвестный ID/будущая версия не подменяются стандартом.
- [x] Запустить новые классы в EditMode / PlayMode и увидеть красный результат.
- [x] Реализовать reader→candidate→валидация→backup→атомарная запись v8. Новая установка получает 8 бесплатных страниц и 8 выбранных ID; миграция оставляет новые слоты пустыми. Старый DTO и enum не перенумеровывать. Библиотечные ссылки в сохранённой странице дополняются собственным snapshot, а не становятся единственным источником данных.
- [x] Проверить сбой записи/обрыв после создания backup, повторное чтение v8, удалённую библиотечную запись, неизвестную ревизию и будущее сохранение: нет silent reset, есть режим восстановления. Пройти `AssemblyProfilePersistenceTests`, `WorkshopStoreTests`, `DataDrivenProfilePersistenceTests`, `StandardWorkshopPersistenceTests`, `BootstrapProfileIsolationTests` на временных путях.
- [x] Зафиксировать: `feat: migrate workshop profiles to version eight`.

## Задача 5. Команды альбома, библиотеки и покупки

**Новые:** `Core/MetaProgression.cs`, `Core/Assembly/RunLoadoutSnapshot.cs`; `Application/AssemblyCommands.cs`, `AssemblyCommandResult.cs`; `Tests/EditMode/MetaProgressionTests.cs`; `Tests/PlayMode/AssemblyTransactionTests.cs`, `AssemblyLoadoutTests.cs`.

**Изменить:** `Core/WorkshopProfile.cs`; `Application/RunController.cs`, `WorkshopReadModel.cs`, `ProfileSaveCoordinator.cs`.

**Интерфейсы:** `int MetaProgression.LevelForXp(int totalXp)`; в `AssemblyCommands`: `PurchasePart(string partId, Guid operationId)`, `SaveEntry(AlbumEntry entry, Guid operationId)`, `SaveLibraryEntry(ComponentLibraryEntry entry, Guid operationId)`, `DeleteEntry(string entryId, Guid operationId)`, `AssignSlot(int slotIndex, string entryId, Guid operationId)` возвращают `AssemblyCommandResult` с Success, Code, Diagnostics. Команды работают через владельца текущего профиля и существующий `SaveCandidate`, публикуют candidate только после успеха; operation receipt сохраняется в том же candidate, повтор возвращает тот же результат, включая ID варианта. `WorkshopReadModel` предоставляет `Entries`, `LoadoutEntries`, `Gold`, `TotalXp`, `Level` и статус записи. `RunController.StartRun(out string reason)` сохраняет сигнатуру, но фиксирует `RunLoadoutSnapshot` из восьми разрешённых страниц и снимка баланса.

- [ ] Написать `MetaProgressionTests` для границ: XP 99→L1, 100→L2, 389→L3, 390→L4, 1329→L7, 1330→L8; отрицательное значение отклоняется, превышение последнего порога остаётся L8.
- [ ] Написать `AssemblyTransactionTests.BuyLongBarrelTwice_ChargesOnce`: L4/94 золота/длинный ствол за 80 → `Assert.That(profile.Gold, Is.EqualTo(14)); Assert.That(profile.IsUnlocked("head.long"), Is.True);` после обоих вызовов. Новая операция для уже купленного права тоже оставляет 14. L3/94 и L4/79 не покупают. IO failure сохраняет 94, старые права, альбом, слоты и черновик. Сохранение и удаление не публикуются до commit.
- [ ] Запустить три новых класса, зафиксировать красный результат.
- [ ] Реализовать команды, проверяя права, числовую и геометрическую совместимость задач 2–3. Перенос одного ID между слотами очищает старое назначение в том же candidate; удаление очищает затронутые слоты только после успешной записи. «Сохранить как вариант» создаёт новый ID, правка создаёт новую ревизию. Применение библиотечной ревизии к странице — отдельное явное действие.
- [ ] `AssemblyLoadoutTests`: 8 разных ID, частично пустой набор допустим; только поддержка/промокашка не запускают бой; одна атакующая башня запускает. Изменение страницы или каталога после StartRun не меняет цену/характеристики объектов этой партии. Повтор после IO не теряет черновик и не повторяет списание. Пройти `WorkshopSaveFailureTests`, `RunControllerTests`, `WorkshopPersistenceTests`.
- [ ] Зафиксировать: `feat: add atomic album commands and level gated purchases`.

## Задача 6. Обоймы, очереди и семь боевых семейств

**Новые:** `Core/WeaponCycleState.cs`, `WeaponCycleRules.cs`; `Tests/EditMode/WeaponCycleTests.cs`, `CompositeWeaponCombatTests.cs`; `Tests/PlayMode/CompositeWeaponIntegrationTests.cs`.

**Изменить:** `Core/WeaponSystem.cs`, `GameTypes.cs`, `ReadModel.cs`, `SimulationEvents.cs`, `TrialRunner.cs`; при необходимости выделить существующие операции попадания в небольшой helper без второй симуляции.

**Интерфейс:** `WeaponCycleResult WeaponCycleRules.Step(WeaponCycleState state, ResolvedWeapon weapon, float delta, bool targetAcquired, float feed)`. State хранит оставшиеся патроны и нормализованный остаток интервала/перезарядки отдельно на каждом mount. Result содержит моменты готовых выстрелов внутри шага и остаток состояния. `delta=0` ничего не продвигает; отсутствие цели позволяет закончить перезарядку, но не накапливает очередь отложенных попаданий.

- [ ] Написать `WeaponCycleTests.StandardMagazine_RepeatsAt252Seconds`: после захвата при t=0 первый выстрел; для N=6/.42/.42 `Assert.That(shotTimes[6], Is.EqualTo(2.52f).Within(.0001f));`. N=3/.189/.882 — следующий цикл в 1.26 с; N=12/.357/1.26 — в 5.187 с. Для N=1 следующий выстрел через ReloadSeconds, без дополнительной задержки Interval.
- [ ] Добавить тесты потери цели после последнего патрона, смены цели, паузы, восстановления на пустом поле, двух/трёх креплений, смены количества активных стволов. Один шаг 1.26 с и эквивалентные мелкие шаги дают одинаковую последовательность при постоянной захваченной цели. Feed учитывается ровно один раз, включая смену магазина; бесконечные/отрицательные времена отклоняются.
- [ ] Запустить три новых класса, увидеть красный результат.
- [ ] Подключить `ResolvedWeapon` к существующим targeting/hit-операциям. Сохранить legacy-ветку старого cooldown. Новый цикл общий для боя и полигона, независим на каждом креплении; завершённое действие не продвигается повторно в `ReloadWithoutTargets`.
- [ ] Проверить семейства: пробой — максимум 7 разных целей; веер — 90° и максимум 8; рикошет — 4 разные цели, скачок до 3; мортира — минимум 6, полёт .8 к фиксированной точке, радиус 2.4 без искусственного лимита 8; промокашка — 0 урона и очистка. Slow не перемножается, скорость не ниже .4. Пройти существующие `MountReadinessTests`, `IndependentMountAimTests`, `SharedTowerProductionTests` и тесты семи семейств.
- [ ] Зафиксировать: `feat: simulate composite weapon magazines and bursts`.

## Задача 7. Общая постройка, баррикада, замедление и ремонт

**Новые:** `Core/Assembly/StructureSupportRules.cs`; `Tests/EditMode/CompositeConstructionTests.cs`, `CompositeSupportTests.cs`; `Tests/PlayMode/CompositeConstructionIntegrationTests.cs`.

**Изменить:** `Core/GameSession.Construction.cs`, `GameSession.Support.cs`, `GameSession.Objectives.cs`, `GameTypes.cs`, `ReadModel.cs`; `Application/RunController.cs`, `ConstructionGesture.cs`.

**Интерфейсы:** `GameSession.QuoteEntry(IReadOnlyList<Cell> cells, ResolvedEntry entry, MaterialTier material, InkColor color)` → существующий `BuildQuote`; `TryBuildEntry(..., out string reason)` → bool с теми же входами. `RunController.QuoteEntry(..., int slot, ...)` / `BuildEntry(..., int slot, ..., out string reason)` берут только снимок начатого набора. Повторные экземпляры расходуют штрихи, а не купленные детали.

- [ ] Написать `CompositeConstructionTests.EightStarters_Cost426Ink`: после размещения по одному на допустимых клетках `Assert.That(session.Ink, Is.EqualTo(214));` из начальных 640. Строительство по слоту башни и сооружения использует ровно их resolved-цену; пакет занятых/недопустимых клеток или нехватка ресурса не списывает часть стоимости. Материал меняет стоимость/HP/постройку по текущим правилам.
- [ ] Написать `CompositeSupportTests`: ремонт 6 HP/с на дальности 5 выбирает одну ближайшую повреждённую постройку; при равенстве — меньший ID. Не лечит себя/другие посты. Два поста на одной цели дают max(rate), а не сумму; HP не превышает максимум. Замедлитель радиуса 1 даёт скорость .6; повторный эффект не перемножается.
- [ ] Запустить три новых класса, увидеть красный результат.
- [ ] Подключить составные объекты к занятости клеток, урону, уничтожению, продаже, навигации и статистике. Баррикада блокирует и разрушается по действующим правилам пути, замедлитель остаётся проходимым. Для переднего щита .4/90° направление берётся у основы, а не у каждого оружия; общая защита ограничена .6.
- [ ] Проверить material/color/усиления без влияния материала на скорость ремонта; ошибка установки не создаёт entity и не расходует ресурс. Новые пользовательские команды не дают строить отдельные бесплатные tools вне набора. Пройти `ConstructionRulesIntegrationTests`, `ConstructionBoundaryTests`, `NavigationTests`, `ArmorCombatIntegrationTests`.
- [ ] Зафиксировать: `feat: build composite towers and support from album slots`.

## Задача 8. Генераторы и надёжные награды

**Новые:** `Core/GeneratorWaveLedger.cs`, `RunRewardReceipt.cs`; `Application/RunRewardService.cs`; `Tests/EditMode/GeneratorWaveTests.cs`; `Tests/PlayMode/AssemblyRewardTests.cs`.

**Изменить:** `Core/GameSession.Progression.cs`, `GameSession.Objectives.cs`, `GameSession.Construction.cs`, `WorkshopProfile.cs`; `Application/RunController.cs`, `ProfileSaveCoordinator.cs`, DTO задачи 4.

**Интерфейсы:** `GeneratorWaveLedger.BeginWave(Guid runId, int waveIndex, IReadOnlyList<StructureState> structures)` фиксирует завершённые живые генераторы; `CompleteWave(...)` возвращает доход штрихов с дедупликацией по run/wave/entity. `RunRewardService.Apply(Guid runId, int completedWaves, bool victory)` принимает накопительный итог, возвращает `AssemblyCommandResult`; постоянный receipt хранит уже выданные волны и бонус победы в одной транзакции с Gold/XP. Новая попытка получает новый runId.

- [ ] Написать `GeneratorWaveTests`: обычный генератор 60/80 HP даёт 12, экономичный 72/68 HP — 15; оба окупаются за 5 полностью пережитых волн. Построенный во время волны, незавершённый к её началу, проданный или уничтоженный до конца не оплачивается; повтор события даёт 0. Третий генератор любого варианта отклоняется; после уничтожения одного можно построить новый. Pen/Marker недопустимы, refund=0.
- [ ] Написать `AssemblyRewardTests.RepeatedVictory_GrantsOnce`: при исходных 0/0 после двух применений одной победы `Assert.That(profile.Gold, Is.EqualTo(94)); Assert.That(profile.TotalXp, Is.EqualTo(120));`. Пять волн + поражение/выход дают 40/50; выход во время шестой волны оставляет 40/50. Повтор открытия результата, reload профиля и возврат в меню не удваивают награду. Полигон не меняет золото, XP и receipts.
- [ ] Запустить два класса в соответствующих режимах, увидеть красный результат.
- [ ] Реализовать ledger генератора и сервис метанаград вместо накопления только `depositedGold`. Награда и receipt сохраняются вместе; при IO failure кандидат остаётся ожидающим повторной записи с видимым статусом. Повтор использует тот же runId, а переход в меню не очищает ожидающее начисление. Не обещать восстановление произвольного боя после аварийного выключения: сохранение mid-run не входит в этот план.
- [ ] Проверить повтор сохранения после ошибки, отсутствие gold/XP от генераторов, отсутствие дохода на завершении несыгранной волны, паузу и restart. Отдельно: неудачная запись награды → покупка → повтор награды не теряет покупку; операции выполняются последовательно над последним опубликованным профилем, ожидающая награда хранит дельту/receipt, а не перезаписывает профиль устаревшей копией. Материал и межволновые бонусы не повышают генераторный доход/возврат. Пройти `GoldPayoutTests`, `FiniteEconomyTests`, `WorkshopSaveFailureTests`.
- [ ] Зафиксировать: `feat: add wave generators and idempotent gold xp rewards`.

## Задача 9. Мастерская: состав, открытия и библиотека

**Новые:** `Application/WorkshopDraftSession.cs`; `Presentation/UI/ComponentAssemblyPanel.cs`, `ComponentCatalogPanel.cs`, `StructureAssemblyPanel.cs`; `Tests/PlayMode/CompositeWorkshopTests.cs`, `ComponentPurchaseUiTests.cs`.

**Изменить:** `WorkshopWizard.cs`, `WorkshopHud.cs`, `WizardAssemblyPanel.cs`, `WizardFoundationPanel.cs`, `WizardWeaponPanel.cs`, `WorkshopResponsiveLayout.cs`, `WorkshopConfirm.cs`, `WorkshopModal.cs`, `Resources/Text/ru.asset`.

**Интерфейсы:** `WorkshopDraftSession.Begin(AlbumEntry source)`, `OpenFoundation()`, `OpenWeapon(int mount)`, `OpenStructure()`, `SelectPart(string id)`, `CancelComponent()`, `SaveComponent(Guid operationId)`; DraftSession владеет копией черновика и вызывает `AssemblyCommands`. `SaveComponent` возвращает `AssemblyCommandResult`; родитель меняется только после успешного «Сохранить и выбрать». Для показа использовать результаты resolver, диагностики, права и fit-проверку; не считать DPS внутри панели.

- [ ] Написать `CompositeWorkshopTests.CancelNestedEdit_PreservesParent`: `Assert.That(parentAfterCancel, Is.EqualTo(parentBeforeEdit));` для неизменяемого snapshot. Оба маршрута доходят до проверки; вложенный редактор основы/оружия имеет пять категорий, сооружение четыре. Успешное Save and Select меняет родителя, IO failure оставляет вложенный черновик. Имя 48 символов принимается, 49 не сохраняется, 65-я страница не добавляется.
- [ ] Написать `ComponentPurchaseUiTests`: заблокированная деталь показывает условия и не устанавливается; покупка L4/94→14 отображается во всех совместимых каталогах; приобретение не подменяет установленную часть. Очередь+барабан предлагает штатный магазин; перегрузка показывает вклад частей и блокирует Save/Trial.
- [ ] Запустить оба класса в PlayMode, увидеть красный результат.
- [ ] Перенести экраны **04–08, 11–14, 25–27, 29–30**. Оставить готовые варианты основы/оружия и кнопку раскрытия состава; экран 06 теперь конструктор оружия. Мортира/веер/броня показывают результат выбранных частей, без интерактивного контура. Каталог фильтрует совместимость и отдельно показывает недоступные детали.
- [ ] Пройти новые тесты, `StandardWorkshopFlowTests`, `WorkshopWizardTests`, `WorkshopSaveFailureTests` с обновлёнными ожиданиями нового маршрута. Снять 1024×768 и 1920×1080: объект, состав, нагрузка, цена и основные действия читаемы одновременно; скролл каталога не скрывает подтверждение. Ошибки сохранения не закрывают черновик.
- [ ] Зафиксировать: `feat: build composite workshop and component library screens`.

## Задача 10. Альбом, главное меню, карта и восемь слотов подготовки

**Новые тесты:** `Tests/PlayMode/AlbumUiTests.cs`, `EightSlotPreparationTests.cs`.

**Изменить:** `Presentation/UI/WizardAlbumPanel.cs`, `WorkshopHud.cs`; `Presentation/Player/PlayerShell.Home.cs`, `.Map.cs`, `.Preparation.cs`, `PlayerShell.cs`, `PlayerLayout.cs`, `PlayerWorkshopActions.cs`, `PlayerGameRoot.cs`; `Resources/Text/ru.asset`.

**Интерфейс:** оболочка читает `WorkshopReadModel.Entries`, `LoadoutEntries`, `Gold`, `TotalXp`, `Level` и результаты команд задачи 5. Вызовы назначения используют индекс 0–7; после сохранения список обновляется по ID, а не по позиции отфильтрованной карточки.

- [ ] Написать `AlbumUiTests`: фильтры Все/Башни/Сооружения/Стандартные/Мои, сохранение и удаление варианта, пустой фильтр «Мои» при доступных стандартах. Восстановление стартового набора не меняет золото и не перезаписывает чужие страницы; при полном альбоме показывает нужное свободное место.
- [ ] Написать `EightSlotPreparationTests.At1024x768_AllEightSlotsVisible`: `Assert.That(visibleSlotCount, Is.EqualTo(8));`. Все восемь назначаемы, повторный ID переносится после подтверждения, поддержка не обходит проверку атакующего состава, пустые слоты не сдвигают индексы остальных. Длинное имя не перекрывает кнопки. Запустить оба класса в PlayMode, увидеть красный результат.
- [ ] Перенести **01–03, 09, 24**: главное меню, карту первого рубежа, подготовку, альбом и пустое состояние. Исправить ссылки входа/возврата в мастерскую; показать уровень/XP там, где указано эскизами. При 4:3 — сетка 4×2, на широком экране 8×1 или 4×2; главное меню, карта и основной набор подготовки без прокрутки.
- [ ] Пройти новые тесты, `PlayerJourneyTests`, `PlayerLayoutTests`, `WorkshopIntegrationTests`. Сохранить сравнение с эскизами на 1024×768, 1280×720, 1920×1080. Проверить клавиатурный фокус и возвращение на ту же страницу после отмены/сохранения.
- [ ] Зафиксировать: `feat: update album home map and eight slot preparation`.

## Задача 11. Боевой HUD, пауза, результаты, настройки и помощь

**Новые тесты:** `Tests/PlayMode/EightSlotBattleTests.cs`, `AlbumPlayerJourneyTests.cs`.

**Изменить:** `Presentation/Input/DrawingInput.cs`; `Presentation/Player/PlayerBattleHud.cs`, `PlayerGameRoot.cs`, `PlayerShell.cs`, `PlayerShell.Dialogs.cs`, `.Settings.cs`, `.Notebook.cs`; при необходимости вынести результат в новый `PlayerShell.Result.cs`; `Resources/Text/ru.asset`.

**Интерфейс:** клавиши 1–8 и карточки вызывают общий выбор `slotIndex` → команды `QuoteEntry/BuildEntry` задачи 7; инспектор читает resolved-снимок построенного объекта и состояние обоймы из `IRunReadModel`. Результат отображает receipt/статус сохранения задачи 8, не начисляет награду сам.

- [ ] Написать `EightSlotBattleTests.DigitEight_SelectsSlotSeven`: `Assert.That(selectedSlotIndex, Is.EqualTo(7));` после клавиши 8. Клавиши 5–8 больше не вызывают старые tools. Пустой слот не строит. Ввод в поле имени или открытый modal не строит на поле. Материал/цвет/снос имеют отдельные действия, preview и фактическая цена одинаковы.
- [ ] Написать `AlbumPlayerJourneyTests`: новый профиль → меню → карта → подготовка → мастерская → альбом → бой → пауза → настройки → продолжить → результат → меню → повторное открытие профиля. Проверить 94/120 для победы и 40/50 после пяти волн; отказ от выхода продолжает прежний бой, подтверждение сохраняет награды завершённых волн.
- [ ] Запустить два класса в PlayMode, увидеть красный результат.
- [ ] Перенести **15–23, 28**: расстановку, активную волну, выбранный объект, паузу, три бонуса после волн 2/4/6, победу, поражение, настройки, помощь, подтверждение выхода. Для генератора показать 0 возврата, доход и число учитываемых волн; материал только карандаш. Для ремонта показать одну цель и HP/с. Для оружия — обойму и смену магазина.
- [ ] Пройти новые тесты, `ToolPriceConsistencyTests`, `PlayerDisplaySettingsTests`, `NativeInputIsolation`-сценарии и `PlayerJourneyTests`. На 1024×768 доступны восемь карточек, выбранный объект и старт волны; модальные окна блокируют фон. Тексты помощи описывают сборку и клавиши 1–8. Снять все перечисленные экраны, включая обе ветки результата.
- [ ] Зафиксировать: `feat: update battle hud results settings and help for albums`.

## Задача 12. Общий полигон для башен и сооружений

**Новые:** `Core/AssemblyTrialRunner.cs`; `Tests/EditMode/AssemblyTrialTests.cs`; `Tests/PlayMode/AssemblyTrialUiTests.cs`.

**Изменить:** `Core/TrialRunner.cs`, `TrialScenarioDefinition.cs`; `Application/RunController.cs`; `Presentation/UI/WizardTrialPanel.cs`; `Presentation/Rendering/NotebookTrialRenderer.cs`, `NotebookTrialPlayback.cs`.

**Интерфейс:** `AssemblyTrialReport AssemblyTrialRunner.Run(AlbumEntry entry, GameBalance balance, AssemblyTrialScenario scenario, int budget = 400, int seed = 1337)`. Scenario задаёт Crowd/Fast/Armored/Blots/Repair/Generator; Report содержит бюджет, seed, длительность, цену, число объектов и применимые метрики. Tower-ветка переиспользует производственный `WeaponSystem`, support-ветка — те же правила задач 7–8. Trial не содержит store профиля.

- [ ] Написать `AssemblyTrialTests`: мастерская/полигон/бой дают одинаковые параметры и последовательность выстрелов на одинаковом входе. Бюджет 400 включает только целое допустимое количество построек; у ремонта измеряется фактический HP, у генератора — доход и число полных волн, у промокашки — очистка без фиктивного DPS.
- [ ] Написать `AssemblyTrialUiTests.GeneratorTrial_DoesNotAwardProfile`: `Assert.That(after.Gold, Is.EqualTo(before.Gold)); Assert.That(after.TotalXp, Is.EqualTo(before.TotalXp));`, receipts тоже равны. Invalid/locked/overload/clearance не запускаются; pause/restart не меняют альбом; до запуска отсутствуют выдуманные результаты. Запустить оба класса, увидеть красный результат.
- [ ] Перенести **10**, подключить запуск из проверки башни/сооружения и возврат в тот же черновик. Подписать бюджет/сценарий, не смешивать теоретический DPS и измеренный урон. Ускоренные волны генератора остаются локальным сценарием пробы.
- [ ] Пройти новые тесты, `TrialBalanceTests`, `TrialBalanceIntegrationTests`, `ExpandedTrialHudTests`, сравнить один рецепт в бою/пробе/preview. Зафиксировать отчёт для одиночной, линейной, плотной и защищённой цели.
- [ ] Зафиксировать: `feat: add shared assembly trials for towers and support`.

## Задача 13. Полный графический набор и визуальная приёмка

**Изменить:** исходники и метаданные задачи 3, `Resources/Art/`, `Presentation/Art/`, адаптеры UI/world, `Editor/FullGameUiCapture.cs`, `Editor/PlayerVisualCapture.cs`. **Новые:** `Tests/Editor/AssemblyArtCoverageTests.cs`; отчёт `docs/validation/modular-album/visual-acceptance.md` и снимки сравнения.

**Контракт:** вся видимая деталь из 54 определений имеет asset mapping нужной ревизии либо явно нейтральное отображение («Без брони», «Без оснащения»). Функциональная категория не обязана состоять из одного спрайта: front/back и неподвижные опоры — части художественного дерева. Стык, масштаб линии и узнаваемость семейства сохраняются.

- [ ] Написать `AssemblyArtCoverageTests.AllDefinitions_HaveVisualMapping`: `Assert.That(missingDefinitionIds, Is.Empty);` на всех 54 определениях. Anchors/muzzle обязательны для нужных семейств; нет отсутствующих спрайтов, дублированных socket ID, неизвестных ревизий и порядка слоёв, зависящего от порядка объектов UI. Повторный импорт сохраняет GUID и трансформации.
- [ ] Запустить класс в EditMode, подтвердить недостающие mappings как красный результат.
- [ ] После принятого пилота расширить графику на четыре рамки, варианты корпуса/брони/подачи/поворота, все семь механизмов, головки/стволы/прицелы/магазины/спуски и четыре семейства сооружений. Использовать листы **31–33**, а не выдавать крупные концепты за уже нарезанные игровые ассеты.
- [ ] Пройти `AssemblyArtCoverageTests`, `AssemblyVisualPilotTests`, `ModularTowerAssetTests`, `NotebookSpriteImportTests`, `NotebookSpriteRenderingTests`. Проверить разрешённые сочетания и крайние позы, включая батарею с тремя стволами; несовместимость видна до сохранения. Перепаковка атласа не меняет не затронутые картинки.
- [ ] Снять **все 30 экранов** по матрице ниже на 1024×768, 1280×720, 1920×1080; дополнительно проверить layout на 1280×960 и 1600×900. Для каждого записать pass/fail, путь снимка и остаточный дефект. Не объявлять весь интерфейс готовым по трём выбранным экранам. Исправить читаемость, пересечения, пустые места и живые числовые подписи.
- [ ] Зафиксировать: `art: complete modular catalog and full screen acceptance`.

## Задача 14. Измеренный баланс и синхронизация Excel

**Новые:** `Validation/AssemblyBalanceScenario.cs`, `Tests/Editor/AssemblyBalanceAcceptanceTests.cs`; `docs/validation/modular-album/balance-report.md` и машинный отчёт прогонов.

**Изменить:** `Editor/BalanceWorkbookExport.cs`, `Editor/AlbumBalanceImport.cs`; `Tools/AlbumDesign2026/build-data.mjs`, `build-workbook.mjs`, `verify-balance.mjs`; `docs/balance/`, `Configuration/Game/PlayerAssemblyCatalog.asset` и связанные документы при изменении чисел.

**Интерфейс:** `AssemblyBalanceScenario.Run(int seed, int budget, TrialScenario scenario)` возвращает записи по рецептам: стоимость, фактический урон, пропуски, HP замка, контроль, ремонт, доход и чистый ресурс. Экспорт runtime явно указывает PlayerBalance → PlayerRules → PlayerContent и ревизию AssemblyCatalog; исторические presets не подменяют текущую игру.

- [ ] Написать `AssemblyBalanceAcceptanceTests.RuntimeExport_MatchesDesignData`: `Assert.That(numericDifferences, Is.Empty);`. Импорт/экспорт сохраняет 54 ID, 8 стартовых рецептов и результаты resolver; карандашный старт стоит 426; повтор одинакового сценария/seed даёт одинаковый отчёт. Убедиться, что намеренное изменение одного входа обнаруживается проверкой рассинхронизации runtime и JSON.
- [ ] Запустить новые тесты и `node Tools/AlbumDesign2026/verify-balance.mjs`; сначала подтвердить обнаружение контролируемого несоответствия, затем вернуть фикстуру. Не менять реальные игровые данные ради отрицательного теста.
- [ ] Прогнать бюджеты **200/400/800**, seeds **1337/2026/4242**, сценарии быстрых/плотных/защищённых целей; отдельно смешанные башни и полную карту из восьми волн с 0/1/2 генераторами и ремонтом. Записать начальные условия, состав, размещение, число объектов и длительность; сравнивать варианты в одинаковых условиях.
- [ ] Проверить отсутствие бесплатного повторного дохода/ремонта, бессмысленных покупок и универсального превосходства. Если вариант не хуже по всем измеренным показателям и лучше хотя бы по одному во всех сопоставимых сценариях — переработать цену/недостаток и повторить затронутые прогоны. Порог полезности для роли объяснить в отчёте, не выводить баланс только из DPS.
- [ ] При корректировках обновить единственный исходник `build-data.mjs`, пересобрать JSON/Excel и импортировать валидированный каталог; поднять баланс-ревизию. Запустить `build-data.mjs`, `build-workbook.mjs`, `verify-balance.mjs`, новые Unity-тесты и затронутые игровые сценарии. Синхронизировать числовые подписи прототипа/эскизов, если они изменились. Книга — 11 листов, без ошибок формул; значения runtime и независимого расчёта совпадают.
- [ ] Зафиксировать: `balance: validate composite catalog in production scenarios`.

## Задача 15. Удаление старого редактора и выпуск проверенной сборки

**Проверить перед удалением:** `Presentation/UI/DrawingEditorPanel.cs`, `DrawingCanvasGraphic.cs`, `DrawingStencilBrowser.cs`, `DrawingWorkbenchUi.cs`, `GuidedDrawingPanel.cs`, `MortarFootprintPanel.cs`, `FanVolleyPanel.cs`, `ArmorReinforcementPanel.cs` и относящиеся к ним graphics/layout/helpers. Это кандидаты, удалять только после проверки фактических references. `DrawingInput.cs` сейчас отвечает и за строительство, поэтому целиком его не удалять по имени.

**Изменить:** оставшиеся вызовы в Presentation, editor menu/capture tools, `Resources/Text/ru.asset`, README, [реестр механик](../../Current-Mechanics.md), [руководство разработки](../../Development-Guide.md), [документ перехода](../../Modular-Transition.md). **Новый:** `Tests/PlayMode/AlbumReleaseAcceptanceTests.cs`.

- [ ] Написать `AlbumReleaseAcceptanceTests.NewJourney_HasNoDrawingEditor`: `Assert.That(reachableDrawingEditors, Is.Empty);`. Из любого пользовательского входа нельзя открыть холст/контур/рисование брони; новый маршрут работает без retired-компонентов; исторические профили читаются; основной scene bootstrap не ссылается на отсутствующие скрипты. Проверка должна обнаруживать любой оставшийся старый вход до удаления.
- [ ] Найти GUID и текстовые references кандидатов в сценах, prefab, assets, editor tools и tests. Сначала отвязать UI, затем удалить неиспользуемые файлы вместе с `.meta`. Тесты отменённого UI заменить проверками нового маршрута; тесты чтения исторических сохранений сохранить. `Drawing`, `Freeform`, геометрические DTO и resolver legacy удалять только при доказанном отсутствии зависимости reader; наличие этих слов само по себе не дефект.
- [ ] Выполнить полный **EditMode** (`PaperDefense.Core.Tests`, `PaperDefense.Editor.Tests`) и **PlayMode** (`PaperDefense.Integration.Tests`), 0 failed; сохранить XML. Старые totals 939/66 не являются требуемым числом новых тестов. Запустить `git diff --check`, проверку локальных ссылок и соответствия баланса; исторический `verify-delivery.mjs` содержит hashes старого кода, его запрет изменений не является критерием новой реализации.
- [ ] Собрать Windows x64 через **Paper Defense → Game → Build player game** (`PaperDefense.Editor.PlayerGameSceneTools.Build`). Проверить `Builds/PlayerGame/build-report.txt`, запустить exe с `-paper-memory-only` и пройти полный маршрут. Отдельно проверить перезапуск с временным тестовым store: каталог, покупка, библиотека, 8 слотов, Gold/XP сохраняются. Ни один тест не пишет в личный профиль.
- [ ] Снять CPU/GPU/GC в одинаковом боевом сценарии до/после на том же устройстве и настройках; проверить продолжительную серию меню → мастерская → полигон → бой без накопления объектов/подписок. Зафиксировать реальные значения и регрессии, не объявлять FPS по unit-тестам. Исправить блокирующие ошибки и повторить только затронутые проверки плюс финальный smoke.
- [ ] Обновить фактический статус в документации, итоговый отчёт со ссылками на тесты/снимки/build и галочки этого плана. Зафиксировать: `refactor: retire drawing editor and accept modular album release`.

## Матрица переноса всех эскизов

Источник — [catalog.js полной галереи](../../design/game-concept-art-2026-10-16/catalog.js). Старое имя файла `06-drawing` не означает сохранение механики рисования.

| № / ID | Что переносится в Unity | Задача | Главная проверка |
|---|---|---|---|
| 01-home | Главное меню | 10 | Действующие входы в альбом, мастерскую, игру, настройки, помощь |
| 02-map | Карта | 10 | Первый рубеж, 8 волн, возврат и подготовка |
| 03-preparation | Набор перед боем | 10 | 8 общих слотов, атакующая башня, 4×2 на 4:3 |
| 04-foundation | Готовые основы | 9 | 2/2/1/3 крепления, вместимость 10/14/16/22 |
| 05-weapons | Оружие и крепления | 9 | Независимый выбор каждого mount, раскрытие состава |
| 06-drawing | Состав оружия | 9 | Пять категорий, отсутствие холста |
| 07-details | Сравнение деталей | 9 | Дельты из resolver, сравнение штатного/длинного ствола |
| 08-review | Проверка и сохранение | 9 | Полный допустимый состав, имя, snapshot и успешный commit |
| 09-album | Общий альбом | 10 | Башни и сооружения, фильтры, назначение по ID |
| 10-trial | Полигон | 12 | Общая симуляция, подписанные сценарий/бюджет, без наград |
| 11-stencils | Каталог деталей | 9 | Уровень + золото, покупка отдельно от установки |
| 12-mortar | Ствол мортиры | 9 | Фиксированная область, без редактирования контура |
| 13-fan | Готовый веер | 9 | 90°, до 8 целей, без ручных лучей |
| 14-armor | Комплекты брони | 9 | Готовые части, общая/направленная защита раздельно |
| 15-planning | Расстановка | 11 | Постройка из восьми слотов, корректная цена |
| 16-battle | Активная волна | 11 | Общие слоты, ресурс, волна и состояние объектов |
| 17-tower | Выбранный объект | 11 | Реальные HP/обойма/нагрузка/приоритет либо данные поддержки |
| 18-pause | Пауза | 11 | Симуляция стоит, фон не принимает команды |
| 19-upgrades | Межволновой бонус | 11 | Один выбор после волн 2/4/6, исключения генератора |
| 20-victory | Победа | 11 | 94 золота / 120 XP, повтор не начисляет заново |
| 21-defeat | Поражение | 11 | За 5 волн — 40/50, без бонуса победы |
| 22-settings | Настройки | 11 | Полный экран/радиусы, корректный возврат |
| 23-help | Помощь | 11 | Новый игровой цикл и управление 1–8 |
| 24-empty | Пустой фильтр | 10 | «Мои» пусто, бесплатные стандарты доступны |
| 25-purchase | Покупка | 9 | Условия, одно списание, 94−80=14 |
| 26-save-error | Ошибка IO | 9 | Черновик остаётся, успешный повтор без дублей |
| 27-invalid | Несовместимость | 9 | Очередь+барабан, понятная причина и исправление |
| 28-leave | Выход из партии | 11 | Награда только за завершённые волны |
| 29-structure | Состав сооружения | 9 | Четыре семейства и четыре категории |
| 30-foundation-parts | Состав основы | 9 | Пять категорий, библиотека, возврат к родителю |
| 31-towers-art | Башни и основы | 3, 13 | Стыки, броня, все разрешённые повороты |
| 32-weapons-art | Семь семейств и части | 6, 13 | Внешний состав соответствует реальному поведению |
| 33-structures-art | Четыре сооружения | 7, 8, 13 | Различимые функции и совместимые составные части |

## Готовность и границы

Переход завершён, когда новый и мигрированный тестовые профили проходят полный маршрут, все 54 определения работают по своим правилам, все 8 слотов общие, бесплатные варианты доступны, покупки/награды устойчивы к повторам и ошибкам записи, полигон соответствует бою, а **все 30 экранов и три листа объектов** имеют результат переноса. Нужны свежие полные тесты, визуальная приёмка, отчёт баланса и запускаемая Windows-сборка.

Этот план не добавляет новую кампанию, сетевую игру, новые валюты, распознавание нарисованных линий, пользовательские траектории, облачные сохранения или возобновление произвольной волны после закрытия приложения. Это отдельные будущие задачи.

## Очистка планов

Этот документ — единственный актуальный план перехода. Прежний `docs/Implementation-Roadmap.md` заменён им; ссылки переведены сюда. Документы `Game-Design`, `Workshop-Assembly`, `Modular-Transition` и `Modular-Asset-Contract` сохраняются как спецификации, а не конкурирующие списки задач.

Исторические отчёты `docs/validation/` и `docs/audit/` остаются доказательствами прошлых проверок. Прежний `cleanup-plan.json` переименован в [cleanup-record.json](../../audit/2026-10-17-album/cleanup-record.json) и помечен как историческая запись, не подлежащая исполнению. Эскиз `15-planning.png` — экран расстановки перед волной, не старый план разработки.
