using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace StepCount;

public class Explosion
{
    private Plugin plugin;

    private DateTime _lastCheckFcPet = DateTime.MinValue;
    private DateTime _lastCheckFreeze = DateTime.MinValue;
    private bool triggerFreeze = false;
    private bool hasPressed = false;
    private bool hasRepRessed = false;
    private const float detectionRadius = 1.0f;

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int VK_F8 = 0x77;

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public Explosion(Plugin plugin)
    {
        this.plugin = plugin;
    }

    /*public unsafe void SendGameCommand(string command)
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
    }*/

    public void Explode()
    {
        // Use Plugin.ClientState instead of local ClientState
        if (Plugin.ObjectTable.LocalPlayer == null) return;

        var cid = Plugin.PlayerState.ContentId;
        CharacterStats stats = plugin.Configuration.GetStats(cid);

        if (!stats.ExplosionEnabled) return;

        var now = DateTime.Now;
        var timeSinceAction = now - _lastCheckFreeze;

        if (triggerFreeze && timeSinceAction < TimeSpan.FromMinutes(10))
        {
            if (timeSinceAction >= TimeSpan.FromSeconds(1.5) && !hasPressed && !hasRepRessed)
            {
                IntPtr hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                SendMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_F8, IntPtr.Zero);
                SendMessage(hWnd, WM_KEYUP, (IntPtr)VK_F8, IntPtr.Zero);
                hasRepRessed = true;
            }
            if (timeSinceAction >= TimeSpan.FromSeconds(2) && !hasPressed)
            {
                hasPressed = true;
                hasRepRessed = false;
                System.Threading.Thread.Sleep(3000);
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
        if (Plugin.ObjectTable.LocalPlayer == null) return;

        var myPos = Plugin.ObjectTable.LocalPlayer.Position;
        string myFcTag = Plugin.ObjectTable.LocalPlayer.CompanyTag.TextValue;
        if (string.IsNullOrEmpty(myFcTag)) return;

        // Use Plugin.ObjectTable
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is IPlayerCharacter pc && pc.GameObjectId != Plugin.ObjectTable.LocalPlayer.GameObjectId && pc.CompanyTag.TextValue == myFcTag)
            {
                float distance = Vector3.Distance(myPos, pc.Position);
                Plugin.Log.Debug($"Player {pc.Name} <{pc.CompanyTag.TextValue}> is {distance:F2} units away.");
                if (Vector3.Distance(myPos, pc.Position) <= detectionRadius)
                {
                    PressF8();
                    triggerFreeze = true;
                    _lastCheckFreeze = DateTime.Now;
                    hasPressed = false;
                    break;
                }
            }
        }
    }

    public void PressF8()
    {
        IntPtr hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        SendMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_F8, IntPtr.Zero);
        SendMessage(hWnd, WM_KEYUP, (IntPtr)VK_F8, IntPtr.Zero);

        _lastCheckFreeze = DateTime.Now;
        triggerFreeze = true;
    }
}
