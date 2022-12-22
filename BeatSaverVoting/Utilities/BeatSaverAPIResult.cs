using System;
using Newtonsoft.Json.Linq;

namespace BeatSaverVoting.Utilities
{
    [Serializable]
    public class Song
    {
        public string levelAuthorName;
        public string songAuthorName;
        public string songName;
        public string songSubName;
        public int upVotes;
        public int downVotes;
        public string key;
        public string hash;

        public string path;

        public Song()
        {

        }

        public Song(JObject jsonNode)
        {
            upVotes = (int)jsonNode["stats"]["upvotes"];
            downVotes = (int)jsonNode["stats"]["downvotes"];

            hash = ((string)jsonNode["versions"][0]?["hash"])?.ToLower();

            key = (string)jsonNode["id"];
        }


        public bool Compare(Song compareTo)
        {
            return compareTo.hash == hash;
        }

        public Song(CustomPreviewBeatmapLevel data)
        {
            songName = data.songName;
            songSubName = data.songSubName;
            songAuthorName = data.songAuthorName;
            levelAuthorName = data.levelAuthorName;
            path = data.customLevelPath;
            hash = SongCore.Collections.hashForLevelID(data.levelID).ToLower();
        }
    }
}
