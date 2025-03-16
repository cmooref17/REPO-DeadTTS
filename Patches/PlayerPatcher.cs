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

namespace REPO_DeadTTS
{
    [HarmonyPatch]
    public static class PlayerPatcher
    {
        internal static PlayerAvatar localPlayer;

        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        private static void InitLocalPlayer(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (___isLocal)
                localPlayer = __instance;
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        private static void OnTtsFollowVoiceSettings(ref PlayerAvatar ___playerAvatar, ref AudioLowPassLogic ___lowPassLogicTTS, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !___playerAvatar.playerDeathHead || !__instance.ttsAudioSource || !__instance.ttsVoice || !__instance.mixerTTSSound)
                return;

            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;

            if (!___playerAvatar.isActiveAndEnabled && ___playerAvatar.playerDeathHead.isActiveAndEnabled == true && ___inLobbyMixerTTS)
            {
                if (__instance.ttsAudioSource.outputAudioMixerGroup != __instance.mixerTTSSound)
                {
                    Plugin.Log("The game has toggled ON lobby chat for player: " + ___playerAvatar.name + ". Disabling TTS lobby mixer.");
                    __instance.ttsVoice.setVoice(1);
                    __instance.ttsAudioSource.outputAudioMixerGroup = __instance.mixerTTSSound;
                    __instance.ttsVoice.StopAndClearVoice();
                }
                __instance.ttsAudioSource.pitch = ConfigSettings.deadTTSPitch.Value;
                __instance.ttsAudioSource.volume = ConfigSettings.deadTTSVolume.Value;

                // Force 3d spatial audio
                if (ConfigSettings.deadTTSSpatialAudio.Value)
                    __instance.ttsAudioSource.spatialBlend = 1;
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "ToggleLobby")]
        [HarmonyPostfix]
        private static void OnToggleOffLobbyChat(bool _toggle, ref PlayerAvatar ___playerAvatar, PlayerVoiceChat __instance)
        {
            if (!__instance.ttsAudioSource || !__instance.mixerTTSSound)
                return;

            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;

            if (!_toggle)
            {
                Plugin.Log("The game has toggled OFF lobby chat for player: " + ___playerAvatar.name);
                __instance.ttsAudioSource.volume = 1.0f;
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "LateUpdate")]
        [HarmonyPrefix]
        private static void MoveTTSAudioTransform(ref PlayerAvatar ___playerAvatar, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!LevelGenerator.Instance.Generated || !GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main || !___playerAvatar)
                return;

            if (___inLobbyMixerTTS && !___playerAvatar.isActiveAndEnabled && ___playerAvatar.playerDeathHead && __instance.ttsVoice)
            {
                __instance.transform.position = Vector3.Lerp(__instance.transform.position, ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            }
        }
    }
}
