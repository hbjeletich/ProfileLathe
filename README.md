# Profile Lathe

A Unity editor tool for generating **solids of revolution** (vases, goblets,
bottles, chess pieces, columns, lampshades) from a hand-drawn 2D cross-section.
You sketch a profile curve, the tool revolves it around the vertical axis into a
watertight mesh, and you save it out as a mesh asset, material, and prefab.

Open it from the menu: **Tools ▸ Profile Lathe**.

![two-panel layout: profile editor + live preview]

## Features

- **Interactive profile editor** — drag control points, click the curve to add a
  point, right-click to delete. Linear or smooth Catmull-Rom interpolation.
- **Live 3D preview** — the revolved solid rebuilds as you edit, orbit/zoom in
  the viewport.
- **Partial sweeps** — revolve less than 360° for cutaway / arc shapes.
- **Procedural surface relief** — bake a tangent-space normal map of vertical
  flutes from a height field (Sobel-filtered), adding detail without geometry.
- **Presets** — Vase, Goblet, Bottle, Pawn to start from.
- **One-click export** — mesh `.asset`, material, optional baked normal map, and
  a prefab wired up and ready to drop in a scene.

## Install

Copy the `ProfileLathe` folder into your project's `Assets/`. That's it — the
included assembly definitions keep the runtime and editor code separated and
editor-only where appropriate.

Works with the Built-in pipeline (`Standard` shader) or URP
(`Universal Render Pipeline/Lit`); the tool picks whichever is available.

Requires Unity 2020.2+ (uses C# 8 switch expressions).

## Architecture

```
Runtime/
  LatheProfile.cs       Pure data: cross-section points + revolve/relief
                        settings. Sampling (incl. Catmull-Rom) and presets.
  LatheMeshBuilder.cs   Revolves the sampled section around Y into a Mesh.
                        Ring-major vertices, quad side walls, capped ends.
  SurfaceBaker.cs       Height-field → tangent-space normal map (Sobel).
Editor/
  ProfileEditorControl.cs   Reusable IMGUI control: the draggable curve canvas.
  LatheWindow.cs            The Tools ▸ Profile Lathe window; live preview,
                            material, and asset/prefab export.
```

The separation is deliberate: nothing in `Runtime/` touches editor or asset
APIs, so the mesh generation could be reused at runtime or in a build pipeline,
and the editor layer is purely the authoring UI on top of it.

## How the revolve works

The cross-section is a list of `(radius, height)` points. For each of N angular
steps the builder rotates the whole section and emits one ring of vertices;
adjacent rings are stitched into quads. A full 360° sweep reuses the first ring
as the last (no seam); a partial sweep adds a terminating ring. Triangle fans
seal the top and bottom when capping is on. Smooth shading averages normals;
flat shading splits vertices per face first.

## License

MIT — yours to use and adapt.
