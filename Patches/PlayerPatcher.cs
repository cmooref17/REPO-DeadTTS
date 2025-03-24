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
        internal static FieldInfo isDisabledField = typeof(PlayerAvatar).GetField("isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
        /*internal static FieldInfo eyeFlashLerpField = typeof(PlayerDeathHead).GetField("eyeFlashLerp", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo eyeFlashField = typeof(PlayerDeathHead).GetField("eyeFlash", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo isSpeakingField = typeof(TTSVoice).GetField("isSpeaking", BindingFlags.NonPublic | BindingFlags.Instance);*/

        private static Dictionary<PlayerAvatar, float> deadPlayersVoicePitch = new Dictionary<PlayerAvatar, float>();


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
                    if (value > 0)
                    {
                        int playerId = -1;
                        int playerSeed = -1;
                        try { playerId = player.photonView.Owner.ActorNumber; }
                        catch (Exception e) { Plugin.LogWarning("Failed to get player id for player: " + player.name + " when calculating random seed. Don't worry about this.\n" + e); }
                        if (playerId != -1)
                        {
                            playerSeed = seed + playerId;
                            System.Random random = new System.Random(playerSeed);
                            float minValue = ConfigSettings.minRandomPitch.Value;
                            float maxValue = ConfigSettings.maxRandomPitch.Value;

                            // Generate a random float between minValue and maxValue
                            randomPitch = (float)(random.NextDouble() * (maxValue - minValue) + minValue);
                        }
                        //Plugin.LogWarning("BaseSeed: " + seed + " PlayerId: " + playerId + " PlayerSeed: " + playerSeed);
                        if (deadPlayersVoicePitch.TryGetValue(player, out float pitch) && randomPitch != pitch)
                            Plugin.Log("Setting dead TTS pitch for player with id: " + playerId + " to: " + randomPitch);
                    }
                    deadPlayersVoicePitch[player] = randomPitch;
                }
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "TtsFollowVoiceSettings")]
        [HarmonyPostfix]
        public static void OnTtsFollowVoiceSettings(ref PlayerAvatar ___playerAvatar, ref AudioLowPassLogic ___lowPassLogicTTS, ref bool ___inLobbyMixerTTS, ref float ___clipLoudnessTTS, PlayerVoiceChat __instance)
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
                
                float pitch = deadPlayersVoicePitch.ContainsKey(___playerAvatar) ? deadPlayersVoicePitch[___playerAvatar] : 1;
                __instance.ttsAudioSource.pitch = pitch;
                if (!(ConfigSettings.disableWhileDead.Value && (bool)isDisabledField.GetValue(localPlayer)))
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

                /*bool isSpeaking = (bool)isSpeakingField.GetValue(__instance.ttsVoice);
                float eyeFlashLerp = isSpeaking ? Mathf.Clamp(Mathf.Max(___clipLoudnessTTS - 0.02f, 0) / 0.2f, 0, 1) : 0;
                eyeFlashField.SetValue(___playerAvatar.playerDeathHead, true);
                eyeFlashLerpField.SetValue(___playerAvatar.playerDeathHead, eyeFlashLerp);*/
            }
        }


        [HarmonyPatch(typeof(PlayerVoiceChat), "ToggleLobby")]
        [HarmonyPostfix]
        public static void OnToggleOffLobbyChat(bool _toggle, ref PlayerAvatar ___playerAvatar, PlayerVoiceChat __instance)
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
        public static void MoveTTSAudioTransform(ref PlayerAvatar ___playerAvatar, ref bool ___inLobbyMixerTTS, PlayerVoiceChat __instance)
        {
            if (!LevelGenerator.Instance.Generated || !GameManager.Multiplayer() || GameDirector.instance.currentState < GameDirector.gameState.Main || !___playerAvatar)
                return;

            if (___inLobbyMixerTTS && (bool)isDisabledField.GetValue(___playerAvatar) && ___playerAvatar.playerDeathHead && __instance.ttsVoice)
            {
                __instance.transform.position = Vector3.Lerp(__instance.transform.position, ___playerAvatar.playerDeathHead.transform.position, 30f * Time.deltaTime);
            }
        }
    }


    /*public class DeadTTS : MonoBehaviour
    {
        public static Dictionary<PlayerAvatar, DeadTTS> avatarToDeadTTSMap = new Dictionary<PlayerAvatar, DeadTTS>();
        public PlayerAvatar playerAvatar;
        public float pitch = 1;

        public void Awake()
        {
            playerAvatar = gameObject.GetComponent<PlayerAvatar>();
            if (!playerAvatar)
            {
                enabled = false;
                return;
            }
            avatarToDeadTTSMap[playerAvatar] = this;
        }


        public void OnDestroy()
        {
            if (playerAvatar && avatarToDeadTTSMap.ContainsKey(playerAvatar))
                avatarToDeadTTSMap.Remove(playerAvatar);
        }


        [PunRPC]
        public void SetTTSPitchRPC(float pitch)
        {
            SetTTSPitch(pitch);
        }


        public void SetTTSPitch(float pitch)
        {
            pitch = Mathf.Clamp(pitch, ConfigSettings.minRandomPitch.Value, ConfigSettings.maxRandomPitch.Value);
        }
    }*/
}
