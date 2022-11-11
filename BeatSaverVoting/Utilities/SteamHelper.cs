using System.Threading.Tasks;
using Steamworks;

namespace BeatSaverVoting.Utilities
{
    public class SteamHelper
    {
        private static SteamHelper _instance;
        public static SteamHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SteamHelper();
                return _instance;
            }
        }
        public HAuthTicket lastTicket;
        public EResult lastTicketResult;

        private readonly SteamPlatformUserModel _userModel = new SteamPlatformUserModel();

        private void OnAuthTicketResponse(GetAuthSessionTicketResponse_t response)
        {
            if (lastTicket == response.m_hAuthTicket)
            {
                lastTicketResult = response.m_eResult;
            }
        }

        public async Task<string> getToken()
        {
            return (await _userModel.GetUserAuthToken()).token;
        }
    }
}
