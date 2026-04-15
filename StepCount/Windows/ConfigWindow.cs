using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("StepCount Config")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;

        Size = new Vector2(232, 120);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        var cid = Plugin.PlayerState.ContentId;
        var stats = configuration.GetStats(cid);

        var configValue = configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Button("Reset Steps"))
        {
            stats.TotalSteps = 0;
            stats.TotalWalkingSeconds = 0;
            configuration.Save();
        }
        var gamble = stats.GamblingModeEnabled;
        if (ImGui.Checkbox("Gambling Mode", ref gamble))
        {
            stats.GamblingModeEnabled = gamble;
            configuration.Save();
        }
        var pet = stats.FcPetEnabled;
        if (ImGui.Checkbox("Explosion?!", ref pet))
        {
            stats.FcPetEnabled = pet;
            configuration.Save();
        }
    }
}
