# Ресурсы существующей игровой оболочки

Техническая справка по существующей базе игры. [Эскизы и целевой дизайн](Game-Design.md) ещё не реализованы в Unity; расположение элементов ниже описывает текущую оболочку. [Порядок будущей реализации](Implementation-Roadmap.md).

Основная сцена — [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Ресурсы ниже сохранены после удаления разработческих галерей; это база для будущего оформления, а не готовое воплощение альбома эскизов.

## Ресурсы и владельцы

| Ресурс / код | Назначение |
|---|---|
| [NotebookPaper.png](../Assets/PaperDefense/Resources/Art/Player/NotebookPaper.png) | Существующая бумажная текстура интерфейса |
| [NotebookLandscape.png](../Assets/PaperDefense/Resources/Art/Player/NotebookLandscape.png) | Существующая иллюстрация меню |
| [NotebookPaperSurface](../Assets/PaperDefense/Presentation/Player/NotebookPaperSurface.cs) | Рамки, бумажные края и штриховка |
| [PlayerIconGraphic](../Assets/PaperDefense/Presentation/Player/PlayerIconGraphic.cs) | Значки игровой оболочки |
| [NotebookVisualTheme.asset](../Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset) | Общие векторные формы деталей, инструментов, объектов и окружения |
| [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset) | Текст игрового интерфейса |

`PlayerGameRoot.notebookStyle` включает существующую бумажную тему. Эта настройка сама по себе не реализует композицию, состояния и маршрут из новых эскизов. Боевые свойства и авторские геометрии принадлежат общим системам игры.

## Проверка и дальнейшая работа

Просматривайте ресурсы в основной игре: меню, подготовка, мастерская, альбом, полигон и бой. `-paper-memory-only` позволяет проверять маршрут без чтения и записи личного профиля. Рисунки, золото и изменения такой сессии живут только в памяти.

Новые композиции следует реализовывать отдельными Unity-элементами с текстом, иконками и интерактивными состояниями. Растровые концепты не являются готовыми UI-спрайтами; цены и характеристики берутся из PlayerRules/PlayerContent. [Дизайн](Game-Design.md), [порядок реализации](Implementation-Roadmap.md), [импорт общей графики](Notebook-Art.md), [последняя проверка технической базы](validation/2026-10-16-main-game-cleanup/report.md).
