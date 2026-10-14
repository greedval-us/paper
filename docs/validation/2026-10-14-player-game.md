# Проверка отдельной игровой сборки — 14 октября 2026

Среда: Unity 6000.5.7f1, Windows. Все автоматические и ручные игровые прогоны выполнялись с профилем memory-only.

## Автоматические проверки

- Начальные проверки входной сцены и состава сборки сначала падали: игровой сцены ещё не было (RED).
- Полный EditMode: 864 / 864, 42.816 с. После добавления проверки самостоятельного игрового каталога — отдельный прогон PlayerSceneTests: 3 / 3.
- Полный PlayMode: 377 / 377, 83.261 с; включает проверки игрового маршрута и регрессию клавиатурной отправки команды под паузой.
- Финальный прогон PlayerJourneyTests после визуальных изменений и иконок мастерской: 7 / 7, 1.348 с.
- Windows x64: BuildPipeline — Succeeded, 0 ошибок, 0 предупреждений; 138 353 193 байт, 2.465 с, 2026-10-14 16:52:27 UTC. Выход: `Builds/PlayerGame/PaperDefense.exe`; машинный итог: `Builds/PlayerGame/build-report.txt`. Отдельный exe в этой проверке не запускался; игровой маршрут проверен в Unity PlayMode.
- Консоль после сборки: ошибок нет. `runInBackground` возвращён к исходному `false`; сохранённая сцена использует обычный профиль (`memoryOnlyProfile: 0`).

Проверены начальное меню без симуляции, изоляция профиля, переходы по настоящим кнопкам, материал и набор чертежей, пауза, настройки, возврат на карту без потери альбома и перевод из мастерской к подготовке. Ревью выявило и помогло исправить доступность команды под модальным окном с клавиатуры, неверную подпись DeepInk и устаревающий счётчик золота.

## Визуальная и ручная проверка

Снимки текущих экранов находятся в `Captures/PlayerGame`. Для фонового редактора используется `PlayerVisualCapture`: он отдельно рендерит игровую камеру и активные Canvas, соединяет их с прозрачностью и восстанавливает состояние сцены. Это проверка композиции, а не измерение FPS.

[Меню](../../Captures/PlayerGame/01-home.png), [карта](../../Captures/PlayerGame/02-map.png), [подготовка](../../Captures/PlayerGame/03-preparation.png), [бой](../../Captures/PlayerGame/04-battle.png), [пауза](../../Captures/PlayerGame/05-pause.png), [улучшения](../../Captures/PlayerGame/06-upgrades-layout.png), [мастерская](../../Captures/PlayerGame/07-workshop.png), [результат](../../Captures/PlayerGame/09-result.png).

Построены шесть башен через обычные команды RunController; после запуска волны в кадре отображались шесть построек и 13 врагов. Переход к поражению проверен настоящей симуляцией: без обороны здоровье базы дошло до нуля за 935 шагов, после чего автоматически открылся экран результата с данными сессии. Награды и статистика не подставляются из концепт-изображений. Снимок выбора улучшений показывает только композицию экрана; переходы и эффекты проверяются автоматическими тестами.

## Происхождение фона

Инструмент: встроенный `image_gen.imagegen`, skill `imagegen`. Результат скопирован в `Assets/PaperDefense/Resources/Art/Player/NotebookLandscape.png`. Интерфейс не запечён в изображение. Исходный файл генерации: `C:/Users/Greedval/.codex/generated_images/01a13b19-0670-7e32-841c-b38af1c7af62/exec-97c767e1-af89-4470-9fda-ed4658e39a9d.png`.

Промпт:

> Create a production game UI BACKGROUND ILLUSTRATION, landscape aspect ratio 16:10, 1600x1000 if possible. It is for a tower defense game entirely drawn with a blue fountain pen in a beloved old squared-paper notebook. Warm ivory cream paper, subtle real paper grain, faded pale blue grid, restrained sepia worn paper edges, very fine expressive navy blue crosshatched pen drawing. Composition REQUIRED: the LEFT 52% and upper left are nearly blank light ivory paper for clean game title and menu controls. On the RIGHT half, an elaborate whimsical medieval castle with many slender towers, blue conical roofs and tiny blue pennants, arches and stone battlements, on a rocky hillside, illustrated in exquisite pen detail, from x58% to x92% and y20% to y78%. Around right castle are little fir trees, winding path and distant mountains. Along the BOTTOM EDGE ONLY, small sketchy mountains, forest and faint ink splashes, never crowd left menu space. No text of any kind, NO words, NO lettering, NO numbers, NO labels, NO logos, NO buttons, NO UI, NO panels, NO characters. No solid bold cartoon shapes. Avoid photorealistic 3D. Draw the scenery directly on paper with lively hand drawn hatch strokes, like a beautifully illustrated fantasy cartographer's notebook. Main light paper color RGB approx 246 240 220. Calm legible negative space around left menu, richly detailed castle focal illustration on right. Full bleed rectangular canvas, not a photographed page at an angle. This asset will sit behind live Unity UI controls.
