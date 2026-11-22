using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Library;
using ZirconClient.Controls;

static class Program
{
    record Item(int Slot, int Width, int Height);
    record Placement(int Slot, int X, int Y, int Width, int Height);
    static int failures;

    static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{label}: expected {expected}, got {actual}");
    }

    // Independent oracle: explicit occupied coordinates, unbounded row search.
    // No production helpers, array capacity estimate or texture conversion reused.
    static List<Placement> Oracle(int columns, Item[] items)
    {
        var used = new HashSet<(int X, int Y)>();
        var result = new List<Placement>();
        foreach (var item in items.OrderBy(item => item.Slot))
        {
            if (item.Width > columns) continue; // A rectangle must fit whole.
            for (int origin = 0; ; origin++)
            {
                int x = origin % columns, y = origin / columns;
                if (x + item.Width > columns) continue;
                var cells = (from dx in Enumerable.Range(0, item.Width)
                             from dy in Enumerable.Range(0, item.Height)
                             select (X: x + dx, Y: y + dy)).ToArray();
                if (cells.Any(used.Contains)) continue;
                used.UnionWith(cells);
                result.Add(new(item.Slot, x, y, item.Width, item.Height));
                break;
            }
        }
        return result;
    }

    static void Check(int columns, int minimum, int length, params Item[] items)
    {
        MirSkin.Textures.Clear();
        var records = new ClientUserItem[length];
        foreach (var item in items)
        {
            records[item.Slot] = new() { Info = new() { Image = item.Slot } };
            MirSkin.Textures[(LibraryFile.Inventory, item.Slot)] =
                new Texture2D(item.Width * 36, item.Height * 36);
            // Deliberately different: the InventoryDialog selects Inventory,
            // so switching footprint lookup to StoreItem must fail this check.
            MirSkin.Textures[(LibraryFile.StoreItem, item.Slot)] = new Texture2D(36, 36);
        }
        var expected = Oracle(columns, items);
        int rows = Math.Max(Math.Max(1, minimum),
            expected.Select(p => p.Y + p.Height).DefaultIfEmpty(0).Max());
        var grid = new DXItemGrid
        {
            UseLegacyFootprints = true,
            ItemLibraryFile = LibraryFile.Inventory,
            GridSize = new(columns, rows),
            ItemGrid = records,
        };
        Equal(rows, grid.GetLegacyRequiredRows(minimum), "required rows");
        for (int cell = 0; cell < columns * rows; cell++)
        {
            int x = cell % columns, y = cell / columns;
            var p = expected.SingleOrDefault(p => x >= p.X && x < p.X + p.Width
                && y >= p.Y && y < p.Y + p.Height);
            Equal(p?.Slot ?? -1, grid.LegacyFootprintAnchor(cell), $"anchor {cell}");
            Equal(p != null && x == p.X && y == p.Y, grid.IsLegacyOriginCell(cell), $"origin {cell}");
            Equal(p != null, grid.GetLegacyPlacement(cell, out int slot, out int col,
                out int row, out int width, out int height), $"placement {cell}");
            if (p == null) continue;
            Equal((p.Slot, p.X, p.Y, p.Width, p.Height), (slot, col, row, width, height), "rectangle");
            Equal(p.Slot, grid.ResolveOperationSlot(cell), $"operation {cell}");
            Equal(records[p.Slot], grid.GetItemForCell(cell), $"item {cell}");
        }
        grid.VisibleHeight = 1;
        grid.ScrollValue = rows - 1;
        Equal(rows - 1, grid.ScrollValue, "scroll limit");
        for (int cell = 0; cell < grid.Cells.Length; cell++)
            Equal(cell / columns == rows - 1, grid.Cells[cell].Visible, $"visible {cell}");
    }

    static void Run(string name, Action action)
    {
        try { action(); Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { failures++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
    }

    static int Main()
    {
        Run("nonzero-x", () => Check(2, 1, 3, new(0, 1, 1), new(1, 1, 1), new(2, 1, 1)));
        Run("sparse-slots", () => Check(6, 6, 187, new(1, 1, 1), new(115, 1, 1), new(186, 1, 1)));
        Run("mixed-inventory-library", () => Check(3, 1, 5, new(0, 1, 1), new(2, 2, 2), new(4, 1, 2)));
        Run("tall-item-capacity", () => Check(2, 1, 1, new Item(0, 2, 4)));
        Run("reject-overwide-without-crash", () => Check(2, 1, 2, new(0, 3, 1), new(1, 1, 1)));
        Run("empty-at-least-one-row", () => Check(2, 0, 0));
        Run("deterministic-mixed-cases", () =>
        {
            var random = new Random(450);
            for (int i = 0; i < 100; i++)
            {
                int columns = random.Next(1, 7), count = random.Next(1, 16);
                var items = Enumerable.Range(0, count).Select(n =>
                    new Item(n * 3, random.Next(1, columns + 1), random.Next(1, 5))).ToArray();
                Check(columns, 1, count * 3, items);
            }
        });
        return failures == 0 ? 0 : 1;
    }
}
