using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace SamplePlugin;

[Serializable]
public class CharacterStats
{
    public double TotalSteps { get; set; } = 0;
    public double TotalWalkingSeconds { get; set; } = 0;
    public bool GamblingModeEnabled { get; set; } = true;
    public bool FcPetEnabled { get; set; } = true;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    

    public Dictionary<ulong, CharacterStats> CharacterData { get; set; } = new();
    public CharacterStats GetStats(ulong cid)
    {
        if (!CharacterData.ContainsKey(cid))
            CharacterData[cid] = new CharacterStats();

        return CharacterData[cid];
    }
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
