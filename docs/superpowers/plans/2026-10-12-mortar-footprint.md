# Mortar Footprint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Игрок задаёт сохраняемый равновеликий отпечаток мортиры и сравнивает настоящий залп по группе и колонне.

**Architecture:** Неизменяемые формы двух креплений живут в Core отдельно от художественного DrawingSnapshot. Снаряд фиксирует геометрию при запуске; сохранения, короткая проба и UI используют этот контракт. Application координирует пробу и альбом; Presentation отвечает за жест и отображение.

**Tech Stack:** Unity 6000.5.7f1, C#, существующие asmdef/Core/UGUI, NUnit/Unity Test Framework; без новых пакетов.

**Spec:** [Подтверждённый дизайн](../specs/2026-10-12-mortar-footprint-design.md).

## Global Constraints

- Первая версия: только мортира, круг/овал; отношение осей `q ∈ [1,3]`, площадь `πr²`, длинная ось вдоль полёта.
- Цена, качество, урон, дальность, интервалы, цвет и строительство не зависят от q.
- Core не ссылается на Unity/IO; `DrawingSnapshot` остаётся формата 2, стабильные ID не меняются.
- Корневой профиль 3→4, версии 1/2/3 читаются как круг; неизвестные будущие версии защищены, повреждённые данные отклоняются.
- Все сравнения и UI-тесты изолированы от профиля игрока. Сохраняем старые круговые результаты и отчёты.
- Исходное состояние `273022d` чистое. Работа в текущем разрешённом workspace; автоматические коммиты не входят в этот план.

## Review Focus

- Две мортиры одного экземпляра должны иметь разные отпечатки, а не общий параметр по типу оружия — Task 2.
- Летящий удар не меняется после удаления башни или изменения страницы — Task 2.
- Старые v3 страницы остаются читаемыми после повышения CurrentVersion; копия legacy страницы не становится пустым несохраняемым рисунком — Task 1.
- Повтор/смена расстановки/новая форма не показывают результаты старого прогона; цена и качество A/B совпадают — Tasks 3–4.
- Рисунок эллипса должен совпадать с областью поражения при повороте и разном масштабе UI, включая последний кадр после исчезновения снаряда — Tasks 2–4.

---

## Task 1: Неизменяемая форма, чертёж и совместимые сохранения

**Files:** Create `Core/MortarFootprint.cs`; modify `Core/TowerBlueprint.cs`, `Core/WorkshopProfile.cs`; tests `Tests/EditMode/MortarFootprintBlueprintTests.cs`, `Tests/PlayMode/MortarFootprintPersistenceTests.cs` плюс существующие persistence/version tests. Paths относительно `Assets/PaperDefense/`.

**Interfaces:**

- `public sealed class MortarFootprint`: `public const float MaximumAspectRatio = 3`; `public static MortarFootprint Circle`; `public float AspectRatio { get; }`; `public MortarFootprint(float aspectRatio)` строго проверяет конечное `[1,3]`.
- `TowerBlueprint.FirstFootprint`, `.SecondFootprint`; дополнительные необязательные параметры `firstFootprint = null`, `secondFootprint = null` в конце конструктора (null означает Circle).
- `TowerBlueprint.WithMortarFootprint(int mount, MortarFootprint footprint)` и `GetMortarFootprint(int mount)` принимают только установленную мортиру 0/1.
- `TowerBlueprint.AsVariant(string id, string name)` копирует Origin/рисунок/формы; прежний AsNew не используется новым действием варианта.
- `BlueprintData`: `FootprintMetadataVersion = 1`, `FirstFootprintAspect`, `SecondFootprintAspect`; DTO по умолчанию не подменяет отсутствующие значения. v4 требует метаданные; старые версии дают Circle.

- [x] Написать tests: q=1/3 допустимы; NaN/Infinity/0/3.01 отвергаются; shape-only edit сохраняет GeometryHash/Origin/Assessment/цену/темп; WithName/WithDrawing/AsVariant сохраняют формы; WithAssembly сбрасывает только заменённое оружие; legacy-вариант сохраняется и не меняет исходную страницу.
- [x] Записать RED до реализации нового контракта. Missing API/compile RED отдельно от поведенческих failures; после введения типов подтвердить именно поведенческие утверждения.
- [x] Реализовать тип/копии/DTO, профиль CurrentVersion=4 и явное чтение 2/3/4. Некруглая форма у крепления без мортиры отклоняется. Не изменять художественный формат и действующие атомарные операции store.
- [x] Проверить v1/v2/v3 migration→Circle, v4 round trip двух разных форм, отсутствующие/неконечные/выходящие за пределы данные, future root/metadata, byte-identical `.v3-original`, неудачную запись/backup через временные пути или injected IO.
- [x] Unity focused EditMode/PlayMode + существующие WorkshopStore/WorkshopPersistence/WorkshopDrawingPersistence; все assertions проходят. Независимое ревью данных до Task 2.

## Task 2: Реальная геометрия попадания и её отображение

**Files:** Create `Core/FootprintGeometry.cs`; modify `Core/WeaponSystem.cs`, `Core/MortarProjectile.cs`, `Core/SimulationEvents.cs`, `Core/ReadModel.cs`, `Presentation/Rendering/NotebookWorldView.cs`; tests `Tests/EditMode/MortarFootprintCombatTests.cs`, `Tests/PlayMode/MortarFootprintRenderingTests.cs`.

**Interfaces:**

- `public readonly struct FootprintGeometry`: конечные полуоси `ForwardRadius`, `SideRadius`, нормализованный `Forward`; `BoundingRadius`; `public static FootprintGeometry Resolve(MortarFootprint footprint, float radius, Point2 from, Point2 to)`; `public bool Contains(Point2 center, Point2 point)`.
- Нулевая траектория использует `(1,0)`; q=1 сохраняет старую круговую ветку. Radius=0 даёт только центр с прежними допусками, отрицательные/неконечные радиусы отвергаются.
- `MortarProjectile.ImpactFootprint`, `ProjectileView.ImpactFootprint`, `SimulationEvent.ImpactFootprint`: `FootprintGeometry?`. Неизменяемая геометрия захватывается при запуске; старые вручную созданные views/events без неё отображаются кругом по Radius.
- `SimulationEventBuffer.Publish(..., FootprintGeometry? impactFootprint = null)` — необязательное дополнение в конце для совместимости вызывающих.

- [x] Написать RED tests постоянной площади, q=3 полуосей `r√3/r÷√3`, вращения на 90°, граничного включения и отсечения точек внутри bounding circle, но вне эллипса.
- [x] Добавить RED реального WeaponSystem: разные формы двух мортир; ожидаемые поражённые враги; захваченная форма после уничтожения источника/замены Blueprint; общий цветовой бюджет сохранён.
- [x] Реализовать broad phase по BoundingRadius и narrow phase Contains, без изменения damage/order/цвета; передавать форму конкретного крепления в LaunchMortar.
- [x] Передать ту же геометрию в launch/impact events и projectile snapshots. World view строит периметр эллипса из мировых точек для telegraph/impact.
- [x] Focused combat/render tests + Rail/SharedTowerProduction/WeaponPublication/IndependentMountAim проходят, а круговые ожидания сохраняются. Независимое ревью до Task 3.

## Task 3: Короткая детерминированная проба

**Files:** Create `Core/MortarFootprintProbe.cs`; modify `Core/TrialRunner.cs` (TrialFrame/recording only), `Application/RunController.cs`; tests `Tests/EditMode/MortarFootprintProbeTests.cs`, `Tests/PlayMode/MortarFootprintProbeIntegrationTests.cs`.

**Interfaces:**

- `public enum FootprintFormation { Group, Column }`.
- `MortarFootprintProbe.Compare(TowerBlueprint blueprint, int mount, MortarFootprint first, MortarFootprint second, GameBalance balance, FootprintFormation formation)` возвращает `FootprintProbeComparison` с immutable First/Second `FootprintProbeReport`.
- `FootprintProbeReport`: `Frames` (owned IReadOnlyList<TrialFrame>), `HitCount`, `MissedCount`, `Cost`, `DrawingQuality`, `Completed`, `Message`.
- `RunController.CompareMortarFootprints(...)` — wrapper с текущим balance, без profile/session mutation.
- `TrialFrame.Impacts` — owned readonly список реальных ProjectileImpact events; дополнительный необязательный параметр конструктора. Большой TrialRunner также записывает bounded impacts для отображения настоящих взрывов.

- [x] Написать RED tests: circle Group=9/Column=6, q3 Group=3/Column=9; обе полосы имеют одинаковые цену/качество/оборудование/исходные позиции, повтор совпадает; результаты/снимки неизменяемы после смены черновика.
- [x] Реализовать fixture одного выбранного mortar mount через WeaponSystem. Оставить оригинальные настройки оружия/модули и одинаковый quality override исходной страницы; исключить остальные стволы. Один запуск, затем убрать источник и дождаться настоящего impact; дать конечный лимит 600 шагов по 1/30 с с явным незавершённым результатом.
- [x] Расстановки — ровно девять целей по формулам spec; ближайший центральный anchor задаёт обычное наведение. Не использовать специальные правила урона или принудительное попадание.
- [x] Сохранить реальные projectiles/impacts/позиции в записи и держать последний impact после полёта. Не менять существующие сценарии/enum 60-секундного полигона.
- [x] Integration test на `InitializeWorkshop(false)`: gold/ink/loadout/album/session Tick не меняются. TrialBalance/TrialPlayback/новые probe tests проходят. Независимое ревью до Task 4.

## Task 4: Рисуемая ось, A/B и варианты страниц

**Files:** Create `Presentation/UI/MortarFootprintPanel.cs`, `Presentation/UI/FootprintProbeSketch.cs`; modify `Presentation/UI/WorkshopHud.cs`; tests `Tests/PlayMode/MortarFootprintHudTests.cs`.

**Interfaces:**

- `MortarFootprintPanel.Open(TowerBlueprint blueprint, int mount)`; конструктор принимает callback изменения неизменяемого Draft и callback Application для CompareMortarFootprints.
- Поле ввода: центр, стрелка +Y (вперёд), draggable endpoint, ellipse preview. Pointer drag задаёт q в `[1,3]`, ширина автоматически обратная; круг доступен отдельной кнопкой.
- Два replay sketches принимают immutable FootprintProbeReport, проигрывают одни timestamps; используем единый мировой масштаб по X/Y с letterbox, рисуем мировые ellipse points.
- Кнопки «Группа», «Колонна», «Повторить», «Зафиксировать как A», «Круг»; подписи «один залп выбранной мортиры», поражено/вне отпечатка, цена/качество.

- [x] Написать RED pointer/UI tests: обе мортиры редактируются отдельно; пределы/опорные точки видны; до отпускания жеста старое воспроизведение остановлено; завершённое изменение сбрасывает результаты/время и запускает новую запись.
- [x] Реализовать соседние editor/probe области; A заморожен до явной фиксации, B отражает текущий Draft. Смена сборки/страницы закрывает или перепривязывает панель с новыми согласованными снимками.
- [x] Добавить к креплениям mortar кнопку «След выстрела», видимую характеристику q; выполнить через Draft.WithMortarFootprint без Save/IO на каждый жест.
- [x] Действие «Сделать вариант» использует AsVariant с новым ID; сохраняет через обычный RunController, исходная страница остаётся неизменной.
- [x] Проверить изолированным HUD/временным store: variant round trip, обе формы, отмена/переоткрытие, сохранение качества и существующего rejected-stroke feedback; inspect actual rendered preview/impact. Независимое ревью UI.

## Task 5: Полная проверка и передача

**Files:** docs `PaperDefense-Architecture.md`, `Balance-Parameters.md`, `Extending-Mechanics.md`, `Workshop-Guide.md`; новый отчёт `docs/validation/verification-mortar-footprint-2026-10-12.md` и raw evidence; Windows output `Builds/MortarFootprint/PaperDefense.exe`.

- [x] Выполнить final independent whole-diff review против `273022d`; устранить все подтверждённые significant findings.
- [x] Полные Unity EditMode/PlayMode проходят, compile Console чиста; неизменные круговые кампании/массовый сценарий проходят.
- [x] Собрать Windows Development Player без ошибок; проверить actual UI только в isolated/memory-only scene/player и восстановить исходную сцену. При GUI launch использовать системное разрешение, без чтения профиля игрока.
- [x] Документировать q `[1,3]`, постоянную площадь, направление и область короткой пробы, v4 migration и новые extension seams; сохранить RED/GREEN/raw build evidence.
- [x] Финально сообщить реализованный срез, фактические тесты/сборку и доступные файлы; не утверждать, что эксперимент доказал идеальный баланс или что реализованы отложенные идеи.
