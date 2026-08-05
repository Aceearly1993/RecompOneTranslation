using ImGuiNET;
using RecompOne.Runtime.Config;

namespace RecompOne.Runtime.Host.Window;

internal sealed class DisplaySettingsSection : ISettingsSection
{
    public string Id => "display";
    public string Title => "Display";
    public int Order => 5;

    public void Draw()
    {
        bool fullscreen = ConfigManager.View.Fullscreen;
        if (ImGui.Checkbox("Fullscreen", ref fullscreen))
        {
            ConfigManager.View.Fullscreen = fullscreen;
            HostWindow.SetFullscreen(fullscreen);
            ConfigManager.SaveView(PanelManager.Panels);
        }

        bool vsync = ConfigManager.View.VSync;
        if (ImGui.Checkbox("VSync", ref vsync))
        {
            ConfigManager.View.VSync = vsync;
            HostWindow.SetVSync(vsync);
            ConfigManager.SaveView(PanelManager.Panels);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Syncs with refresh rate");

        bool native = ConfigManager.View.NativeResolution;
        if (ImGui.Checkbox("Native resolution", ref native))
        {
            ConfigManager.View.NativeResolution = native;
            Hle.GpuHle.NativeResolution = native;
            ConfigManager.SaveView(PanelManager.Panels);
            NoticePopup.Show("You need to restart the application to apply this configuration");
        }
        if (ConfigManager.View.NativeResolution != (Hle.GlVram.Scale == 1))
            ImGui.TextDisabled("restart is required");

        ImGui.Separator();

        string[] backends = ["auto", "gl45", "gl33"];
        string current = ConfigManager.View.GpuBackend;
        int index = Array.IndexOf(backends, current);
        if (index < 0) index = 0;
        if (ImGui.Combo("Graphics backend", ref index, backends, backends.Length))
        {
            ConfigManager.View.GpuBackend = backends[index];
            ConfigManager.SaveView(PanelManager.Panels);
            NoticePopup.Show("You need to restart the application to apply this configuration");
        }
        ImGui.TextDisabled($"running: {Hle.GpuBackendFactory.Selected}");

        ImGui.Separator();

        float scale = ConfigManager.View.UiScale;
        if (ImGui.SliderFloat("UI scale", ref scale, 0.5f, 3f, "%.2fx"))
        {
            ConfigManager.View.UiScale = scale;
            ImGui.GetIO().FontGlobalScale = ConfigManager.View.UiScale;
            ConfigManager.SaveView(PanelManager.Panels);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("scales interface");
    }
}
