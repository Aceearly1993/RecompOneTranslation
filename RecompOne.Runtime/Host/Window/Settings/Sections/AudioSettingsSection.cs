using ImGuiNET;
using RecompOne.Runtime.Config;

namespace RecompOne.Runtime.Host.Window;

internal sealed class AudioSettingsSection : ISettingsSection
{
    public string Id => "audio";
    public string Title => "Audio";
    public int Order => 10;
    
    public static void Apply()
    {
        var game = ConfigManager.Game;
        Audio.SetMasterVolume(game.Muted ? 0f : game.MasterVolume);
        var spu = Runtime.Spu;
        if (spu == null) return;
        spu.VoiceGain = game.SpuVolume;
        spu.XaGain = game.XaVolume;
    }

    public void Draw()
    {
        var game = ConfigManager.Game;

        float volume = game.MasterVolume;
        if (ImGui.SliderFloat("Master", ref volume, 0f, 1f, "%.2f"))
        {
            game.MasterVolume = Math.Clamp(volume, 0f, 1f);
            Apply();
            ConfigManager.SaveGame();
        }

        float spu = game.SpuVolume;
        if (ImGui.SliderFloat("SPU", ref spu, 0f, 1f, "%.2f"))
        {
            game.SpuVolume = Math.Clamp(spu, 0f, 1f);
            Apply();
            ConfigManager.SaveGame();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sound effects and sequented music");

        float xa = game.XaVolume;
        if (ImGui.SliderFloat("XA", ref xa, 0f, 1f, "%.2f"))
        {
            game.XaVolume = Math.Clamp(xa, 0f, 1f);
            Apply();
            ConfigManager.SaveGame();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Streamed audio from the disc");

        ImGui.Separator();

        bool muted = game.Muted;
        if (ImGui.Checkbox("Mute", ref muted))
        {
            game.Muted = muted;
            Apply();
            ConfigManager.SaveGame();
        }
    }
}
