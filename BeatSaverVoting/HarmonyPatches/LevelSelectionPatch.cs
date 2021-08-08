using HarmonyLib;
using UnityEngine;

namespace BeatSaverVoting.HarmonyPatches
{
    [HarmonyPatch(typeof(LevelListTableCell))]
    [HarmonyPatch("SetDataFromLevelAsync", MethodType.Normal)]
    internal class LevelListTableCellSetDataFromLevel
    {
        private static void Postfix(IPreviewBeatmapLevel level, bool isFavorite, ref UnityEngine.UI.Image ____favoritesBadgeImage)
        {
            var hash = SongCore.Collections.hashForLevelID(level.levelID).ToLower();
            var voteStatus = Plugin.CurrentVoteStatus(hash);

            if (voteStatus == null && !isFavorite)
            {
                return;
            }

            ____favoritesBadgeImage.enabled = true;

            if (isFavorite)
            {
                switch (voteStatus)
                {
                    case Plugin.VoteType.Upvote: { ____favoritesBadgeImage.sprite = Plugin.FavoriteUpvoteIcon; break; }
                    case Plugin.VoteType.Downvote: { ____favoritesBadgeImage.sprite = Plugin.FavoriteDownvoteIcon; break; }
                    case null: { ____favoritesBadgeImage.sprite = Plugin.FavoriteIcon; break; }
                }
            }
            else
            {
                switch (voteStatus)
                {
                    case Plugin.VoteType.Upvote: { ____favoritesBadgeImage.sprite = Plugin.UpvoteIcon; break; }
                    case Plugin.VoteType.Downvote: { ____favoritesBadgeImage.sprite = Plugin.DownvoteIcon; break; }
                }
            }

            ____favoritesBadgeImage.rectTransform.sizeDelta = new Vector2(3.5f, 7);
            var transform = ____favoritesBadgeImage.transform;
            var position = transform.position;
            position = new Vector3(-0.47f, position.y, position.z);
            transform.position = position;
        }
    }
}
