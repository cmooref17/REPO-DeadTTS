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

        private static FieldInfo textField = typeof(WorldSpaceUITTS).GetField("text", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo playerAvatarField = typeof(WorldSpaceUITTS).GetField("playerAvatar", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo followTransformField = typeof(WorldSpaceUITTS).GetField("followTransform", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo worldPositionField = typeof(WorldSpaceUITTS).GetField("worldPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo followPositionField = typeof(WorldSpaceUITTS).GetField("followPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo wordTimeField = typeof(WorldSpaceUITTS).GetField("wordTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo voiceChatField = typeof(PlayerAvatar).GetField("voiceChat", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo ttsVoiceField = typeof(WorldSpaceUITTS).GetField("ttsVoice", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo isSpeakingField = typeof(TTSVoice).GetField("isSpeaking", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(WorldSpaceUIParent), "TTS")]
        [HarmonyPrefix]
        private static void OnTTSUI(PlayerAvatar _player, string _text, float _time, WorldSpaceUIParent __instance)
        {
            if (ConfigSettings.deadTTSSpatialAudio.Value && GameDirector.instance.currentState == GameDirector.gameState.Main && _player && !_player.isActiveAndEnabled && _player.playerDeathHead && !string.IsNullOrEmpty(_text))
            {
                try
                {
                    WorldSpaceUITTS component = GameObject.Instantiate(__instance.TTSPrefab, __instance.transform.position, __instance.transform.rotation, __instance.transform).GetComponent<WorldSpaceUITTS>();
                    if (!component)
                        return;

                    var text = (TMP_Text)textField.GetValue(component);
                    text.text = _text;

                    playerAvatarField.SetValue(component, _player);

                    Transform followTransform = _player.playerDeathHead.transform;

                    followTransformField.SetValue(component, followTransform);
                    worldPositionField.SetValue(component, followTransform.position);
                    followPositionField.SetValue(component, followTransform.position);
                    wordTimeField.SetValue(component, _time);
                    var voiceChat = (PlayerVoiceChat)voiceChatField.GetValue(_player);
                    ttsVoiceField.SetValue(component, voiceChat.ttsVoice);

                    deadTTSElements.Add(component);
                }
                catch (Exception e)
                {
                    Plugin.LogError("Error initializing dead TTS UI:\n" + e);
                }
            }
        }


        [HarmonyPatch(typeof(WorldSpaceUITTS), "Update")]
        [HarmonyPrefix]
        private static void OnUpdatePrefix(WorldSpaceUITTS __instance)
        {
            if (ConfigSettings.deadTTSSpatialAudio.Value && deadTTSElements.Contains(__instance))
            {
                try
                {
                    var ttsVoice = (TTSVoice)ttsVoiceField.GetValue(__instance);
                    bool isSpeaking = (bool)isSpeakingField.GetValue(ttsVoice);
                    if (isSpeaking)
                    {
                        FieldInfo textAlphaField = typeof(WorldSpaceUITTS).GetField("textAlpha", BindingFlags.NonPublic | BindingFlags.Instance);
                        textAlphaField.SetValue(__instance, 10.0f);
                    }
                }
                catch (Exception e)
                {
                    Plugin.LogError("Error updating dead TTS UI location:\n" + e);
                    deadTTSElements.Remove(__instance);
                }
            }
        }
    }
}
