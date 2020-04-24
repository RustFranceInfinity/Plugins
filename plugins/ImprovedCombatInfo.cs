using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ImprovedCombatInfo", "Ash @ rust France Infinity", "1.0.0")]
    [Description("Log hit, death, location and more")]
    class ImprovedCombatInfo : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        [PluginReference] private Plugin CoordinateToSquare;

        // Permissions
        private const string FPerm = "improvedcombatinfo.admin";

        // itself
        private static ImprovedCombatInfo FInstance;

        #endregion

        // Dictionary<ulong, List<ImprovedHitInfo>> FImprovedHitInfoPerPlayer = new Dictionary<ulong, List<ImprovedHitInfo>>();
        static HashSet<PlayerData> FLoadedPlayerData = new HashSet<PlayerData>();

        #region Data
        class ImprovedHitInfo
        {
            public DateTime FDate { get; set; }
            public ulong FIdFrom { get; set; }
            public string FNameFrom { get; set; }
            public Vector3 FPositionFrom { get; set; }

            public ulong FIdTo { get; set; }
            public string FNameTo { get; set; }
            public Vector3 FPositionTo { get; set; }

            public string FWeaponName { get; set; }

            public float FDistance { get; set; }

        }

        class ImprovedKillInfo
        {
            public List<DateTime> FDates { get; set; }
            public ulong FIdFrom { get; set; }
            public string FNameFrom { get; set; }

            public ulong FIdTo { get; set; }
            public string FNameTo { get; set; }
        }

        class PlayerData
        {
            public ulong FId { get; set; }
            public List<ImprovedHitInfo> FImprovedHitInfos { get; set; }
            public List<ImprovedKillInfo> FImprovedKillInfos { get; set; }
            public List<ImprovedKillInfo> FImprovedKillByInfos { get; set; }

            internal static void TryLoad(BasePlayer player)
            {
                if (Find(player) != null)
                    return;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"ICL/{player.userID}");

                if (data == null || data.FId == 0)
                {
                    data = new PlayerData
                    {
                        FId = player.userID,
                        FImprovedHitInfos = new List<ImprovedHitInfo>(),
                        FImprovedKillInfos = new List<ImprovedKillInfo>(),
                        FImprovedKillByInfos = new List<ImprovedKillInfo>()
                    };
                }

                FInstance.PrintWarning($"Save '{data.FId}' from TryLoad");
                data.Save();
                FLoadedPlayerData.Add(data);
            }

            internal void Save() => Interface.Oxide.DataFileSystem.WriteObject($"ICL/{FId}", this, true);
            internal static PlayerData Find(BasePlayer player)
            {
                return FLoadedPlayerData.ToList().Find((p) => p.FId == player.userID);
            }

            internal static PlayerData Find(string playerIdOrName)
            {
                BasePlayer user = GetPlayerActifOnTheServerByIdOrNameIFN(playerIdOrName);
                if (user != null)
                    return PlayerData.Find(user);
                return null;
            }
        }
        #endregion

        #region Method
        static BasePlayer GetPlayerActifOnTheServerByIdOrNameIFN(string parIdOrName)
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
                string uppedName = parIdOrName.ToUpper();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.displayName.ToUpper() == uppedName)
                    {
                        playerFound = player;
                        break;
                    }
                }
                if (playerFound == null)
                {
                    foreach (var player in BasePlayer.sleepingPlayerList)
                    {
                        if (player.displayName.ToUpper() == uppedName)
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
                        if (player.displayName.ToUpper().Contains(uppedName))
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
                        if (player.displayName.ToUpper() == uppedName)
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
        void RecordHitInfoIFN(BaseCombatEntity parEntity, HitInfo parHitInfo, bool parIsKill = false)
        {
            if (parEntity == null || parHitInfo == null || parHitInfo.Initiator == null)
                return;

            // forget the self inflicted damages
            if (parEntity == parHitInfo.Initiator) return;

            // track arrow and bullet
            if (parEntity.lastDamage != DamageType.Bullet && parEntity.lastDamage != DamageType.Arrow)
                return;

            // take into account only player or bots
            BaseEntity attackerEntity = parEntity.lastAttacker ?? parHitInfo?.Initiator;
            if (!(attackerEntity is BasePlayer) || !(parEntity is BasePlayer))
                return;

            BasePlayer victim = parEntity.ToPlayer();
            BasePlayer attacker = attackerEntity.ToPlayer();

            if (/*BasePlayer.FindBot(victim.userID) || */!BasePlayer.activePlayerList.Contains(attacker))
                return;

            RecordHitInfo(attacker, victim, parHitInfo, parIsKill);
        }

        void RecordHitInfo(BasePlayer parAttacker, BasePlayer parVictim, HitInfo parHitInfo, bool parIsKill)
        {
            ImprovedHitInfo improvedHitInfo = new ImprovedHitInfo();
            improvedHitInfo.FDate = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);
            improvedHitInfo.FIdFrom = parAttacker.userID;
            improvedHitInfo.FNameFrom = parAttacker.displayName;
            improvedHitInfo.FPositionFrom = parAttacker.ServerPosition;
            improvedHitInfo.FIdTo = parVictim.userID;
            improvedHitInfo.FNameTo = parVictim.displayName;
            improvedHitInfo.FPositionTo = parVictim.ServerPosition;
            improvedHitInfo.FWeaponName = parHitInfo?.WeaponPrefab?.ShortPrefabName ?? "NULL";
            improvedHitInfo.FDistance = parHitInfo?.ProjectileDistance ?? 0f;

            PlayerData attackerPlayerData = PlayerData.Find(parAttacker);
            if (attackerPlayerData != null)
            {
                attackerPlayerData.FImprovedHitInfos.Add(improvedHitInfo);
                if (parIsKill)
                {
                    ImprovedKillInfo iki = attackerPlayerData.FImprovedKillInfos.Find((p) => p.FIdTo == parVictim.userID);
                    if (iki == null)
                    {
                        iki = new ImprovedKillInfo();
                        iki.FDates = new List<System.DateTime>();
                        iki.FIdFrom = parAttacker.userID;
                        iki.FNameFrom = parAttacker.displayName;
                        iki.FIdTo = parVictim.userID;
                        iki.FNameTo = parVictim.displayName;

                        attackerPlayerData.FImprovedKillInfos.Add(iki);
                    }
                    iki.FDates.Add(improvedHitInfo.FDate);
                    iki.FNameFrom = parAttacker.displayName;
                    iki.FNameTo = parVictim.displayName;
                }
                PrintWarning($"Save '{attackerPlayerData.FId}' from RecordHitInfo::Attacker");
                attackerPlayerData.Save();
            }

            PlayerData victimPlayerData = PlayerData.Find(parVictim);
            if (victimPlayerData != null)
            {
                victimPlayerData.FImprovedHitInfos.Add(improvedHitInfo);
                if (parIsKill)
                {
                    ImprovedKillInfo iki = victimPlayerData.FImprovedKillByInfos.Find((p) => p.FIdFrom == parAttacker.userID);
                    if (iki == null)
                    {
                        iki = new ImprovedKillInfo();
                        iki.FDates = new List<System.DateTime>();
                        iki.FIdFrom = parAttacker.userID;
                        iki.FNameFrom = parAttacker.displayName;
                        iki.FIdTo = parVictim.userID;
                        iki.FNameTo = parVictim.displayName;

                        victimPlayerData.FImprovedKillByInfos.Add(iki);
                    }
                    iki.FDates.Add(improvedHitInfo.FDate);
                    iki.FNameFrom = parAttacker.displayName;
                    iki.FNameTo = parVictim.displayName;
                }
                PrintWarning($"Save '{victimPlayerData.FId}' from RecordHitInfo::Victim");
                victimPlayerData.Save();
            }
        }

        #endregion

        #region Hooks
        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("scl", "cmdShowHitInfo");
            AddCovalenceCommand("icl", "cmdListHitInfo");
            AddCovalenceCommand("ikl", "cmdListKill");
            AddCovalenceCommand("ikb", "cmdListKillBy");

            // register permission
            permission.RegisterPermission(FPerm, this);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            FInstance.PrintWarning($"OnPlayerConnected '{player.userID}'");
            PlayerData.TryLoad(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            FInstance.PrintWarning($"OnPlayerDisconnected '{player.userID}'");
            PlayerData.TryLoad(player);
        }

        void Unloaded()
        {
        }

        void OnServerInitialized()
        {
            FInstance = this;

            if (!CoordinateToSquare)
                PrintWarning("ImproveCombatLog won't be working at its best because its translator ('CoordinateToSquare') is not present");

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                FInstance.PrintWarning($"OnServerInitialized '{player.userID}'");
                PlayerData.TryLoad(player);
            }
        }
        void OnEntityTakeDamage(BaseCombatEntity parEntity, HitInfo parHitInfo)
        {
            RecordHitInfoIFN(parEntity, parHitInfo);
        }

        void OnEntityDeath(BaseCombatEntity parEntity, HitInfo parHitInfo)
        {
            RecordHitInfoIFN(parEntity, parHitInfo, true);
        }
        #endregion

        #region Commands
        private void cmdListHitInfo(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
                {
                    PrintToConsole(player, "\nDate idAttacker nameAttacker positionAttacker weapon distance idVictim nameVictim positionVictim");
                    foreach (ImprovedHitInfo ihi in playerData.FImprovedHitInfos.OrderByDescending((d) => d.FDate).Take((args.Length >= 2 ? int.Parse(args[1]) : 15)).Reverse())
                        PrintToConsole(player, $"{ihi.FDate.ToString("dd/MM/yyyy HH:mm:ss.fff")} {ihi.FIdFrom} {ihi.FNameFrom} {ihi.FPositionFrom.ToString().Replace(",", "")} {ihi.FWeaponName} {ihi.FDistance.ToString("F1")}m {ihi.FIdTo} {ihi.FNameTo} {ihi.FPositionTo.ToString().Replace(",", "")} ");
                }
            }
        }

        private void cmdShowHitInfo(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
                {
                    uint index = 0;
                    float duration = 60f;
                    DateTime dateFrom = new DateTime(), dateTo = new DateTime();
                    bool checkDateFrom = false, checkDateTo = false;
                    string dateTimeFormat = "dd/MM/yyyy HH:mm:ss";
                    if (args.Length >= 2 && DateTime.TryParseExact(args[1], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateFrom))
                        checkDateFrom = true;
                    if (args.Length >= 3 && DateTime.TryParseExact(args[2], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTo))
                        checkDateTo = true;

                    Vector3 currentPosition = player.ServerPosition;
                    PrintToConsole(player, "\nDate idAttacker nameAttacker positionAttacker weapon distance idVictim nameVictim positionVictim");
                    foreach (ImprovedHitInfo ihi in playerData.FImprovedHitInfos.OrderBy((d) => d.FDate))
                    {
                        if (Vector3.Distance(currentPosition, ihi.FPositionFrom) < 150f && (!checkDateFrom || DateTime.Compare(ihi.FDate, dateFrom) > 0) && (!checkDateTo || DateTime.Compare(ihi.FDate, dateTo) < 0))
                        {
                            PrintToConsole(player, $"{ihi.FDate.ToString("dd/MM/yyyy HH:mm:ss.fff")} {ihi.FIdFrom} {ihi.FNameFrom} {ihi.FPositionFrom.ToString().Replace(",", "")} {ihi.FWeaponName} {ihi.FDistance.ToString("F1")}m {ihi.FIdTo} {ihi.FNameTo} {ihi.FPositionTo.ToString().Replace(",", "")} ");

                            Color color = ihi.FIdFrom == playerData.FId ? Color.green : Color.red;
                            Color invertColor = ihi.FIdFrom == playerData.FId ? Color.red : Color.green;
                            player.SendConsoleCommand("ddraw.arrow", duration, color, ihi.FPositionTo, ihi.FPositionFrom, 0.25);
                            player.SendConsoleCommand("ddraw.text", duration, color, ihi.FPositionFrom, ihi.FNameFrom);
                            player.SendConsoleCommand("ddraw.text", duration, invertColor, ihi.FPositionTo, ihi.FNameTo);
                            player.SendConsoleCommand("ddraw.text", duration, color, (ihi.FPositionFrom + ihi.FPositionTo) / 2, ++index);
                        }
                    }
                }
            }
        }

        private void cmdListKill(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1)
            {
                BasePlayer user = GetPlayerActifOnTheServerByIdOrNameIFN(args[0]);
                PlayerData playerData = user != null ? PlayerData.Find(user) : null;
                if (user != null && playerData != null)
                {
                    if (playerData.FImprovedKillInfos.Count == 0)
                        PrintToConsole(player, $"le joueur '{playerData.FId}' n'a tué personne");
                    else
                    {
                        PrintToConsole(player, $"le joueur '{playerData.FImprovedKillInfos[0].FNameFrom}' ({playerData.FId}) a tué");
                        foreach (ImprovedKillInfo iki in playerData.FImprovedKillInfos.OrderBy((p) => p.FIdTo))
                        {
                            PrintToConsole(player, $"{iki.FIdTo} ('{iki.FNameTo}') {iki.FDates.Count} fois");
                            foreach (DateTime date in iki.FDates)
                                PrintToConsole(player, $"--> {date.ToString("dd/MM/yyyy HH:mm:ss.fff")}");
                        }
                    }
                }
            }
        }

        private void cmdListKillBy(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1)
            {
                BasePlayer user = GetPlayerActifOnTheServerByIdOrNameIFN(args[0]);
                PlayerData playerData = user != null ? PlayerData.Find(user) : null;
                if (user != null && playerData != null)
                {
                    if (playerData.FImprovedKillByInfos.Count == 0)
                        PrintToConsole(player, $"le joueur '{playerData.FId}' n'a jamais été tué");
                    else
                    {
                        PrintToConsole(player, $"le joueur '{playerData.FImprovedKillInfos[0].FNameTo}' ({playerData.FId}) a été tué par");
                        foreach (ImprovedKillInfo iki in playerData.FImprovedKillByInfos.OrderBy((p) => p.FIdFrom))
                        {
                            PrintToConsole(player, $"{iki.FIdFrom} {iki.FNameFrom} {iki.FDates.Count} fois");
                            foreach (DateTime date in iki.FDates)
                                PrintToConsole(player, $"--> {date.ToString("dd/MM/yyyy HH:mm:ss.fff")}");
                        }
                    }
                }
            }
        }

        #endregion
    }
}