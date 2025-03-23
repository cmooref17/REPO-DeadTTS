using HarmonyLib;
using Photon.Realtime;
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
    public static class PlayerPatcher
    {
        internal static PlayerAvatar localPlayer;
        internal static FieldInfo isDisabledField = typeof(PlayerAvatar).GetField("isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<PlayerAvatar, float> deadPlayersVoicePitch = new Dictionary<PlayerAvatar, float>();

        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        public static void InitLocalPlayer(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (___isLocal)
                localPlayer = __instance;
        }


        [HarmonyPatch(typeof(RoundDirector), "StartRoundRPC")]
        [HarmonyPrefix]
        public static void RandomizePitch(int value, RoundDirector __instance)
        {
            int seed = value;
            deadPlayersVoicePitch.Clear();
            for (int i = 0; i < GameDirector.instance.PlayerList.Count; i++)
            {
                var player = GameDirector.instance.PlayerList[i];
                if (player)
                {
                    float randomPitch = 1;
                    if (value > 0)
                    {
                        int playerId = 0;
                        try { playerId = player.photonView.Owner.ActorNumber; }
                        catch (Exception e)
                        {
                            Plugin.LogWarning("Failed to get player id for player: " + player.name + " when calculating random seed. Don't worry about this.\n" + e);
                        }
                        seed += playerId;
                        System.Random random = new System.Random(seed);
                        float minValue = 0.1f; // Minimum value (hardcoded for now)
                        float maxValue = 2.0f; // Maximum value (hardcoded for now)

                        // Generate a random float between minValue and maxValue
                        randomPitch = (float)(random.NextDouble() * (maxValue - minValue) + minValue);
                    }
                    deadPlayersVoicePitch[player] = randomPitch;
                }
            }
        }


        /*[HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathDone")]
        [HarmonyPostfix]
        private static void OnPlayerDeath(ref int ___steamIDshort, ref string ___steamID, PlayerAvatar __instance)
        {
            //deadPlayersVoicePitch[__instance] = -1;
            int playerId;
            
            int gameSeed = 0;
            var seedField = typeof(UnityEngine.Random).GetField("seed", BindingFlags.NonPublic | BindingFlags.Static);
            gameSeed = (int)seedField.GetValue(null);

            int seed = (GameDirector.instance ? GameDirector.instance.Seed : 0) + playerId;
            System.Random random = new System.Random(seed);
            float minValue = 0.1f; // Minimum value (hardcoded for now)
            float maxValue = 2.0f; // Maximum value (hardcoded for now)

            // Generate a random float between minValue and maxValue
            float randomPitch = (float)(random.NextDouble() * (maxValue - minValue) + minValue);
            deadPlayersVoicePitch[__instance] = randomPitch;
            Plugin.LogWarning("Random Pitch: " + randomPitch + " GameSeed: " + (GameDirector.instance ? GameDirector.instance.Seed : 0) + " GameSeed2: " + gameSeed + " PlayerId: " + playerId);
        }*/


        [HarmonyPatch(typeof(PlayerVoiceChat), "TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        private static void OnTtsFollowVoiceSettings(ref PlayerAvatar ___playerAvatar, ref AudioLowPassLogic ___lowPassLogicTTS, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !___playerAvatar.playerDeathHead || !__instance.ttsAudioSource || !__instance.ttsVoice || !__instance.mixerTTSSound)
                return;

            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;

            if ((bool)isDisabledField.GetValue(___playerAvatar) && ___playerAvatar.playerDeathHead.isActiveAndEnabled && ___inLobbyMixerTTS)
            {
                if (__instance.ttsAudioSource.outputAudioMixerGroup != __instance.mixerTTSSound)
                {
                    Plugin.Log("The game has toggled ON lobby chat for player: " + ___playerAvatar.name + ". Disabling TTS lobby mixer.");
                    __instance.ttsVoice.setVoice(1);
                    __instance.ttsAudioSource.outputAudioMixerGroup = __instance.mixerTTSSound;
                    __instance.ttsVoice.StopAndClearVoice();
                }
                //__instance.ttsAudioSource.pitch = ConfigSettings.deadTTSPitch.Value;
                float pitch = deadPlayersVoicePitch.ContainsKey(___playerAvatar) ? deadPlayersVoicePitch[___playerAvatar] : 1;
                __instance.ttsAudioSource.pitch = pitch;
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

            if (___inLobbyMixerTTS && (bool)isDisabledField.GetValue(___playerAvatar) && ___playerAvatar.playerDeathHead && __instance.ttsVoice)
            {
                __instance.transform.position = Vector3.Lerp(__instance.transform.position, ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            }
        }
    }
}
