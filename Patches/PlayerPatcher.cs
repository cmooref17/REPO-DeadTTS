using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using REPO_DeadTTS.Config;
using System;
using System.Collections;
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
        internal static FieldInfo spectatedField = typeof(PlayerDeathHead).GetField("spectated", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static Dictionary<PlayerAvatar, float> deadPlayersVoicePitch = new Dictionary<PlayerAvatar, float>();


        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        public static void InitPlayer(ref bool ___isLocal, PlayerAvatar __instance)
        {
            if (___isLocal)
                localPlayer = __instance;
            __instance.gameObject.AddComponent<DeadTTSPlayerData>();
        }


        [HarmonyPatch(typeof(RoundDirector), "StartRoundLogic")]
        [HarmonyPrefix]
        public static void RandomizeTTSPitch(int value, RoundDirector __instance)
        {
            int seed = value;
            deadPlayersVoicePitch.Clear();
            for (int i = 0; i < GameDirector.instance.PlayerList.Count; i++)
            {
                var playerAvatar = GameDirector.instance.PlayerList[i];
                var playerData = playerAvatar.GetComponent<DeadTTSPlayerData>();
                if (playerAvatar)
                {
                    float randomPitch = 1;
                    if (GameManager.Multiplayer() && value > 0)
                    {
                        int playerId = -1;
                        int playerSeed = -1;
                        try { playerId = playerAvatar.photonView.Owner.ActorNumber; }
                        catch (Exception e)
                        {
                            Plugin.LogWarning($"Failed to get player id for player: {(playerData && !string.IsNullOrEmpty(playerData.playerName) ? playerData.playerName : playerAvatar.name)} when calculating random seed. Don't worry about this.");
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
                        if (deadPlayersVoicePitch.TryGetValue(playerAvatar, out float pitch) && randomPitch != pitch)
                            Plugin.Log("Setting dead TTS pitch for player with id: " + playerId + " to: " + randomPitch);
                        Plugin.LogVerbose("DeadTTS Pitch BaseSeed: " + seed + " PlayerId: " + playerId + " PlayerSeed: " + playerSeed);
                    }
                    deadPlayersVoicePitch[playerAvatar] = randomPitch;
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        public static void OnTtsFollowVoiceSettings(ref PlayerAvatar ___playerAvatar, ref AudioLowPassLogic ___lowPassLogicTTS, ref bool ___inLobbyMixerTTS, ref float ___clipLoudnessTTS, PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !GameManager.Multiplayer())
                return;
            if (GameDirector.instance.currentState != GameDirector.gameState.Main || !(SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsArena()))
                return;

            var playerData = ___playerAvatar.GetComponent<DeadTTSPlayerData>();
            // if this player is dead
            if (IsPlayerDead(___playerAvatar) && ___inLobbyMixerTTS)
            {
                if (!ConfigSettings.onlyEnableWhenDiscovered.Value || (!___playerAvatar.GetPlayerDeathHead() || (bool)serverSeenField.GetValue(___playerAvatar.GetPlayerDeathHead())) || IsLocalPlayerDead() || !SemiFunc.RunIsLevel())
                {
                    if (__instance.ttsAudioSource && __instance.mixerTTSSound)
                    {
                        if (__instance.ttsAudioSource.outputAudioMixerGroup != __instance.mixerTTSSound)
                        {
                            __instance.ttsAudioSource.outputAudioMixerGroup = __instance.mixerTTSSound;
                            if (!___playerAvatar.GetPlayerDeathHead())
                            {
                                Plugin.LogError("Game did not assign player a death head.");
                            }
                            if (!__instance.ttsVoice)
                            {
                                Plugin.LogError("Game did not assign player a tts voice.");
                            }
                            else
                            {
                                Plugin.LogVerbose($"The game has toggled ON lobby chat for player: {(playerData && !string.IsNullOrEmpty(playerData.playerName) ? playerData.playerName : ___playerAvatar.name)} ({___playerAvatar.photonView.Owner.ActorNumber}). Disabling TTS lobby mixer.");
                                __instance.ttsVoice.setVoice(1);
                                __instance.ttsVoice.StopAndClearVoice();
                            }
                        }

                        float pitch = deadPlayersVoicePitch.ContainsKey(___playerAvatar) ? deadPlayersVoicePitch[___playerAvatar] : 1;
                        __instance.ttsAudioSource.pitch = pitch;

                        // Don't enable dead tts for local player.
                        // Don't enable dead tts for any player if local player (you) is dead, if set in config
                        if (___playerAvatar != localPlayer && !(ConfigSettings.disableWhileDead.Value && IsLocalPlayerDead()))
                        {
                            __instance.ttsAudioSource.volume = ConfigSettings.deadTTSVolume.Value;

                            // Force 3d spatial audio
                            if (ConfigSettings.useSpatialAudioDeadTTS.Value)
                                __instance.ttsAudioSource.spatialBlend = 1;
                        }
                        else
                        {
                            __instance.ttsAudioSource.volume = 1;
                        }
                    }
                    else
                    {
                        string error = __instance.ttsAudioSource == null ? "Game did not assign player a ttsAudioSource. " : "";
                        error += __instance.mixerTTSSound == null ? "Game did not assign player a mixerTTSSound." : "";
                        if (!string.IsNullOrEmpty(error))
                            Plugin.LogError(error.TrimEnd(' '));
                    }
                }

                // flash eyes while talking
                if (___playerAvatar.GetPlayerDeathHead())
                {
                    bool isSpeaking = (bool)isSpeakingField.GetValue(__instance.ttsVoice);
                    Material eyeMaterial = (Material)eyeMaterialField.GetValue(___playerAvatar.GetPlayerDeathHead());
                    int eyeMaterialAmount = (int)eyeMaterialAmountField.GetValue(___playerAvatar.GetPlayerDeathHead());
                    if (isSpeaking)
                    {
                        float eyeFlashLerp = (float)eyeFlashLerpField.GetValue(___playerAvatar.GetPlayerDeathHead());
                        float eyeFlashLerpTarget = Mathf.Clamp01(Mathf.Max(___clipLoudnessTTS, 0) / 0.2f);
                        eyeFlashLerp = Mathf.Lerp(eyeFlashLerp, eyeFlashLerpTarget, 20f * Time.deltaTime);
                        eyeMaterial.SetFloat(eyeMaterialAmount, Mathf.Pow(eyeFlashLerp, 0.5f));
                        eyeFlashLerpField.SetValue(___playerAvatar.GetPlayerDeathHead(), eyeFlashLerp);
                    }
                    else
                    {
                        eyeMaterial.SetFloat(eyeMaterialAmount, 0);
                        eyeFlashLerpField.SetValue(___playerAvatar.GetPlayerDeathHead(), 0);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "ToggleMixer")]
        [HarmonyPostfix]
        public static void OnToggleOffLobbyChat(bool _lobby, bool _distorted, ref PlayerAvatar ___playerAvatar, PlayerVoiceChat __instance)
        {
            if (!GameManager.Multiplayer())
                return;
            if (GameDirector.instance.currentState != GameDirector.gameState.Main || !(SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsArena()))
                return;

            var playerData = ___playerAvatar.GetComponent<DeadTTSPlayerData>();
            var photonView = playerData ? playerData.photonView : ___playerAvatar.photonView;
            if (!_lobby)
            {
                Plugin.LogVerbose($"The game has toggled OFF lobby chat for player: {(playerData && !string.IsNullOrEmpty(playerData.playerName) ? playerData.playerName : ___playerAvatar.name)} ({___playerAvatar.photonView.Owner.ActorNumber})");
                if (__instance.ttsAudioSource && __instance.mixerTTSSound)
                {
                    __instance.ttsAudioSource.volume = 1.0f;
                    __instance.ttsAudioSource.pitch = 1.0f;
                    __instance.ttsVoice.setVoice(0);
                }

                // reset eyes
                if (___playerAvatar.GetPlayerDeathHead())
                {
                    Material eyeMaterial = (Material)eyeMaterialField.GetValue(___playerAvatar.GetPlayerDeathHead());
                    int eyeMaterialAmount = (int)eyeMaterialAmountField.GetValue(___playerAvatar.GetPlayerDeathHead());
                    eyeMaterial.SetFloat(eyeMaterialAmount, 0);
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "LateUpdate")]
        [HarmonyPrefix]
        public static void MoveTTSAudioTransform(ref PlayerAvatar ___playerAvatar, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!___playerAvatar || !GameManager.Multiplayer())
                return;
            if (GameDirector.instance.currentState != GameDirector.gameState.Main || !(SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsArena()))
                return;

            if (___inLobbyMixerTTS && IsPlayerDead(___playerAvatar) && ___playerAvatar.GetPlayerDeathHead() && __instance.ttsVoice)
            {
                __instance.transform.position = Vector3.Lerp(__instance.transform.position, ___playerAvatar.GetPlayerDeathHead().transform.position, 30f * Time.deltaTime);
            }
        }


        // Helpers
        public static bool IsLocalPlayerDead()
        {
            return IsPlayerDead(PlayerAvatar.instance);
        }


        public static bool IsPlayerDead(PlayerAvatar playerAvatar)
        {

            return playerAvatar && (bool)isDisabledField.GetValue(playerAvatar) && (!playerAvatar.GetPlayerDeathHead() || playerAvatar.GetPlayerDeathHead().isActiveAndEnabled);
        }
    }
}
