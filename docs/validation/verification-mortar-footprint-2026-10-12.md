# Проверка отпечатка мортиры — 2026-10-12

Статус: Task 5 завершена. Текущие принятые результаты: 445 EditMode + 209 PlayMode = 654 теста; Console 0 ошибок / 0 предупреждений; Windows Development сборка успешна; изолированный Player прошёл; итоговый review принял исправления. База сравнения — `273022d`, Unity 6000.5.7f1. Прежние и промежуточные raw результаты сохранены отдельно от принятой проверки ниже.

## Уже записанная проверка

Результаты ниже прочитаны из raw JSON: status=succeeded, completed=total. Это целевые наборы и регрессии задач, не полный запуск всех тестов проекта.

| Срез | EditMode | PlayMode | Raw evidence |
|---|---:|---:|---|
| Task 1, формы/копии/профиль | 30/30 | 97/97 | [Edit](mortar-footprint-task1-greenEdit.json), [Play](mortar-footprint-task1-greenPlay.json) |
| Task 2, финальный бой/геометрия/контур | 43/43 | 7/7 | [Edit](mortar-footprint-task2-finalEdit.json), [Play](mortar-footprint-task2-finalPlay.json) |
| Task 3, проба/запись/изоляция | 34/34 | 9/9 | [Edit](mortar-footprint-task3-greenEdit.json), [Play](mortar-footprint-task3-greenPlay.json) |
| Task 3, обновлённая проба | 4/4 | — | [Edit](mortar-footprint-task3-finalEdit.json) |
| Task 4, UI и авторский рисунок | — | 21/21 | [Play](mortar-footprint-task4-finalPlay.json) |

Сохранены исходные RED: [Task 1 Edit](mortar-footprint-task1-redEdit.json), [Task 1 Play](mortar-footprint-task1-redPlay.json), [Task 2 Edit](mortar-footprint-task2-redEdit.json), [Task 2 Play](mortar-footprint-task2-redPlay.json), [захват снаряда](mortar-footprint-task2-captureRedEdit.json), [граница Edit](mortar-footprint-task2-boundaryRedEdit.json), [граница Play](mortar-footprint-task2-boundaryRedPlay.json), [Task 3 Edit](mortar-footprint-task3-redEdit.json), [Task 3 Play](mortar-footprint-task3-redPlay.json), [настоящий след в записи](mortar-footprint-task3-recordingRedEdit.json). Промежуточные GREEN и отдельный catalog Edit также оставлены в каталоге validation. RED — история разработки, не результат текущего полного набора.

[Production-проба TargetNotebookBalance](mortar-footprint-target-probe.json) записала r=2.4: круг q=1 поражает 9 в группе и 6 в колонне; овал q=3 — 3 и 9, completed=true. Источник: `MortarFootprintProbe.Compare`, Standard, одна legacy-мортира, Pencil, None. Это подтверждает покрытие двух заданных расстановок, не идеальный баланс и не результат полной партии.

## Проверяемый контракт

Формы независимы для двух креплений; q∈[1,3], a=r√q, b=r/√q, площадь πr². Длинная ось следует направлению запуска To−From, нулевое направление использует +X. Снаряд владеет геометрией и переживает изменение/удаление источника. Цена, качество, урон, дальность, интервалы, цвет и строительство не зависят от q. Художественные штрихи/оценка остаются отдельными; DrawingSnapshot формата 2 и стабильные ID не меняются.

Профиль v4 содержит метаданные формы версии 1 и две пропорции. v1/v2/v3 читаются как круг; оригинал миграции сохраняется, повреждённые v4-данные отклоняются, будущие версии защищены от перезаписи. Store-тесты используют временные пути/внедряемый IO.

Проба запускает настоящий WeaponSystem для одного выбранного ствола при исходном качестве, одинаковых настройках A/B, Pencil/None и девяти неподвижных целях. Показывает цену исходной сборки и поражено/вне отпечатка; реальные события попадания остаются в записи. Лимит 600 шагов по 1/30с даёт явное незавершение. Не меняются профиль, награды, расходы или время партии. Полный полигон проверяет всю сборку отдельно.

## Зафиксированный UI и изолированная визуальная проверка

[Raw UI evidence](mortar-footprint-ui.json) фиксирует настоящий UGUI в временной сцене с MemoryOnly до Awake; основной агент визуально проверил три скриншота. Жест PointerDown/Drag/Up дал q=3 для одного крепления, q=1 для другого, замороженный A=1. Группа A/B=9/3, колонна=6/9; цена обоих вариантов 176, качество 100. Проверка полевого залпа через обычные SaveBlueprint/AssignBlueprint/StartRun/BuildBlueprint/NextWave и контролируемые шаги дала два снаряда: полуоси 4.15692234/1.38564074 и 2.4/2.4. Это проверка фактического контура и независимых креплений в Editor; собранный Player отдельно прошёл pressure-сценарий ниже.

Проверенные изображения: [колонна](screenshots/mortar-footprint-column-live.png), [группа](screenshots/mortar-footprint-group-verified.png), [поле](screenshots/mortar-footprint-field-verified.png). В raw записано восстановление `Assets/PaperDefense/Scenes/PaperDefense.unity`, dirty=false, runInBackground=false и исходного preloaded InputSystem_Actions. Профиль игрока не читался.

Контролы: `Mortar footprint 0`/`Mortar footprint 1`; `Footprint group`, `Footprint column`, `Footprint repeat`, `Footprint pin A`, `Footprint circle`. Жест конца оси фиксирует q∈[1,3] при отпускании; начало жеста инвалидирует обе записи. A неизменен до pin/reopen, обе полосы имеют один цикл с удержанием настоящего final impact. `Save blueprint as new` сохраняет стабильное имя, показывает «СДЕЛАТЬ ВАРИАНТ» и использует AsVariant. Task 4 final PlayMode 21/21; основной агент сообщил 0 ошибок Console для этого запуска. Это не заменяет итоговую проверку compile Console и полный набор.
## Полные наборы и Windows Development сборка

До исправления UI основной агент выполнил полный EditMode: **445/445**, status=succeeded ([raw](mortar-footprint-finalEdit.json)); полный PlayMode: **206/206**, status=succeeded ([raw](mortar-footprint-finalPlay.json)). Основной агент сообщил итоговую Console: **0 ошибок**. Целевые наборы выше остаются историей RED/GREEN, их количества не прибавляются к полному итогу.

Историческая сборка до исправления UI, [raw build report](mortar-footprint-build.json): succeeded, StandaloneWindows64, development=true, сцена `Assets/PaperDefense/Scenes/PaperDefense.unity`, выход `Builds/MortarFootprint/PaperDefense.exe`. Размер 198.54 MB, длительность 12.9414725с, **0 ошибок / 0 предупреждений**. Сборка завершена 2026-10-12T14:21:04.4837609Z. Успешная сборка сама по себе не подтверждает результат runtime-сценария.

Изолированный actual Player pressure ×2 до исправления UI завершён; исторический отчёт и сравнение приведены ниже. Принятые build/Player и итоговый re-review приведены ниже. Minor о читаемости исправлен в той же волне.
## Исторический Player до исправления UI и прежний круговой результат

[Rendered Windows Player ×2 raw](2026-10-12-142236-mortar-footprint-player-x2.json), [полный лог](2026-10-12-142236-mortar-footprint-player-x2.log): completed=true, результат passed=true, Victory, 3000 убийств, база 250 HP, 5687 шагов, 94.7220011с реального времени. Основной агент сообщил exit code 0. MemoryOnly зафиксирован до и после измерения, workshopProfileMemoryOnly=true: профиль игрока изолирован.

Отпечаток состояния **9a4e907503fce63c** совпал с [историческим круговым Player ×2](2026-10-12-130111-combat-drawing-fixes-player-x2.json). Вместе с полными регрессионными наборами это подтверждает сохранение прежнего кругового результата в проверенных кампаниях и массовом сценарии. Совпадение не обещает межплатформенную детерминированность или одинаковое покрытие новых овалов.

Основной агент проверил отсутствие Exception/error в Player-логе. Лог содержит диагностику graphics ring buffer и при закрытии — ComputeBuffer disposal/MemoryLeaks; последние также присутствовали в историческом baseline. Поэтому результат сценария успешен, но лог Player не объявляется полностью чистым. Исторические raw отчёты и логи сохранены.

Эти исторические результаты не заменяют принятую проверку после исправления ниже.
## Принятая проверка после исправления UI

Whole-diff review обнаружил P2: перегруженный черновик или пустое имя вызывали throwing probe из панели без проверки. Исправление использует внедрённую HUD-валидацию `run.PreviewBlueprint` на настоящем session balance, без изменений Core/Application; добавлены проверки обычного лимита 10, custom-лимита 8, пустого имени и восстановления после ошибки. Minor читаемости исправлен. При форматировании re-review обнаружил U+FFFD в строках: строки восстановлены, HUD-тест проверяет точные символы. Промежуточный неуспешный text assertion сохранён в [raw](mortar-footprint-finalFix-textAssertionPlay.json).

Текущий код: полный EditMode **445/445** ([accepted raw](mortar-footprint-acceptedEdit.json)), полный PlayMode **209/209** ([accepted raw](mortar-footprint-acceptedPlay.json)), оба status=succeeded; всего **654 теста**. Основной агент сообщил Console **0 ошибок / 0 предупреждений**. Предыдущие 445/206, build и runtime относятся к коду до этой волны и не подменяют принятую проверку новой сборки ниже.

## Явное ограничение длинной записи

Короткое A/B и обычное поле показывают настоящие формы попаданий. Длинный 60-секундный TrialRunner сохраняет реальные `TrialFrame.Impacts`, но существующий `TrialLaneSketch` пока отображает снаряды/ShotEvent без новых контуров этих impacts. Отображение следов в длинном replay явно отложено решением основного агента; оно не заявляется реализованным.
## Принятая сборка, actual Player и итоговый review

[Accepted build raw](mortar-footprint-acceptedBuild.json): succeeded, StandaloneWindows64 Development, `Builds/MortarFootprint/PaperDefense.exe`, 198.54 MB, 11.3918143с, **0 ошибок / 0 предупреждений**. Сборка завершена 2026-10-12T14:41:37.2786958Z; после принятых 654 тестов и этой сборки код не менялся.

[Accepted actual Player ×2](2026-10-12-144203-mortar-footprint-accepted-player-x2.json), [лог](2026-10-12-144203-mortar-footprint-accepted-player-x2.log): Completed/Passed=true, MemoryOnly=true, Victory, 3000 убийств, база 250 HP, 5687 шагов, 94.7326089с; основной агент подтвердил helper exit 0. Fingerprint **9a4e907503fce63c** совпал с историческим круговым baseline. Полные регрессии и повторный массивный прогон подтверждают сохранение проверенных круговых результатов. Основной агент не обнаружил Exception/errors в логе; остаются graphics ring buffer diagnostic и прежние ComputeBuffer disposal/MemoryLeaks при закрытии. Лог не объявляется полностью чистым.

[Whole-diff review и история исправлений](mortar-footprint-whole-review.md), [принятый scoped fix review](mortar-footprint-fix-review.md): все три findings устранены (валидация недопустимого черновика, читаемость UI, восстановление строк), новое нарушение не найдено; итог scoped review — 0 Critical, 0 Important, 0 Minor. Проверка документации принята. Сборка/Player относятся к текущему принятому коду; ранние RED/GREEN и доисправительные результаты остаются историей.

## Решения по процессу и границам

Документация выполнялась параллельно с UI на непересекающихся путях; стоимость решения — необходимость сверить окончательные имена, жесты и evidence после стабилизации UI. Эта сверка выполнена.

Отображение `TrialFrame.Impacts` в длинном 60-секундном `TrialLaneSketch` явно отложено основным агентом; стоимость при необходимости — расширение renderer и соответствующих тестов. Короткое A/B и обычное поле показывают настоящие формы. Произвольные контуры, поперечный отпечаток, специализации других оружий, анимация штрихов и послебоевая аналитика также не заявляются реализованными. Эксперимент подтверждает заданное покрытие, а не идеальный баланс.