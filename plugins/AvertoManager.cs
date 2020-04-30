// Requires: AshTools

using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Averto", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("Avertissements manager")]
    class AvertoManager : RustPlugin
    {
        // Config, instance, plugin references
        AshTools FTools;

        static HashSet<Averto> FPlayerDatas;
        static List<Averto> FAvertoWaitingForRemoval;

        // Permissions
        private const string perm = "avertomanager.admin";
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #region Data

        class Averto
        {
            public ulong FPlayerId;
            public string FPlayerName;
            public string FPlayerNameUpper;

            public ulong FAdminId;
            public string FAdminName;

            public int FAvertoId;
            public string FText;
            public DateTime FDate;

        }
        #endregion

        #region Methods
        void LoadDatas()
        {
            FPlayerDatas = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Averto>>($"AvertoManager");

            if (FPlayerDatas == null)
                FPlayerDatas = new HashSet<Averto>();
        }

        void Save()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"AvertoManager", FPlayerDatas);
        }

        List<Averto> RetrieveAllAverto(string parPlayer)
        {
            List<Averto> data = FPlayerDatas.ToList().FindAll((p) => p.FPlayerNameUpper == parPlayer);
            if (data.Count == 0)
            {
                try
                {
                    ulong playerId = ulong.Parse(parPlayer);
                    data = FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == playerId);
                }
                catch (Exception)
                {
                    BasePlayer playerFound = (BasePlayer)FTools.Call("GetPlayerActifOnTheServerByIdOrNameIFN", parPlayer);
                    if (playerFound != null)
                        data = RetrieveAllAverto(playerFound.UserIDString);
                }
            }
            else
            {
                data = RetrieveAllAverto(data[0].FPlayerId.ToString());
            }

            if (data.Count == 0 && parPlayer.Length >= 2)
            {
                List<ulong> playerIds = new List<ulong>();
                foreach (Averto averto in FPlayerDatas)
                {
                    if (averto.FPlayerNameUpper.Contains(parPlayer) && !playerIds.Contains(averto.FPlayerId))
                        playerIds.Add(averto.FPlayerId);
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
        void AddAverto(BasePlayer parAdmin, string[] parArguments)
        {
            int argSize = parArguments.Length;
            if (argSize < 3)
            {
                DisplayUsage(parAdmin);
                return;
            }

            BasePlayer player = (BasePlayer)FTools.Call("GetPlayerActifOnTheServerByIdOrNameIFN", parArguments[1].ToUpper());
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
                BasePlayer adminPlayer = (BasePlayer)FTools.Call("GetPlayerActifOnTheServerByIdOrNameIFN", parArguments[3].ToUpper());
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

            // create new averto
            int maxId = 0;
            foreach (Averto locAverto in FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == player.userID))
            {
                if (locAverto.FAvertoId > maxId)
                    maxId = locAverto.FAvertoId;
            }
            Averto averto = new Averto();
            averto.FAdminId = adminId;
            averto.FAdminName = adminName;
            averto.FAvertoId = maxId + 1;
            averto.FDate = date;
            averto.FPlayerId = player.userID;
            averto.FPlayerName = player.displayName;
            averto.FPlayerNameUpper = averto.FPlayerName.ToUpper();
            averto.FText = parArguments[2];

            FPlayerDatas.Add(averto);
            PrintToConsole(parAdmin, "Le joueur " + player.displayName + " a reçu un nouvel avertisssement, c'est le n°" + averto.FAvertoId);
            PrintToChat(player, "Vous avez reçu un nouvel avertisssement, c'est le n°<color=red>" + FPlayerDatas.ToList().FindAll((p) => p.FPlayerId == player.userID).Count + "</color>");
            Save();
        }

        // remove <id|name> [avertoId]
        void RemoveAverto(BasePlayer parPlayer, string[] parArguments)
        {
            if (parArguments.Length > 1)
            {
                int removed = 0;
                List<Averto> playerAverto = FAvertoWaitingForRemoval;

                if (playerAverto.Count > 0 && parArguments[1] == "confirm")
                {
                    foreach (Averto averto in playerAverto)
                        removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == averto.FPlayerId && p.FAvertoId == averto.FAvertoId);
                    playerAverto.Clear();
                    FAvertoWaitingForRemoval.Clear();
                    Save();
                }
                else
                {
                    FAvertoWaitingForRemoval.Clear();
                    playerAverto = RetrieveAllAverto(parArguments[1].ToUpper());
                    if (playerAverto.Count == 0)
                    {
                        PrintToConsole(parPlayer, "Le joueur " + parArguments[1] + " n'a pas reçu d'avertisssement");
                    }
                    else if (parArguments.Length > 2)
                    {
                        if (parArguments[2] == "confirm")
                        {
                            foreach (Averto averto in playerAverto)
                                removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == averto.FPlayerId);
                            playerAverto.Clear();
                            Save();
                        }
                        else
                        {
                            try
                            {
                                int numero = int.Parse(parArguments[2]);
                                if (parArguments.Length > 3 && parArguments[3] == "confirm")
                                {
                                    foreach (Averto averto in playerAverto)
                                        removed += FPlayerDatas.RemoveWhere((p) => p.FPlayerId == averto.FPlayerId && p.FAvertoId == numero);
                                    playerAverto.Clear();
                                    Save();
                                }
                                else
                                {
                                    for (int i = playerAverto.Count - 1; i >= 0; --i)
                                        if (playerAverto[i].FAvertoId != numero)
                                            playerAverto.RemoveAt(i);
                                }
                            }
                            catch (Exception)
                            {
                                playerAverto.Clear();
                            }
                        }
                    }
                }
                if (removed == 0 && playerAverto.Count > 0)
                {
                    FAvertoWaitingForRemoval = playerAverto;
                    PrintToConsole(parPlayer, "Il y a " + FAvertoWaitingForRemoval.Count + " éléments en attente de suppression, faire \"averto remove confirm\" pour valider");
                    string currentPlayer = "";
                    foreach (Averto averto in playerAverto.OrderBy((d) => d.FDate))
                    {
                        if (currentPlayer != averto.FPlayerName)
                        {
                            currentPlayer = averto.FPlayerName;
                            PrintToConsole(parPlayer, "<color=green>" + averto.FPlayerName + "</color>(" + averto.FPlayerId + ") a reçu <color=red>" + FPlayerDatas.ToList().FindAll((p) => p.FPlayerName == averto.FPlayerName).Count + "</color> avertisssement(s)");
                        }
                        PrintToConsole(parPlayer, "[" + averto.FAvertoId + "] Le " + averto.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + averto.FText + "</color> par " + averto.FAdminName);
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
        void ListAverto(BasePlayer parPlayer, string[] parArguments)
        {
            if (parArguments.Length == 1)
            {
                foreach (Averto averto in FPlayerDatas.OrderBy((d) => d.FDate))
                    PrintToConsole(parPlayer, "<color=green>" + averto.FPlayerName + "</color> (" + averto.FPlayerId + ") [" + averto.FAvertoId + "] le " + averto.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + averto.FText + "</color> par " + averto.FAdminName);
            }
            else if (parArguments[1] == "size")
            {
                PrintToConsole(parPlayer, "il y a actuellement " + FPlayerDatas.Count + " avertissments actifs");
            }
            else
            {
                List<Averto> playerAverto = RetrieveAllAverto(parArguments[1].ToUpper());
                if (playerAverto.Count == 0)
                {
                    PrintToConsole(parPlayer, "Le joueur " + parArguments[1] + " n'a pas reçu d'avertisssement");
                }
                else
                {
                    string currentPlayer = "";
                    foreach (Averto averto in playerAverto.OrderBy((d) => d.FDate))
                    {
                        if (currentPlayer != averto.FPlayerName)
                        {
                            currentPlayer = averto.FPlayerName;
                            PrintToConsole(parPlayer, "<color=green>" + averto.FPlayerName + "</color>(" + averto.FPlayerId + ") a reçu <color=red>" + FPlayerDatas.ToList().FindAll((p) => p.FPlayerName == averto.FPlayerName).Count + "</color> avertisssement(s)");
                        }
                        PrintToConsole(parPlayer, "[" + averto.FAvertoId + "] Le " + averto.FDate.ToString("dd/MM/yyyy HH:mm") + " -> <color=green>" + averto.FText + "</color> par " + averto.FAdminName);
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
                ["AvertoCommand"] = "averto",
                ["DoNotExists"] = "Player '{0}' do not exists on the server",
                ["Usage"] = "<color=red>averto</color> [list|add|remove]"
                          + "\n<color=green>\tlist [id|name]: </color> la liste de tous les avertissements [optionnel, un nom ou id de joueur]"
                          + "\n<color=green>\tadd <id|name> <motif> [admin] [date]: </color> ajout d'un avertissement concernant un joueur [optionnel: admin et date de l'avertissement]"
                          + "\n<color=green>\tremove <id|name> [avertoId]:  </color> supprimer un joueur de la liste  [optionnel: juste un avertissement via son id]"
            }, this);
        }
        #endregion Localization

        #region Initialization
        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("averto", "AvertoCommand");

            // Register permissions for commands
            permission.RegisterPermission(perm, this);
        }
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            FTools = (AshTools)Manager.GetPlugin("AshTools");

            LoadDatas();
            FAvertoWaitingForRemoval = new List<Averto>();
            foreach (Averto averto in FPlayerDatas)
                averto.FPlayerNameUpper = averto.FPlayerName.ToUpper();
        }
        #endregion

        #region Commands
        private void AvertoCommand(IPlayer parPlayer, string parCommand, string[] parArguments)
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
                        AddAverto(player, parArguments);
                        break;
                    case "list":
                        ListAverto(player, parArguments);
                        break;
                    case "remove":
                        RemoveAverto(player, parArguments);
                        break;
                    default:
                        break;
                }
            }
        }

        #endregion
    }
}