using HarmonyLib;
using Photon.Pun;
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
using UnityEngine.UI;

namespace REPO_DeadTTS.Patches
{
    [HarmonyPatch]
    public static class PlayerPatcher
    {
        internal static PlayerAvatar localPlayer;
        internal static FieldInfo voiceChatField = typeof(PlayerAvatar).GetField("voiceChat", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo isDisabledField = typeof(PlayerAvatar).GetField("isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo serverSeenField = typeof(PlayerDeathHead).GetField("serverSeen", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static FieldInfo eyeMaterialField = typeof(PlayerDeathHead).GetField("eyeMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo eyeMaterialAmountField = typeof(PlayerDeathHead).GetField("eyeMaterialAmount", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo eyeFlashLerpField = typeof(PlayerDeathHead).GetField("eyeFlashLerp", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo isSpeakingField = typeof(TTSVoice).GetField("isSpeaking", BindingFlags.NonPublic | BindingFlags.Instance);

        //internal static HashSet<PlayerAvatar> deadPlayers = new HashSet<PlayerAvatar>();
        internal static Dictionary<PlayerAvatar, float> deadPlayersVoicePitch = new Dictionary<PlayerAvatar, float>();


        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        public static void InitPlayer(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (___isLocal)
                localPlayer = __instance;
        }


        [HarmonyPatch(typeof(RoundDirector), "StartRoundLogic")]
        [HarmonyPrefix]
        public static void RandomizeTTSPitch(int value, RoundDirector __instance)
        {
            int seed = value;
            deadPlayersVoicePitch.Clear();
            for (int i = 0; i < GameDirector.instance.PlayerList.Count; i++)
            {
                var player = GameDirector.instance.PlayerList[i];
                if (player)
                {
                    float randomPitch = 1;
                    if (GameManager.Multiplayer() && value > 0)
                    {
                        int playerId = -1;
                        int playerSeed = -1;
                        try { playerId = player.photonView.Owner.ActorNumber; }
                        catch (Exception e)
                        {
                            Plugin.LogWarning("Failed to get player id for player: " + player.name + " when calculating random seed. Don't worry about this.");
                            Plugin.LogWarningVerbose("Error: " + e);
                        }

                        if (playerId != -1)
                        {
                            playerSeed = seed + playerId;
                            System.Random random = new System.Random(playerSeed);
                            float minValue = ConfigSettings.minRandomPitch.Value;
                            float maxValue = ConfigSettings.maxRandomPitch.Value;
                            // Generate a random float between minValue and maxValue
                            randomPitch = (float)(random.NextDouble() * (maxValue - minValue) + minValue);
                        }
                        if (deadPlayersVoicePitch.TryGetValue(player, out float pitch) && randomPitch != pitch)
                            Plugin.Log("Setting dead TTS pitch for player with id: " + playerId + " to: " + randomPitch);
                        Plugin.LogVerbose("BaseSeed: " + seed + " PlayerId: " + playerId + " PlayerSeed: " + playerSeed);
                    }
                    deadPlayersVoicePitch[player] = randomPitch;
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        public static void OnTtsFollowVoiceSettings(ref PlayerAvatar ___playerAvatar, ref AudioLowPassLogic ___lowPassLogicTTS, ref bool ___inLobbyMixerTTS, ref float ___clipLoudnessTTS, PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !___playerAvatar.playerDeathHead)
                return;

            if (GameManager.Multiplayer() && GameDirector.instance.currentState >= GameDirector.gameState.Main)
            {
                // if player is dead
                if (IsPlayerDead(___playerAvatar) && ___playerAvatar.playerDeathHead.isActiveAndEnabled && ___inLobbyMixerTTS)
                {
                    if (!ConfigSettings.enableWhenDiscovered.Value || (bool)serverSeenField.GetValue(___playerAvatar.playerDeathHead) || IsLocalPlayerDead() || !SemiFunc.RunIsLevel())
                    {
                        if (__instance.ttsAudioSource && __instance.ttsVoice && __instance.mixerTTSSound)
                        {
                            if (__instance.ttsAudioSource.outputAudioMixerGroup != __instance.mixerTTSSound)
                            {
                                Plugin.LogVerbose("The game has toggled ON lobby chat for player: " + ___playerAvatar.name + ". Disabling TTS lobby mixer.");
                                __instance.ttsVoice.setVoice(1);
                                __instance.ttsAudioSource.outputAudioMixerGroup = __instance.mixerTTSSound;
                                __instance.ttsVoice.StopAndClearVoice();
                            }

                            float pitch = deadPlayersVoicePitch.ContainsKey(___playerAvatar) ? deadPlayersVoicePitch[___playerAvatar] : 1;
                            __instance.ttsAudioSource.pitch = pitch;
                            if (!(ConfigSettings.disableWhileDead.Value && IsLocalPlayerDead()) && ___playerAvatar != localPlayer)
                            {
                                //__instance.ttsAudioSource.pitch = ConfigSettings.deadTTSPitch.Value;
                                __instance.ttsAudioSource.volume = ConfigSettings.deadTTSVolume.Value;

                                // Force 3d spatial audio
                                if (ConfigSettings.deadTTSSpatialAudio.Value)
                                    __instance.ttsAudioSource.spatialBlend = 1;
                            }
                            else
                            {
                                __instance.ttsAudioSource.volume = 1;
                            }
                        }
                    }

                    // flash eyes while talking
                    if (___playerAvatar.playerDeathHead)
                    {
                        bool isSpeaking = (bool)isSpeakingField.GetValue(__instance.ttsVoice);
                        Material eyeMaterial = (Material)eyeMaterialField.GetValue(___playerAvatar.playerDeathHead);
                        int eyeMaterialAmount = (int)eyeMaterialAmountField.GetValue(___playerAvatar.playerDeathHead);
                        if (isSpeaking)
                        {
                            float eyeFlashLerp = (float)eyeFlashLerpField.GetValue(___playerAvatar.playerDeathHead);
                            float eyeFlashLerpTarget = Mathf.Clamp01(Mathf.Max(___clipLoudnessTTS, 0) / 0.2f);
                            eyeFlashLerp = Mathf.Lerp(eyeFlashLerp, eyeFlashLerpTarget, 20f * Time.deltaTime);
                            eyeMaterial.SetFloat(eyeMaterialAmount, Mathf.Pow(eyeFlashLerp, 0.5f));
                            eyeFlashLerpField.SetValue(___playerAvatar.playerDeathHead, eyeFlashLerp);
                        }
                    }
                    /*deadPlayers.Add(___playerAvatar);
                    return;*/
                }
            }

            /*if (deadPlayers.Contains(___playerAvatar))
            {
                deadPlayers.Remove(___playerAvatar);
                if (__instance.ttsAudioSource && __instance.ttsVoice)
                {
                    __instance.ttsAudioSource.volume = 1.0f;
                    __instance.ttsAudioSource.pitch = 1.0f;
                    __instance.ttsVoice.setVoice(0);
                }

                if (___playerAvatar.playerDeathHead)
                {
                    Material eyeMaterial = (Material)eyeMaterialField.GetValue(___playerAvatar.playerDeathHead);
                    int eyeMaterialAmount = (int)eyeMaterialAmountField.GetValue(___playerAvatar.playerDeathHead);
                    eyeMaterial.SetFloat(eyeMaterialAmount, 0);
                }
            }*/
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "ToggleLobby")]
        [HarmonyPostfix]
        public static void OnToggleOffLobbyChat(bool _toggle, ref PlayerAvatar ___playerAvatar, PlayerVoiceChat __instance)
        {
            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;

            if (!_toggle)
            {
                Plugin.LogVerbose("The game has toggled OFF lobby chat for player: " + ___playerAvatar.name);
                if (__instance.ttsAudioSource && __instance.mixerTTSSound)
                {
                    __instance.ttsAudioSource.volume = 1.0f;
                    __instance.ttsAudioSource.pitch = 1.0f;
                    __instance.ttsVoice.setVoice(0);
                }

                // reset eyes
                if (___playerAvatar.playerDeathHead)
                {
                    Material eyeMaterial = (Material)eyeMaterialField.GetValue(___playerAvatar.playerDeathHead);
                    int eyeMaterialAmount = (int)eyeMaterialAmountField.GetValue(___playerAvatar.playerDeathHead);
                    eyeMaterial.SetFloat(eyeMaterialAmount, 0);
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "LateUpdate")]
        [HarmonyPrefix]
        public static void MoveTTSAudioTransform(ref PlayerAvatar ___playerAvatar, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!LevelGenerator.Instance.Generated || !___playerAvatar)
                return;
            if (!GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main)
                return;

            if (___inLobbyMixerTTS && IsPlayerDead(___playerAvatar) && ___playerAvatar.playerDeathHead && __instance.ttsVoice)
            {
                __instance.transform.position = Vector3.Lerp(__instance.transform.position, ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            }
        }


        // Helpers
        public static bool IsLocalPlayerDead()
        {
            return IsPlayerDead(PlayerAvatar.instance);
        }


        public static bool IsPlayerDead(PlayerAvatar playerAvatar)
        {
            if (!playerAvatar || !playerAvatar.playerDeathHead)
                return false;
            return (bool)isDisabledField.GetValue(playerAvatar) && playerAvatar.playerDeathHead.isActiveAndEnabled;
        }
    }
}
