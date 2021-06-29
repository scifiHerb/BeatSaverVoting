using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;
using BeatSaverVoting.Utilities;
using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine.UI;

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
            Logging.Log.Error($"resultsView: {resultsView}");

            if (!resultsView) return;
            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BeatSaverVoting.UI.votingUI.bsml"), resultsView.gameObject, this);
            resultsView.didActivateEvent += ResultsView_didActivateEvent;
            SetColors();
        }

        private AnimationClip GenerateButtonAnimation(float r, float g, float b, float a = 0.502f, float x = 1, float y = 1)
        {
            return GenerateButtonAnimation(
                AnimationCurve.Constant(0, 1, r),
                AnimationCurve.Constant(0, 1, g),
                AnimationCurve.Constant(0, 1, b),
                AnimationCurve.Constant(0, 1, a),
                AnimationCurve.Constant(0, 1, x),
                AnimationCurve.Constant(0, 1, y)
            );
        }

        private AnimationClip GenerateButtonAnimation(AnimationCurve r, AnimationCurve g, AnimationCurve b, AnimationCurve a, AnimationCurve x, AnimationCurve y)
        {
            var animation = new AnimationClip { legacy = true };

            animation.SetCurve("Icon", typeof(Transform), "localScale.x", x);
            animation.SetCurve("Icon", typeof(Transform), "localScale.y", y);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.r", r);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.g", g);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.b", b);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.a", a);

            return animation;
        }

        private void SetColors()
        {
            var upArrow = upButton.GetComponentInChildren<ImageView>();
            var downArrow = downButton.GetComponentInChildren<ImageView>();

            if (upArrow != null && downArrow != null)
            {
                var upAnim = upButton.GetComponent<ButtonStaticAnimations>();
                var downAnim = downButton.GetComponent<ButtonStaticAnimations>();

                upAnim.SetPrivateField("_normalClip", GenerateButtonAnimation(0.341f, 0.839f, 0.341f));
                downAnim.SetPrivateField("_normalClip", GenerateButtonAnimation( 0.984f, 0.282f, 0.305f));
                upAnim.SetPrivateField("_highlightedClip", GenerateButtonAnimation(0.341f, 0.839f, 0.341f, 1, 1.5f, 1.5f));
                downAnim.SetPrivateField("_highlightedClip", GenerateButtonAnimation( 0.984f, 0.282f, 0.305f, 1, 1.5f, 1.5f));
            }
        }

        private void ResultsView_didActivateEvent(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            GetVotesForMap();
        }

        [UIAction("up-pressed")]
        private void UpvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong, true, UpdateUIAfterVote);
        }
        [UIAction("down-pressed")]
        private void DownvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong, false, UpdateUIAfterVote);
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
            var www = UnityWebRequest.Get($"{Plugin.BeatsaverURL}/api/maps/by-hash/{hash.ToLower()}");
            www.SetRequestHeader("user-agent", userAgent);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Logging.Log.Error($"Unable to connect to {Plugin.BeatsaverURL}! " +
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
            yield return cd.Coroutine;

            try
            {
                _firstVote = true;
                _lastBeatSaverSong = null;

                if (cd.Result is Song song)
                {
                    _lastBeatSaverSong = song;

                    voteText.text = (_lastBeatSaverSong.upVotes - _lastBeatSaverSong.downVotes).ToString();
                    if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
                    bool canVote = openVRHelper.vrPlatformSDK == VRPlatformSDK.Oculus || openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR ||
                                   Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc");

                    UpInteractable = canVote;
                    DownInteractable = canVote;

                    string lastLevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(_lastSong as CustomPreviewBeatmapLevel).ToLower();
                    if (Plugin.VotedSongs.ContainsKey(lastLevelHash))
                    {
                        switch (Plugin.VotedSongs[lastLevelHash].voteType)
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
            yield return cd.Coroutine;

            if (cd.Result is Song song)
                VoteForSong(song, upvote, callback);
        }

        private void VoteForSong(Song song, bool upvote, VoteCallback callback)
        {
            if (song == null)
            {
                callback?.Invoke(null, false, false, -1);
                return;
            }

            var userTotal = Plugin.VotedSongs.ContainsKey(song.hash) ? (Plugin.VotedSongs[song.hash].voteType == Plugin.VoteType.Upvote ? 1 : -1) : 0;
            var oldValue = song.upVotes - song.downVotes - userTotal;
            VoteForSong(song.key, song.hash, upvote, oldValue, callback);
        }

        private void VoteForSong(string key, string hash, bool upvote, int currentVoteCount, VoteCallback callback)
        {
            try
            {
                var flag1 = File.Exists(Path.Combine(UnityGame.InstallPath, "Beat Saber_Data\\Plugins\\x86_64\\steam_api64.dll"));
                if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
                if (openVRHelper.vrPlatformSDK == VRPlatformSDK.Oculus || !flag1)
                {
                    StartCoroutine(VoteWithOculusID(hash, upvote, currentVoteCount, callback));
                }
                else if ((openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR || Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc")))
                {
                    StartCoroutine(VoteWithSteamID(key, hash, upvote, currentVoteCount, callback));
                }
            }
            catch(Exception ex)
            {
                Logging.Log.Warn("Failed To Vote For Song " + ex.Message);
            }

        }

        private IEnumerator VoteWithOculusID(string hash, bool upvote, int currentVoteCount, VoteCallback callback)
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

            yield return PerformVoteBM(hash, new BMPayload() { auth = new BMAuth() {oculusId = oculusId.ToString(), proof = nonce}, direction = upvote, hash = hash}, currentVoteCount, callback);
        }

        private IEnumerator VoteWithSteamID(string key, string hash, bool upvote, int currentVoteCount, VoteCallback callback)
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
                                callback?.Invoke(hash, false, false, -1);
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
                                callback?.Invoke(hash, false, false, -1);
                                yield break;
                        }

                        break;
                    default:
                        UpInteractable = false;
                        DownInteractable = false;
                        voteText.text = "Auth\nfailed";
                        callback?.Invoke(hash, false, false, -1);
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
                callback?.Invoke(hash, false, false, -1);
                yield break;
            }

            SteamHelper.Instance.lastTicketResult = EResult.k_EResultRevoked;

            //Logging.Log.Debug("Steam info: " + steamId + ", " + authTicketHexString);
            yield return PerformVoteAPI(key, hash, new Payload() {steamID = steamId.m_SteamID.ToString(), ticket = authTicketHexString, direction = (upvote ? 1 : -1)}, upvote, callback);
            yield return PerformVoteBM(hash, new BMPayload() {auth = new BMAuth() {steamId = steamId.m_SteamID.ToString(), proof = authTicketHexString}, direction = upvote, hash = hash}, currentVoteCount, null);
        }

        private IEnumerator PerformVoteBM(string hash, BMPayload payload, int currentVoteCount, VoteCallback callback)
        {
            Logging.Log.Debug($"Voting BM...");
            var json = JsonConvert.SerializeObject(payload);
            var voteWWW = UnityWebRequest.Post($"{Plugin.BmioURL}/vote", json);

            var jsonBytes = new System.Text.UTF8Encoding().GetBytes(json);
            voteWWW.uploadHandler = new UploadHandlerRaw(jsonBytes);
            voteWWW.SetRequestHeader("Content-Type", "application/json");
            voteWWW.SetRequestHeader("user-agent", userAgent);
            voteWWW.timeout = 30;
            yield return voteWWW.SendWebRequest();

            if (voteWWW.isNetworkError)
            {
                Logging.Log.Error(voteWWW.error);
                callback?.Invoke(hash, false, false, currentVoteCount);
            }
            else if (voteWWW.responseCode != 200)
            {
                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                callback?.Invoke(hash, false, false, currentVoteCount);
            } else {
                callback?.Invoke(hash, true, payload.direction, currentVoteCount + (payload.direction ? 1 : -1));
            }
        }

        private void UpdateUIAfterVote(string hash, bool success, bool upvote, int newTotal) {
            if (!success) return;
            
            voteText.text = newTotal.ToString();

            UpInteractable = !upvote;
            DownInteractable = upvote;

            if (!Plugin.VotedSongs.ContainsKey(hash) || Plugin.VotedSongs[hash].voteType != (upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote))
            {
                Plugin.VotedSongs[hash] = new Plugin.SongVote(hash, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote);
                Plugin.WriteVotes();
            }
        }

        private IEnumerator PerformVoteAPI(string key, string hash, Payload payload, bool upvote, VoteCallback callback)
        {
            Logging.Log.Debug($"Voting...");
            string json = JsonUtility.ToJson(payload);
           // Logging.Log.Info(json);
           UnityWebRequest voteWWW;
           voteWWW = UnityWebRequest.Post($"{Plugin.BeatsaverURL}/api/vote/steam/{key}", json);

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
                    callback?.Invoke(hash, true, upvote, (int) node["stats"]["upVotes"] - (int) node["stats"]["downVotes"]);
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
                            voteText.text = "Beatmap not\nfound";
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
                    callback?.Invoke(hash, false, false, -1);
                }
            }
        }
    }
}
