# Ресурсы игрового оформления

Текущая ревизия исходных и runtime-каталогов — `modular-full-catalog-2026-10-24-v8`: 54 определения деталей, 141 родная растровая область и 431 alias, всего 572 записи runtime-каталога. Современные детали используют 10 PNG-источников, включая 51 область `part.full.*`. Фактический PNG атласа имеет размер 4096×4352. Эти числа проверены чтением JSON и заголовка PNG; они не являются результатом визуальной приёмки.

Художественные первоисточники — три утверждённых листа: [башни и основы](design/album-assembly-2026-10-17/art/01-towers.png), [части оружия](design/album-assembly-2026-10-17/art/02-weapon-parts.png), [сооружения](design/album-assembly-2026-10-17/art/03-structures.png). Они задают силуэты, материалы, синюю ручку и тёплые светлые поверхности. Игровые PNG — отдельные прозрачные части с авторскими креплениями; полные концептные страницы в атлас не импортируются. [Контракт ресурсов](Modular-Asset-Contract.md).

## Исходники

| Документ или PNG | Назначение |
|---|---|
| [sprites.source.json](../ArtSource/Notebook/NewStyle/sprites.source.json) | Области исходных PNG, физические bounds и aliases |
| [assembly-parts.source.json](../ArtSource/Notebook/NewStyle/assembly-parts.source.json) | 54 определения, canvas/trim/region, крепления, слои, оси, muzzle и физические области; независимая историческая компоновка |
| [full-catalog-layout.json](../ArtSource/Notebook/NewStyle/full-catalog-layout.json) | Листы и точные области полного каталога |
| [StandardFoundation-v10.png](../ArtSource/Notebook/NewStyle/StandardFoundation-v10.png) | Современная двухместная основа, canvas 1536×1024 |
| [BatteryFoundation-v4.png](../ArtSource/Notebook/NewStyle/BatteryFoundation-v4.png) | Современная трёхместная основа, canvas 1254×1254 |
| [AssemblyAccessories-v2.png](../ArtSource/Notebook/NewStyle/AssemblyAccessories-v2.png) | Шесть групп принадлежностей, canvas 1536×1024 |
| `TurnHub-v1.png` | Импортированный отдельный поворотный узел `part.full.turn.standard`; новый capture проверяется отдельно |
| [animation.json](../ArtSource/Notebook/NewStyle/animation.json) | Формы, кадры, палитра и отдача; самостоятельная версия документа |

Промпты, роли референсов и выбранные изображения сохраняются в соседних `*.provenance.json`. Импорт v8 с отдельным узлом выполнен; source и runtime содержат 141 область и 431 alias. Импорт копирует родные RGBA-области без ресэмплинга и вращения, с промежутками 4 px. Alpha помогает выбрать trim, но не определяет механические крепления. [Сборка и проверка](../Tools/ArtAssembly13/README.md).

## Runtime и предпросмотр

Общие ресурсы — `Resources/Art/NotebookAtlas.png`, `NotebookSprites.json`, `NotebookMounts.json` и `NotebookVisualTheme.asset`. Современные альбом, подготовка, полигон и бой используют общие привязки составных деталей. Названия, цены, характеристики и состояния остаются живым текстом UI.

[AssemblyVisualComposer](../Assets/PaperDefense/Presentation/Art/AssemblyVisualComposer.Catalog.cs) предоставляет `Compose`, `ComposeFoundation`, `ComposeWeapon` и `ComposePart`. [WeaponAssemblyGraphic](../Assets/PaperDefense/Presentation/UI/WeaponAssemblyGraphic.cs) показывает выбранный рецепт или деталь. [NotebookArtGraphic](../Assets/PaperDefense/Presentation/Art/NotebookArtGraphic.cs) для семи известных `ui.weapon.*` без animation clip вызывает `ComposeWeapon` с `AssemblyPreviewPose.ForWeapon()`: механизм и ствол семейства, стандартные прицел, магазин и спуск. Значок семейства и современный предпросмотр используют одни части и крепления; выбранные улучшения показывает сам рецепт. Поза и масштаб зависят от места показа.

Текущий фон меню загружается ключом `Art/Player/HomeDefenseLandscapeV2`. Фоны карты, подготовки и настроек, декор основы, домик, книга альбома, справка, бумажная карточка и руины — самостоятельные изображения `Resources/Art/Player`. Сборка общего атласа их не создаёт; отсутствие в atlas manifest не означает, что они не используются.

## Существующие профили

`ModularSprites`, `PreparationTowers` и структурные области `ScenerySprites` ещё нужны совместимости. [ProfileMigrationV8](../Assets/PaperDefense/Core/ProfileMigrationV8.cs) переводит старую страницу в новый рецепт только при эквивалентности; остальные сохраняются как `Legacy`. Для них остаются исторические чертежи, собственная геометрия рисунка, адаптер пилота и совместимые `tower.*`-иллюстрации. Современная замена не позволяет удалить все старые семантические ID. В смешанном `ScenerySprites` также остаётся живое окружение.

[Инвентаризация ART3](validation/modular-album/art3-assets-inventory.json) фиксирует состояние перед заменой. [План удаления](validation/modular-album/art3-retirement-plan.json) содержит точный список, хеши и условия; это не подтверждение выполненного удаления. Эскизы, provenance и исторические снимки сохраняются.

## Проверка

Новый импорт требует проверки родных пикселей, упаковки, креплений, физических габаритов, соседних оружий, вариантов и старых профилей. Визуальную приёмку проводят по фактическим кадрам текущей ревизии и их хешам, сравнивая с тремя исходными листами. Старый успешный прогон не подтверждает новый ресурс. Исправление cleanhub требует новых кадров; этот документ не объявляет его визуальную приёмку завершённой.

Фоны показывают целиком с исходными пропорциями; UI проверяют в 16:9 и 4:3, с длинными именами и клавиатурным фокусом. `-paper-memory-only` позволяет пройти маршрут без личного профиля.
