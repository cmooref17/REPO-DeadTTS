using BepInEx.Configuration;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace REPO_DeadTTS.Config
{
    [Serializable]
    public static class ConfigSettings
    {
        public static ConfigEntry<float> minRandomPitch;
        public static ConfigEntry<float> maxRandomPitch;
        public static ConfigEntry<float> deadTTSVolume;
        public static ConfigEntry<bool> displayDeadTTSText;
        public static ConfigEntry<bool> deadTTSSpatialAudio;
        public static ConfigEntry<bool> enableWhenDiscovered;
        public static ConfigEntry<bool> disableWhileDead;
        public static ConfigEntry<string> deadTTSColor;
        public static ConfigEntry<bool> fixMissingDeathHeads;
        public static ConfigEntry<bool> verboseLogs;
        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        internal static void BindConfigSettings()
        {
            Plugin.Log("Binding Configs");
            //deadTTSPitch = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Pitch", 1.1f, new ConfigDescription("Affects the TTS pitch of all dead players.\nValues will be clamped between 0.1 and 2.0", new AcceptableValueRange<float>(0.1f, 2.0f))));
            minRandomPitch = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Random Pitch Min", 0.8f, new ConfigDescription("The lower range limit when randomizing dead players pitch.\nValues will be clamped between 0.8 and 2.0", new AcceptableValueRange<float>(0.8f, 2.0f))));
            maxRandomPitch = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Random Pitch Max", 1.5f, new ConfigDescription("The upper range limit when randomizing dead players pitch.\nValues will be clamped between 0.8 and 2.0", new AcceptableValueRange<float>(0.8f, 2.0f))));

            deadTTSVolume = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Volume", 0.5f, new ConfigDescription("Affects the TTS volume of all dead players.\nSet to 0 to mute the TTS of dead players. If muted, the TTS text will still appear.\nValues will be clamped between 0.0 and 1.0", new AcceptableValueRange<float>(0.0f, 1.0f))));
            displayDeadTTSText = AddConfigEntry(Plugin.instance.Config.Bind("General", "Display Dead TTS Text", true, "If true, TTS Text will appear from dead players' heads."));
            deadTTSSpatialAudio = AddConfigEntry(Plugin.instance.Config.Bind("General", "Use Spatial Audio", true, "If true, TTS audio from dead players should be 3D directional.\nIf false, the audio should appear as if it's in your head all the time."));
            enableWhenDiscovered = AddConfigEntry(Plugin.instance.Config.Bind("General", "Enable When Discovered", false, "If true, you will only hear DeadTTS from players once their head is \"discovered\"."));
            disableWhileDead = AddConfigEntry(Plugin.instance.Config.Bind("General", "Disable While Dead", false, "Dead TTS will be disabled while you are dead."));
            deadTTSColor = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Text Color Hex", "CC3333", new ConfigDescription("Hex color value for dead TTS text color. Hex string must be 6 characters long. Leave this field blank to use the vanilla TTS color.")));

            fixMissingDeathHeads = AddConfigEntry(Plugin.instance.Config.Bind("General", "Fix Missing Death Heads", true, new ConfigDescription("Fixes an uncommon bug where the game may not assign a player a death head.")));
            verboseLogs = AddConfigEntry(Plugin.instance.Config.Bind("General", "Verbose Logs", false, new ConfigDescription("Enables verbose logs. Useful for debugging.")));

            if (minRandomPitch.Value < 0.8f)
                minRandomPitch.Value = (float)minRandomPitch.DefaultValue;
            if (maxRandomPitch.Value > 2.0f)
                maxRandomPitch.Value = (float)maxRandomPitch.DefaultValue;
            minRandomPitch.Value = Mathf.Clamp(minRandomPitch.Value, 0.8f, 2.0f);
            maxRandomPitch.Value = Mathf.Max(maxRandomPitch.Value, minRandomPitch.Value);
            deadTTSVolume.Value = Mathf.Clamp(deadTTSVolume.Value, 0.0f, 2.0f);
            deadTTSColor.Value = deadTTSColor.Value.Trim(' ');
        }


        internal static ConfigEntry<T> AddConfigEntry<T>(ConfigEntry<T> configEntry)
        {
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }
    }
}