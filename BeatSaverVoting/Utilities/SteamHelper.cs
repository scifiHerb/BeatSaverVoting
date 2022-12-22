using System.Threading.Tasks;

namespace BeatSaverVoting.Utilities
{
    public class SteamHelper
    {
        private static SteamHelper _instance;
        public static SteamHelper Instance => _instance ?? (_instance = new SteamHelper());

        private readonly SteamPlatformUserModel _userModel = new SteamPlatformUserModel();

        public async Task<string> GetToken()
        {
            return (await _userModel.GetUserAuthToken()).token;
        }
    }
}
