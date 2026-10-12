# Excel механик и баланса

1. В Unity выполните `Paper Defense > Export mechanics and balance JSON` или вызовите `PaperDefense.Editor.BalanceWorkbookExport.Export()`.
2. В корне проекта выполните `Tools/BalanceWorkbook/Build.ps1`.
3. Проверьте `docs/balance/PaperDefense-Mechanics-Balance.xlsx`. `workbook-validation.json` сохраняет хэш данных, результат сверки формул и ограничения проверки.

Экспорт читает обе настройки через `CreateWorkshopBalance()`, включая Target, генерацию угроз и значения по умолчанию. Он не меняет настройки, профиль или текущую партию. JSON содержит полный словарь public-полей, каталоги, configured/effective механики и эталоны `TowerCatalog`.

Генератор использует только `@oai/artifact-tool` из bundled workspace runtime. `node_modules` — локальная junction, исключённая из Git. Если файлы источников изменились после экспорта, генератор останавливается и требует новый экспорт Unity. Числа баланса не копируются в генератор.

В книге нет игрового переключателя механик. Лист «Механики» описывает developer configuration для следующего запуска. Значения Excel не импортируются обратно в Unity.
