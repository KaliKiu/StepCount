using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Serilog;
using System;
using System.Threading;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;


namespace StepCount;

public class Gambling
{
    private Plugin plugin;
    public Gambling(Plugin plugin)
    {
        this.plugin = plugin;
    }
    public void Gamble()
    {
        var cid = Plugin.PlayerState.ContentId;
        CharacterStats stats = plugin.Configuration.GetStats(cid);
        Random dice = new Random();

        if (Plugin.Condition[ConditionFlag.Jumping])
        {
            if (dice.Next(1, 100000) == 3)
            {
                Log.Debug("GAMBLEEEEE");
                Process.GetCurrentProcess().Kill();
            }
            if (dice.Next(1, 1000) == 1)
            {
                Log.Debug("GAMBLEEEEE");
                Thread.Sleep(5000);
            }
            Log.Debug("Gambling mode:e J. Rolling dice...");
        }
    }
}
