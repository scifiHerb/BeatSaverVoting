using System.Threading.Tasks;
using Oculus.Platform;

namespace BeatSaverVoting.Utilities
{
    public class OculusHelper
    {
        private static OculusHelper _instance;
        public static OculusHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new OculusHelper();
                return _instance;
            }
        }
        private readonly OculusPlatformUserModel _userModel = new OculusPlatformUserModel();
        private ulong _userId;

        public async Task<ulong> getUserId()
        {
            // Cache user id
            if (_userId != 0) return _userId;

            if (!Core.IsInitialized()) Core.Initialize();
            var user = await _userModel.GetUserInfo();
            ulong.TryParse(user.platformUserId, out _userId);

            return _userId;
        }

        public async Task<string> getToken()
        {
            if (!Core.IsInitialized()) Core.Initialize();
            return await _userModel.GetUserAuthToken();
        }
    }
}
