using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows;

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


        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        double decimalHours = this.plugin.Configuration.TotalWalkingSeconds / 3600.0;

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

                // We force the cursor to the offset position for EVERY pass
                ImGui.SetCursorPos(new Vector2(startPos.X + (x * thick), startPos.Y + (y * thick)));
                DrawHudContent(blackOutline, blackOutline, decimalHours);
            }
        }

        ImGui.SetCursorPos(startPos);
        DrawHudContent(neonColor, whiteColor, decimalHours);

        ImGui.Separator();
    }

    private void DrawHudContent(Vector4 labelColor, Vector4 valueColor, double decimalHours)
    {
        // Capture the line start so WALK knows exactly where to align
        var lineStart = ImGui.GetCursorPosX();

        ImGui.TextColored(labelColor, "STEPS ");
        ImGui.SameLine(lineStart + 75);
        ImGui.TextColored(valueColor, $"{this.plugin.Configuration.TotalSteps:N0}");

        // We don't use a simple TextColored here, we ensure it's on a new line 
        // but at the same X-offset as the first line
        ImGui.SetCursorPosX(lineStart);
        ImGui.TextColored(labelColor, "WALK  ");
        ImGui.SameLine(lineStart + 75);
        ImGui.TextColored(valueColor, $"{decimalHours:F2}h");
    }
}
