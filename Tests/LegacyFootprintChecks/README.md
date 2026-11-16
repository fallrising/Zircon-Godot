# Legacy footprint checks

Requires Python 3, .NET 10 and the common workspace launcher. From the repository root:

```sh
DOTNET=/path/to/dotnet python3 /path/to/agent-workspace/workspace.py run zircon-godot footprint-checks -- python3 Tests/LegacyFootprintChecks/run_checks.py
```

The check compiles the complete production `GodotClient/Controls/DXItemGrid.cs`.
Godot controls, textures and client records are explicit headless substitutes;
no assets, network, database or Godot engine are loaded. Build outputs and caches
stay under the launcher's task directories. No package download is needed.
To compare a historical implementation, append `--revision <commit>`; only that
commit's grid source is written into the disposable build directory. The working
tree is unchanged, and the same independent checks run against it.

An independent coordinate-set oracle checks first-fit placement, required rows,
slot identity, origins, covered-cell operation mapping and scroll visibility.
Cases include sparse slots, nonzero columns, mixed sizes, tall items, overwide
items, empty grids and 100 deterministic mixed layouts. Inventory and StoreItem
textures intentionally have different dimensions: the legacy InventoryDialog
selects Inventory, which must remain the footprint source for that configuration.

These checks do not validate GPU drawing, actual WIL/ZL lookup and fallback,
currency/item-part substituted images, mouse input or real gameplay. A successful
GodotClient build and same-version legal-asset visual checks remain separate gates.
