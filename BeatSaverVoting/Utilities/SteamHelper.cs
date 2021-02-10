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

        public Callback<GetAuthSessionTicketResponse_t> m_GetAuthSessionTicketResponse;

        private void OnAuthTicketResponse(GetAuthSessionTicketResponse_t response)
        {
            if (lastTicket == response.m_hAuthTicket)
            {
                lastTicketResult = response.m_eResult;
            }
        }

        public void SetupAuthTicketResponse()
        {
            if (m_GetAuthSessionTicketResponse == null)
                m_GetAuthSessionTicketResponse = Callback<GetAuthSessionTicketResponse_t>.Create(OnAuthTicketResponse);
        }
    }
}
