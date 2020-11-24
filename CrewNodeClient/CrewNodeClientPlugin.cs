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

            defaultRegions.Insert(0, new KMDGIDEDGGM( // KMDGIDEDGGM = RegionInfo
                Name.Value, ip, new[]
                {
                    new EEKGADNPDBH($"{Name.Value}-Master-1", ip, port) // EEKGADNPDBH = ServerInfo
                })
            );

            ServerManager.DefaultRegions = defaultRegions.ToArray();

            // HarmonyFileLog.Enabled = true;
            // FileLog.logPath = @"C:\Users\Temporary\Desktop\log\oldClient_HarmonyLogger-" + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString() + ".txt"; ;
            Log.LogDebug("Patching..");
            Debug.Log("Credits: js6pak for client patches");
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

            [HarmonyPatch(typeof(OPIJAMILNFD), nameof(OPIJAMILNFD.LNADDBHDNDE))] // OPIJAMILNFD = GameOptionsData, GetAdjustedNumImpostors = LNADDBHDNDE
            public static class Imposter_Patch
            {
                [HarmonyPrefix]
                public static bool Prefix(OPIJAMILNFD __instance, int HNINNEIPHDK, ref int __result) // OPIJAMILNFD = GameOptionsData, HNINNEIPHDK = playerCount
                {
                    int total = ((OPIJAMILNFD.DECPEFPMMMF.Length) <= HNINNEIPHDK ? 3 : OPIJAMILNFD.DECPEFPMMMF[HNINNEIPHDK]); // GameOptionsData = OPIJAMILNFD, MaxImpostors = DECPEFPMMMF, playerCount = HNINNEIPHDK, GameOptionsData = OPIJAMILNFD, MaxImpostors = DECPEFPMMMF, playerCount = HNINNEIPHDK

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

            /*
            [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Deserialize))]
            public static class Deserialize_Patch
            {
                public static bool Prefix(ref PlayerVoteArea __instance, MessageReader IGFFAFNNIAB)
                {
                    byte b = IGFFAFNNIAB.ReadByte();
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
            */

            [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.MABLHGPLBKK))]
            public static class Populate_Patch
            {
                public static bool Prefix(MeetingHud __instance, Il2CppStructArray<byte> HJBJHGNOOFL)
                {
                    __instance.TitleText.Text = "Voting Results";
                    int num = 0;
                    for (int i = 0; i < __instance.FALDLDJHDDJ.Length; i++)
                    {
                        PlayerVoteArea playerVoteArea = __instance.FALDLDJHDDJ[i];
                        playerVoteArea.ClearForResults();
                        int num2 = 0;
                        for (int j = 0; j < __instance.FALDLDJHDDJ.Length; j++)
                        {
                            if (!BFCIOGPHFFI.MJOFLHBPLHE(HJBJHGNOOFL[j], (byte)128))
                            {
                                GameData.IHEKEPMDGIJ playerById = GameData.Instance.GetPlayerById((byte)__instance.FALDLDJHDDJ[j].TargetPlayerId);
                                int num3 = (int)((HJBJHGNOOFL[j] & 15) - 1);
                                if (num3 == (int)playerVoteArea.TargetPlayerId)
                                {
                                    SpriteRenderer spriteRenderer = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                                    PlayerControl.SetPlayerMaterialColors((int)playerById.LHKAPPDILFP, spriteRenderer);
                                    spriteRenderer.transform.SetParent(playerVoteArea.transform);
                                    spriteRenderer.transform.localPosition = __instance.BEDJEPCINAI + new Vector3(__instance.LNFHFGONEGA.x * (float)num2, 0f, 0f);
                                    spriteRenderer.transform.localScale = Vector3.zero;
                                    __instance.StartCoroutine(FBBJKJLHFKF.KGIPENFLALI((float)num2 * 0.5f, spriteRenderer.transform, 0.5f));
                                    num2++;
                                }
                                else if ((i == 0 && num3 == -1) || num3 == 254)
                                {
                                    SpriteRenderer spriteRenderer2 = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                                    PlayerControl.SetPlayerMaterialColors((int)playerById.LHKAPPDILFP, spriteRenderer2);
                                    spriteRenderer2.transform.SetParent(__instance.SkippedVoting.transform);
                                    spriteRenderer2.transform.localPosition = __instance.BEDJEPCINAI + new Vector3(__instance.LNFHFGONEGA.x * (float)num, 0f, 0f);
                                    spriteRenderer2.transform.localScale = Vector3.zero;
                                    __instance.StartCoroutine(FBBJKJLHFKF.KGIPENFLALI((float)num * 0.5f, spriteRenderer2.transform, 0.5f));
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
                    if (!DestroyableSingleton<HudManager>.GHJCLNEIJHD.Chat.FGKJAIIJJBC)
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
                    foreach (PlayerVoteArea playerVoteArea in from x in __instance.FALDLDJHDDJ.ToArray<PlayerVoteArea>()
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

            [HarmonyPatch(typeof(IntroCutscene.KKAPKNMJNFA), "MoveNext")]
            public static class Cutscene_Patch
            {
                [HarmonyPrefix]
                public static void Prefix(IntroCutscene.KKAPKNMJNFA __instance)
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