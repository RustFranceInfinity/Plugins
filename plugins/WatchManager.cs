using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WatchManager", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("Watch manager, track people to watch and inform admin at connection")]
    class WatchManager : RustPlugin
    {
        static HashSet<WatchInfo> FPlayerDatas;
        static List<WatchInfo> FWatchWaitingForRemoval;

        private const string perm = "watchmanager.admin";
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

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
                    BasePlayer playerFound = GetPlayerActifOnTheServerByIdOrNameIFN(parPlayer);
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

        BasePlayer GetPlayerActifOnTheServerByIdOrNameIFN(string parIdOrName)
        {
            BasePlayer playerFound = null;
            try
            {
                ulong id = ulong.Parse(parIdOrName);
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player.userID == id)
                    {
                        playerFound = player;
                        break;
                    }
                }
                if (playerFound == null)
                {
                    foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                    {
                        if (player.userID == id)
                        {
                            playerFound = player;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.displayName.ToUpper() == parIdOrName)
                    {
                        playerFound = player;
                        break;
                    }
                }
                if (playerFound == null)
                {
                    foreach (var player in BasePlayer.sleepingPlayerList)
                    {
                        if (player.displayName.ToUpper() == parIdOrName)
                        {
                            playerFound = player;
                            break;
                        }
                    }
                }
                if (playerFound == null)
                {
                    List<BasePlayer> playerFounds = new List<BasePlayer>();
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player.displayName.ToUpper().Contains(parIdOrName))
                        {
                            playerFounds.Add(player);
                            break;
                        }
                    }
                    if (playerFounds.Count == 1)
                    {
                        playerFound = playerFounds[0];
                    }
                }
                if (playerFound == null)
                {
                    List<BasePlayer> playerFounds = new List<BasePlayer>();
                    foreach (var player in BasePlayer.sleepingPlayerList)
                    {
                        if (player.displayName.ToUpper() == parIdOrName)
                        {
                            playerFounds.Add(player);
                            break;
                        }
                    }
                    if (playerFounds.Count == 1)
                    {
                        playerFound = playerFounds[0];
                    }
                }
            }

            return playerFound;
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

            BasePlayer player = GetPlayerActifOnTheServerByIdOrNameIFN(parArguments[1].ToUpper());
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
            if (argSize > 3)
            {
                BasePlayer adminPlayer = GetPlayerActifOnTheServerByIdOrNameIFN(parArguments[3].ToUpper());
                if (adminPlayer != null)
                {
                    adminId = adminPlayer.userID;
                    adminName = adminPlayer.displayName;
                }
                else
                {
                    adminId = 0;
                    adminName = parArguments[3];
                }
            }

            // overwrite date
            if (argSize > 4)
            {
                try
                {
                    DateTime localDate = DateTime.Parse(parArguments[4]);
                    date = localDate;
                }
                catch (Exception)
                {
                }
            }

            // create new watch
            int maxId = 0;
            foreach (WatchInfo watch in FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == player.userID))
            {
                if (watch.FWatchId > maxId)
                    maxId = watch.FWatchId;
            }
            WatchInfo watch = new WatchInfo();
            watch.FAdminId = adminId;
            watch.FAdminName = adminName;
            watch.FWatchId = maxId + 1;
            watch.FDate = date;
            watch.FPlayerId = player.userID;
            watch.FPlayerName = player.displayName;
            watch.FPlayerNameUpper = watch.FPlayerName.ToUpper();
            watch.FText = parArguments[2];

            FPlayerDatas.Add(watch);
            PrintToConsole(parAdmin, "Le joueur " + player.displayName + " est maintenant sous surveillance");
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
                        PrintToConsole(parPlayer, "[" + watch.FWatchId + "] Le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + watch.FText + "</color> par " + watch.FAdminName);
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
                    PrintToConsole(parPlayer, "<color=green>" + watch.FPlayerName + "</color> (" + watch.FPlayerId + ") [" + watch.FWatchId + "] le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + watch.FText + "</color> par " + watch.FAdminName);
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
                        PrintToConsole(parPlayer, "[" + watch.FWatchId + "] Le " + watch.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + watch.FText + "</color> par " + watch.FAdminName);
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
                          + "\n<color=green>\tadd <id|name> <motif> [admin] [date]: </color> ajout d'une surveillance concernant un joueur [optionnel: admin et date]"
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
            permission.RegisterPermission(perm, this);
        }
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            LoadDatas();
            FWatchWaitingForRemoval = new List<WatchInfo>();
            foreach (WatchInfo watch in FPlayerDatas)
                watch.FPlayerNameUpper = watch.FPlayerName.ToUpper();
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, perm))
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
            if (!permission.UserHasPermission(player.UserIDString, perm))
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