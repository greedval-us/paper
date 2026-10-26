# Unified notebook assets implementation plan

> **For agentic workers:** execute the scoped art/integration tasks with one Unity writer (root); source workers and visual reviewers operate independently. Preserve shared user changes.

**Goal:** привести игровые основы, оружие, детали и сооружения к языку модульных эскизов и убрать доказанно устаревшие материалы.

**Architecture:** нативные PNG и существующие crops/anchors/composition остаются источником представлений. Известные семантические UI-значки получают ту же полную составную иллюстрацию. Импорт публикует атлас, каталог и крепления одной транзакцией.

**Tech Stack:** Unity6000.5.7f1/uGUI, C#, Node authoring tools, built-in image_gen.

**Spec:** `../specs/2026-10-24-unified-notebook-assets-design.md`.

## Global constraints

- Сохранить54ID и ревизии деталей, правила профиля/баланс, настоящие сокеты, дула, физические области, валидные прежние рецепты.
- Изображения генерирует root; PNG копируются без обработки. Новые области и точки измеряются, а не угадываются по alpha-центру.
- Эскизы/историческая приёмка/пользовательские данные,12 зависимостей и `New Unity Project/` защищены.

## Review focus

- Две/три посадки должны совпадать с авторскими центрами на новом рисунке.
- Основа сохраняет реальные нижнюю часть/плоскость/передний край с непрерывными полосами.
- Магазины/подачи читаются как чернильные картриджи в общем стиле эскизов.
- UI-семейства показывают современную сборку; неизвестные/анимированные ресурсы сохраняют прежнее поведение.
- Удалённый PNG не должен иметь retained importer, Resource/GUID или profile-compatibility references.

### Task1: source audit and new art

- [x] Сопоставить3 листа эскизов и9 production-листов; записать inventory/style/import reports.
- [x] Сгенерировать четыре замены под проверенную композицию, записать prompt/reference/SHA/native dimensions. Четвёртая — отдельный TurnHub после обнаруженного на настоящих кадрах прямоугольного остатка старой площадки.
- [x] Физически проверить стиль и механические точки каждого кандидата; отбраковать некорректный.

### Task2: shared UI illustrations

Files: `Presentation/Art/NotebookArtGraphic.cs`, локальный resolver иллюстраций при необходимости, соответствующий PlayMode regression fixture.

- [x] Сначала тесты: известные ui.weapon семейства рисуют детали полного оружия, а не старый целый спрайт; повторный Bind и неизвестный/анимированный ресурс корректно сбрасывают режим. Проверить реальный mesh/UV либо независимый композиционный контракт.
- [x] Root подтверждает RED; затем минимальный resolver/renderer switch без изменения callback/layout/profile data.
- [x] Пройти целевые тесты и визуально проверить реальные upgrade/catalog/help/trial surfaces.

### Task3: durable metadata and import

Files: `Tools/ArtAssembly13/build-catalog.mjs`, `ArtSource/Notebook/NewStyle/{sprites.source,assembly-parts.source,full-catalog-layout}.json`, packing and runtime generated outputs.

- [x] Измерить новые source canvases/crops/attach/seats/regions; сохранить логические трансформации и valid-recipe witnesses.
- [x] Внести изменения в authoring-generator, поднять visual revision, пересобрать JSON и packing без уменьшения PNG.
- [x] Проверить source/runtime fits и реальные контакты; импортировать штатной Unity-транзакцией.

### Task4: retirement and acceptance

- [x] После замены удалить только reference-proven obsolete assets; записать hash/reference/deletion ledger и актуальный asset contract.
- [x] Проверить all54 definitions, native pixels/packing, profile persistence and affected UI/world tests.
- [x] Снять актуальные сборки и затронутые сцены в пяти размерах; поручить физический QA и сделать gallery/index с SHA.
- [x] Независимый review, финальные подходящие полные проверки, чистая остановленная Game scene; подготовить selective commit с сохранением чужих изменений.

Результат: 4 новых PNG; 54 ID/77 фрагментов, 141 native crop/431 aliases. Полные проверки 1584 EditMode + 865 PlayMode. Строгая галерея 275/275 с совпадающими SHA; художественные и компактные UI-отличия перечислены в [итоговом отчёте](../../validation/modular-album/art3-acceptance.md).
