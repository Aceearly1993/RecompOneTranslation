using System.Numerics;
using ImGuiNET;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Host.Window;

namespace RecompOne.Runtime.Modding;

internal sealed class ModsPopup : IPanel
{
    public string Name => "Mods";
    public bool IsOpen { get; set; }

    static readonly Dictionary<string, uint> _icons = new();
    static readonly HashSet<string> _iconTried = new();

    public void Draw()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new Vector2(520, 460), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));

        bool open = IsOpen;
        if (!ImGui.Begin(Name, ref open,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoSavedSettings))
        {
            IsOpen = open;
            ImGui.End();
            return;
        }

        var mods = ModLoader.Mods;
        int active = 0;
        foreach (var m in mods) if (m.Loaded) active++;

        ImGui.TextDisabled($"{mods.Count} mod(s)  ·  {active} active");
        ImGui.Separator();
        ImGui.Spacing();

        if (mods.Count == 0)
            ImGui.TextDisabled("No mods found. Drop mods into the mods folder and restart.");
        else
            foreach (var mod in mods) DrawMod(mod);

        IsOpen = open;
        ImGui.End();
    }

    void DrawMod(ModEntry mod)
    {
        ImGui.PushID(mod.Info.Id);
        ImGui.BeginChild("card", new Vector2(0, 0), ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY);

        var top = ImGui.GetCursorPos();
        float avail = ImGui.GetContentRegionAvail().X;

        uint tex = Icon(mod);
        if (tex != 0)
        {
            ImGui.Image((nint)tex, new Vector2(44, 44));
            ImGui.SameLine();
        }

        ImGui.BeginGroup();

        ImGui.TextUnformatted(mod.Info.Name);
        if (!string.IsNullOrEmpty(mod.Info.Version))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"v{mod.Info.Version}");
        }
        if (!string.IsNullOrEmpty(mod.Info.Author))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"· {mod.Info.Author}");
        }
        if (!mod.Enabled)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("[disabled]");
        }
        else if (!mod.Loaded)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.45f, 1f), "[failed]");
        }

        if (!string.IsNullOrWhiteSpace(mod.Info.Description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(mod.Info.Description);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();

        bool enabled = mod.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
            ModLoader.SetEnabled(mod.Info.Id, enabled);

        ImGui.EndGroup();

        const string dots = "...";
        float bw = ImGui.CalcTextSize(dots).X + ImGui.GetStyle().FramePadding.X * 2f;
        ImGui.SetCursorPos(new Vector2(top.X + avail - bw, top.Y));

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.20f));
        if (ImGui.SmallButton(dots))
            ImGui.OpenPopup("opts");
        ImGui.PopStyleColor(3);

        if (ImGui.BeginPopup("opts"))
        {
            if (!mod.Enabled) ImGui.BeginDisabled();
            if (ImGui.MenuItem("Reload"))
                ModLoader.Reload(mod.Info.Id);
            if (!mod.Enabled) ImGui.EndDisabled();
            ImGui.Separator();
            ImGui.TextDisabled(mod.Info.Id);
            ImGui.TextDisabled($"{mod.HookCount} hook(s)");
            ImGui.EndPopup();
        }

        ImGui.EndChild();
        ImGui.Spacing();
        ImGui.PopID();
    }

    static uint Icon(ModEntry mod)
    {
        if (mod.IconData == null) return 0;
        if (_icons.TryGetValue(mod.Info.Id, out var t)) return t;
        if (!_iconTried.Add(mod.Info.Id)) return 0;
        var tex = HostWindow.UploadPng(mod.IconData);
        if (tex != 0) _icons[mod.Info.Id] = tex;
        return tex;
    }
}
