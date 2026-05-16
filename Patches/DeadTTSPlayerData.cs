using BepInEx;
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
    public class DeadTTSPlayerData : MonoBehaviour
    {
        internal static PlayerAvatar localPlayer;
        internal static FieldInfo playerNameField = typeof(PlayerAvatar).GetField("playerName", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo steamIdField = typeof(PlayerAvatar).GetField("steamId", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static MethodInfo setupClient = typeof(PlayerDeathHead).GetMethod("SetupClient", BindingFlags.NonPublic | BindingFlags.Instance);


        public bool isLocalPlayer = false;
        public PlayerAvatar playerAvatar;
        public PhotonView photonView { get { return playerAvatar ? playerAvatar.photonView : null; } }
        private string _playerName;
        public string playerName { 
            get
            {
                if (string.IsNullOrEmpty(_playerName))
                    _playerName = (string)playerNameField.GetValue(playerAvatar);
                return _playerName; 
            }
            internal set { _playerName = value; }
        }
        private string _steamId;
        public string steamId {
            get
            {
                if (string.IsNullOrEmpty(_steamId))
                    _steamId = (string)steamIdField.GetValue(playerAvatar);
                return _steamId;
            }
            internal set { _steamId = value; }
        }
        public PlayerDeathHead playerDeathHead;


        private void Awake()
        {
            playerAvatar = GetComponent<PlayerAvatar>();
            isLocalPlayer = playerAvatar && playerAvatar == PlayerPatcher.localPlayer;
        }


        // For setting name (logging purposes) and steam id
        [HarmonyPatch(typeof(PlayerAvatar), "AddToStatsManagerRPC")]
        [HarmonyPostfix]
        public static void OnAddToStatsManagerRPC(string _playerName, string _steamID, PlayerAvatar __instance)
        {
            var playerData = __instance.GetComponent<DeadTTSPlayerData>();
            if (playerData)
            {
                playerData.playerName = _playerName;
                playerData.steamId = _steamID;
            }
        }


        [HarmonyPatch(typeof(PlayerDeathHead), "SetupDone")]
        [HarmonyPostfix]
        public static void OnDeathHeadSetupDone(PlayerDeathHead __instance)
        {
            if (!GameManager.Multiplayer() || PhotonNetwork.IsMasterClient)
            {
                var playerAvatar = __instance.playerAvatar;
                var playerData = playerAvatar?.GetComponent<DeadTTSPlayerData>();

                if (playerData)
                {
                    if (ConfigSettings.fixMissingDeathHeads.Value)
                        playerData.ReassignPlayerDeathHead(__instance);

                    if (GameManager.Multiplayer())
                    {
                        var deathHeadPhotonView = playerData.playerDeathHead.GetComponent<PhotonView>();
                        playerData.photonView.RPC("ReassignPlayerDeathHeadRPC", RpcTarget.OthersBuffered, deathHeadPhotonView.ViewID);
                        Plugin.Log($"Sending PlayerDeathHead assignment assurance to clients for player: {playerData.playerName} ({playerAvatar.photonView.Owner.ActorNumber}) ...");
                    }
                    else
                    {
                        Plugin.LogError("[OnDeathHeadSetupDone] Could not find PlayerAvatar assigned to PlayerDeathHead.");
                    }
                }
            }
        }


        [PunRPC]
        public void ReassignPlayerDeathHeadRPC(int deathHeadViewId, PhotonMessageInfo _info = default)
        {
            if (!SemiFunc.MasterOnlyRPC(_info) || !ConfigSettings.fixMissingDeathHeads.Value)
                return;

            Debug.Assert(playerAvatar != null);

            PlayerDeathHead[] playerDeathHeads = FindObjectsOfType<PlayerDeathHead>();
            foreach (var deathHead in playerDeathHeads)
            {
                var deathHeadPhotonView = deathHead.GetComponent<PhotonView>();
                if (deathHeadPhotonView && deathHeadPhotonView.ViewID == deathHeadViewId)
                {
                    ReassignPlayerDeathHead(deathHead);
                    StartCoroutine((IEnumerator)setupClient.Invoke(playerDeathHead, null));
                    return;
                }
            }
            Plugin.LogError($"Unknown error while trying to reassign PlayerDeathHead component to player: {(!string.IsNullOrEmpty(playerName) ? playerName : (playerAvatar ? playerAvatar.name : ""))} ({photonView.Owner.ActorNumber}) with death head view id: " + deathHeadViewId);
        }


        [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathDone")]
        [HarmonyPrefix]
        public static void ReassignPlayerDeathHeadBackup(PlayerAvatar __instance)
        {
            if (!ConfigSettings.fixMissingDeathHeads.Value)
                return;

            var playerData = __instance.GetComponent<DeadTTSPlayerData>();
            if (playerData && playerData.playerDeathHead)
                playerData.ReassignPlayerDeathHead(playerData.playerDeathHead);
        }

        
        internal void ReassignPlayerDeathHead(PlayerDeathHead deathHead)
        {
            playerDeathHead = deathHead;
            int deathHeadViewId = playerDeathHead.GetComponent<PhotonView>().ViewID;
            if (PlayerPatcher.GetPlayerDeathHead(playerAvatar) != playerDeathHead || playerDeathHead.playerAvatar != playerAvatar || playerDeathHead.transform.parent != playerAvatar.transform.parent)
            {
                PlayerPatcher.playerDeathHeadField.SetValue(playerAvatar, playerDeathHead);
                playerDeathHead.playerAvatar = playerAvatar;
                playerDeathHead.transform.SetParent(playerAvatar.transform.parent);
                Plugin.Log($"Reassigned PlayerDeathHead for player: {(!string.IsNullOrEmpty(playerName) ? playerName : (playerAvatar ? playerAvatar.name : ""))} ({photonView.Owner.ActorNumber}) - Assigned death head view id: " + deathHeadViewId);
            }

            // Display crown based on steam id instead of by player avatar to fix strange issues
            if (GameManager.Multiplayer() && playerAvatar)
            {
                try
                {
                    string steamId = (string)steamIdField.GetValue(playerAvatar);
                    PlayerAvatar crownedPlayer = SessionManager.instance.CrownedPlayerGet();
                    string crownedSteamId = crownedPlayer ? (string)steamIdField.GetValue(crownedPlayer) : "";
                    if (SemiFunc.IsMultiplayer() && (playerAvatar == crownedPlayer || (!string.IsNullOrEmpty(steamId) && steamId == crownedSteamId)))
                        playerDeathHead.playerCrown.gameObject.SetActive(true);
                }
                catch { }
            }
        }
    }
}
