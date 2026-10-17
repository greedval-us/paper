# Ресурсы игрового оформления

Основная сцена — [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Меню, карта, подготовка, основа башни, оружие, рисунок и настройки (01–06, 22) используют оформление эскизов. Новый набор охватывает также постройки, врагов, окружение и эффекты; композиции 07–21 и 23–28 ещё предстоят, включая полный HUD 15–17. Следующий экран — 07 «Рабочая геометрия и подробности». [Статус переноса](Implementation-Roadmap.md).

## Источники и ресурсы

| Ресурс / код | Назначение |
|---|---|
| [ArtSource/Notebook/NewStyle](../ArtSource/Notebook/NewStyle) | PNG-листы, иллюстрации, логотипы, `sprites.source.json` и `animation.json` |
| `Resources/Art/NotebookAtlas.png` | Единая текстура деталей, врагов, окружения, эффектов и значков |
| `Resources/Art/NotebookSprites.json` | Семантический каталог областей атласа и aliases |
| [NotebookSpriteCatalog](../Assets/PaperDefense/Presentation/Art/NotebookSpriteCatalog.cs) | Загрузка спрайтов и общий атлас для UI и пакетных Mesh |
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
| [WeaponAssemblyGraphic](../Assets/PaperDefense/Presentation/UI/WeaponAssemblyGraphic.cs) | Целая сборка из спрайтов общего атласа для нового черновика; собственная геометрия принятого рисунка с сохранением пропорций |
| [GuidedDrawingLayout](../Assets/PaperDefense/Presentation/UI/GuidedDrawingLayout.cs), [DrawingSummaryLayout](../Assets/PaperDefense/Presentation/UI/DrawingSummaryLayout.cs) | Экран 06: слои над настоящим холстом, шесть инструментов, нижняя строка, меню «···», бумажная сводка, цветные шкалы и действующая проба |
| [DrawingCanvasGraphic](../Assets/PaperDefense/Presentation/UI/DrawingCanvasGraphic.cs), [FootprintProbeSketch](../Assets/PaperDefense/Presentation/UI/FootprintProbeSketch.cs) | Редактируемые авторские штрихи и запись настоящей пробы; окружение повторно использует общие рисунки |
| [PlayerShell.Notebook](../Assets/PaperDefense/Presentation/Player/PlayerShell.Notebook.cs) | Полная видимость фонов и общие элементы страниц |
| [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset) | Текст игрового интерфейса |

Источник правок — PNG и JSON в `NewStyle`; редакторский `NotebookSpriteImportTools.Rebuild()` собирает runtime-ресурсы. [Порядок импорта](Notebook-Art.md), [формат источников](../ArtSource/Notebook/README.md).

Декорации основы созданы встроенным `image_gen` по эскизу 04 с использованием навыка `imagegen`. [Точные промпты и происхождение](../ArtSource/Notebook/NewStyle/foundation-generation.json). Это отдельные прозрачные спрайты вне атласа; текст и игровые значения в них отсутствуют.

Стандартная иллюстрация назначается только подходящей конструкции. Стартовый набор содержит одиночное и двойное орудие, пробойник и бронированную мортиру; веер имеет собственный рисунок. Пользовательский чертёж отображается собственной геометрией, сохраняя пустые слои и точки креплений. Цены и характеристики читаются из игровых данных.

## Проверка

Фоны показываются целиком с собственными пропорциями; дополнительное место окна заполняется бумагой. Имена, цены и состояния остаются живым текстом. Основные действия карты, подготовки и настроек не требуют прокрутки. Кнопки и области ввода остаются отдельными Unity-элементами.

Проверяйте основную игру при 16:9 и 4:3, длинные имена и клавиатурный фокус. `-paper-memory-only` позволяет пройти маршрут без личного профиля. Применение общей темы не завершает композиции оставшихся экранов.

[Рисунок — экран 06](validation/2026-10-17-workshop-drawing/report.md) · [Оружие — экран 05](validation/2026-10-17-workshop-weapons/report.md) · [Основа башни — экран 04](validation/2026-10-17-workshop-foundation/report.md) · [Графика и разрешение](validation/2026-10-17-art-resolution/report.md) · [Дизайн](Game-Design.md) · [Порядок реализации](Implementation-Roadmap.md).
