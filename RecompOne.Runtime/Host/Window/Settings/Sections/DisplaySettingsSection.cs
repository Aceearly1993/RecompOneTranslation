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

        bool borderless = ConfigManager.View.BorderlessFullscreen;
        if (ImGui.Checkbox("Borderless fullscreen", ref borderless))
        {
            ConfigManager.View.BorderlessFullscreen = borderless;
            if (ConfigManager.View.Fullscreen)
            {
                HostWindow.SetFullscreen(false);
                HostWindow.SetFullscreen(true);
            }
            ConfigManager.SaveView(PanelManager.Panels);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fills bordeless window instead of exclusive mode");

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
