# Сравнительная книга совместимости

Инструмент оставлен для сравнения прежних пресетов Historical и Target и проверки совместимости их экспорта. Основная игра использует PlayerRules и составной каталог; значения Target не заменяют её баланс. Для текущего каталога используйте [AlbumDesign2026](../AlbumDesign2026/README.md) и свежий [Player runtime-экспорт](../../docs/balance/assembly-runtime.json).

Если нужна сравнительная книга:

1. В Unity выполните `Paper Defense > Export mechanics and balance JSON` или `PaperDefense.Editor.BalanceWorkbookExport.Export()` для свежего `docs/balance/balance-data.json`.
2. Настройте bundled `@oai/artifact-tool` по [инструкции инструментов](../README.md#excel-и-зависимости).
3. Из корня проекта выполните `Tools/BalanceWorkbook/Build.ps1`.

Результат — `docs/balance/PaperDefense-Mechanics-Balance.xlsx`, `workbook-validation.json` рядом с ним и превью в `Tools/BalanceWorkbook/.previews`. Старые книги, протоколы и PNG не читаются как входы. Excel не импортируется в Unity. При изменении исходников или assets после экспорта проверка хешей требует повторного экспорта.

## Обязательные данные совместимости

Текущая реализация `build.mjs` безусловно читает следующие JSON. Они сохранены как входные данные инструмента, а не как актуальные доказательства качества игры:

- `docs/validation/content/after/editmode-tests.json`
- `docs/validation/content/after/playmode-tests.json`
- `docs/validation/content/after/baseline-comparison.json`
- `docs/validation/content/after/editor-acceptance.json`
- `docs/validation/content/after/player-acceptance.json`
- `docs/validation/content/after/development-build.json`
- `docs/validation/content/data-only-diff.json`

Удаление любого из этих файлов останавливает генератор. Свежий экспорт баланса не заменяет их. Дополнительный параметр `-CurrentEvidence` принимает существующие машинные JSON свежих проверок; их пути и SHA256 попадут в книгу и протокол. Markdown-отчёты для этого параметра не подходят. [Сохранённые проверки и их ограничения](../../docs/validation/README.md).

`Tools/Validate-ContentDataOnly.ps1` создаёт новый `content/code-freeze.json` в режиме `Freeze`, а `Verify` читает этот снимок и записывает `data-only-diff.json`. Старый freeze не нужен для нового эксперимента; результаты прежнего эксперимента не следует выдавать за свежую проверку.
