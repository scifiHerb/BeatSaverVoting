using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        internal const string BeatsaverURL = "https://beatsaver.com";
        internal const string BmioURL = "https://api.beatmaps.io";
        private static readonly string VotedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> VotedSongs = new Dictionary<string, SongVote>();

        [OnStart]
        public void OnApplicationStart()
        {
            BS_Utils.Utilities.BSEvents.lateMenuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            BS_Utils.Utilities.BSEvents.gameSceneLoaded += BSEvents_gameSceneLoaded1;

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
