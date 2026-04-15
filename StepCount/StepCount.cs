using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using SamplePlugin.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;


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
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const string CommandName = "/stepcount";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("StepCount");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private DateTime _lastCheckGambling = DateTime.MinValue;
    private DateTime _lastCheckFcPet = DateTime.MinValue;
    private DateTime _lastCheckFreeze = DateTime.MinValue;

    private Vector3 _lastPosition = Vector3.Zero;

    private float _distanceBuffer = 0f;

    private const float LalaStrideLength = 1.35f;

    const double SecondsPerStep = 0.2;

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int VK_F8 = 0x77;

    private bool triggerFreeze= false;
    private bool hasPressed = false;
    private bool hasRepRessed = false;


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

        var cid = ClientState.LocalContentId;
        CharacterStats stats = Configuration.GetStats(cid);

        if (!stats.FcPetEnabled) return;

        var now = DateTime.Now;
        var timeSinceAction = now - _lastCheckFreeze;

        if (triggerFreeze && timeSinceAction < TimeSpan.FromMinutes(10))
        {
            if (timeSinceAction >= TimeSpan.FromSeconds(1.5) && !hasPressed && !hasRepRessed)
            {
                IntPtr windowHandle = Plugin.GameGui.GetAddonByName("ConfigKeybind", 1);
                IntPtr hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                SendMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_F8, IntPtr.Zero);
                SendMessage(hWnd, WM_KEYUP, (IntPtr)VK_F8, IntPtr.Zero);
                hasRepRessed = true;
            }
            if (timeSinceAction >= TimeSpan.FromSeconds(2) && !hasPressed)
            {
                hasPressed = true;
                hasRepRessed = false;
                System.Threading.Thread.Sleep(1000);
            }
                return;
        }
        if (triggerFreeze && timeSinceAction >= TimeSpan.FromMinutes(2))
        {
            triggerFreeze = false;
            hasPressed = false;
        }

        if (!triggerFreeze && (now - _lastCheckFcPet > TimeSpan.FromSeconds(1)))
        {
            _lastCheckFcPet = now;
            CheckForPlayers();
        }
    }

    public void CheckForPlayers()
    {
        if (ClientState.LocalPlayer == null) return;

        float detectionRadius = 3.0f;
        var myPos = ClientState.LocalPlayer.Position;

        foreach (var obj in ObjectTable)
        {
            if (obj is IPlayerCharacter pc && pc.GameObjectId != ClientState.LocalPlayer.GameObjectId)
            {
                Log.Debug("Player is away: " + Vector3.Distance(myPos, pc.Position) + " units away.");
                if (Vector3.Distance(myPos, pc.Position) <= detectionRadius)
                {

                    PressF8();

                    triggerFreeze = true;
                    _lastCheckFreeze = DateTime.Now;
                    hasPressed = false;

                    Log.Debug("Player detected! Button pressed. Starting 1.5s countdown to Sleep.");
                    break;
                }
            }
        }
    }

    public void PressF8()
    {
        IntPtr windowHandle = Plugin.GameGui.GetAddonByName("ConfigKeybind", 1);
        IntPtr hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        SendMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_F8, IntPtr.Zero);
        SendMessage(hWnd, WM_KEYUP, (IntPtr)VK_F8, IntPtr.Zero);
        
        _lastCheckFreeze = DateTime.Now;
        triggerFreeze = true;
    }
    public void Gambling()
    {
        var cid = ClientState.LocalContentId;
        CharacterStats stats = Configuration.GetStats(cid);
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
