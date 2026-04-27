using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Inventory;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Lumina.Excel.Sheets;
using StepCount.Windows;
using System;
using System.Buffers;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFXIVFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using Dalamud.Game.Inventory;



namespace StepCount;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IGameInventory InventoryManager { get; private set; } = null!;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);


    private const string CommandName = "/stepcount";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("StepCount");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public Explosion Explosion { get; init; }
    public Gambling Gambling { get; init; }
    public WebHook webHook { get; init; }

    private DateTime _lastCheckGambling = DateTime.MinValue;
    private DateTime _lastBotCheck = DateTime.MinValue;
    private DateTime _lastCommand = DateTime.MinValue;
    private DateTime _lastFetch = DateTime.MinValue;
    private DateTime _lastRoost = DateTime.MinValue;
    private DateTime _lastWebHook = DateTime.MinValue;

    private WebHook? _discordWebhook;

    private Vector3 _lastPosition = Vector3.Zero;
    private float _distanceBuffer = 0f;
    private const float LalaStrideLength = 1.35f;
    const double SecondsPerStep = 0.2;

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int VK_W = 0x57;
    private const int VK_4 = 0x34;
    private int count = 1;
    private bool _isKeyDown = false;

    private const int TGP = 12678;

    //we chillin? 
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

        var cid = ClientState.LocalContentId;
        CharacterStats stats = Configuration.GetStats(cid);
        _discordWebhook = new WebHook(stats.WebHookUrl);

        this.Explosion = new Explosion(this);

        Framework.Update += OnUpdate;
    }

    public void OnUpdate(IFramework framework)
    {
        StepCalc();

        var cid = ClientState.LocalContentId;
        CharacterStats stats = Configuration.GetStats(cid);
        var gamble = stats.GamblingModeEnabled;
        
        if((DateTime.Now - _lastCheckGambling > TimeSpan.FromSeconds(1.5))&&gamble)
        {
            _lastCheckGambling = DateTime.Now;
            //Gambling.Gamble();
        }
        if ((DateTime.Now - _lastBotCheck > TimeSpan.FromSeconds(0.05))&& gamble)
        {
            _lastBotCheck = DateTime.Now;
            Bot();
        }
        if (((DateTime.Now - _lastWebHook).TotalMinutes >= 5) && stats.WebHookEnabled && !string.IsNullOrEmpty(stats.WebHookUrl))
        {
            _lastWebHook = DateTime.Now; // Reset the timer

            // Call the async send method without blocking the game thread
            _ = SendPeriodicUpdate();
        }
        if (DateTime.Now - _lastFetch > TimeSpan.FromSeconds(0.3))
        {
            
            ushort territoryId = ClientState.TerritoryType;
            if(territoryId == 179) _lastRoost = DateTime.Now;
            //Log.Debug("Fetching new territory" + (int)(DateTime.Now - _lastRoost).TotalSeconds);
            _lastFetch = DateTime.Now;
        }

        Explosion.Explode();
    }

    public async Task SendPeriodicUpdate()
    {
        if (_discordWebhook == null) return;

        ushort territoryId = ClientState.TerritoryType;
        var territorySheet = DataManager.GetExcelSheet<TerritoryType>();
        var row = territorySheet?.GetRow(territoryId);
        string zoneName = row?.PlaceName.Value.Name.ToString() ?? "Unknown Area";

        // Try this first:
        var st = DateTime.UtcNow;
        try
        {
            Log.Debug("Send message... DC");
            string message = $"--------------\nUpdate\n{ClientState.LocalPlayer?.Name}\n" + zoneName + "/"+ territoryId+"\n" + st + "\nLast InnTime: " + (int)(DateTime.Now - _lastRoost).TotalSeconds + " Seconds\n_TGC_Count: " + GetItemCount(TGP);
            await _discordWebhook.Send(message);
        }
        catch (Exception ex)
        {
            Log.Debug($"Webhook failed: {ex.Message}");
        }
    }
    public uint GetItemCount(uint itemId)
    {
        uint total = 0;

        // These are the 4 main inventory tabs (0-3)
        GameInventoryType[] inventoryTypes = {
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4
    };

        foreach (var type in inventoryTypes)
        {
            // Get all items in this specific tab
            var container = InventoryManager.GetInventoryItems(type);

            foreach (var item in container)
            {
                if (item.ItemId == itemId)
                {
                    total += (uint)item.Quantity;
                }
            }
        }

        return total;
    }
    public void Bot()
    {
        IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
        if (hWnd == IntPtr.Zero) return;

        // Determine which key we are talking about
        int currentKey = (count % 2 == 0) ? VK_W : VK_4;

        if (!_isKeyDown)
        {
            // First pass: Press the key down
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)currentKey, IntPtr.Zero);
            _isKeyDown = true;
            Log.Debug($"Pressing: {currentKey}");
        }
        else
        {
            // Second pass (0.2s later): Release the key
            PostMessage(hWnd, WM_KEYUP, (IntPtr)currentKey, IntPtr.Zero);
            _isKeyDown = false;
            count++; // Only move to the next key AFTER we released the current one
            Log.Debug($"Releasing: {currentKey}");
        }
        if ((DateTime.Now - _lastCommand > TimeSpan.FromSeconds(3)))
        {
            _lastCommand = DateTime.Now;
            SendGameCommand("/fc meow");
        }
    }

    public unsafe void SendGameCommand(string command)
    {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        if (framework == null) return;

        var uiModule = framework->UIModule;
        if (uiModule == null) return;

        var utf8Command = Utf8String.FromString(command);
        if (utf8Command != null)
        {
            uiModule->ProcessChatBoxEntry(utf8Command);
            utf8Command->Dtor();
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
