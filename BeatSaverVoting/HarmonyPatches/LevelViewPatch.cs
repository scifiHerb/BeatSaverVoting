using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using showLevelStats;
using HMUI;

namespace showLevelStats.HarmonyPatches
{
    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContent", MethodType.Normal)]
    internal class LevelListTableCellSetDataFromLevel
    {
        public static CurvedTextMeshPro textMesh;

        private static void Postfix(IBeatmapLevel level, BeatmapDifficulty defaultDifficulty, BeatmapCharacteristicSO defaultBeatmapCharacteristic, PlayerData playerData, TextMeshProUGUI ____actionButtonText)
        {
            BeatSaverVoting.UI.VotingUI.instance.lastSong = level;
            BeatSaverVoting.UI.VotingUI.instance.GetVotesForMap();
        }

        public enum BeatmapDifficulty
        {
            // Token: 0x04000006 RID: 6
            Easy,
            // Token: 0x04000007 RID: 7
            Normal,
            // Token: 0x04000008 RID: 8
            Hard,
            // Token: 0x04000009 RID: 9
            Expert,
            // Token: 0x0400000A RID: 10
            ExpertPlus
        }
    }
}
