using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("WatchManager", "Ash @ Rust France Infinity", "1.0.3")]
    [Description("Watch manager, track people to watch and inform admin at connection")]
    class WatchManager : RustPlugin
    {
        // Require plugins
        [PluginReference] private Plugin AshTools;

        static HashSet<WatchInfo> FPlayerDatas;
        static List<WatchInfo> FWatchWaitingForRemoval;

        // Timers
        private Timer FTimer;
        private static ConfigFile FConfigFile;

        // Permissions
        private const string FPermission = "watchmanager.admin";
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #region Config

        private class ConfigFile
        {
            public float RefreshInterval { get; set; }
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    RefreshInterval = 300f,
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            FConfigFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            FConfigFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigFile);

        #endregion

        #region Data

        class WatchInfo
        {
            public ulong FPlayerId;
            public string FPlayerName;
            public string FPlayerNameUpper;

            public ulong FAdminId;
            public string FAdminName;

            public int FWatchId;
            public string FText;
            public DateTime FDate;

            public DateTime FDateStopWatching;
        }
        #endregion

        #region Methods
        void LoadDatas()
        {
            FPlayerDatas = Interface.Oxide.DataFileSystem.ReadObject<HashSet<WatchInfo>>($"WatchManager");

            if (FPlayerDatas == null)
                FPlayerDatas = new HashSet<WatchInfo>();
        }

        void Save()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"WatchManager", FPlayerDatas);
        }

        List<WatchInfo> RetrieveAllWatch(string parPlayer)
        {
            List<WatchInfo> data = FPlayerDatas.ToList().FindAll((p) => p.FPlayerNameUpper == parPlayer);
            if (data.Count == 0)
            {
                try
                {
                    ulong playerId = ulong.Parse(parPlayer);
                    data = FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == playerId);
                }
                catch (Exception)
                {
                    BasePlayer playerFound = (BasePlayer)AshTools?.Call("GetPlayerActifOnTheServerByIdOrNameIFN", parPlayer);
                    if (playerFound != null)
                        data = RetrieveAllWatch(playerFound.UserIDString);
                }
            }
            else
            {
                data = RetrieveAllWatch(data[0].FPlayerId.ToString());
            }

            if (data.Count == 0 && parPlayer.Length >= 2)
            {
                List<ulong> playerIds = new List<ulong>();
                foreach (WatchInfo watch in FPlayerDatas)
                {
                    if (watch.FPlayerNameUpper.Contains(parPlayer) && !playerIds.Contains(watch.FPlayerId))
                        playerIds.Add(watch.FPlayerId);
                }
                foreach (ulong id in playerIds)
                    data.AddRange(FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == id));
            }

            return data;
        }

        // draw usage
        void DisplayUsage(BasePlayer parPlayer)
        {
            PrintToConsole(parPlayer, string.Format(lang.GetMessage("Usage", this, parPlayer.UserIDString)));
        }

        // add <id|name> <motif> [admin] [date]"
        void AddWatch(BasePlayer parAdmin, string[] parArguments)
        {
            int argSize = parArguments.Length;
            if (argSize < 3)
            {
                DisplayUsage(parAdmin);
                return;
            }

            BasePlayer player = (BasePlayer)AshTools?.Call("GetPlayerActifOnTheServerByIdOrNameIFN", parArguments[1].ToUpper());
            if (player == null)
            {
                PrintToConsole(parAdmin, string.Format(lang.GetMessage("DoNotExists", this, parAdmin.UserIDString), parArguments[1]));
                return;
            }

            string motif = parArguments[2];
            ulong adminId = parAdmin.userID;
            string adminName = parAdmin.displayName;
            DateTime date = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);

            // overwrite admin information
            bool hasDelay = false;
            DateTime dateWithDelay = date;
            if (argSize > 3)
            {
                float nbDay;
                if (hasDelay = float.TryParse(parArguments[3], out nbDay))
                    dateWithDelay = dateWithDelay.AddDays(nbDay);
            }

            // create new watch
            int maxId = 0;
            foreach (WatchInfo watch in FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == player.userID))
            {
                if (watch.FWatchId > maxId)
                    maxId = watch.FWatchId;
            }
            WatchInfo watchInfo = new WatchInfo();
            watchInfo.FAdminId = adminId;
            watchInfo.FAdminName = adminName;
            watchInfo.FWatchId = maxId + 1;
            watchInfo.FDate = date;
            watchInfo.FDateStopWatching = dateWithDelay;
            watchInfo.FPlayerId = player.userID;
            watchInfo.FPlayerName = player.displayName;
            watchInfo.FPlayerNameUpper = watchInfo.FPlayerName.ToUpper();
            watchInfo.FText = parArguments[2];

            FPlayerDatas.Add(watchInfo);
            PrintToConsole(parAdmin, "Le joueur " + player.displayName + " est maintenant sous surveillance" + (DateTime.Compare(watchInfo.FDateStopWatching, watchInfo.FDate) > 0 ? " jusqu'au " + watchInfo.FDateStopWatching.ToString("dd/MM/yyyy HH:mm") : ""));

            // envoie un message à tout les staffs actif en jeu
            foreach (BasePlayer user in BasePlayer.activePlayerList)
            {
                if (user != parAdmin && permission.UserHasPermission(user.UserIDString, FPermission))
                    PrintToChat(user, "Le joueur " + player.displayName + " a été mis sous surveillance par " + parAdmin.displayName + (watchInfo.FDateStopWatching > watchInfo.FDate ? " jusqu'au " + watchInfo.FDateStopWatching.ToString("dd/MM/yyyy HH:mm") : ""));
            }
            Save();
        }

        // remove <id|name> [watchId]
        void RemoveWatch(BasePlayer parPlayer, string[] parArguments)
        {
            if (parArguments.Length > 1)
            {
                int removed = 0;
                List<WatchInfo> playerWatch = FWatchWaitingForRemoval;

                if (playerWatch.Count > 0 && parArguments[1] == "confirm")
                {
                    foreach (WatchInfo watch in playerWatch)
                        removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == watch.FPlayerId && p.FWatchId == watch.FWatchId);
                    playerWatch.Clear();
                    FWatchWaitingForRemoval.Clear();
                    Save();
                }
                else
                {
                    FWatchWaitingForRemoval.Clear();
                    playerWatch = RetrieveAllWatch(parArguments[1].ToUpper());
                    if (playerWatch.Count == 0)
                    {
                        PrintToConsole(parPlayer, "Le joueur " + parArguments[1] + " n'est pas sous surveillance");
                    }
                    else if (parArguments.Length > 2)
                    {
                        if (parArguments[2] == "confirm")
                        {
                            foreach (WatchInfo watch in playerWatch)
                                removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == watch.FPlayerId);
                            playerWatch.Clear();
                            Save();
                        }
                        else
                        {
                            try
                            {
                                int numero = int.Parse(parArguments[2]);
                                if (parArguments.Length > 3 && parArguments[3] == "confirm")
                                {
                                    foreach (WatchInfo watch in playerWatch)
                                        removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == watch.FPlayerId && p.FWatchId == numero);
                                    playerWatch.Clear();
                                    Save();
                                }
                                else
                                {
                                    for (int i = playerWatch.Count - 1; i >= 0; --i)
                                        if (playerWatch[i].FWatchId != numero)
                                            playerWatch.RemoveAt(i);
                                }
                            }
                            catch (Exception)
                            {
                                playerWatch.Clear();
                            }
                        }
                    }
                }
                if (removed == 0 && playerWatch.Count > 0)
                {
                    FWatchWaitingForRemoval = playerWatch;
                    PrintToConsole(parPlayer, "Il y a " + FWatchWaitingForRemoval.Count + " éléments en attente de suppression, faire \"watch remove confirm\" pour valider");
                    string currentPlayer = "";
                    foreach (WatchInfo watch in playerWatch.OrderBy((d) => d.FDate))
                    {
                        if (currentPlayer != watch.FPlayerName)
                        {
                            currentPlayer = watch.FPlayerName;
                            PrintToConsole(parPlayer, "<color=green>" + watch.FPlayerName + "</color>(" + watch.FPlayerId + ") est sous surveillance pour <color=red>" + FPlayerDatas.ToList().FindAll((p) => p.FPlayerName == watch.FPlayerName).Count + "</color> problème(s)");
                        }
                        PrintToConsole(parPlayer, "<color=red>" + watch.FWatchId + "</color> Le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=yellow>" + watch.FText + "</color> par " + watch.FAdminName + (watch.FDateStopWatching > watch.FDate ? " jusqu'au " + watch.FDateStopWatching.ToString("dd/MM/yyyy HH:mm") : ""));
                    }
                }
                else if (removed == 0)
                    PrintToConsole(parPlayer, "Aucun éléments n'a été supprimé");
                else
                    PrintToConsole(parPlayer, removed + " éléments ont été supprimés");
            }
            else
            {
                DisplayUsage(parPlayer);
            }
        }

        // list [id|name]
        void ListWatch(BasePlayer parPlayer, string[] parArguments)
        {
            if (parArguments.Length == 1)
            {
                foreach (WatchInfo watch in FPlayerDatas.OrderBy((d) => d.FDate))
                    PrintToConsole(parPlayer, "<color=green>" + watch.FPlayerName + "</color> (" + watch.FPlayerId + ") [<color=red>" + watch.FWatchId + "</color>] le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=yellow>" + watch.FText + "</color> par " + watch.FAdminName + (watch.FDateStopWatching > watch.FDate ? " jusqu'au " + watch.FDateStopWatching.ToString("dd/MM/yyyy HH:mm") : ""));
            }
            else if (parArguments[1] == "size")
            {
                PrintToConsole(parPlayer, "il y a actuellement " + FPlayerDatas.Count + " joueurs sous surveillance");
            }
            else
            {
                List<WatchInfo> playerWatch = RetrieveAllWatch(parArguments[1].ToUpper());
                if (playerWatch.Count == 0)
                {
                    PrintToConsole(parPlayer, "Le joueur " + parArguments[1] + " n'est pas sous surveillance");
                }
                else
                {
                    string currentPlayer = "";
                    foreach (WatchInfo watch in playerWatch.OrderBy((d) => d.FDate))
                    {
                        if (currentPlayer != watch.FPlayerName)
                        {
                            currentPlayer = watch.FPlayerName;
                            PrintToConsole(parPlayer, "<color=green>" + watch.FPlayerName + "</color>(" + watch.FPlayerId + ") est sous surveillance pour <color=red>" + FPlayerDatas.ToList().FindAll((p) => p.FPlayerName == watch.FPlayerName).Count + "</color> problème(s)");
                        }
                        PrintToConsole(parPlayer, "<color=red>" + watch.FWatchId + "</color> Le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=yellow>" + watch.FText + "</color> par " + watch.FAdminName + (watch.FDateStopWatching > watch.FDate ? " jusqu'au " + watch.FDateStopWatching.ToString("dd/MM/yyyy HH:mm") : ""));
                    }
                }
            }
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WatchCommand"] = "watch",
                ["DoNotExists"] = "Player '{0}' do not exists on the server",
                ["Usage"] = "<color=red>watch</color> [list|add|remove]"
                          + "\n<color=green>\tlist [id|name]: </color> la liste de toutes les surveillance [optionnel, un nom ou id de joueur]"
                          + "\n<color=green>\tadd <id|name> <motif> [delay]: </color> ajout d'une surveillance concernant un joueur [optionnel: le nombre de jour de watch]"
                          + "\n<color=green>\tremove <id|name> [watchId]:  </color> supprimer un joueur de la liste  [optionnel: juste une surveillance via son id]"
            }, this);
        }
        #endregion Localization

        #region Initialization
        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("watch", "WatchCommand");

            // Register permissions for commands
            permission.RegisterPermission(FPermission, this);
        }
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            if (!AshTools)
                PrintError("AshTools is not present, this plugins will not works");

            LoadDatas();
            FWatchWaitingForRemoval = new List<WatchInfo>();
            foreach (WatchInfo watch in FPlayerDatas)
                watch.FPlayerNameUpper = watch.FPlayerName.ToUpper();

            FTimer?.Destroy();
            FTimer = timer.Every(FConfigFile.RefreshInterval, () =>
            {
                DateTime currentTime = DateTime.Now;
                List<WatchInfo> dataToRemove = new List<WatchInfo>();
                foreach (WatchInfo data in FPlayerDatas)
                {
                    if (data.FDateStopWatching > data.FDate && currentTime > data.FDateStopWatching)
                        dataToRemove.Add(data);
                }
                foreach (WatchInfo data in dataToRemove)
                    FPlayerDatas.Remove(data);
                if (dataToRemove.Count > 0)
                    Save();
            });
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, FPermission) && AshTools)
            {
                string[] args = { "list" };
                PrintToConsole(player, "Liste des joueurs sous surveillance:");
                ListWatch(player, args);
                PrintToChat(player, "La liste des joueurs sous surveillance à été affichée dans la console");
            }
        }
        #endregion

        #region Commands
        private void WatchCommand(IPlayer parPlayer, string parCommand, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (!permission.UserHasPermission(player.UserIDString, FPermission) || !AshTools)
                return;

            if (parArguments.Length == 0)
            {
                DisplayUsage(player);
            }
            else
            {
                switch (parArguments[0])
                {
                    case "add":
                        AddWatch(player, parArguments);
                        break;
                    case "list":
                        ListWatch(player, parArguments);
                        break;
                    case "remove":
                        RemoveWatch(player, parArguments);
                        break;
                    default:
                        break;
                }
            }
        }

        #endregion
    }
}