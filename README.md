# EMI for Casualties: Unknown

EMI is a BepInEx 5 crafting-tree HUD for Casualties: Unknown.

Current release: `1.0.0`.

## Build

```powershell
dotnet build .\EMI.csproj -c Release
```

The plugin is written to `bin\Release\EMI.dll`. Copy that file to
`BepInEx\plugins\EMI.dll` to test it in game.

The project references the game and BepInEx assemblies in the parent game
directory. It should therefore be built from its current `CraftingTree`
location.

## Current scope

- Adds normal-recipe, crafting-tree, and compendium tabs to the crafting panel.
- Lists every registered item and liquid in a searchable icon grid using the
  active game's localized names and descriptions.
- Opens producer recipes with left click and usage recipes with right or middle
  click without closing the crafting panel.
- Includes a quality catalog listing every compatible item and liquid together
  with its quality value.
- Persists preferred producer recipes and quality candidates in
  `BepInEx\plugins\EMI\preferences.json`.
- Applies compatible preferences to the crafting tree as locked selections;
  repair recipes are visible in the catalog but cannot be preferred.
- Uses the pinned recipe as the tree root.
- Applies each producer selection to every occurrence of the same product.
- Applies each quality-material selection to every matching visible quality
  requirement, while respecting the candidate exclusions of every node.
- Collapses repeated ingredients into counters and propagates required craft
  counts through downstream recipes.
- Supports concrete ingredients, quality alternatives, and repair-cycle
  boundaries.
- Uses the original pinned-recipe text area to show remaining leaf materials.
- Warns when the remaining crafting steps exceed the player's current INT,
  using the runtime recipe requirements modified by learned blueprints.
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
- Prevents hover tooltips from covered original controls from appearing through
  the crafting-tree overlay and its selection popup.
- Prevents covered original buttons from changing the cursor shown over EMI
  blank areas.
