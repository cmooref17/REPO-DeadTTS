using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace REPO_DeadTTS
{
    public static class ReflectionExtension
    {
        public static readonly FieldInfo PlayerDeathHeadFieldInfo = AccessTools.Field(typeof(PlayerAvatar), "playerDeathHead");

        public static PlayerDeathHead GetPlayerDeathHead(this PlayerAvatar avatar) =>
            (PlayerDeathHead)PlayerDeathHeadFieldInfo.GetValue(avatar);

        public static void SetPlayerDeathHead(this PlayerAvatar avatar, PlayerDeathHead playerDeathHead) =>
            PlayerDeathHeadFieldInfo.SetValue(avatar, playerDeathHead);
    }
}
