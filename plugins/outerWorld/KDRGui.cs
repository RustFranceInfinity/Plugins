using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("KDRGui", "Ankawi/LaserHydra", "2.0.1", ResourceId = 2042)]
    [Description("GUI that portrays kills, deaths, player name, and K/D Ratio")]
    class KDRGui : RustPlugin
    {
        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();

        static HashSet<PlayerData> LoadedPlayerData = new HashSet<PlayerData>();
        List<UIObject> UsedUI = new List<UIObject>();

        static HashSet<ulong> AllPlayerIds = new HashSet<ulong>();

        // Permissions
        private const string FPerm = "kdrgui.admin";

        #region UI Classes

        // UI Classes - Created by LaserHydra
        class UIColor
        {
            double red;
            double green;
            double blue;
            double alpha;

            public UIColor(double red, double green, double blue, double alpha)
            {
                this.red = red;
                this.green = green;
                this.blue = blue;
                this.alpha = alpha;
            }

            public override string ToString()
            {
                return $"{red.ToString()} {green.ToString()} {blue.ToString()} {alpha.ToString()}";
            }
        }

        class UIObject
        {
            List<object> ui = new List<object>();
            List<string> objectList = new List<string>();

            public UIObject()
            {
            }

            public string RandomString()
            {
                string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                List<char> charList = chars.ToList();

                string random = "";

                for (int i = 0; i <= UnityEngine.Random.Range(5, 10); i++)
                    random = random + charList[UnityEngine.Random.Range(0, charList.Count - 1)];

                return random;
            }

            public void Draw(BasePlayer player)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", JsonConvert.SerializeObject(ui).Replace("{NEWLINE}", Environment.NewLine));
            }

            public void Destroy(BasePlayer player)
            {
                foreach (string uiName in objectList)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", uiName);
            }

            public string AddPanel(string name, double left, double top, double width, double height, UIColor color, bool mouse = false, string parent = "Overlay")
            {
                name = name + RandomString();

                string type = "";
                if (mouse) type = "NeedsCursor";

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Image"},
                                {"color", color.ToString()}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            },
                            new Dictionary<string, string> {
                                {"type", type}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddText(string name, double left, double top, double width, double height, UIColor color, string text, int textsize = 15, string parent = "Overlay", int alignmode = 0)
            {
                name = name + RandomString(); text = text.Replace("\n", "{NEWLINE}"); string align = "";

                switch (alignmode)
                {
                    case 0: { align = "LowerCenter"; break; };
                    case 1: { align = "LowerLeft"; break; };
                    case 2: { align = "LowerRight"; break; };
                    case 3: { align = "MiddleCenter"; break; };
                    case 4: { align = "MiddleLeft"; break; };
                    case 5: { align = "MiddleRight"; break; };
                    case 6: { align = "UpperCenter"; break; };
                    case 7: { align = "UpperLeft"; break; };
                    case 8: { align = "UpperRight"; break; };
                }

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Text"},
                                {"text", text},
                                {"fontSize", textsize.ToString()},
                                {"color", color.ToString()},
                                {"align", align}
                            },
                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddButton(string name, double left, double top, double width, double height, UIColor color, string command = "", string parent = "Overlay", string closeUi = "")
            {
                name = name + RandomString();

                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"close", closeUi},
                                {"command", command},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }

            public string AddImage(string name, double left, double top, double width, double height, UIColor color, string url = "http://oxidemod.org/data/avatars/l/53/53411.jpg?1427487325", string parent = "Overlay")
            {
                ui.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"sprite", "assets/content/textures/generic/fulltransparent.tga"},
                                {"url", url},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString().Replace(",", ".")} {((1 - top) - height).ToString().Replace(",", ".")}"},
                                {"anchormax", $"{(left + width).ToString().Replace(",", ".")} {(1 - top).ToString().Replace(",", ".")}"}
                            }
                        }
                    }
                });

                objectList.Add(name);
                return name;
            }
        }
        #endregion

        #region Data
        class PlayerData
        {
            public ulong id;
            public string name;
            public int kills;
            public int deaths;
            internal float KDR => deaths == 0 ? kills : (float)Math.Round(((float)kills) / deaths, 1);

            internal static void TryLoad(BasePlayer player)
            {
                if (Find(player) != null)
                    return;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRGui/{player.userID}");

                if (data == null || data.id == 0)
                {
                    data = new PlayerData
                    {
                        id = player.userID,
                        name = player.displayName
                    };
                }
                else
                    data.Update(player);

                data.Save();
                LoadedPlayerData.Add(data);
            }

            internal static void TryLoad(ulong playerId)
            {
                if (Find(playerId) != null)
                    return;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRGui/{playerId}");

                if (data != null)
                    LoadedPlayerData.Add(data);
            }

            internal void Update(BasePlayer player)
            {
                name = player.displayName;
                Save();
            }

            internal void Save() => Interface.Oxide.DataFileSystem.WriteObject($"KDRGui/{id}", this, true);
            internal static PlayerData Find(BasePlayer player)
            {
                PlayerData data = LoadedPlayerData.ToList().Find((p) => p.id == player.userID);
                return data;
            }
            internal static PlayerData Find(ulong playerId)
            {
                PlayerData data = LoadedPlayerData.ToList().Find((p) => p.id == playerId);
                return data;
            }
        }

        void LoadAllPlayers()
        {
            AllPlayerIds = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>($"KDRGui/allPlayerIds");
            foreach (ulong playerId in AllPlayerIds)
            {
                PrintWarning($"player '{playerId}' from AllPlayerIds");
                PlayerData.TryLoad(playerId);
            }
        }

        void SaveAllPlayers()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"KDRGui/allPlayerIds", AllPlayerIds);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("lsd", "cmdTop");
            AddCovalenceCommand("5689", "cmdKdr");

            // register permission
            permission.RegisterPermission(FPerm, this);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            PrintWarning($"Try to load '{player.userID}/{player.displayName}' from OnPlayerConnected");
            PlayerData.TryLoad(player);
            if (!AllPlayerIds.Contains(player.userID))
            {
                AllPlayerIds.Add(player.userID);
                SaveAllPlayers();
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerData.TryLoad(player);
            if (!AllPlayerIds.Contains(player.userID))
            {
                AllPlayerIds.Add(player.userID);
                SaveAllPlayers();
            }
        }

        void Unloaded()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                foreach (var ui in UsedUI)
                    ui.Destroy(player);
            }
        }

        void OnServerInitialized()
        {
            LoadAllPlayers();
            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintWarning($"Try to load '{player.userID}/{player.displayName}' from the active player list");
                PlayerData.TryLoad(player);
                if (!AllPlayerIds.Contains(player.userID))
                    AllPlayerIds.Add(player.userID);
            }
            SaveAllPlayers();
        }

        HitInfo TryGetLastWounded(ulong id, HitInfo info)
        {
            if (LastWounded.ContainsKey(id))
            {
                HitInfo output = LastWounded[id];
                LastWounded.Remove(id);
                return output;
            }

            return info;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity?.ToPlayer() != null && info?.Initiator?.ToPlayer() != null)
            {
                NextTick(() =>
                {
                    if (entity.ToPlayer().IsWounded())
                        LastWounded[entity.ToPlayer().userID] = info;
                });
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == info.Initiator) return;
                if (entity == null || info.Initiator == null) return;

                if (info?.Initiator?.ToPlayer() == null && (entity?.name?.Contains("autospawn") ?? false))
                    return;
                if (entity.ToPlayer() != null)
                {
                    if (entity.ToPlayer().IsWounded())
                    {
                        info = TryGetLastWounded(entity.ToPlayer().userID, info);
                    }
                }
                if (entity != null && entity is BasePlayer && info?.Initiator != null && info.Initiator is BasePlayer)
                {
                    PlayerData victimData = PlayerData.Find((BasePlayer)entity);
                    PlayerData attackerData = PlayerData.Find((BasePlayer)info.Initiator);

                    victimData.deaths++;
                    attackerData.kills++;

                    victimData.Save();
                    attackerData.Save();

                    if (!AllPlayerIds.Contains(victimData.id))
                        AllPlayerIds.Add(victimData.id);
                    if (!AllPlayerIds.Contains(attackerData.id))
                        AllPlayerIds.Add(attackerData.id);
                    SaveAllPlayers();
                }
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region UI Handling
        void DrawKDRConsole(BasePlayer player, string sizeStr)
        {
            int size = 15;
            try
            {
                size = int.Parse(sizeStr);
            }
            catch (Exception)
            {
            }

            PrintToConsole(player, $"id name kills deaths KDR");
            foreach (PlayerData data in LoadedPlayerData.OrderByDescending((d) => d.kills).Take(size))
                PrintToConsole(player, $"{data.id} {data.name} {data.kills} {data.deaths} {data.KDR}");
        }
        void DrawKDRWindow(BasePlayer player)
        {
            UIObject ui = new UIObject();
            string panel = ui.AddPanel("panel1", 0.0192382892057026, 0.0285714285714286, 0.958248472505092, 0.874285714285714, new UIColor(0, 0, 0, 0.9), true, "Overlay");
            ui.AddText("label8", 0.785876726886291, 0.248366013071895, 0.072051009564293, 0.718954248366013, new UIColor(1, 1, 1, 0.5), GetKDRs(), 20, panel, 7);
            ui.AddText("label7", 0.603528161530287, 0.248366013071895, 0.0763230605738576, 0.718954248366013, new UIColor(255, 0, 0, 0.4), GetDeaths(), 20, panel, 7);
            ui.AddText("label6", 0.369925611052072, 0.248366013071895, 0.0756960680127524, 0.718954248366013, new UIColor(0, 255, 0, 0.3), GetTopKills(), 20, panel, 7);
            ui.AddText("label5", 0.0786397449521785, 0.248366013071895, 0.3756960680127524, 0.718954248366013, new UIColor(1, 1, 1, 0.5), GetNames(), 20, panel, 7);
            string close = ui.AddButton("button1", 0.849096705632306, 0.0326797385620915, 0.124335812964931, 0.0871459694989107, new UIColor(1, 0, 0, 0), "", panel, panel);
            ui.AddText("button1_Text", 0, 0, 1, 1, new UIColor(1, 0, 0, 1), "Fermer", 19, close, 3);
            ui.AddText("label4", 0.590775770456961, 0.163398692810458, 0.0935175345377258, 0.0610021786492375, new UIColor(128, 0, 0, 1), "<color=#850606>Morts</color>", 24, panel, 7);
            ui.AddText("label3", 0.360361317747078, 0.163398692810458, 0.1722635494155154, 0.0610021786492375, new UIColor(166, 24, 40, 1), "<color=#008000>Éliminations</color>", 24, panel, 7);
            ui.AddText("label2", 0.0786397449521785, 0.163398692810458, 0.3767587672688629, 0.0610021786492375, new UIColor(1, 0, 0, 1), "<color=#C0C0C0>Nom du joueur</color>", 24, panel, 7);
            ui.AddText("label1", 0.775876726886291, 0.163398692810458, 0.125398512221041, 0.0610021786492375, new UIColor(1, 0, 0, 1), "<color=#C0C0C0>E/M Ratio</color>", 24, panel, 7);
            ui.AddText("label0", 0.355876726886291, 0.045398692810458, 0.305398512221041, 0.0610021786492375, new UIColor(1, 1, 1, 1), "<color=blue>Rust</color> France <color=red>Infinity™</color> <color=#af8700>Stats</color>", 33, panel, 7);

            ui.Draw(player);
            UsedUI.Add(ui);
        }
        private void LoadSleepers()
        {
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                PrintWarning($"Try to load '{player.userID}/{player.displayName}' from the sleepers list");
                PlayerData.TryLoad(player);
                if (!AllPlayerIds.Contains(player.userID))
                {
                    AllPlayerIds.Add(player.userID);
                    SaveAllPlayers();
                }
            }
        }
        string GetTopKills()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.kills}").Take(15).ToArray());
        }
        string GetDeaths()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.deaths}").Take(15).ToArray());
        }
        string GetKDRs()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.KDR}").Take(15).ToArray());
        }
        string GetNames()
        {
            return string.Join("\n", LoadedPlayerData.OrderByDescending((d) => d.kills).Select((d) => $"{d.name}").Take(15).ToArray());
        }
        #endregion

        #region Commands
        private void cmdTop(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm))
            {
                LoadSleepers();
                if (args.Length >= 1)
                    DrawKDRConsole(player, args[0]);
                else
                    DrawKDRWindow(player);
            }
        }

        private void cmdKdr(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm))
                GetCurrentStats(player);
        }

        void GetCurrentStats(BasePlayer player)
        {
            PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRGui/{player.userID}");
            int kills = data.kills;
            int deaths = data.deaths;
            string playerName = data.name;
            float kdr = data.KDR;

            PrintToChat(player, "<color=#af8700> Nom : </color>" + $"{playerName}"
                        + "\n" + "<color=blue> Eliminations : </color>" + $"{kills}"
                        + "\n" + "<color=white> Morts : </color>" + $"{deaths}"
                        + "\n" + "<color=red> K/D Ratio : </color>" + $"{kdr}");
        }
        #endregion
    }
}