# Ресурсы игрового оформления

> Техническая база до перехода. Свободное рисование отменено в целевом дизайне; упоминания Drawing/Freeform ниже описывают ещё существующий код и совместимость. Новые правила: [конструктор](Workshop-Assembly.md), [характеристики](Component-Stats-and-Abilities.md), [план перехода](superpowers/plans/2026-10-18-modular-album-implementation.md).

Целевой комплект от 18 октября — [альбом составных рисунков](design/album-assembly-2026-10-17/README.md): 9 представлений и 3 листа объектов. Это концепты для следующей реализации. В текущем проекте уже импортированы четыре фрагмента пилота [GunPilotParts-v1.png](../ArtSource/Notebook/NewStyle/GunPilotParts-v1.png). Полная библиотека 54 деталей ещё предстоит.

Основная сцена — [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Стандартная мастерская уже использует три шага без холста. Следующий перенос охватывает конструктор частей, сооружения, открытия и 8 общих слотов; числовые подписи должны оставаться живыми элементами UI. [Порядок реализации](superpowers/plans/2026-10-18-modular-album-implementation.md).

## Источники и ресурсы

| Ресурс / код | Назначение |
|---|---|
| [ArtSource/Notebook/NewStyle](../ArtSource/Notebook/NewStyle) | PNG-листы, иллюстрации, логотипы, `sprites.source.json` и `animation.json` |
| `Resources/Art/NotebookAtlas.png` | Единая текстура деталей, врагов, окружения, эффектов и значков; 84 спрайта после добавления четырёх фрагментов пулемёта, пиксели прежних 80 областей сохранены |
| `Resources/Art/NotebookSprites.json` | Семантический каталог областей атласа и aliases |
| [NotebookSpriteCatalog](../Assets/PaperDefense/Presentation/Art/NotebookSpriteCatalog.cs) | Загрузка спрайтов и общий атлас для UI и пакетных Mesh |
| [NotebookMountLayout](../Assets/PaperDefense/Presentation/Art/NotebookMountLayout.cs), [NotebookMounts.json](../Assets/PaperDefense/Resources/Art/NotebookMounts.json) | Общие стандартные слоты, посадочный диаметр, авторский canvas/trim, attach и muzzle; schema 2 добавляет четыре фрагмента и ось поворота пилота, чтение schema 1 сохранено |
| [GunPilotParts-v1.png](../ArtSource/Notebook/NewStyle/GunPilotParts-v1.png), `part.gun.*` | Неподвижная опора, стволы с отдачей, вращающийся корпус люльки и передняя муфта только для `chassis.standard` / `weapon.gun` в обоих слотах; [промпт и происхождение](../ArtSource/Notebook/NewStyle/GunPilotParts-v1.provenance.json) |
| [NotebookVisualTheme.asset](../Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset) | Анимация, время кадров, палитра и отдача; стандартные `Marks` пусты |
| `Resources/Art/Player/HomeDefenseLandscape` | Фон меню без запечённых игровых подписей и кнопок |
| `Resources/Art/Player/MapFieldIllustration` | Целый рисунок поля карты |
| `Resources/Art/Player/PreparationLandscape` | Декоративный фон подготовки |
| `Resources/Art/Player/SettingsLandscape` | Фон настроек по эскизу 22 |
| `Resources/Art/Player/FoundationPageDecor`, `FoundationPreviewScenery` | Прозрачный нижний пейзаж и окружение предпросмотра основы 04; отдельные PNG с сохранёнными пропорциями |
| `Resources/Art/Player/DrawingWorkshopHouse` | Подробный домик шапки 06 по эскизу; прозрачный PNG, один Sprite с импортом до 512px; [промпт и происхождение](validation/2026-10-17-workshop-drawing/assets.md) |
| [FoundationDetailGraphic](../Assets/PaperDefense/Presentation/UI/FoundationDetailGraphic.cs) | Тонкие рамки, круглые шаги, деления креплений, отметка выбора, стрелки и видимый фокус экранов 04–06 |
| `logo.home`, `logo.settings` | Декоративные логотипы из общего каталога |
| `tower.*`, `hero.castle`, `icon.ink-*` | Стандартные конструкции, замок и цветные чернильницы для страниц |
| `chassis.standard`, `chassis.heavy`, `chassis.precision`, `chassis.battery` | Существующие рисунки корпусов из общего атласа для карточек и предпросмотра экрана 04 |
| [NotebookPaperSurface](../Assets/PaperDefense/Presentation/Player/NotebookPaperSurface.cs) | Бумага, клетка, рамки, штриховка и состояния кнопок |
| [PlayerShell.Home](../Assets/PaperDefense/Presentation/Player/PlayerShell.Home.cs) | Главное меню и пять действий |
| [PlayerShell.Map](../Assets/PaperDefense/Presentation/Player/PlayerShell.Map.cs) | Карта с действующими условиями миссии и переходами |
| [PlayerShell.Preparation](../Assets/PaperDefense/Presentation/Player/PlayerShell.Preparation.cs) | Четыре карточки, материал, готовность, замена и запуск |
| [PlayerShell.Settings](../Assets/PaperDefense/Presentation/Player/PlayerShell.Settings.cs) | Полный экран, текущее и выбранное разрешение, применение, радиусы и уменьшение движения |
| [WizardFoundationPanel](../Assets/PaperDefense/Presentation/UI/WizardFoundationPanel.cs) | Экран 04: каталог корпусов, выбранная основа, реальные параметры и отдельная покупка |
| [WizardWeaponPanel](../Assets/PaperDefense/Presentation/UI/WizardWeaponPanel.cs), [WizardWeaponHeader](../Assets/PaperDefense/Presentation/UI/WizardWeaponHeader.cs) | Экран 05: текущая сборка, вертикальные крепления, семь карточек оружия, цены, нагрузка и доступ к действующим дополнениям |
| [WeaponAssemblyGraphic](../Assets/PaperDefense/Presentation/UI/WeaponAssemblyGraphic.cs) | Общая композиция стандартной сборки из атласа, включая части пилотного пулемёта; собственная геометрия исторического рисунка с сохранением пропорций |
| [GuidedDrawingLayout](../Assets/PaperDefense/Presentation/UI/GuidedDrawingLayout.cs), [DrawingSummaryLayout](../Assets/PaperDefense/Presentation/UI/DrawingSummaryLayout.cs) | Экран 06: слои над настоящим холстом, шесть инструментов, нижняя строка, меню «···», бумажная сводка, цветные шкалы и действующая проба |
| [DrawingCanvasGraphic](../Assets/PaperDefense/Presentation/UI/DrawingCanvasGraphic.cs), [FootprintProbeSketch](../Assets/PaperDefense/Presentation/UI/FootprintProbeSketch.cs) | Редактируемые авторские штрихи и запись настоящей пробы; окружение повторно использует общие рисунки |
| [PlayerShell.Notebook](../Assets/PaperDefense/Presentation/Player/PlayerShell.Notebook.cs) | Полная видимость фонов и общие элементы страниц |
| [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset) | Текст игрового интерфейса |

Источник правок — PNG и JSON в `NewStyle`; редакторский `NotebookSpriteImportTools.Rebuild()` собирает runtime-ресурсы. [Порядок импорта](Notebook-Art.md), [формат источников](../ArtSource/Notebook/README.md).

Декорации основы созданы встроенным `image_gen` по эскизу 04 с использованием навыка `imagegen`. [Точные промпты и происхождение](../ArtSource/Notebook/NewStyle/foundation-generation.json). Это отдельные прозрачные спрайты вне атласа; текст и игровые значения в них отсутствуют.

Стандартная иллюстрация назначается только подходящей конструкции. Стартовый набор содержит одиночное и двойное орудие, пробойник и бронированную мортиру; веер имеет собственный рисунок. Пользовательский чертёж отображается собственной геометрией, сохраняя пустые слои и точки креплений. Цены и характеристики читаются из игровых данных.

В пилотном пулемёте порядок слоёв — опора (0), стволы (1), корпус люльки (2), муфта (3). Корпус с ID `part.gun.cradle-back` перекрывает хвост стволов при отдаче. `pivotOffset = (0, 46)` размечен в канонических пикселях; дуло принадлежит стволам. UI, preview, полигон и бой используют одну композицию и габарит движения. Другие основы и оружия сохраняют прежнее отображение; профиль v7 и Core не меняются. [Отчёт](validation/2026-10-17-split-gun-pilot/report.md) и [лист поз](validation/2026-10-17-split-gun-pilot/01-split-gun-poses.png).

Полная приёмка пилота требует отдельных front/back-фрагментов основы, production-брони и свободного хода соседних орудий. Нынешние сокеты разделены примерно на 0,2238 U при радиусе длинного ствола около 0,30 U; все независимые углы ещё не проходят геометрическую приёмку. Ограничения наведения в Core для сокрытия пересечений не введены.

## Проверка

Фоны показываются целиком с собственными пропорциями; дополнительное место окна заполняется бумагой. Имена, цены и состояния остаются живым текстом. Основные действия карты, подготовки и настроек не требуют прокрутки. Кнопки и области ввода остаются отдельными Unity-элементами.

Проверяйте основную игру при 16:9 и 4:3, длинные имена и клавиатурный фокус. `-paper-memory-only` позволяет пройти маршрут без личного профиля. Применение общей темы не завершает композиции оставшихся экранов.

[Рисунок — экран 06](validation/2026-10-17-workshop-drawing/report.md) · [Оружие — экран 05](validation/2026-10-17-workshop-weapons/report.md) · [Основа башни — экран 04](validation/2026-10-17-workshop-foundation/report.md) · [Графика и разрешение](validation/2026-10-17-art-resolution/) · [Дизайн](Game-Design.md) · [Порядок реализации](superpowers/plans/2026-10-18-modular-album-implementation.md).
