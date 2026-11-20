# Каталог контента и баланс

Основная цепочка конфигурации: [PlayerBalance](../Assets/PaperDefense/Configuration/Game/PlayerBalance.asset) → [PlayerRules](../Assets/PaperDefense/Configuration/Game/PlayerRules.asset) → [PlayerContent](../Assets/PaperDefense/Configuration/Game/PlayerContent.asset) → [PlayerAssemblyCatalog](../Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset).

## Ответственность источников

| Источник | Содержимое |
|---|---|
| GameBalanceAsset | Выбор пресета через dataPreset |
| BalancePresetAsset.rules | Карта, волны, материалы, враги, строительство, эффекты, прогрессия, толпа и полигон |
| ContentDatabaseAsset | База определений, эффектов и совместимых проекций; ссылка assemblyCatalog |
| AssemblyCatalogAsset | Версионированные составные детали и стартовые рецепты |
| Resources/Configuration/AssemblyFits.json | Физические размеры и совместимость; загрузка через AssemblyFitResources |
| Tools/AlbumDesign2026/build-data.mjs | Авторский числовой источник составного каталога |
| docs/balance/assembly-runtime.json | Экспорт основной игры через производственный resolver |

Каталог `album-v1.4` содержит 91 определение. В него входят детали основы, оружия и семи семейств сооружений. ID и ревизия определяют конкретную деталь; порядок карточек и отображаемое имя не заменяют идентичность. [Характеристики](Component-Stats-and-Abilities.md), [добавление деталей](Extending-Mechanics.md).

## Подготовка и снимки

BalancePresetAsset создаёт баланс из rules и независимой копии базы. Его overrides полностью заменяют существующую запись с тем же ID: отсутствие записи означает наследование, а 0 и false остаются явными значениями. Проверки каталога и BalanceValidation выполняются до запуска; неверные данные не заменяются молча значениями по умолчанию.

GameSession, WeaponSystem и TrialRunner захватывают снимки. Изменение Inspector влияет на следующую подготовку, а собранному Player для доставки новых assets нужна новая сборка.

AssemblyRecipe хранит ссылки на выбранные части, RecipeSnapshot — снимки использованных определений. AssemblyResolver применяет совместимость, модификаторы, материал и числовые ограничения. Публикация нового состава дополнительно требует AssemblyFitRules; отдельная проверка чисел не доказывает физическую совместимость.

## Сохранённые данные

Профиль v9 хранит альбом до 64 страниц, восемь назначенных ID, библиотеку компонентов, права, XP/золото и квитанции. Начатая партия получает собственный набор. Изменение библиотечного оружия не переписывает уже сохранённую башню.

Chassis/Weapons/Modules, Drawing и Freeform остаются частью совместимых API и readers. Новые детали основной мастерской добавляются в составной каталог. Удаление или отключение определения не должно уничтожать пользовательские страницы и права. Старые enum, ID и версии вложенных данных не перенумеровываются. [Совместимость профилей](Content-Profile-Compatibility.md).

## Обновление и проверка

Числовое изменение в build-data.mjs проходит импорт AlbumBalanceImport, экспорт BalanceWorkbookExport.ExportPlayerAssembly и независимую сверку. Порядок команд находится в [Tools/AlbumDesign2026](../Tools/AlbumDesign2026/README.md). Правка Excel автоматически не меняет игру.

Historical и Target — отдельные пресеты для редакторских сравнений. Их экспорт не заменяет цепочку Player. Новое поведение атаки, эффект с новым состоянием или новая категория требуют C#-реализации и проверки совместимости. [Источники параметров](Balance-Parameters.md), [проверки](validation/README.md).
