# Notebook ink source

`notebook-theme.json` is the canonical visual source. Its coordinates, palette,
strokes, fills, reference subsets, key poses, frame timing, muzzle anchors and
weapon recoil curves are committed data. Runtime C# interprets this data; it does
not generate silhouettes or decorative hatching.

`author_notebook.py` is the repeatable offline authoring recipe. Run it with
Python 3 to regenerate the JSON and six SVG contact sheets in
`Assets/PaperDefense/Art`. No packages are needed. Inspect the SVGs in any browser.
The PNG sheets beside this file are visual review exports, not game textures.

In Unity choose **Paper Defense → Art → Import notebook art and current
templates**, or invoke `PaperDefense.Editor.NotebookArtImportTools.ImportAll()`.
The importer validates the entire candidate before replacing the last good
`Resources/Art/NotebookVisualTheme.asset`. Invalid data reports its source path,
shape/clip ID and reason. **Export notebook SVG contact sheets** writes SVGs from
the exact validated data consumed by Unity.

Schema 1 supports polyline, convex polygon, circle and ring. Marks have a name,
palette channel, normalized points or center/radius, width and alpha. Main
drawable strokes carry `Reference: true`; fills, faces, fasteners, ink spots and
hatching are decoration. The fifteen modular parts publish only reference marks
to revision 3 of their existing drawing template ID. The importer preserves each
template's pivot, weight and name and only opts current parts into the Template
visual style. It never changes numeric combat data, historical source catalogs,
legacy templates, saved drawings, evaluation stamps or player profiles.

Current template publication targets the main Target override and the materialized
catalogs in `Configuration/Tests`. The shared `NotebookContent` and Historical
assets remain archival. This prevents current art updates from rewriting old
reference contours merely because both presets share a source database.

All weapons face local +Y and rotate about (0,0). Their mounting offsets come from
the chassis definition. Enemy artwork faces local +X. Wall masks use N=1, E=2,
S=4, W=8. Frames store a shape ID, time in seconds and a rigid offset/rotation,
uniform scale and alpha. Recoil is a piecewise linear curve with exact zero at
both endpoints; it never modifies the underlying drawing geometry.

The manifest covers 15 modular part shapes, 12 enemy appearances with five states
and four authored frames each, 26 object variants, 22 semantic effect shapes and
clips, and 25 UI icons. Shape IDs and completeness are validated by the importer;
the actual catalog count includes pose frames and duplicated UI vectors.
