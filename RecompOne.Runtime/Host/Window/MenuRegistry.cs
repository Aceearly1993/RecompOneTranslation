using ImGuiNET;

namespace RecompOne.Runtime.Host.Window;

public static class MenuRegistry
{
    public const int OrderSettings = 0;
    public const int OrderMods = 100;
    public const int OrderDebug = 400;
    public const int OrderDefault = 500;

    readonly record struct Entry(string Label, Action Draw, string? Parent, int Order);

    static readonly List<Entry> _menus = [];
    static readonly List<Action> _windows = [];

    public static void Register(string label, Action drawItems) => Register(label, drawItems, null, OrderDefault);

    public static void Register(string label, Action drawItems, string? parent) => Register(label, drawItems, parent, OrderDefault);

    public static void Register(string label, Action drawItems, string? parent, int order)
    {
        if (string.IsNullOrEmpty(label) || drawItems == null) return;
        _menus.Add(new Entry(label, drawItems, parent, order));
    }

    public static void RegisterWindow(Action draw)
    {
        if (draw == null) return;
        _windows.Add(draw);
    }

    internal static void DrawMenus()
    {
        var tops = new List<(string Label, int Order, int Index, bool IsParent)>();
        var seen = new HashSet<string>();

        for (int i = 0; i < _menus.Count; i++)
        {
            var m = _menus[i];
            if (m.Parent == null)
            {
                tops.Add((m.Label, m.Order, i, false));
                continue;
            }

            if (!seen.Add(m.Parent)) continue;

            int order = m.Order;
            for (int j = i + 1; j < _menus.Count; j++)
                if (_menus[j].Parent == m.Parent && _menus[j].Order < order) order = _menus[j].Order;

            tops.Add((m.Parent, order, i, true));
        }

        tops.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));

        foreach (var top in tops)
        {
            if (!top.IsParent)
            {
                var m = _menus[top.Index];
                if (!ImGui.BeginMenu(m.Label)) continue;
                m.Draw();
                ImGui.EndMenu();
                continue;
            }

            if (!ImGui.BeginMenu(top.Label)) continue;
            foreach (var child in _menus)
            {
                if (child.Parent != top.Label) continue;
                if (!ImGui.BeginMenu(child.Label)) continue;
                child.Draw();
                ImGui.EndMenu();
            }
            ImGui.EndMenu();
        }
    }

    internal static void DrawWindows()
    {
        foreach (var draw in _windows)
            draw();
    }
}
