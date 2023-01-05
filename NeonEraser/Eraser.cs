using Steamworks;
using System.Reflection;
using UnityEngine.InputSystem;

namespace NeonEraser
{
    public class Eraser : MelonLoader.MelonMod
    {
        private const int NEWTIMEUPLOAD = 600000;
        private const long NEWTIMELOCAL = 600000000;

        private readonly CallResult<LeaderboardFindResult_t> findLeaderboardForUpload =
            CallResult<LeaderboardFindResult_t>.Create
            (new CallResult<LeaderboardFindResult_t>.APIDispatchDelegate
                (OverrideScore));

        
        private static CallResult<LeaderboardScoreUploaded_t> leaderboardScoreUploadedResult2;


        public override void OnUpdate()
        {
            if (!Keyboard.current.tKey.wasPressedThisFrame) return;

            if (leaderboardScoreUploadedResult2 == null)
            {
                typeof(LeaderboardIntegrationSteam).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[0]);
                leaderboardScoreUploadedResult2 = (CallResult<LeaderboardScoreUploaded_t>)typeof(LeaderboardIntegrationSteam).
                    GetField("leaderboardScoreUploadedResult2", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            }

            GameDataManager.levelStats["GRID_EXTERMINATOR"]._timeBestMicroseconds = NEWTIMELOCAL;
            GameDataManager.SaveGame();
            Erase("GRID_EXTERMINATOR");
        }

        private void EraseAll()
        {
            GameData gameData = Singleton<Game>.Instance.GetGameData();
            foreach (var campaign in gameData.campaigns)
                foreach (var missionData in campaign.missionData)
                    foreach (var level in missionData.levels)
                        Erase(level.levelID);
        }

        private void Erase(string boardName)
        {
            SteamAPICall_t hAPICall = SteamUserStats.FindOrCreateLeaderboard(boardName, ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds);
            findLeaderboardForUpload.Set(hAPICall, null);
        }

        private static void OverrideScore(LeaderboardFindResult_t pCallback, bool bIOFailure)
        {
            if (pCallback.m_bLeaderboardFound != 1 || bIOFailure)
            {
                UnityEngine.Debug.LogError("No leaderboard fetched!");
                return;
            }
            SteamAPICall_t hAPICall = SteamUserStats.UploadLeaderboardScore(pCallback.m_hSteamLeaderboard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate, NEWTIMEUPLOAD, null, 0);
            leaderboardScoreUploadedResult2.Set(hAPICall, null);
        }
    }
}