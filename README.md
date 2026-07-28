# EMI for Casualties: Unknown

EMI is a BepInEx 5 crafting-tree HUD for Casualties: Unknown.

## Build

```powershell
dotnet build .\EMI.csproj -c Release
```

The plugin is written to `bin\Release\EMI.dll`. Copy that file to
`BepInEx\plugins\EMI\EMI.dll` to test it in game.

The project references the game and BepInEx assemblies in the parent game
directory. It should therefore be built from its current `CraftingTree`
location.

## Current scope

- Adds normal-recipe and crafting-tree tabs to the crafting panel.
- Uses the pinned recipe as the tree root.
- Applies each producer selection to every occurrence of the same product.
- Applies each quality-material selection to every matching visible quality
  requirement, while respecting the candidate exclusions of every node.
- Collapses repeated ingredients into counters and propagates required craft
  counts through downstream recipes.
- Supports concrete ingredients, quality alternatives, and repair-cycle
  boundaries.
- Uses the original pinned-recipe text area to show remaining leaf materials.
- Merges matching remaining quality requirements across different parent
  recipes, even when their internal excluded-item IDs differ.
- Covers shallower tree nodes from inventory first, recalculates partially
  covered downstream craft counts, and strictly allocates liquid volumes.
- Accounts for durability consumed by every non-destroyed item requirement.
  Existing and planned items contribute uses according to their condition and
  actual quality value; no tool is treated as permanently reusable.
- Keeps the crafting-tree rows independent from inventory refreshes.
- Highlights and prioritizes currently executable selected tree recipes, then
  executable recipes that can produce an unfilled concrete or quality leaf.
- Uses a full-row translucent highlight plus a 7-pixel accent bar so recipe
  priorities remain visible despite the original button color tint.
- Highlights expandable tree leaves in light blue and terminal leaves in dark
  blue, while leaving expanded nodes in their normal row colors.
- Formats abstract quality items and liquids in natural language, and gives
  EMI buttons native hover tooltips with descriptive secondary text.
