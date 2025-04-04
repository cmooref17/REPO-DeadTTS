using HarmonyLib;
using REPO_DeadTTS.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace REPO_DeadTTS.Patches
{
    [HarmonyPatch]
    public static class UIPatcher
    {
        private static HashSet<WorldSpaceUITTS> deadTTSElements = new HashSet<WorldSpaceUITTS>();
        private static Dictionary<PlayerAvatar, bool> isDisabledStates = new Dictionary<PlayerAvatar, bool>();

        private static FieldInfo textField = typeof(WorldSpaceUITTS).GetField("text", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo playerAvatarField = typeof(WorldSpaceUITTS).GetField("playerAvatar", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo followTransformField = typeof(WorldSpaceUITTS).GetField("followTransform", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo worldPositionField = typeof(WorldSpaceUITTS).GetField("worldPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo followPositionField = typeof(WorldSpaceUITTS).GetField("followPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo wordTimeField = typeof(WorldSpaceUITTS).GetField("wordTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo ttsVoiceField = typeof(WorldSpaceUITTS).GetField("ttsVoice", BindingFlags.NonPublic | BindingFlags.Instance);
        

        [HarmonyPatch(typeof(WorldSpaceUIParent), "TTS")]
        [HarmonyPrefix]
        public static void OnTTSUI(PlayerAvatar _player, string _text, float _time, WorldSpaceUIParent __instance)
        {
            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;
            // Don't use spatial tts audio/ui for dead players when local player is also dead (if configured)
            /*if (ConfigSettings.disableWhileDead.Value && PlayerPatcher.IsLocalPlayerDead())
                return;*/

            if (ConfigSettings.deadTTSSpatialAudio.Value && GameDirector.instance.currentState == GameDirector.gameState.Main && _player && (bool)PlayerPatcher.isDisabledField.GetValue(_player) && _player.playerDeathHead && !string.IsNullOrEmpty(_text))
            {
                if (!ConfigSettings.enableWhenDiscovered.Value || (bool)PlayerPatcher.serverSeenField.GetValue(_player.playerDeathHead) || PlayerPatcher.IsLocalPlayerDead() || !SemiFunc.RunIsLevel())
                {
                    try
                    {
                        WorldSpaceUITTS component = GameObject.Instantiate(__instance.TTSPrefab, __instance.transform.position, __instance.transform.rotation, __instance.transform).GetComponent<WorldSpaceUITTS>();
                        if (!component)
                            return;

                        var text = (TMP_Text)textField.GetValue(component);
                        text.text = _text;
                        if (!PlayerPatcher.IsLocalPlayerDead())
                        {
                            string formattedText = text.text;
                            try
                            {
                                formattedText = $"<color=#{ConfigSettings.deadTTSColor.Value.TrimStart('#')}>{text.text}</color>";
                                text.text = formattedText;
                            }
                            catch (Exception e)
                            {
                                Plugin.LogError("Failed to apply dead TTS color: " + ConfigSettings.deadTTSColor.Value + "\n" + e);
                            }
                        }

                        playerAvatarField.SetValue(component, _player);

                        Transform followTransform = _player.playerDeathHead.transform;

                        followTransformField.SetValue(component, followTransform);
                        worldPositionField.SetValue(component, followTransform.position);
                        followPositionField.SetValue(component, followTransform.position);
                        wordTimeField.SetValue(component, _time);
                        var voiceChat = (PlayerVoiceChat)PlayerPatcher.voiceChatField.GetValue(_player);
                        ttsVoiceField.SetValue(component, voiceChat.ttsVoice);

                        deadTTSElements.Add(component);

                        // Clean up old elements in the hashset
                        try { deadTTSElements.RemoveWhere(obj => obj == null); }
                        catch { }
                    }
                    catch (Exception e)
                    {
                        Plugin.LogError("Error initializing dead TTS UI:\n" + e);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(WorldSpaceUITTS), "Update")]
        [HarmonyPrefix]
        public static void UpdateUIPositionPrefix(WorldSpaceUITTS __instance)
        {
            if (ConfigSettings.displayDeadTTSText.Value && deadTTSElements.Contains(__instance))
            {
                try
                {
                    var playerAvatar = (PlayerAvatar)playerAvatarField.GetValue(__instance);
                    bool isDisabled = (bool)PlayerPatcher.isDisabledField.GetValue(playerAvatar);
                    isDisabledStates[playerAvatar] = isDisabled;
                    PlayerPatcher.isDisabledField.SetValue(playerAvatar, false);
                }
                catch (Exception e)
                {
                    Plugin.LogError("Error (A) updating dead TTS UI location:\n" + e);
                    deadTTSElements.Remove(__instance);
                }
            }
        }


        [HarmonyPatch(typeof(WorldSpaceUITTS), "Update")]
        [HarmonyPostfix]
        public static void UpdateUIPositionPostfix(WorldSpaceUITTS __instance)
        {
            if (deadTTSElements.Contains(__instance))
            {
                try
                {
                    var playerAvatar = (PlayerAvatar)playerAvatarField.GetValue(__instance);
                    if (isDisabledStates.TryGetValue(playerAvatar, out bool isDisabled))
                        PlayerPatcher.isDisabledField.SetValue(playerAvatar, isDisabled);
                }
                catch (Exception e)
                {
                    Plugin.LogError("Error (B) updating dead TTS UI location:\n" + e);
                    deadTTSElements.Remove(__instance);
                }
            }
        }
    }
}
