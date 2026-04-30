using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace StepCount.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("StepCountHUD##StepCounterWindow", 
            ImGuiWindowFlags.NoScrollbar | 
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoDecoration)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 50),
            MaximumSize = new Vector2(500, 100)
        };

        this.RespectCloseHotkey = false;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Access ClientState through the plugin reference
        var cid = Plugin.PlayerState.ContentId;

        // If we aren't fully logged in, don't draw anything to avoid errors
        if (cid == 0)
        {
            ImGui.Text("Waiting for character...");
            return;
        }

        // Get the specific stats for this character
        var stats = this.plugin.Configuration.GetStats(cid);
        double decimalHours = stats.TotalWalkingSeconds / 3600.0;

        // --- DRAWING LOGIC ---
        var neonColor = new Vector4(0.0f, 1.0f, 1.0f, 1.0f);
        var whiteColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        var blackOutline = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

        var startPos = ImGui.GetCursorPos();
        float thick = 1.5f;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                ImGui.SetCursorPos(new Vector2(startPos.X + (x * thick), startPos.Y + (y * thick)));
                DrawHudContent(blackOutline, blackOutline, stats.TotalSteps, decimalHours);
            }
        }

        ImGui.SetCursorPos(startPos);
        DrawHudContent(neonColor, whiteColor, stats.TotalSteps, decimalHours);

        ImGui.Separator();
    }

    private void DrawHudContent(Vector4 labelColor, Vector4 valueColor, double totalSteps, double decimalHours)
    {
        var lineStart = ImGui.GetCursorPosX();

        ImGui.TextColored(labelColor, "STEPS ");
        ImGui.SameLine(lineStart + 75);
        ImGui.TextColored(valueColor, $"{totalSteps:N0}");

        ImGui.SetCursorPosX(lineStart);
        ImGui.TextColored(labelColor, "WALK  ");
        ImGui.SameLine(lineStart + 75);
        ImGui.TextColored(valueColor, $"{decimalHours:F2}h");
    }
}
