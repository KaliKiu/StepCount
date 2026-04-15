using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using SamplePlugin.Windows;
using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private const string CommandName = "/stepcount";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("StepCount");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private DateTime _lastCheck = DateTime.MinValue;

    private Vector3 _lastPosition = Vector3.Zero;

    private float _distanceBuffer = 0f;

    private const float LalaStrideLength = 1.35f;

    const double SecondsPerStep = 0.2;


    public Plugin()
    {
        Log.Information("Loading Plugin..");
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open StepCount"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("Loading StepCounting..");

        Framework.Update += OnUpdate;
    }

    public void OnUpdate(IFramework framework)
    {
        StepCalc();
        if (DateTime.Now - _lastCheck < TimeSpan.FromSeconds(1))
        {
            return;
        }
        _lastCheck = DateTime.Now;
        Gambling();
    }

    private bool _queuedLogout = false;

    public void Gambling()
    {
        var cid = ClientState.LocalContentId;
        CharacterStats stats = Configuration.GetStats(cid);
        if (stats.GamblingMode)
        {
            Random dice = new Random();

            if (Plugin.Condition[ConditionFlag.Jumping])
            {
                if (dice.Next(1, 1000000) == 3)
                {
                    Log.Debug("GAMBLEEEEE");
                    Process.GetCurrentProcess().Kill();
                }
                if (dice.Next(1, 1000) == 1)
                {
                    Log.Debug("GAMBLEEEEE");
                    Thread.Sleep(5000);
                }
                else
                {
                    Log.Debug("FREEZE FAILED");
                }
                Log.Debug("gamblefailed");
                return;
            }
            Log.Debug("Not jumping");
            return;
        }
        }

    public void StepCalc()
    {
        var player = ClientState.LocalPlayer;
        var cid = ClientState.LocalContentId;

        if (player == null || cid == 0) return;

        if (Condition[ConditionFlag.Mounted] ||
            Condition[ConditionFlag.InFlight] ||
            Condition[ConditionFlag.BetweenAreas])
        {
            _lastPosition = player.Position;
            return;
        }

        var currentPos = player.Position;

        if (_lastPosition == Vector3.Zero)
        {
            _lastPosition = currentPos;
            return;
        }

        Vector2 oldPos2D = new Vector2(_lastPosition.X, _lastPosition.Z);
        Vector2 newPos2D = new Vector2(currentPos.X, currentPos.Z);

        float travel = Vector2.Distance(oldPos2D, newPos2D);

        if (travel > 0.001f && travel < 10.0f)
        {
            _distanceBuffer += travel;

            if (_distanceBuffer >= LalaStrideLength)
            {

                var stats = Configuration.GetStats(cid);

                stats.TotalSteps++;
                stats.TotalWalkingSeconds += SecondsPerStep;

                _distanceBuffer -= LalaStrideLength;

                Log.Debug($"Step! Char: {cid} Total: {stats.TotalSteps}");

                if (stats.TotalSteps % 100 == 0)
                {
                    Configuration.Save();
                }
            }
        }
        _lastPosition = currentPos;
        return;
    }

    public void Dispose()
    {
        if (this.Configuration != null)
        {
            this.Configuration.Save();
            Log.Information("Step data saved successfully during shutdown.");
        }

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        Framework.Update -= OnUpdate;
    }

    private void OnCommand(string command, string args)
    {

        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
