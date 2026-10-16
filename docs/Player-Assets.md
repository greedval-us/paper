# Ресурсы существующей игровой оболочки

Техническая справка по действующим системам игры. [Эскизы и целевой дизайн](Game-Design.md) задают визуальный ориентир; [статус переноса в Unity](Implementation-Roadmap.md) отделяет реализованные экраны от оставшейся работы.

Основная сцена — [PaperDefenseGame.unity](../Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity). Ресурсы ниже включают сохранённую основу и новое оформление меню, карты и подготовки по эскизам 01–03; композиции экранов 04–28 ещё предстоит перенести.

## Ресурсы и владельцы

| Ресурс / код | Назначение |
|---|---|
| [NotebookPaper.png](../Assets/PaperDefense/Resources/Art/Player/NotebookPaper.png) | Существующая бумажная текстура интерфейса |
| [NotebookLandscape.png](../Assets/PaperDefense/Resources/Art/Player/NotebookLandscape.png) | Сохранённая прежняя иллюстрация |
| [HomeDefenseLandscape.png](../Assets/PaperDefense/Resources/Art/Player/HomeDefenseLandscape.png) | Фон нового меню по эскизу 01 без запечённых подписей и кнопок |
| [MapLandscape.png](../Assets/PaperDefense/Resources/Art/Player/MapLandscape.png) | Большой рисунок поля для карты 02; подписи, сводка и действия остаются живыми |
| [PreparationLandscape.png](../Assets/PaperDefense/Resources/Art/Player/PreparationLandscape.png) | Декоративный вид подготовки 03, уменьшающийся ради карточек и действий |
| [PreparationTowers.png](../Assets/PaperDefense/Resources/Art/Player/PreparationTowers.png) | Иллюстрации стандартных конструкций для карточек; выбор соответствует реальной сборке |
| [MissionIllustrations.png](../Assets/PaperDefense/Resources/Art/Player/MissionIllustrations.png) | Миниатюра замка и иллюстрации чернильниц |
| [NotebookPaperSurface](../Assets/PaperDefense/Presentation/Player/NotebookPaperSurface.cs) | Общая бумага, клетка, рамки, штриховка и состояния кнопок |
| [PlayerShell.Home](../Assets/PaperDefense/Presentation/Player/PlayerShell.Home.cs) | Адаптивная композиция меню, живой заголовок и пять действий |
| [PlayerShell.Map](../Assets/PaperDefense/Presentation/Player/PlayerShell.Map.cs) | Карта с реальными условиями миссии, чернильницами и переходами без прокрутки |
| [PlayerShell.Preparation](../Assets/PaperDefense/Presentation/Player/PlayerShell.Preparation.cs) | Четыре карточки в один ряд, материал, готовность, замена и запуск без прокрутки |
| [PlayerShell.Notebook](../Assets/PaperDefense/Presentation/Player/PlayerShell.Notebook.cs) | Общие элементы новых экранов и выбор иллюстрации по реальной конструкции |
| [PlayerIconGraphic](../Assets/PaperDefense/Presentation/Player/PlayerIconGraphic.cs) | Значки игровой оболочки |
| [NotebookVisualTheme.asset](../Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset) | Общие векторные формы деталей, инструментов, объектов и окружения |
| [player-ru.asset](../Assets/PaperDefense/Resources/Text/player-ru.asset) | Текст игрового интерфейса |

`PlayerGameRoot.notebookStyle` включает бумажную тему. Главное меню, карта и подготовка используют новые композиции; применение общей темы к другим страницам само по себе не переносит их по эскизам. Прежние ресурсы сохранены для существующих потребителей и не задают композицию новых экранов.

`PreparationTowers` оформляет только соответствующие стандартные конструкции. Стартовый набор содержит одиночное и двойное орудие, пробойник и бронированную мортиру; веер имеет собственную иллюстрацию для конструкции с веером. Пользовательские рисунки отображаются живой геометрией. Разрезанные иллюстрации создаются в памяти при запуске; исходные PNG эскизов не меняются. Боевые свойства и авторские геометрии принадлежат общим системам игры.

## Проверка и дальнейшая работа

Просматривайте ресурсы в основной игре: меню, подготовка, мастерская, альбом, полигон и бой. `-paper-memory-only` позволяет проверять маршрут без чтения и записи личного профиля. Рисунки, золото и изменения такой сессии живут только в памяти.

Новые композиции следует реализовывать отдельными Unity-элементами с текстом, иконками и интерактивными состояниями. Растровые концепты не являются готовыми UI-спрайтами; цены и характеристики берутся из PlayerRules/PlayerContent. [Дизайн](Game-Design.md), [порядок реализации](Implementation-Roadmap.md), [импорт общей графики](Notebook-Art.md), [последняя проверка технической базы](validation/2026-10-16-main-game-cleanup/report.md).
