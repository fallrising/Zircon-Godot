// Headless boundary substitutes only; placement logic comes from DXItemGrid.cs.
using System;
using System.Collections.Generic;

namespace Godot
{
    public record struct Vector2(float X, float Y);
    public record struct Vector2I(int X, int Y)
    {
        public static implicit operator Vector2(Vector2I value) => new(value.X, value.Y);
    }
    public record struct Color(float R, float G, float B, float A = 1);
    public static class Colors { public static Color Lime => new(0, 1, 0); }
    public class Texture2D(int width, int height)
    {
        public Vector2 GetSize() => new(width, height);
    }
}

namespace Library
{
    public enum LibraryFile { StoreItem, Inventory }
    public enum GridType { Inventory }
    public class ItemInfo { public int Image; }
    public class ClientUserItem { public ItemInfo Info; }
}

namespace ZirconClient.Controls
{
    using Godot;
    using Library;
    public class DXControl
    {
        public Vector2 Location, Size;
        public bool PassThrough, IsControl, Visible = true, Border;
        public int ZIndex;
        public Color BorderColour, BackColour;
        public void AddControl(DXControl child) { }
        public void QueueRedraw() { }
        public void Dispose() { }
        public virtual void _Draw() { }
        public void DrawLine(Vector2 start, Vector2 end, Color colour, float width) { }
    }
    public class DXItemCell : DXControl
    {
        public const int CellWidth = 36, CellHeight = 36;
        public static DXItemCell SelectedCell;
        public int GridIndex, Slot;
        public DXItemGrid HostGrid;
        public LibraryFile ItemLibraryFile;
        public ClientUserItem[] ItemGrid;
        public GridType GridType;
        public bool ReadOnly;
        public void RefreshItem() { }
    }
    public static class MirSkin
    {
        public static readonly Dictionary<(LibraryFile, int), Texture2D> Textures = new();
        public static Texture2D GetTexture(LibraryFile library, int image)
            => Textures.GetValueOrDefault((library, image));
    }
}
