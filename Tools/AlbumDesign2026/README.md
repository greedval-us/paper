# Материалы альбома 2026

Исходник проектного баланса — build-data.mjs. Реальная база — docs/audit/2026-10-17-album/current-runtime.json, экспортированная через Unity из PlayerBalance.asset. Игровые assets эти скрипты не изменяют.

Используется Node.js 24, @oai/artifact-tool и Playwright из bundled runtime Codex. node_modules — локальная ссылка на его библиотеки; зависимости не копируются в Git. Для снимков установлен Microsoft Edge, отдельный временный профиль.

```powershell
node Tools/AlbumDesign2026/build-data.mjs
node Tools/AlbumDesign2026/build-workbook.mjs
node Tools/AlbumDesign2026/verify-balance.mjs
node Tools/AlbumDesign2026/check-gallery.mjs
node Tools/AlbumDesign2026/package-materials.mjs
node Tools/AlbumDesign2026/package-full-gallery.mjs
node docs/design/game-concept-art-2026-10-16/verify.cjs
node Tools/AlbumDesign2026/check-full-gallery.mjs
node Tools/AlbumDesign2026/verify-delivery.mjs
```

Последовательность важна: данные → Excel и проверки → снимки галереи. check-gallery не запускает Unity. Excel проверяется расчётом библиотекой и сканированием ошибок; настольный Excel отдельно не запускался. Одноразовые редакторские скрипты подготовки комплекта не входят в процесс пересборки.

Полная художественная галерея находится в [game-concept-art-2026-10-16](../../docs/design/game-concept-art-2026-10-16/index.html): 30 экранов и 3 листа объектов. package-full-gallery собирает манифест и сопоставление всех 28 исходных экранов; verify.cjs проверяет неизменность файлов после упаковки; check-full-gallery проверяет просмотр в Edge. PNG редактируются через image_gen, а не пересоздаются этими скриптами. Интерактивная gallery остаётся дополнением.
