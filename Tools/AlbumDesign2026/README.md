# Баланс составного альбома

`build-data.mjs` задаёт 91 определение деталей, включая 36 частей сооружений, и расчётные рецепты. Он записывает `docs/balance/album-balance-v1.json`. Игровой каталог обновляется при импорте в Unity.

После изменения параметров выполните по порядку:

1. Из корня проекта: `node Tools/AlbumDesign2026/build-data.mjs`.
2. В Unity импортируйте JSON через `AlbumBalanceImport` в PlayerAssemblyCatalog.
3. Вызовите `BalanceWorkbookExport.ExportPlayerAssembly()`, чтобы обновить фактические значения resolver в `docs/balance/assembly-runtime.json`.
4. Запустите независимые проверки:

```powershell
node --test Tools/AlbumDesign2026/verify-balance.test.mjs
node Tools/AlbumDesign2026/verify-balance.mjs
```

Верификатор сравнивает числа и идентичности рецептов с runtime-экспортом, проверяет совместимость деталей и целочисленный доход генераторов. Результат записывается в `docs/balance/analytical-validation.json`. Успех на сохранённом экспорте не подтверждает изменённые после экспорта assets и не заменяет игровые тесты. [Результаты проверок](../../docs/validation/README.md).

Перед импортом `node Tools/AlbumDesign2026/verify-balance.mjs --design-only` проверяет арифметику и достижимость всех деталей каталога и записывает `runtimeVerified:false`. Для приёмки выполните обычный запуск после свежего Unity-экспорта. Текущая версия — `album-v1.4`. Сумма цен открытия деталей составляет 3793 золота. Цены семейств задаёт `WorkshopProfile.GetStructureOffer`. Шипы, маяк и призма стоят в сумме ещё 245 золота.

## Excel

`node Tools/AlbumDesign2026/build-workbook.mjs` создаёт книгу, протоколы и числовые превью из двух JSON: `album-balance-v1.json` и `assembly-runtime.json`. Требуется bundled `@oai/artifact-tool`; путь можно задать через `CODEX_WORKSPACE_NODE_MODULES`. `ALBUM_WORKBOOK_OUTPUT_DIR` задаёт отдельный каталог результата; по умолчанию это `docs/balance`. Старые Excel и PNG не являются входами генератора. Изменения книги не переносятся в игру.

Файл `docs/validation/modular-album/assembly-balance-results.json` читается только при наличии как дополнительные измерения. Без него книга собирается без измерительного отчёта; старые измерения не подтверждают текущую игру. Для включения измерений получите новый результат из игровой симуляции.
