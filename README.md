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
- Collapses repeated ingredients into counters and propagates required craft
  counts through downstream recipes.
- Supports concrete ingredients, quality alternatives, repair-cycle
  boundaries, and reusable-tool deduplication.
- Hides the original `pinRecipeText` rendering.
- Does not calculate inventory coverage or remaining leaf requirements yet.
