using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;
using BeatSaverVoting.Utilities;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;

namespace BeatSaverVoting.UI
{
    public class VotingUI : NotifiableSingleton<VotingUI>
    {

        [Serializable]
        private struct Payload
        {
            public string steamID;
            public string ticket;
            public int direction;
        }

        [Serializable] private struct BMAuth
        {
            public string steamId;
            public string oculusId;
            public string proof;
        }

        private struct BMPayload
        {
            public BMAuth auth;
            public bool direction;
            public string hash;
        }

        internal IBeatmapLevel _lastSong;
        private OpenVRHelper openVRHelper;
        private bool _firstVote;
        private Song _lastBeatSaverSong;
        private string userAgent = $"BeatSaverVoting/{Assembly.GetExecutingAssembly().GetName().Version}";
        [UIComponent("voteTitle")]
        public TextMeshProUGUI voteTitle;
        [UIComponent("voteText")]
        public TextMeshProUGUI voteText;
        [UIComponent("upButton")]
        public Transform upButton;
        [UIComponent("downButton")]
        public Transform downButton;
        private bool upInteractable = true;
        [UIValue("UpInteractable")]
        public bool UpInteractable
        {
            get => upInteractable;
            set
            {
                upInteractable = value;
                NotifyPropertyChanged();
            }
        }
        private bool downInteractable = true;
        [UIValue("DownInteractable")]
        public bool DownInteractable
        {
            get => downInteractable;
            set
            {
                downInteractable = value;
                NotifyPropertyChanged();
            }
        } 

        internal void Setup()
        {
            var resultsView = Resources.FindObjectsOfTypeAll<ResultsViewController>().FirstOrDefault();
            if (!resultsView) return;
            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BeatSaverVoting.UI.votingUI.bsml"), resultsView.gameObject, this);
            resultsView.didActivateEvent += ResultsView_didActivateEvent;
            UnityEngine.UI.Image upArrow = upButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image downArrow = downButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            if(upArrow != null && downArrow != null)
            {
                upArrow.color = new Color(0.341f, 0.839f, 0.341f);
                downArrow.color = new Color(0.984f, 0.282f, 0.305f);
            }
        }

        private void ResultsView_didActivateEvent(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            //Utilities.Logging.Log.Info("Initializing VotingUI");
            GetVotesForMap();
        }

        [UIAction("up-pressed")]
        private void UpvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong.key, _lastBeatSaverSong.hash, true, UpdateUIAfterVote);
        }
        [UIAction("down-pressed")]
        private void DownvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong.key, _lastBeatSaverSong.hash, false, UpdateUIAfterVote);
        }

        private void GetVotesForMap()
        {
            if(!(_lastSong is CustomPreviewBeatmapLevel))
            {
                downButton.gameObject.SetActive(false);
                upButton.gameObject.SetActive(false);
                voteText.text = "";
                voteTitle.text = "";
                return;
            }
            voteText.text = "Loading...";
            StartCoroutine(GetRatingForSong(_lastSong));
        }

        private IEnumerator GetSongInfo(string hash)
        {
            var www = UnityWebRequest.Get($"{Plugin.beatsaverURL}/api/maps/by-hash/{hash.ToLower()}");
            www.SetRequestHeader("user-agent", userAgent);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Logging.Log.Error($"Unable to connect to {Plugin.beatsaverURL}! " +
                                  (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
            }
            else
            {
                Song result = null;
                try
                {
                    var jNode = JObject.Parse(www.downloadHandler.text);
                    if (jNode.Children().Any())
                    {
                        result = new Song(jNode);
                    }
                    else
                    {
                        Logging.Log.Error("Song doesn't exist on BeatSaver!");
                    }
                }
                catch (Exception e)
                {
                    Logging.Log.Critical("Unable to get song rating! Excpetion: " + e);
                }

                yield return result;
            }
        }

        private IEnumerator GetRatingForSong(IBeatmapLevel level)
        {
            var cd = new CoroutineWithData(this, GetSongInfo(SongCore.Utilities.Hashing.GetCustomLevelHash(level as CustomPreviewBeatmapLevel)));
            yield return cd.coroutine;

            try
            {
                _firstVote = true;

                if (cd.result is Song song)
                {
                    _lastBeatSaverSong = song;

                    voteText.text = (_lastBeatSaverSong.upVotes - _lastBeatSaverSong.downVotes).ToString();
                    if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
                    bool canVote = openVRHelper.vrPlatformSDK == VRPlatformSDK.Oculus || openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR ||
                                   Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc");

                    UpInteractable = canVote;
                    DownInteractable = canVote;

                    string lastLevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(_lastSong as CustomPreviewBeatmapLevel).ToLower();
                    if (Plugin.votedSongs.ContainsKey(lastLevelHash))
                    {
                        switch (Plugin.votedSongs[lastLevelHash].voteType)
                        {
                            case Plugin.VoteType.Upvote: { UpInteractable = false; } break;
                            case Plugin.VoteType.Downvote: { DownInteractable = false; } break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Log.Critical("Unable to get song rating! Excpetion: " + e);
            }
        }

        internal void VoteForSong(string hash, bool upvote, VoteCallback callback)
        {
            StartCoroutine(VoteForSongAsync(hash, upvote, callback));
        }

        private IEnumerator VoteForSongAsync(string hash, bool upvote, VoteCallback callback)
        {
            var cd = new CoroutineWithData(this, GetSongInfo(hash));
            yield return cd.coroutine;

            if (cd.result is Song song)
            {
                VoteForSong(song.key, hash, upvote, callback);
            }
        }

        internal void VoteForSong(string key, string hash, bool upvote, VoteCallback callback)
        {
            try
            {
                var flag1 = File.Exists(Path.Combine(UnityGame.InstallPath, "Beat Saber_Data\\Plugins\\x86_64\\steam_api64.dll"));
                if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
                if (openVRHelper.vrPlatformSDK == VRPlatformSDK.Oculus || !flag1)
                {
                    StartCoroutine(VoteWithOculusID(hash, upvote, callback));
                }
                else if ((openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR || Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc")))
                {
                    StartCoroutine(VoteWithSteamID(key, hash, upvote, callback));
                }
            }
            catch(Exception ex)
            {
                Logging.Log.Warn("Failed To Vote For Song " + ex.Message);
            }

        }

        private IEnumerator VoteWithOculusID(string hash, bool upvote, VoteCallback callback)
        {
            UpInteractable = false;
            DownInteractable = false;
            voteText.text = "Voting...";
            Logging.Log.Debug($"Getting user proof...");

            var task = Task.Run(async () =>
            {
                var a = await OculusHelper.Instance.getUserId();
                var b = await OculusHelper.Instance.getToken();

                return (a, b);
            });

            yield return new WaitUntil(() => task.IsCompleted);
            var (oculusId, nonce) = task.Result;

            yield return PerformVoteBM(new BMPayload() { auth = new BMAuth() {oculusId = oculusId.ToString(), proof = nonce}, direction = upvote, hash = hash}, callback);
        }

        private IEnumerator VoteWithSteamID(string key, string hash, bool upvote, VoteCallback callback)
        {
            if (!SteamManager.Initialized)
            {
                Logging.Log.Error($"SteamManager is not initialized!");
            }

            UpInteractable = false;
            DownInteractable = false;
            voteText.text = "Voting...";
            Logging.Log.Debug($"Getting a ticket...");

            var steamId = SteamUser.GetSteamID();
            string authTicketHexString = "";

            byte[] authTicket = new byte[1024];
            var authTicketResult = SteamUser.GetAuthSessionTicket(authTicket, 1024, out var length);
            if (authTicketResult != HAuthTicket.Invalid)
            {
                var beginAuthSessionResult = SteamUser.BeginAuthSession(authTicket, (int) length, steamId);
                switch (beginAuthSessionResult)
                {
                    case EBeginAuthSessionResult.k_EBeginAuthSessionResultOK:
                        var result = SteamUser.UserHasLicenseForApp(steamId, new AppId_t(620980));

                        SteamUser.EndAuthSession(steamId);

                        switch (result)
                        {
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultDoesNotHaveLicense:
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "User does not\nhave license";
                                yield break;
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultHasLicense:
                                SteamHelper.Instance.SetupAuthTicketResponse();

                                SteamHelper.Instance.lastTicket = SteamUser.GetAuthSessionTicket(authTicket, 1024, out length);
                                if (SteamHelper.Instance.lastTicket != HAuthTicket.Invalid)
                                {
                                    Array.Resize(ref authTicket, (int) length);
                                    authTicketHexString = BitConverter.ToString(authTicket).Replace("-", "");
                                }

                                break;
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultNoAuth:
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "User is not\nauthenticated";
                                yield break;
                        }

                        break;
                    default:
                        UpInteractable = false;
                        DownInteractable = false;
                        voteText.text = "Auth\nfailed";
                        yield break;
                }
            }

            Logging.Log.Debug("Waiting for Steam callback...");

            float startTime = Time.time;
            yield return new WaitWhile(() => { return SteamHelper.Instance.lastTicketResult != EResult.k_EResultOK && (Time.time - startTime) < 20f; });

            if (SteamHelper.Instance.lastTicketResult != EResult.k_EResultOK)
            {
                Logging.Log.Error($"Auth ticket callback timeout");
                UpInteractable = true;
                DownInteractable = true;
                voteText.text = "Callback\ntimeout";
                yield break;
            }

            SteamHelper.Instance.lastTicketResult = EResult.k_EResultRevoked;

            //Logging.Log.Debug("Steam info: " + steamId + ", " + authTicketHexString);
            yield return PerformVoteAPI(key, new Payload() {steamID = steamId.m_SteamID.ToString(), ticket = authTicketHexString, direction = (upvote ? 1 : -1)}, upvote, callback);
            yield return PerformVoteBM(new BMPayload() {auth = new BMAuth() {steamId = steamId.m_SteamID.ToString(), proof = authTicketHexString}, direction = upvote, hash = hash}, null);
        }

        private IEnumerator PerformVoteBM(BMPayload payload, VoteCallback callback)
        {
            Logging.Log.Debug($"Voting BM...");
            var json = JsonConvert.SerializeObject(payload);
            var voteWWW = UnityWebRequest.Post($"{Plugin.bmioURL}/vote", json);

            var jsonBytes = new System.Text.UTF8Encoding().GetBytes(json);
            voteWWW.uploadHandler = new UploadHandlerRaw(jsonBytes);
            voteWWW.SetRequestHeader("Content-Type", "application/json");
            voteWWW.SetRequestHeader("user-agent", userAgent);
            voteWWW.timeout = 30;
            yield return voteWWW.SendWebRequest();

            if (voteWWW.isNetworkError)
            {
                Logging.Log.Error(voteWWW.error);
                callback?.Invoke(false, false, -1);
            }
            else if (voteWWW.responseCode != 200)
            {
                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                callback?.Invoke(false, false, -1);
            } else {
                var oldValue = _lastBeatSaverSong.upVotes - _lastBeatSaverSong.downVotes;
                callback?.Invoke(true, payload.direction, oldValue + (payload.direction ? 1 : -1));
            }
        }

        private void UpdateUIAfterVote(bool success, bool upvote, int newTotal) {
            if (!success) return;
            
            voteText.text = newTotal.ToString();

            if (upvote)
            {
                UpInteractable = false;
                DownInteractable = true;
            }
            else
            {
                DownInteractable = false;
                UpInteractable = true;
            }
            string lastlevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(_lastSong as CustomPreviewBeatmapLevel).ToLower();
            if (!Plugin.votedSongs.ContainsKey(lastlevelHash))
            {
                Plugin.votedSongs.Add(lastlevelHash, new Plugin.SongVote(_lastBeatSaverSong.key, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote));
                Plugin.WriteVotes();
            }
            else if (Plugin.votedSongs[lastlevelHash].voteType != (upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote))
            {
                Plugin.votedSongs[lastlevelHash] = new Plugin.SongVote(_lastBeatSaverSong.key, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote);
                Plugin.WriteVotes();
            }
        }

        private IEnumerator PerformVoteAPI(string key, Payload payload, bool upvote, VoteCallback callback)
        {
            Logging.Log.Debug($"Voting...");
            string json = JsonUtility.ToJson(payload);
           // Logging.Log.Info(json);
           UnityWebRequest voteWWW;
           voteWWW = UnityWebRequest.Post($"{Plugin.beatsaverURL}/api/vote/steam/{_lastBeatSaverSong.key}", json);

            byte[] jsonBytes = new System.Text.UTF8Encoding().GetBytes(json);
            voteWWW.uploadHandler = new UploadHandlerRaw(jsonBytes);
            voteWWW.SetRequestHeader("Content-Type", "application/json");
            voteWWW.SetRequestHeader("user-agent", userAgent);
            voteWWW.timeout = 30;
            yield return voteWWW.SendWebRequest();

            if (voteWWW.isNetworkError)
            {
                Logging.Log.Error(voteWWW.error);
                voteText.text = voteWWW.error;
            }
            else
            {
                if (!_firstVote)
                {
                    yield return new WaitForSecondsRealtime(2f);
                }

                _firstVote = false;

                if (voteWWW.responseCode >= 200 && voteWWW.responseCode <= 299)
                {
                    var node = JObject.Parse(voteWWW.downloadHandler.text);
                    callback?.Invoke(true, upvote, (int) node["stats"]["upVotes"] - (int) node["stats"]["downVotes"]);
                }
                else
                {
                    switch (voteWWW.responseCode)
                    {
                        case 500:
                            UpInteractable = false;
                            DownInteractable = false;
                            voteText.text = "Server \nerror";
                            Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            break;
                        case 401:
                            UpInteractable = false;
                            DownInteractable = false;
                            voteText.text = "Invalid\nauth ticket";
                            Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            break;
                        case 404:
                            UpInteractable = false;
                            DownInteractable = false;
                            voteText.text = "Beatmap not\found";
                            Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            break;
                        case 400:
                            UpInteractable = false;
                            DownInteractable = false;
                            voteText.text = "Bad\nrequest";
                            Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            break;
                        default:
                            UpInteractable = true;
                            DownInteractable = true;
                            voteText.text = "Error\n" + voteWWW.responseCode;
                            Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            break;
                    }
                    callback?.Invoke(false, false, -1);
                }
            }
        }
    }
}
