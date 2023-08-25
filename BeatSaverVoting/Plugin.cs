using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BS_Utils.Utilities;
using HarmonyLib;
using IPA;
using IPA.Utilities;
using IPA.Config.Stores;
using IPALogger = IPA.Logging.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using BeatSaberMarkupLanguage.Settings;
using BeatSaverVoting.Settings;
using BeatSaverVoting.UI;
using UnityEngine.SceneManagement;

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
            return votedSongs.ContainsKey(hash) ? votedSongs[hash].voteType : (VoteType?) null;
        }

        internal const string BeatsaverURL = "https://api.beatsaver.com";
        private static readonly string VotedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> votedSongs = new Dictionary<string, SongVote>();

        internal static HMUI.TableView tableView;
        internal static Sprite favoriteIcon;
        internal static Sprite favoriteUpvoteIcon;
        internal static Sprite favoriteDownvoteIcon;
        internal static Sprite upvoteIcon;
        internal static Sprite downvoteIcon;

        [OnStart]
        public void OnApplicationStart()
        {
            BSEvents.menuSceneActive += BSEvents_levelSelected;
            BSEvents.gameSceneLoaded += BSEvents_gameSceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;

            favoriteIcon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("BeatSaverVoting.Icons.Favorite.png");
            favoriteUpvoteIcon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("BeatSaverVoting.Icons.FavoriteUpvote.png");
            favoriteDownvoteIcon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("BeatSaverVoting.Icons.FavoriteDownvote.png");
            upvoteIcon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("BeatSaverVoting.Icons.Upvote.png");
            downvoteIcon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("BeatSaverVoting.Icons.Downvote.png");

            _harmony = new Harmony("com.kyle1413.BeatSaber.BeatSaverVoting");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            if (!File.Exists(VotedSongsPath))
            {
                File.WriteAllText(VotedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
            }
            else
            {
                votedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText(VotedSongsPath, Encoding.UTF8)) ?? votedSongs;
            }
        }

        void SceneLoaded(Scene nextScene, LoadSceneMode mode)
        {
            Debug.Log(nextScene.name);
            Debug.Log(mode);
        }

        [OnExit]
        public void OnEnd()
        {
            _harmony.UnpatchSelf();
        }

        private static void BSEvents_gameSceneLoaded()
        {
            UI.VotingUI.instance.lastSong = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData?.difficultyBeatmap.level;
        }

        private static void BSEvents_menuSceneLoadedFresh(ScenesTransitionSetupDataSO data)
        {
        }

        private static bool flag = false;
        private static void
        BSEvents_levelSelected()
        {
            if (!flag)
            {
                UI.VotingUI.instance.Setup();
                tableView = Resources.FindObjectsOfTypeAll<LevelCollectionTableView>().FirstOrDefault()
                    .GetField<HMUI.TableView, LevelCollectionTableView>("_tableView");
            }
            flag = true;
            UI.VotingUI.instance.GetVotesForMap();
        }

        [Init]
        public void Init(IPALogger pluginLogger, IPA.Config.Config conf)
        {
            Utilities.Logging.log = pluginLogger;
            Configuration.Instance = conf.Generated<Configuration>();
            BSMLSettings.instance.AddSettingsMenu("BeatSaber Voting", "BeatSaverVoting.Settings.settings.bsml", SettingsHandler.instance);
        }

        public static void WriteVotes()
        {
            File.WriteAllText(VotedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
        }

    }
}
