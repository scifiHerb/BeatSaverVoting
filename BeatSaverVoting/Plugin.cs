using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BS_Utils.Utilities;
using HarmonyLib;
using IPA;
using IPALogger = IPA.Logging.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace BeatSaverVoting
{
    public delegate void VoteCallback(string hash, bool success, bool userDirection, int newTotal);

    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        private static Harmony _harmony;

        public enum VoteType { Upvote, Downvote };

        public struct SongVote
        {
            public string hash;
            [JsonConverter(typeof(StringEnumConverter))]
            public VoteType voteType;

            public SongVote(string hash, VoteType voteType)
            {
                this.hash = hash;
                this.voteType = voteType;
            }
        }

        public static void VoteForSong(string hash, VoteType type, VoteCallback callback)
        {
            UI.VotingUI.instance.VoteForSong(hash, type == VoteType.Upvote, callback);
        }

        public static VoteType? CurrentVoteStatus(string hash)
        {
            return VotedSongs.ContainsKey(hash) ? VotedSongs[hash].voteType : (VoteType?) null;
        }

        internal const string BmioURL = "https://api.beatsaver.com";
        private static readonly string VotedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> VotedSongs = new Dictionary<string, SongVote>();

        internal static HMUI.TableView TableView;
        internal static UnityEngine.Sprite FavoriteIcon;
        internal static UnityEngine.Sprite FavoriteUpvoteIcon;
        internal static UnityEngine.Sprite FavoriteDownvoteIcon;
        internal static UnityEngine.Sprite UpvoteIcon;
        internal static UnityEngine.Sprite DownvoteIcon;

        [OnStart]
        public void OnApplicationStart()
        {
            BSEvents.lateMenuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            BSEvents.gameSceneLoaded += BSEvents_gameSceneLoaded1;

            FavoriteIcon = UIUtilities.LoadSpriteFromResources("BeatSaverVoting.Icons.Favorite.png");
            FavoriteUpvoteIcon = UIUtilities.LoadSpriteFromResources("BeatSaverVoting.Icons.FavoriteUpvote.png");
            FavoriteDownvoteIcon = UIUtilities.LoadSpriteFromResources("BeatSaverVoting.Icons.FavoriteDownvote.png");
            UpvoteIcon = UIUtilities.LoadSpriteFromResources("BeatSaverVoting.Icons.Upvote.png");
            DownvoteIcon = UIUtilities.LoadSpriteFromResources("BeatSaverVoting.Icons.Downvote.png");

            _harmony = new Harmony("com.kyle1413.BeatSaber.BeatSaverVoting");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            if (!File.Exists(VotedSongsPath))
            {
                File.WriteAllText(VotedSongsPath, JsonConvert.SerializeObject(VotedSongs), Encoding.UTF8);
            }
            else
            {
                VotedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText(VotedSongsPath, Encoding.UTF8));
            }
        }

        private void BSEvents_gameSceneLoaded1()
        {
            UI.VotingUI.instance._lastSong = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData?.difficultyBeatmap.level;
        }

        private void BSEvents_menuSceneLoadedFresh(ScenesTransitionSetupDataSO data)
        {
            UI.VotingUI.instance.Setup();
            TableView = UnityEngine.Resources.FindObjectsOfTypeAll<LevelCollectionTableView>().FirstOrDefault()
                .GetField<HMUI.TableView>("_tableView");
        }

        [Init]
        public void Init(IPALogger pluginLogger)
        {
            Utilities.Logging.Log = pluginLogger;
        }

        public static void WriteVotes()
        {
            File.WriteAllText(VotedSongsPath, JsonConvert.SerializeObject(VotedSongs), Encoding.UTF8);
        }

    }
}
