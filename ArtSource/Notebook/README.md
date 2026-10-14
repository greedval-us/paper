# Исходники тетрадного оформления

[Руководство оформления](../../docs/Notebook-Art.md) · [Ассеты игрока](../../docs/Player-Assets.md) · [Указатель документации](../../docs/README.md)

[notebook-theme.json](notebook-theme.json) — канонический визуальный источник: палитра, контуры, заливки, эталонные подмножества штрихов, позы, время кадров, точки выстрела и отдача. Текущий набор содержит **465 форм, 112 клипов и 7 кривых отдачи**. Runtime C# интерпретирует эти данные.

## Генерация и импорт

[author_notebook.py](author_notebook.py) — воспроизводимый рецепт; он вызывает [author_battlefield.py](author_battlefield.py) для расширения окружения. Нужен Python 3 без сторонних пакетов. Из корня проекта:

```powershell
python ArtSource/Notebook/author_notebook.py
python -m unittest discover -s ArtSource/Notebook -p 'test_*.py'
```

Генерация **перезаписывает JSON и семь SVG-листов** в `Assets/PaperDefense/Art`. Ручные правки канонического JSON нужно перенести в рецепт или сохранить отдельно до запуска. PNG рядом с исходниками — изображения для просмотра, а не runtime-текстуры игры.

В Unity выполните **Paper Defense → Art → Import notebook art and current templates** или вызовите `PaperDefense.Editor.NotebookArtImportTools.ImportAll()`. Импорт проверяет весь кандидат до замены последнего рабочего ресурса [NotebookVisualTheme.asset](../../Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset). Ошибка содержит путь источника, ID формы/клипа и причину. **Export notebook SVG contact sheets** экспортирует SVG из проверенных данных, которые читает Unity. Обратного импорта SVG нет.

| Контактный лист | Содержимое |
|---|---|
| [01-modular-parts.svg](../../Assets/PaperDefense/Art/01-modular-parts.svg) | 4 основания, 7 орудий, 4 модуля |
| [02-enemy-families.svg](../../Assets/PaperDefense/Art/02-enemy-families.svg) | Семейства и уровни прорисовки врагов |
| [03-enemy-key-poses.svg](../../Assets/PaperDefense/Art/03-enemy-key-poses.svg) | Ключевые позы |
| [04-notebook-objects.svg](../../Assets/PaperDefense/Art/04-notebook-objects.svg) | Объекты и соединения стен |
| [05-ink-effects.svg](../../Assets/PaperDefense/Art/05-ink-effects.svg) | Семантические эффекты |
| [06-workshop-icons.svg](../../Assets/PaperDefense/Art/06-workshop-icons.svg) | Значки мастерской и инструментов |
| [07-battlefield.svg](../../Assets/PaperDefense/Art/07-battlefield.svg) | Окружение, смола, ремонт, входы и отметки баррикад |

## Формат и привязки

Schema 1 поддерживает polyline, простой выпуклый polygon, circle и ring. Элемент хранит имя, канал палитры, нормализованные точки либо центр/радиус, толщину и alpha. Основные контуры имеют `Reference: true`; лица, крепёж, заливки, пятна и штриховка — декор.

Пятнадцать модульных деталей публикуют только reference-штрихи в revision 3 существующего ID шаблона. Импорт сохраняет pivot, вес и имя шаблона; обновляет Target overrides и материализованные каталоги `Configuration/Tests`. Общий NotebookContent, Historical, отдельные PlayerContent/PlayerRules, профили и рисунки игрока этим инструментом не переписываются. Обновлённый общий визуальный ресурс доступен всем сценам.

Оружие направлено по локальной +Y и вращается вокруг (0,0); смещения креплений принадлежат основанию. Враги направлены по локальной +X. Биты стен: N=1, E=2, S=4, W=8. Кадр хранит ID формы, секунды, жёсткое смещение/поворот, равномерный масштаб и alpha. Кривая отдачи кусочно-линейная, на обоих концах нулевая; она не меняет исходную геометрию рисунка.

Шесть семейств врагов — дудл, бегун, грызун, плевун, громила и щитоносец — имеют три уровня прорисовки и состояния idle/walk/attack/hit/death. Число форм включает ключевые позы и UI-варианты, поэтому не равно числу игровых объектов. Полноту новых семейств и окружения проверяет [test_battlefield_art.py](test_battlefield_art.py).

Свободные рисунки игрока используют правила геометрии; исторические эталоны и оценки сохраняются для совместимости. Точность обводки не даёт боевого бонуса. [Правила формы](../../docs/Freeform-Towers.md).
