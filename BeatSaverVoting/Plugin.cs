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

        internal static string beatsaverURL = "https://beatsaver.com";
        internal static string bmioURL = "https://api.beatmaps.io";
        internal static string votedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> votedSongs = new Dictionary<string, SongVote>();
        [OnStart]
        public void OnApplicationStart()
        {
            BS_Utils.Utilities.BSEvents.lateMenuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            BS_Utils.Utilities.BSEvents.gameSceneLoaded += BSEvents_gameSceneLoaded1;

            if (!File.Exists(votedSongsPath))
            {
                File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
            }
            else
            {
                votedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText(votedSongsPath, Encoding.UTF8));
            }
        }

        private void BSEvents_gameSceneLoaded1()
        {
            UI.VotingUI.instance._lastSong = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level;
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
            File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
        }

    }
}
