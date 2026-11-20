# Инструменты проекта

Команды выполняются из корня проекта. Редактор — Unity 6000.5.7f1; независимые числовые проверки используют Node.js.

## Игровой баланс

```powershell
node Tools/AlbumDesign2026/build-data.mjs
```

`build-data.mjs` записывает `docs/balance/album-balance-v1.json`. После изменения параметров импортируйте его через `AlbumBalanceImport`, затем вызовите `BalanceWorkbookExport.ExportPlayerAssembly()` в Unity, чтобы обновить `docs/balance/assembly-runtime.json`. Затем запустите:

```powershell
node --test Tools/AlbumDesign2026/verify-balance.test.mjs
node Tools/AlbumDesign2026/verify-balance.mjs
```

Тесты и верификатор сравнивают независимый расчёт с экспортом; он должен соответствовать проверяемым игровым assets. [Подробный порядок](AlbumDesign2026/README.md).

## Unity и проверка инструментов

Главная сцена — `Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity`; меню `Paper Defense → Game → Open player game` открывает её. Test Runner содержит сборки Core, Editor и Integration. Тесты сцен могут менять открытую сцену; перед запуском сохраните свою работу. `PlayerVisualCapture.Capture` снимает производственные Canvas. [Запуск, сборка и ручной маршрут](../docs/Development-Guide.md), [сохранённые результаты](../docs/validation/README.md).

Python-инструменты проверяются командой:

```powershell
python -m unittest discover -s Tools/tests -p 'test_*.py'
```

## Excel и зависимости

Числовые проверки не требуют `@oai/artifact-tool`. Для генерации Excel нужен bundled runtime; его расположение сообщает `load_workspace_dependencies`. Текущий генератор — `Tools/AlbumDesign2026/build-workbook.mjs`. [BalanceWorkbook](BalanceWorkbook/README.md) предназначен для сравнения пресетов и совместимости; он не является источником текущего баланса Player.

## Графика

Детали рисует `NightNotebookArt` (`paper-mechanisms-v4`), композицию собирает `AssemblyVisualComposer`, бой оформляет `InkSwarmArt` (`ink-swarm-v1`), меню — `FieldMenuSkin`. Внешние ресурсы находятся в `Assets/PaperDefense/Graphics`; физические ограничения — в `Assets/PaperDefense/Resources/Configuration/AssemblyFits.json`. [Действующий графический стандарт](../Assets/PaperDefense/Graphics/README.md).

Перед удалением данных проверяйте чтение файлов из кода, GUID сериализованных assets и загрузки Resources. Старый путь вывода кадров не требует сохранения ранее созданных изображений; входные JSON миграции и сравнительных инструментов могут быть необходимы даже после очистки отчётов.

[Документация](../docs/README.md)
