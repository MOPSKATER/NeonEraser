using HarmonyLib;
using Steamworks;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MelonLoader.MelonLaunchOptions;

namespace NeonEraser
{
    public class Eraser : MelonLoader.MelonMod
    {
        private const int NEWTIMEUPLOAD = 600000;
        private const long NEWTIMELOCAL = 600000000;
        internal static Eraser eraser;

        //private readonly CallResult<LeaderboardFindResult_t> findLeaderboardForUploadGlobal =
        //    CallResult<LeaderboardFindResult_t>.Create
        //    (new CallResult<LeaderboardFindResult_t>.APIDispatchDelegate
        //        (AdjustGlobal));

        private static CallResult<LeaderboardScoreUploaded_t> leaderboardScoreUploadedResult2;


        private static readonly MethodInfo OnLeaderboardScoreUploaded2Info = typeof(LeaderboardIntegrationSteam).
            GetMethod("OnLeaderboardScoreUploaded2", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly static List<CallResult<LeaderboardFindResult_t>> findResults = new();
        private readonly static List<CallResult<LeaderboardScoreUploaded_t>> upload = new();

        private static bool buttonEraseAllCreated = false;

        private static GameObject buttonEraseRush = null;
        private static readonly FieldInfo m_levelRushType = typeof(MenuScreenLevelRush).GetField("m_levelRushType", BindingFlags.NonPublic | BindingFlags.Instance);


        public override void OnApplicationLateStart()
        {
            eraser = this;
            typeof(LeaderboardIntegrationSteam).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[0]);
            leaderboardScoreUploadedResult2 = (CallResult<LeaderboardScoreUploaded_t>)typeof(LeaderboardIntegrationSteam).
                GetField("leaderboardScoreUploadedResult2", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);


            HarmonyLib.Harmony harmony = new("de.MOPSKATER.NeonEraser");

            // IL buttons
            MethodInfo target = typeof(MenuButtonLevel).GetMethod("SetLevelData", BindingFlags.Public | BindingFlags.Instance);
            HarmonyMethod patch = new(GetType().GetMethod("PostSetLevelData", BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(target, null, patch);

            // Rush buttons
            target = typeof(MenuScreenLevelRush).GetMethod("OnSelectRush", BindingFlags.Public | BindingFlags.Instance);
            patch = new(GetType().GetMethod("PostOnSelectRush", BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(target, null, patch);

            target = typeof(MenuScreenLevelRush).GetMethod("OnHardModeToggle", BindingFlags.Public | BindingFlags.Instance);
            patch = new(GetType().GetMethod("PostOnHardModeToggle", BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(target, null, patch);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (buttonEraseAllCreated) return;

            MenuScreenTitle titleScreen = (MenuScreenTitle)MainMenu.Instance()._screenTitle;
            GameObject eraseAllButton = Utils.InstantiateUI(
                titleScreen.buttonsToLoad[4].gameObject,
                "EraseAll",
                titleScreen.levelRushButton.transform.parent);
            MenuButtonHolder buttonHolder = eraseAllButton.GetComponent<MenuButtonHolder>();
            titleScreen.buttonsToLoad.Add(buttonHolder);
            Button button = buttonHolder.ButtonRef;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(EraseAllAntiMissclick);
            buttonHolder.buttonTextRef.SetText("Erase all levels");
            buttonEraseAllCreated = true;
        }

        public static void PostSetLevelData(MenuButtonLevel __instance, LevelData ld) // TODO set custom icon to fix empty backround
        {
            GameObject eraseButton = Utils.InstantiateUI(__instance.gameObject.transform.Find("Icon Holder/Medal Box").gameObject,
                "EraseButton",
                __instance.gameObject.transform);
            eraseButton.SetActive(true);
            UnityEngine.Object.Destroy(eraseButton.transform.Find("Medal Icon").gameObject);
            eraseButton.transform.localPosition = new Vector3(eraseButton.transform.localPosition.x, 0, eraseButton.transform.localPosition.z);
            eraseButton.AddComponent<MenuButtonEraser>().Setup(ld.levelID);
        }

        private void EraseAllAntiMissclick()
        {
            if (Keyboard.current.leftCtrlKey.isPressed)
            {
                EraseAll();
                return;
            }
        }

        internal void EraseAll()
        {
            GameData gameData = Singleton<Game>.Instance.GetGameData();
            foreach (var campaign in gameData.campaigns)
                foreach (var missionData in campaign.missionData)
                    foreach (var level in missionData.levels)
                        Erase(level.levelID);
        }

        internal void Erase(string levelID)
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

        public static void PostOnSelectRush(ref MenuScreenLevelRush __instance)
        {
            if (buttonEraseRush == null)
            {
                buttonEraseRush = Utils.InstantiateUI(
                    __instance.startRushButton.gameObject,
                    "Rush Eraser",
                    __instance.startRushButton.transform.parent);
                buttonEraseRush.transform.localPosition += new Vector3(0f, -70f, 0f);
                Button btn = buttonEraseRush.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(EraseRushAntiMissclick);
            }
            UpdateRushEraser(__instance);
        }

        public static void PostOnHardModeToggle(ref MenuScreenLevelRush __instance)
        {
            UpdateRushEraser(__instance);
        }

        private static void UpdateRushEraser(MenuScreenLevelRush instance)
        {

            TextMeshProUGUI buttonText = buttonEraseRush.GetComponentInChildren<TextMeshProUGUI>();
            buttonText.SetText("Erase " + (LevelRush.LevelRushType) m_levelRushType.GetValue(instance) switch
            {
                LevelRush.LevelRushType.WhiteRush => "Whites ",
                LevelRush.LevelRushType.MikeyRush => "Mikeys ",
                LevelRush.LevelRushType.YellowRush => "Yellows ",
                LevelRush.LevelRushType.RedRush => "Reds ",
                LevelRush.LevelRushType.VioletRush => "Violets ",
                _ => "Error"
            } + (instance.heavenToggle.isOn ? "Heavenrush" : "Hellrush"));
        }

        private static void EraseRushAntiMissclick()
        {
            if (!Keyboard.current.leftCtrlKey.isPressed) return;

            string[] levelRushLeaderboardNames = new string[]
                {
                    "HeavenRush",
                    "RedRush",
                    "VioletRush",
                    "YellowRush",
                    "MikeyRush"
                };

            MenuScreenLevelRush levelRushScreen = MainMenu.Instance()._screenLevelRush;
            string rushType = levelRushLeaderboardNames[LevelRush.GetIndexFromRushType((LevelRush.LevelRushType)m_levelRushType.GetValue(levelRushScreen))];
            string leaderboard = rushType + (levelRushScreen.heavenToggle.isOn ? "_heaven" : "_hell");
            Debug.Log("Resetting " + leaderboard);

            SteamAPICall_t hAPICall = SteamUserStats.FindOrCreateLeaderboard(leaderboard, ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds);
            var result = CallResult<LeaderboardFindResult_t>.Create
            (new CallResult<LeaderboardFindResult_t>.APIDispatchDelegate
                (OverrideRushScore));

            result.Set(hAPICall);
            findResults.Add(result);
        }

        private static void OverrideRushScore(LeaderboardFindResult_t pCallback, bool bIOFailure)
        {
            if (pCallback.m_bLeaderboardFound != 1 || bIOFailure)
            {
                Debug.LogError("No leaderboard fetched!");
                return;
            }

            int nScore = 6000000;
            int[] pScoreDetails = null;

            LevelRush.LevelRushType levelRushType = LevelRush.GetCompletedLevelRushStats().levelRushType;
            pScoreDetails = new int[]
            {
                0
            };

            SteamAPICall_t hAPICall = SteamUserStats.UploadLeaderboardScore(pCallback.m_hSteamLeaderboard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate, nScore, pScoreDetails, 1);
            var result = CallResult<LeaderboardScoreUploaded_t>.Create
                (new CallResult<LeaderboardScoreUploaded_t>.APIDispatchDelegate
                (OnLeaderboardScoreUploaded2));

            result.Set(hAPICall);
            upload.Add(result);
        }
    }
}