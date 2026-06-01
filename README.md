# Profile Lathe

A Unity editor tool for generating **solids of revolution** — vases, goblets,
bottles, chess pieces, columns, lampshades — from a hand-drawn 2D cross-section.
Sketch a profile curve, watch it revolve into a watertight mesh in a live
preview, add procedural surface relief, and export a mesh, material, and prefab
ready to drop into a scene.

Open it from **Tools ▸ Profile Lathe**.

<!-- ┌─────────────────────────────────────────────────────────────┐
     │  REPLACE: hero GIF of the two-panel window — dragging a       │
     │  profile point on the left, solid updating live on the right. │
     │  A GIF sells this far better than a still. ~10s loop.         │
     └─────────────────────────────────────────────────────────────┘ -->
![Profile Lathe — profile editor and live preview](Documentation~/hero.gif)

---

## Why this exists

Modeling a simple radially-symmetric object — a vase, a goblet, a turned table
leg — in a DCC tool and reimporting it is heavy for what is fundamentally a 2D
problem: a curve spun around an axis. Profile Lathe keeps that whole loop inside
Unity. You author the silhouette, the solid is generated procedurally, and you
get clean engine-native assets out the other end with no round trip.

## Features

- **Interactive profile editor.** Drag control points, click an empty spot on
  the curve to insert a point, right-click to delete. Switch between straight
  (linear) segments and smooth Catmull-Rom interpolation.
- **Live 3D preview.** The revolved solid rebuilds as you edit. Drag to orbit,
  scroll to zoom; the camera persists across edits. Rendered with a
  self-contained `PreviewRenderUtility` scene, so it never touches your open
  scene or its lighting.
- **Full or partial sweeps.** Revolve a full 360° for a closed solid, or less
  for an arc / cutaway. Closed sweeps share the seam ring so there's no doubled
  geometry.
- **Procedural surface relief.** Bake a tangent-space normal map of vertical
  flutes from a height field, adding fine detail without adding triangles. The
  bake is a Sobel-filtered gradient of the height function — the same technique
  used for terrain and detail normals, generalized to a procedural source.
- **Smooth or flat shading**, **double-sided materials** for hollow forms,
  adjustable color / metallic / roughness.
- **Presets** — Vase, Goblet, Bottle, Pawn — to start from instead of a blank
  canvas.
- **One-click export.** Writes a mesh `.asset`, a material, an optional baked
  normal-map texture, and a prefab with everything wired up, into a folder you
  choose.

<!-- REPLACE: 3–4 small screenshots — a few exported shapes in a scene,
     the flute relief close-up, a partial-sweep cutaway. A gallery row. -->
| Vase | Goblet | Fluted column |
|---|---|---|
| ![](Documentation~/vase.png) | ![](Documentation~/goblet.png) | ![](Documentation~/flutes.png) |

## Installation

### Via Package Manager (Git URL) — recommended

1. In Unity, open **Window ▸ Package Manager**.
2. Click **+** ▸ **Add package from git URL…**
3. Paste:
   ```
   https://github.com/yourname/ProfileLathe.git
   ```
4. To lock to a specific release instead of the latest commit, append a tag:
   ```
   https://github.com/yourname/ProfileLathe.git#v1.0.0
   ```

Requires the Git executable to be installed and on your `PATH`.

### Manual

Download the repository and copy the package folder into your project's
`Packages/` directory (or `Assets/` — both work; the included assembly
definitions keep runtime and editor code correctly separated either way).

### Requirements

- **Unity 2020.2 or newer** (the code uses C# 8 switch expressions).
- Works with the **Built-in** render pipeline (`Standard` shader) or **URP**
  (`Universal Render Pipeline/Lit`) — the tool selects whichever is present.
  Note: the double-sided toggle relies on a runtime cull property and takes
  effect on URP; Built-in's `Standard` has no runtime cull control.

## Usage

1. Open **Tools ▸ Profile Lathe**.
2. Pick a preset, or draw your own cross-section on the left panel. The dashed
   blue line is the axis of revolution; the X position of each point is its
   radius from that axis, Y is its height.
3. Adjust the revolve settings — segments, sweep angle, caps, world scale.
4. Optionally enable **Vertical Flutes** under Surface Relief and tune the count
   and depth.
5. Set a material color / metallic / roughness.
6. Set an output folder and name, then **Save Mesh + Prefab**.

**Editor shortcuts:** drag a point to move it · click empty curve space to add a
point · right-click a point to delete it.

## Architecture

The package is split into a render-pipeline-agnostic **Runtime** layer that does
the geometry, and an **Editor** layer that is purely the authoring UI on top of
it. Nothing in `Runtime/` references editor or asset APIs, so the mesh generation
could be driven at runtime or from a build script without the window.

```
Runtime/
  LatheProfile.cs        Pure data: cross-section points + revolve/relief
                         settings. Owns sampling (linear & Catmull-Rom),
                         world-scale mapping, and the built-in presets.
  LatheMeshBuilder.cs    Revolves the sampled section around the Y axis into a
                         UnityEngine.Mesh. Ring-major vertices, quad side walls,
                         triangle-fan caps, smooth/flat normals, tangents.
  SurfaceBaker.cs        Procedural height field → tangent-space normal map via
                         a Sobel filter. Reusable for other relief sources.

Editor/
  ProfileEditorControl.cs  Reusable IMGUI control that draws the draggable curve
                           canvas inside a Rect and reports edits. No window
                           state — it just mutates the LatheProfile it's handed.
  LatheWindow.cs           The Tools ▸ Profile Lathe window. Owns the two-panel
                           layout, the PreviewRenderUtility preview, material
                           setup, and the asset/prefab export.
```

### How the revolve works

The cross-section is a list of `(radius, height)` points. For each of *N*
angular steps the builder rotates the entire section by that step's angle and
emits one **ring** of vertices; adjacent rings are stitched into quads with
consistent (clockwise, Unity-convention) winding so normals face outward.

- A **full 360° sweep** reuses the first ring as the last, so there's no
  duplicated seam.
- A **partial sweep** adds one terminating ring instead.
- **Caps** are triangle fans from a center vertex at the top and bottom.
- **Smooth shading** averages vertex normals across shared faces; **flat
  shading** first splits the mesh into per-face vertices so each face gets a
  hard normal.

### How the relief bake works

The flute pattern is evaluated as a height field — a cosine repeated *fluteCount*
times around the U axis, scaled by depth. That field is differentiated with a
Sobel kernel to recover surface gradients, which are packed into an RGBA
tangent-space normal map. Because U wraps around the revolve, the map is set to
repeat horizontally so the flutes tile seamlessly around the form. This adds the
*appearance* of carved relief at any view distance without spending a single
extra triangle.

## Design notes & limitations

- **The mesh is a single-walled shell.** A goblet looks hollow because it *is*
  hollow — there's no inner wall, just one surface. The double-sided material
  toggle makes that read correctly; true wall thickness (offsetting the profile
  inward and generating interior geometry) is a planned enhancement, not a
  current feature. This was a deliberate scope decision: a shell covers the
  large majority of lathe shapes, and double-sided rendering closes the visual
  gap cheaply.
- **Flat shading multiplies vertex count** (per-face splitting). Fine for
  preview and most game assets; not intended for extreme densities.

## Roadmap

- Wall thickness (true hollow interiors with a rim).
- Bézier tangent handles in the profile editor.
- Additional relief sources (texture-driven, noise) reusing `SurfaceBaker`.
- Optional UV unwrap modes for the side wall.

## License

MIT — see [LICENSE.md](LICENSE.md). Yours to use, modify, and ship.