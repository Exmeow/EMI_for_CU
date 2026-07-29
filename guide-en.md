# User Guide

This mod provides in-game assistance for crafting.

This mod supports English and will automatically detect the language used in the game.

## Crafting Tree

The Crafting Tree lets you plan an entire crafting process.

To use it, first open the crafting panel. The mod adds three buttons on the right side of the panel: `RECIPE` opens the base game's recipe view, while `TREE` and `CATALOG` open the new pages added by this mod.

First, click the item you want to craft in the recipe list on the left, then favorite that recipe from the base game's recipe page. The product of the favorited recipe becomes the final product of the Crafting Tree.

We will use the `Sling bag` as an example. After favoriting its recipe, open the `TREE` page.

<img src="https://pic.imgdd.cc/i/033xFqIR6KVLpFfDYDhmDF.png" width="300"/>

On this page, you decide how to produce the item at every step of the process.

When an item row is light blue, you can select a recipe to produce it. For example, click the `...` button on the right side of the `Foliage bag` row.

<img src="https://pic.imgdd.cc/i/033xFqw2ol2Q7A56D5kq2Y.png" width="400"/>

Here you can choose which recipe will be used to make it. Because only one recipe can produce the `Foliage bag`, simply select that recipe.

<img src="https://pic.imgdd.cc/i/033xFtxqcsZhnfRiqxr6OY.png" width="400"/>

After choosing recipes for `Rope` and `Canvas` as well, the next row is `Item with Foliage`. This appears because the game only requires the Foliage quality, so any item with that quality can be used as the crafting material.

You may stop expanding the tree at this point. The Remaining Materials list will still show the correct quantity for this abstract material requirement.

If you continue expanding the tree, you must choose the specific item that should satisfy the abstract requirement. In this example, `Foliage`, `Dried foliage`, and `Wrapvine` are all valid candidates.

### Materials That Use Durability

<img src="https://pic.imgdd.cc/i/033xG3UCYT6nPzqCsPDnBv.png" width="400"/>

Some required items are not fully consumed during crafting. Instead, they only lose some durability, and `USES DURABILITY` is displayed beneath them.

You can also include the production of these tools in the Crafting Tree and continue expanding them. The mod handles this mechanic correctly: needing to cut five times will not make the list tell you to craft five copies of `Flimsy knife`.

## Remaining Materials

This mod replaces the base game's Remaining Materials list. It only displays materials that you have not yet obtained.

<img src="https://pic.imgdd.cc/i/033xGEFHnt5oiUdgT32k8c.png" width="200"/>

Unlike the base game, this list displays the materials at the endpoints of the Crafting Tree, as determined by the route you planned. It also automatically accounts for items you already possess.

In particular, the list accounts for items you already own that appear midway through the crafting route. For example, if you are making a `Sling bag` and already have a `Foliage bag`, the list will not ask you to obtain the endpoint materials needed to make another `Foliage bag`.

The list also checks the INT requirements of all remaining crafting steps and compares the highest requirement with your current INT level. A warning appears if the requirement exceeds your INT level.

<img src="https://pic.imgdd.cc/i/033xGutbV2oDBG9rwi7bFK.png" width="200"/> <img src="https://pic.imgdd.cc/i/033xGut4eJPBeFevPXUzo0.png" width="200"/>

## Crafting

Once you have obtained all or some of the required materials, you can begin crafting.

<img src="https://pic.imgdd.cc/i/033xGZYC3aYR5AAGvyP9wF.png" width="500"/>

The mod highlights and moves to the top every step that can currently be performed. Select and craft the highlighted recipes to work through the plan until you produce the final item.

## Catalog

The `CATALOG` page lets you browse every item in the game and its related recipes.

(The current version displays every item immediately and does not account for gradual discovery or unlocking. Sorry!)

<img src="https://pic.imgdd.cc/i/033xGfab5mJ10fT0MViXTr.png" width="300"/>

Left-click an item to view recipes that produce it. Right-click it to view recipes that use it.

After opening a recipe list, click a recipe to favorite or unfavorite it.

<img src="https://pic.imgdd.cc/i/033xGjZX7hCRRVXHF0YdeB.png" width="400"/>

You cannot favorite multiple recipes that produce the same output. Favoriting another one automatically unfavorites the recipe you selected earlier.

Favorited recipes are saved permanently to a file and are not lost when you close the game.

Favorited recipes are selected automatically when you plan a Crafting Tree. If you prefer not to plan the same crafting route repeatedly, make good use of favorites.

**Have fun!**
