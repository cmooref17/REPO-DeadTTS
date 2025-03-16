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
        public static ConfigEntry<float> deadTTSPitch;
        public static ConfigEntry<float> deadTTSVolume;
        public static ConfigEntry<bool> displayDeadTTSText;
        public static ConfigEntry<bool> deadTTSSpatialAudio;
        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        public static void BindConfigSettings()
        {
            Plugin.Log("Binding Configs");
            deadTTSPitch = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Pitch", 1.1f, new ConfigDescription("Affects the TTS pitch of all dead players.\nValues will be clamped between 0.1 and 2.0", new AcceptableValueRange<float>(0.1f, 2.0f))));
            deadTTSVolume = AddConfigEntry(Plugin.instance.Config.Bind("General", "Dead TTS Volume", 0.8f, new ConfigDescription("Affects the TTS volume of all dead players.\nValues will be clamped between 0.1 and 1.0", new AcceptableValueRange<float>(0.1f, 1.0f))));
            displayDeadTTSText = AddConfigEntry(Plugin.instance.Config.Bind("General", "Display Dead TTS Text", true, "If true, TTS Text will appear from dead players' heads."));
            deadTTSSpatialAudio = AddConfigEntry(Plugin.instance.Config.Bind("General", "Use Spatial Audio", true, "If true, TTS audio from dead players should be 3D directional.\nIf false, the audio should appear as if it's in your head all the time."));
            deadTTSPitch.Value = Mathf.Clamp(deadTTSPitch.Value, 0.1f, 2.0f);
            deadTTSVolume.Value = Mathf.Clamp(deadTTSVolume.Value, 0.1f, 2.0f);
        }


        public static ConfigEntry<T> AddConfigEntry<T>(ConfigEntry<T> configEntry)
        {
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }
    }
}