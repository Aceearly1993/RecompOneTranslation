using ImGuiNET;
using RecompOne.Runtime.Config;

namespace RecompOne.Runtime.Host.Window;

internal static class MainMenuBar
{
    static bool _registered;

    public static void RegisterBuiltins()
    {
        if (_registered) return;
        _registered = true;
        MenuRegistry.Register("Settings", ConfigMenu, null, MenuRegistry.OrderSettings);
        MenuRegistry.Register("Mods", ModsMenu, null, MenuRegistry.OrderMods);
        MenuRegistry.Register("Debug", DebugMenu, null, MenuRegistry.OrderDebug);
    }

    public static void Draw()
    {
        if (!ImGui.BeginMainMenuBar()) return;
        MenuRegistry.DrawMenus();
        ImGui.EndMainMenuBar();
    }

    static void ConfigMenu()
    {
        if (ImGui.MenuItem("Settings..."))
            if (PanelManager.Get<SettingsPopup>() is { } popup) popup.IsOpen = true;

        ImGui.Separator();

        bool showBar = !ConfigManager.View.HideTopBar;
        if (ImGui.MenuItem("Show Menu Bar", "F1", showBar))
        {
            ConfigManager.View.HideTopBar = showBar;
            ConfigManager.SaveView(PanelManager.Panels);
        }

        bool fs = ConfigManager.View.Fullscreen;
        if (ImGui.MenuItem("Fullscreen", "F11", fs))
        {
            ConfigManager.View.Fullscreen = !fs;
            HostWindow.SetFullscreen(!fs);
            ConfigManager.SaveView(PanelManager.Panels);
        }
    }

    static void ModsMenu()
    {
        if (ImGui.MenuItem("Mods..."))
            if (PanelManager.Get<Modding.ModsPopup>() is { } popup) popup.IsOpen = true;
    }

    static void DebugMenu()
    {
        if (ImGui.BeginMenu("GPU"))
        {
            Toggle<OutputPanel>("Output");
            Toggle<VramViewerPanel>("VRAM Viewer");
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("CPU"))
        {
            Toggle<CpuStatePanel>("CPU State");
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Memory"))
        {
            Toggle<RamMapPanel>("RAM Map");
            Toggle<MemoryEditorPanel>("Memory Editor");
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Audio"))
        {
            Toggle<SpuViewerPanel>("SPU Viewer");
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("CD"))
        {
            Toggle<CdDebugPanel>("CD Debug");
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("System"))
        {
            Toggle<OverlayEventsPanel>("Overlay Events");
            Toggle<ConsolePanel>("Console");
            ImGui.EndMenu();
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Reset View")) ConfigManager.ResetView(PanelManager.Panels);
    }

    static void Toggle<T>(string label) where T : class, IPanel
    {
        var panel = PanelManager.Get<T>();
        if (panel == null) return;
        bool open = panel.IsOpen;
        if (ImGui.MenuItem(label, null, open)) panel.IsOpen = !open;
    }
}
