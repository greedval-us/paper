# Ресурсы игрового оформления

Основная сцена — [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Меню, карта, подготовка и настройки (01–03, 22) используют оформление эскизов. Новый набор охватывает также постройки, врагов, окружение и эффекты; композиции 04–21 и 23–28 ещё предстоят, включая полный HUD 15–17. [Статус переноса](Implementation-Roadmap.md).

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
| `logo.home`, `logo.settings` | Декоративные логотипы из общего каталога |
| `tower.*`, `hero.castle`, `icon.ink-*` | Стандартные конструкции, замок и цветные чернильницы для страниц |
| [NotebookPaperSurface](../Assets/PaperDefense/Presentation/Player/NotebookPaperSurface.cs) | Бумага, клетка, рамки, штриховка и состояния кнопок |
| [PlayerShell.Home](../Assets/PaperDefense/Presentation/Player/PlayerShell.Home.cs) | Главное меню и пять действий |
| [PlayerShell.Map](../Assets/PaperDefense/Presentation/Player/PlayerShell.Map.cs) | Карта с действующими условиями миссии и переходами |
| [PlayerShell.Preparation](../Assets/PaperDefense/Presentation/Player/PlayerShell.Preparation.cs) | Четыре карточки, материал, готовность, замена и запуск |
| [PlayerShell.Settings](../Assets/PaperDefense/Presentation/Player/PlayerShell.Settings.cs) | Полный экран, текущее и выбранное разрешение, применение, радиусы и уменьшение движения |
| [PlayerShell.Notebook](../Assets/PaperDefense/Presentation/Player/PlayerShell.Notebook.cs) | Полная видимость фонов и общие элементы страниц |
| [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset) | Текст игрового интерфейса |

Источник правок — PNG и JSON в `NewStyle`; редакторский `NotebookSpriteImportTools.Rebuild()` собирает runtime-ресурсы. [Порядок импорта](Notebook-Art.md), [формат источников](../ArtSource/Notebook/README.md).

Стандартная иллюстрация назначается только подходящей конструкции. Стартовый набор содержит одиночное и двойное орудие, пробойник и бронированную мортиру; веер имеет собственный рисунок. Пользовательский чертёж отображается собственной геометрией, сохраняя пустые слои и точки креплений. Цены и характеристики читаются из игровых данных.

## Проверка

Фоны показываются целиком с собственными пропорциями; дополнительное место окна заполняется бумагой. Имена, цены и состояния остаются живым текстом. Основные действия карты, подготовки и настроек не требуют прокрутки. Кнопки и области ввода остаются отдельными Unity-элементами.

Проверяйте основную игру при 16:9 и 4:3, длинные имена и клавиатурный фокус. `-paper-memory-only` позволяет пройти маршрут без личного профиля. Применение общей темы не завершает композиции оставшихся экранов.

[Актуальные снимки и проверка](validation/2026-10-17-art-resolution/report.md) · [Дизайн](Game-Design.md) · [Порядок реализации](Implementation-Roadmap.md).
