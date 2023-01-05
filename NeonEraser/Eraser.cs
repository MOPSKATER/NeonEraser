﻿using Steamworks;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonEraser
{
    public class Eraser : MelonLoader.MelonMod
    {
        private const int NEWTIMEUPLOAD = 600000;
        private const long NEWTIMELOCAL = 600000000;

        private readonly CallResult<LeaderboardFindResult_t> findLeaderboardForUploadGlobal =
            CallResult<LeaderboardFindResult_t>.Create
            (new CallResult<LeaderboardFindResult_t>.APIDispatchDelegate
                (AdjustGlobal));
        private static CallResult<LeaderboardScoreUploaded_t> leaderboardScoreUploadedResult2;


        private static readonly MethodInfo OnLeaderboardScoreUploaded2Info = typeof(LeaderboardIntegrationSteam).
            GetMethod("OnLeaderboardScoreUploaded2", BindingFlags.NonPublic | BindingFlags.Static);

        private static List<CallResult<LeaderboardFindResult_t>> findResults = new List<CallResult<LeaderboardFindResult_t>>();
        private static List<CallResult<LeaderboardScoreUploaded_t>> upload = new List<CallResult<LeaderboardScoreUploaded_t>>();


        public override void OnUpdate()
        {

            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                findResults.Clear();
                upload.Clear();
                if (leaderboardScoreUploadedResult2 == null)
                {
                    typeof(LeaderboardIntegrationSteam).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[0]);
                    leaderboardScoreUploadedResult2 = (CallResult<LeaderboardScoreUploaded_t>)typeof(LeaderboardIntegrationSteam).
                        GetField("leaderboardScoreUploadedResult2", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                }
            }

            if (Keyboard.current.uKey.wasPressedThisFrame)
            {
                SteamAPICall_t hAPICall = SteamUserStats.FindOrCreateLeaderboard("GlobalNeonRankings", ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds);
                findLeaderboardForUploadGlobal.Set(hAPICall, null);
            }
        }

        private void EraseAll()
        {
            GameData gameData = Singleton<Game>.Instance.GetGameData();
            foreach (var campaign in gameData.campaigns)
                foreach (var missionData in campaign.missionData)
                    foreach (var level in missionData.levels)
                        Erase(level.levelID);
        }

        private void Erase(string levelID)
        {
            SteamAPICall_t hAPICall = SteamUserStats.FindOrCreateLeaderboard(levelID, ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds);
            var result = CallResult<LeaderboardFindResult_t>.Create
            (new CallResult<LeaderboardFindResult_t>.APIDispatchDelegate
                (OverrideScore));
            result.Set(hAPICall);
            findResults.Add(result);

            GameDataManager.levelStats[levelID]._timeBestMicroseconds = NEWTIMELOCAL;
            GameDataManager.levelStats[levelID]._timeLastMicroseconds = NEWTIMELOCAL;
            GameDataManager.SaveGame();
        }

        private static void OverrideScore(LeaderboardFindResult_t pCallback, bool bIOFailure)
        {
            if (pCallback.m_bLeaderboardFound != 1 || bIOFailure)
            {
                Debug.LogError("No leaderboard fetched!");
                return;
            }

            SteamAPICall_t hAPICall = SteamUserStats.UploadLeaderboardScore(pCallback.m_hSteamLeaderboard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate, NEWTIMEUPLOAD, null, 0);
            var result = CallResult<LeaderboardScoreUploaded_t>.Create
                (new CallResult<LeaderboardScoreUploaded_t>.APIDispatchDelegate
                (OnLeaderboardScoreUploaded2));
            result.Set(hAPICall);
            upload.Add(result);
        }

        private static void OnLeaderboardScoreUploaded2(LeaderboardScoreUploaded_t pCallback, bool bIOFailure)
        {
            OnLeaderboardScoreUploaded2Info.Invoke(null, new object[] { pCallback, bIOFailure });
        }

        private static void AdjustGlobal(LeaderboardFindResult_t pCallback, bool bIOFailure)
        {
            if (pCallback.m_bLeaderboardFound != 1 || bIOFailure)
            {
                Debug.LogError("No leaderboard fetched!");
                return;
            }
            LeaderboardScoreCalculation.GetGlobalNeonScoreUploadData(out int nScore, out int num);
            int[] pScoreDetails = new int[] { num };

            SteamAPICall_t hAPICall = SteamUserStats.UploadLeaderboardScore(pCallback.m_hSteamLeaderboard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate, nScore, pScoreDetails, 1);
            leaderboardScoreUploadedResult2.Set(hAPICall, null);
        }
    }
}