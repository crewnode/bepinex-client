using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using HarmonyLib.Tools;
using Hazel;
using UnhollowerBaseLib;
using UnityEngine;

namespace CrewNodeClient
{
    [BepInPlugin("com.crewnode.client", "Crew Node Client", "0.1.0")]
    [BepInProcess("Among Us.exe")]
    public class CrewNodeClientPlugin : BasePlugin
    {
        public Harmony Harmony { get; } = new Harmony("com.crewnode.client");

        public ConfigEntry<string> Name { get; set; }
        public ConfigEntry<string> Ip { get; set; }
        public ConfigEntry<ushort> Port { get; set; }

        public override void Load()
        {
            Name = Config.Bind("Custom region", "Name", "CrewNode");
            Ip = Config.Bind("Custom region", "Ip", "35.236.73.105");
            Port = Config.Bind("Custom region", "Port", (ushort) 22023);

            var defaultRegions = ServerManager.DefaultRegions.ToList();

            var split = Ip.Value.Split(':');
            var ip = split[0];
            var port = ushort.TryParse(split.ElementAtOrDefault(1), out var p) ? p : (ushort) 22023;

            Name.Value = "CrewNode";

            defaultRegions.Insert(0, new RegionInfo(
                Name.Value, ip, new[]
                {
                    new ServerInfo($"{Name.Value}-Master-1", ip, port)
                })
            );

            ServerManager.DefaultRegions = defaultRegions.ToArray();

            // HarmonyFileLog.Enabled = true;
            // FileLog.logPath = @"C:\Users\Temporary\Desktop\log\oldClient_HarmonyLogger-" + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString() + ".txt"; ;
            Log.LogDebug("Patching..");
            Harmony.PatchAll();
        }

        [HarmonyDebug]
        public static class Patches
        {
            public static class RemovePlayerLimit
            {
                public static int Page = 0;
                private static string _lastText;

                public static int MaxPages
                {
                    get
                    {
                        return (int)Math.Ceiling(PlayerControl.AllPlayerControls.Count / 10m);
                    }
                }

                public static void UpdatePageText(MeetingHud meetingHud)
                {
                    bool flag = meetingHud.TimerText.Text == RemovePlayerLimit._lastText;
                    if (!flag)
                    {
                        meetingHud.TimerText.Text = (RemovePlayerLimit._lastText = string.Format("Page {0}/{1} ", RemovePlayerLimit.Page + 1, RemovePlayerLimit.MaxPages) + meetingHud.TimerText.Text);
                    }
                }
            }

            [HarmonyPatch(typeof(GameOptionsData), "GetAdjustedNumImpostors")]
            public static class Imposter_Patch
            {
                [HarmonyPrefix]
                public static bool Prefix(GameOptionsData __instance, int playerCount, ref int __result)
                {
                    int total = ((GameOptionsData.MaxImpostors.Length) <= playerCount ? 3 : GameOptionsData.MaxImpostors[playerCount]);
                    __result = total;
                    return false;
                }
            }

            [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.GetState))]
            public static class GetState_Patch
            {
                public static bool Prefix(PlayerVoteArea __instance, ref byte __result)
                {
                    __result = (byte)((int)(__instance.votedFor + 1 & 255) | (__instance.isDead ? 128 : 0) | (__instance.didVote ? 64 : 0) | (__instance.didReport ? 32 : 0));
                    return false;
                }
            }

            [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Deserialize))]
            public static class Deserialize_Patch
            {
                public static bool Prefix(ref PlayerVoteArea __instance, MessageReader reader)
                {
                    byte b = reader.ReadByte();
                    __instance.votedFor = (sbyte)((b & 255) - 1);
                    __instance.isDead = ((b & 128) > 0);
                    __instance.didVote = ((b & 64) > 0);
                    __instance.didReport = ((b & 32) > 0);
                    __instance.Flag.enabled = (__instance.didVote && !__instance.resultsShowing);
                    __instance.Overlay.gameObject.SetActive(__instance.isDead);
                    __instance.Megaphone.enabled = __instance.didReport;
                    return false;
                }
            }

            [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
            public static class Populate_Patch
            {
                public static bool Prefix(MeetingHud __instance, Il2CppStructArray<byte> states)
                {
                    __instance.TitleText.Text = "Voting Results";
                    int num = 0;
                    for (int i = 0; i < __instance.playerStates.Length; i++)
                    {
                        PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                        playerVoteArea.ClearForResults();
                        int num2 = 0;
                        for (int j = 0; j < __instance.playerStates.Length; j++)
                        {
                            if (!Extensions.HasAnyBit(states[j], (byte)128))
                            {
                                GameData.PlayerInfo playerById = GameData.Instance.GetPlayerById((byte)__instance.playerStates[j].TargetPlayerId);
                                int num3 = (int)((states[j] & 15) - 1);
                                if (num3 == (int)playerVoteArea.TargetPlayerId)
                                {
                                    SpriteRenderer spriteRenderer = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                                    PlayerControl.SetPlayerMaterialColors((int)playerById.ColorId, spriteRenderer);
                                    spriteRenderer.transform.SetParent(playerVoteArea.transform);
                                    spriteRenderer.transform.localPosition = __instance.CounterOrigin + new Vector3(__instance.CounterOffsets.x * (float)num2, 0f, 0f);
                                    spriteRenderer.transform.localScale = Vector3.zero;
                                    __instance.StartCoroutine(Effects.Bloop((float)num2 * 0.5f, spriteRenderer.transform, 0.5f));
                                    num2++;
                                }
                                else if ((i == 0 && num3 == -1) || num3 == 254)
                                {
                                    SpriteRenderer spriteRenderer2 = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                                    PlayerControl.SetPlayerMaterialColors((int)playerById.ColorId, spriteRenderer2);
                                    spriteRenderer2.transform.SetParent(__instance.SkippedVoting.transform);
                                    spriteRenderer2.transform.localPosition = __instance.CounterOrigin + new Vector3(__instance.CounterOffsets.x * (float)num, 0f, 0f);
                                    spriteRenderer2.transform.localScale = Vector3.zero;
                                    __instance.StartCoroutine(Effects.Bloop((float)num * 0.5f, spriteRenderer2.transform, 0.5f));
                                    num++;
                                }
                            }
                        }
                    }
                    return false;
                }
            }

            [HarmonyPatch(typeof(MeetingHud), "Update")]
            public static class Meeting_Patch
            {
                [HarmonyPostfix]
                public static void Postfix(MeetingHud __instance)
                {
                    if (!DestroyableSingleton<HudManager>.Instance.Chat.IsOpen)
                    {
                        bool flag = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.mouseScrollDelta.y > 0f;
                        if (flag)
                        {
                            RemovePlayerLimit.Page = Mathf.Clamp(RemovePlayerLimit.Page - 1, 0, RemovePlayerLimit.MaxPages - 1);
                        }
                        else
                        {
                            bool flag2 = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.mouseScrollDelta.y < 0f;
                            if (flag2)
                            {
                                RemovePlayerLimit.Page = Mathf.Clamp(RemovePlayerLimit.Page + 1, 0, RemovePlayerLimit.MaxPages - 1);
                            }
                        }
                    }
                    RemovePlayerLimit.UpdatePageText(__instance);
                    int num = 0;
                    foreach (PlayerVoteArea playerVoteArea in from x in __instance.playerStates.ToArray<PlayerVoteArea>()
                                                              orderby x.isDead
                                                              select x)
                    {
                        bool flag3 = num >= 10 * RemovePlayerLimit.Page && num < 10 * (RemovePlayerLimit.Page + 1);
                        playerVoteArea.gameObject.SetActive(flag3);
                        bool flag4 = flag3;
                        if (flag4)
                        {
                            int num2 = num - RemovePlayerLimit.Page * 10;
                            int num3 = num2 % 2;
                            int num4 = num2 / 2;
                            playerVoteArea.transform.localPosition = __instance.VoteOrigin + new Vector3(__instance.VoteButtonOffsets.x * (float)num3, __instance.VoteButtonOffsets.y * (float)num4, -1f);
                        }
                        num++;
                    }
                }
            }

            [HarmonyPatch(typeof(NameTextBehaviour), nameof(NameTextBehaviour.ShakeIfInvalid))]
            public static class ShakeIfInvalid_Patch
            {
                [HarmonyPrefix]
                public static bool Prefix(NameTextBehaviour __instance, ref bool __result)
                {
                    var text = __instance.nameSource.text;
                    string[] bannedNames = { "Outwitt", "0utwitt"};

                    __result = false;
                    __result = __result || (text == null || text.Length == 0);
                    __result = __result || text.Equals("Enter Name", StringComparison.OrdinalIgnoreCase);
                    __result = __result || (BlockedWords.ContainsWord(text));
                    __result = __result || (string.IsNullOrWhiteSpace(text));

                    for (int i = 0; i < bannedNames.Length; i++)
                        __result = __result || bannedNames[i].Contains(text);

                    if (__result)
                        __instance.StartCoroutine(Effects.Bounce(__instance.nameSource.transform, 0.75f, 0.25f));

                    return false;
                }
            }

            [HarmonyPatch(typeof(IntroCutscene.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObBoInisLi1PlyoCoUnique), "MoveNext")]
            public static class Cutscene_Patch
            {
                [HarmonyPrefix]
                public static void Prefix(IntroCutscene.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObBoInisLi1PlyoCoUnique __instance)
                {
                    Il2CppArrayBase<PlayerControl> il2CppArrayBase = __instance.yourTeam.ToArray();
                    __instance.yourTeam.Clear();

                    for (int i = 0; i < il2CppArrayBase.Count; i++)
                    {
                        bool flag = i > 12;

                        if (flag)
                        {
                            break;
                        }

                        __instance.yourTeam.Add(il2CppArrayBase[i]);
                    }
                }
            }

            [HarmonyPatch(typeof(PingTracker), "Update")]
            public static class Ping_Patch
            {
                // Token: 0x06000028 RID: 40 RVA: 0x00002A73 File Offset: 0x00000C73
                public static void Postfix(PingTracker __instance)
                {
                    TextRenderer text = __instance.text;
                    text.Text += "[FFFFFFFF]\n<~ [6D29FFFF]Crew[FF6A14FF]Node [FFFFFFFF]~>";
                }
            }
        }
    }
}