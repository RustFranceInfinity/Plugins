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
    [Info("ImprovedCombatInfo", "Ash @ rust France Infinity", "1.0.3")]
    [Description("Log hit, death, location and more")]
    class ImprovedCombatInfo : RustPlugin
    {
        // Config, instance, plugin references
        [PluginReference] private Plugin AshTools;

        #region Configuration
        private static ConfigurationFile FConfigFile;

        private class ConfigurationFile
        {
            public float DisplayDuration { get; set; }
            public float DelayBetweenSave { get; set; }
            public float DistanceShowSingle { get; set; }
            public float DistanceShowAll { get; set; }
            public bool FilterHazardousDamage { get; set; }

            public static ConfigurationFile DefaultConfig()
            {
                return new ConfigurationFile
                {
                    DisplayDuration = 60f,
                    DelayBetweenSave = 300f,
                    DistanceShowSingle = 150f,
                    DistanceShowAll = 50f,
                    FilterHazardousDamage = true
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            FConfigFile = ConfigurationFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            PrintWarning($"Configuration file {Name}.json is loading");
            base.LoadConfig();
            try
            {
                FConfigFile = Config.ReadObject<ConfigurationFile>();
                if (FConfigFile == null)
                {
                    PrintWarning($"Configuration file {Name}.json is NULL; using defaults");
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigFile);

        #endregion

        #region Declaration

        // Permissions
        private const string FPerm = "improvedcombatinfo.admin";

        // itself
        private static ImprovedCombatInfo FInstance;

        // player's data
        static HashSet<PlayerData> FLoadedPlayerData = new HashSet<PlayerData>();

        // players already seen on server
        static HashSet<ulong> FAllPlayerIds = new HashSet<ulong>();
        bool FAllPlayerModified;

        // instigator of the last hit leading to a wounded state
        Dictionary<ulong, HitInfo> FLastInfoOnWoundedPlayer = new Dictionary<ulong, HitInfo>();

        #endregion

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
            public DamageType FDamageType { get; set; }
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

            internal bool FDataModified;

            internal static bool TryLoad(BasePlayer parPlayer)
            {
                if (Find(parPlayer) != null)
                    return false;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"ICL/{parPlayer.userID}");

                if (data == null || data.FId == 0)
                {
                    data = new PlayerData
                    {
                        FId = parPlayer.userID,
                        FImprovedHitInfos = new List<ImprovedHitInfo>(),
                        FImprovedKillInfos = new List<ImprovedKillInfo>(),
                        FImprovedKillByInfos = new List<ImprovedKillInfo>()
                    };
                }

                data.Save();
                FLoadedPlayerData.Add(data);
                return true;
            }
            internal static bool TryLoad(ulong parPlayerId)
            {
                if (Find(parPlayerId) != null)
                    return false;

                PlayerData data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>($"KDRGui/{parPlayerId}");

                if (data != null)
                    FLoadedPlayerData.Add(data);
                return true;
            }

            internal void Save()
            {
                FDataModified = true;
            }

            internal void ForceSave()
            {
                if (FDataModified)
                {
                    Interface.Oxide.DataFileSystem.WriteObject($"ICL/{FId}", this, true);
                    FDataModified = false;
                }
            }
            internal static PlayerData Find(BasePlayer parPlayer)
            {
                return FLoadedPlayerData.ToList().Find((p) => p.FId == parPlayer.userID);
            }
            internal static PlayerData Find(ulong parPlayerId)
            {
                return FLoadedPlayerData.ToList().Find((p) => p.FId == parPlayerId);
            }

            internal static PlayerData Find(string playerIdOrName)
            {
                BasePlayer user = (BasePlayer)FInstance.AshTools?.Call("GetPlayerActifOnTheServerByIdOrNameIFN", playerIdOrName);
                if (user != null)
                {
                    TryLoad(user.userID);
                    return PlayerData.Find(user);
                }
                return null;
            }
        }

        void LoadAllPlayers()
        {
            FAllPlayerIds = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>($"ICL/allPlayerIds");
        }
        void AllPlayersDataHasChanged()
        {
            FAllPlayerModified = true;
        }
        void SaveAllPlayersIFN()
        {
            if (FAllPlayerModified)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"ICL/allPlayerIds", FAllPlayerIds);
                FAllPlayerModified = false;
            }
        }
        #endregion

        #region Method
        void RecordHitInfoIFN(BaseCombatEntity parEntity, HitInfo parHitInfo, bool parIsKill = false)
        {
            if (parEntity == null || parHitInfo == null || parHitInfo.Initiator == null)
                return;

            // forget the self inflicted damages
            if (parEntity == parHitInfo.Initiator)
                return;

            // take into account only player or bots
            BaseEntity attackerEntity = parEntity.lastAttacker ?? parHitInfo?.Initiator;
            if (!(attackerEntity is BasePlayer) || !(parEntity is BasePlayer))
                return;

            BasePlayer victim = parEntity.ToPlayer();
            BasePlayer attacker = attackerEntity.ToPlayer();

            // fiter on bot
            if (BasePlayer.FindBot(victim.userID) || BasePlayer.FindBot(attacker.userID))
                return;

            // filter on self kill
            if (victim.userID == attacker.userID)
                return;

            // filter on weapon name invalid
            if (parHitInfo?.WeaponPrefab?.ShortPrefabName == null)
                return;

            // filter on hazardous damage
            if (FConfigFile.FilterHazardousDamage &&
                (victim.lastDamage == DamageType.Cold || victim.lastDamage == DamageType.ColdExposure || victim.lastDamage == DamageType.Drowned || victim.lastDamage == DamageType.ElectricShock
                || victim.lastDamage == DamageType.Fall || victim.lastDamage == DamageType.Heat || victim.lastDamage == DamageType.Hunger || victim.lastDamage == DamageType.Poison
                || victim.lastDamage == DamageType.Radiation || victim.lastDamage == DamageType.RadiationExposure || victim.lastDamage == DamageType.Thirst || victim.lastDamage == DamageType.Suicide))
                return;

            RecordHitInfo(attacker, victim, parHitInfo, parIsKill);
        }

        void RecordHitInfo(BasePlayer parAttacker, BasePlayer parVictim, HitInfo parHitInfo, bool parIsKill)
        {
            string weaponName = parHitInfo?.WeaponPrefab?.ShortPrefabName;
            if (weaponName == null)
                return;

            ImprovedHitInfo improvedHitInfo = new ImprovedHitInfo();
            improvedHitInfo.FDate = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local);
            improvedHitInfo.FIdFrom = parAttacker.userID;
            improvedHitInfo.FNameFrom = parAttacker.displayName;
            improvedHitInfo.FPositionFrom = parAttacker.ServerPosition;
            improvedHitInfo.FIdTo = parVictim.userID;
            improvedHitInfo.FNameTo = parVictim.displayName;
            improvedHitInfo.FPositionTo = parVictim.ServerPosition;
            improvedHitInfo.FWeaponName = parHitInfo.WeaponPrefab.ShortPrefabName;
            improvedHitInfo.FDistance = parHitInfo?.ProjectileDistance ?? 0f;
            improvedHitInfo.FDamageType = parVictim.lastDamage;

            PlayerData attackerPlayerData = PlayerData.Find(parAttacker);
            if (attackerPlayerData != null)
            {
                attackerPlayerData.FImprovedHitInfos.Add(improvedHitInfo);
                if (!FAllPlayerIds.Contains(attackerPlayerData.FId))
                {
                    FAllPlayerIds.Add(attackerPlayerData.FId);
                    AllPlayersDataHasChanged();
                }

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
                attackerPlayerData.Save();
            }
            else
            {
                PrintWarning($"{parAttacker.userID} is not found in the DB");
                return;
            }

            PlayerData victimPlayerData = PlayerData.Find(parVictim);
            if (victimPlayerData != null)
            {
                victimPlayerData.FImprovedHitInfos.Add(improvedHitInfo);
                if (!FAllPlayerIds.Contains(victimPlayerData.FId))
                {
                    FAllPlayerIds.Add(victimPlayerData.FId);
                    AllPlayersDataHasChanged();
                }

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
                victimPlayerData.Save();
            }
            else
            {
                PrintWarning($"{parVictim.userID} is not found in the DB");
                return;
            }
        }

        #endregion

        #region Hooks
        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("scl", "cmdShowHitInfo");
            AddCovalenceCommand("scl_all", "cmdShowAllHitInfo");
            AddCovalenceCommand("icl", "cmdListHitInfo");
            AddCovalenceCommand("ikl", "cmdListKill");
            AddCovalenceCommand("ikb", "cmdListKillBy");

            // register permission
            permission.RegisterPermission(FPerm, this);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            PlayerData.TryLoad(player);
            if (!FAllPlayerIds.Contains(player.userID))
            {
                FAllPlayerIds.Add(player.userID);
                AllPlayersDataHasChanged();
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
        }

        void Unloaded()
        {
        }

        void OnServerInitialized()
        {
            FInstance = this;
            if (!AshTools)
            {
                PrintError("AshTools is not present, this plugins will not works");
                Unsubscribe("OnEntityTakeDamage");
                Unsubscribe("OnEntityDeath");
                return;
            }

            LoadAllPlayers();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                PlayerData.TryLoad(player);
                PrintWarning($"Loaded ICL data from OnServerInitialized for '{player.userID}/{player.displayName}= {PlayerData.Find(player)?.FImprovedHitInfos.Count.ToString() ?? "0"}'");
                if (!FAllPlayerIds.Contains(player.userID))
                {
                    FAllPlayerIds.Add(player.userID);
                    AllPlayersDataHasChanged();
                }
            }
            foreach (ulong playerId in FAllPlayerIds)
            {
                if (PlayerData.TryLoad(playerId))
                    PrintWarning($"Loaded ICL data from OnServerInitialized/FAllPlayerIds for '{playerId}= {PlayerData.Find(playerId)?.FImprovedHitInfos.Count.ToString() ?? "0"}'");
            }

            timer.Every(FConfigFile.DelayBetweenSave, () =>
            {
                SaveAllPlayersIFN();
                foreach (PlayerData playerData in FLoadedPlayerData)
                    playerData.ForceSave();
            });

        }

        private void OnServerSave()
        {
            SaveAllPlayersIFN();
            foreach (PlayerData playerData in FLoadedPlayerData)
                playerData.ForceSave();
        }

        void OnEntityTakeDamage(BaseCombatEntity parEntity, HitInfo parHitInfo)
        {
            BasePlayer player = parEntity.ToPlayer();
            if (player != null)
            {
                if (parHitInfo?.Initiator?.ToPlayer() != null)
                {
                    NextTick(() =>
                    {
                        if (player.IsWounded())
                            FLastInfoOnWoundedPlayer[player.userID] = parHitInfo;
                    });
                }
                RecordHitInfoIFN(parEntity, parHitInfo);
            }
        }

        void OnEntityDeath(BaseCombatEntity parEntity, HitInfo parHitInfo)
        {
            BasePlayer player = parEntity.ToPlayer();
            if (player != null)
            {
                ulong userId = player.userID;
                if (player.IsWounded() && FLastInfoOnWoundedPlayer.ContainsKey(userId))
                {
                    parHitInfo = FLastInfoOnWoundedPlayer[userId];
                    FLastInfoOnWoundedPlayer.Remove(userId);
                }
                RecordHitInfoIFN(parEntity, parHitInfo, true);
            }
        }
        #endregion

        #region Commands
        private void cmdListHitInfo(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1 && AshTools)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
                {
                    PrintToConsole(player, "\nDate idAttacker nameAttacker positionAttacker weapon distance idVictim nameVictim positionVictim damageType");
                    foreach (ImprovedHitInfo ihi in playerData.FImprovedHitInfos.OrderByDescending((d) => d.FDate).Take((args.Length >= 2 ? int.Parse(args[1]) : 15)).Reverse())
                        PrintToConsole(player, $"{ihi.FDate.ToString("dd/MM/yyyy HH:mm:ss.fff")} {ihi.FIdFrom} {ihi.FNameFrom} {ihi.FPositionFrom.ToString().Replace(",", "")} {ihi.FWeaponName} {ihi.FDistance.ToString("F1")}m {ihi.FIdTo} {ihi.FNameTo} {ihi.FPositionTo.ToString().Replace(",", "")} {ihi.FDamageType.ToString()}");
                }
            }
        }

        private void cmdShowHitInfo(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1 && AshTools)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
                {
                    uint index = 0;
                    float duration = FConfigFile.DisplayDuration;
                    float distanceMax = FConfigFile.DistanceShowSingle;
                    DateTime dateFrom = new DateTime(), dateTo = new DateTime();
                    bool checkDateFrom = false, checkDateTo = false;
                    string dateTimeFormat = "dd/MM/yyyy HH:mm:ss";
                    if (args.Length >= 2 && DateTime.TryParseExact(args[1], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateFrom))
                        checkDateFrom = true;
                    if (args.Length >= 3 && DateTime.TryParseExact(args[2], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTo))
                        checkDateTo = true;

                    Vector3 currentPosition = player.ServerPosition;
                    PrintToConsole(player, "\nDate idAttacker nameAttacker positionAttacker weapon distance idVictim nameVictim positionVictim damageType");
                    foreach (ImprovedHitInfo ihi in playerData.FImprovedHitInfos.OrderBy((d) => d.FDate))
                    {
                        if ((Vector3.Distance(currentPosition, ihi.FPositionFrom) < distanceMax || Vector3.Distance(currentPosition, ihi.FPositionTo) < distanceMax) && (!checkDateFrom || DateTime.Compare(ihi.FDate, dateFrom) > 0) && (!checkDateTo || DateTime.Compare(ihi.FDate, dateTo) < 0) && (ihi.FWeaponName != null))
                        {
                            PrintToConsole(player, $"[{++index}] {ihi.FDate.ToString("dd/MM/yyyy HH:mm:ss.fff")} {ihi.FIdFrom} {ihi.FNameFrom} {ihi.FPositionFrom.ToString().Replace(",", "")} {ihi.FWeaponName} {ihi.FDistance.ToString("F1")}m {ihi.FIdTo} {ihi.FNameTo} {ihi.FPositionTo.ToString().Replace(",", "")} {ihi.FDamageType.ToString()}");

                            Color color = ihi.FIdFrom == playerData.FId ? Color.green : Color.red;
                            Color invertColor = ihi.FIdFrom == playerData.FId ? Color.red : Color.green;
                            Vector3 wristFrom = ihi.FPositionFrom; wristFrom[1] += 1;
                            Vector3 wristTo = ihi.FPositionTo; wristTo[1] += 1;
                            player.SendConsoleCommand("ddraw.arrow", duration, color, wristFrom, wristTo, 0.25);
                            player.SendConsoleCommand("ddraw.text", duration, color, wristFrom, ihi.FNameFrom);
                            player.SendConsoleCommand("ddraw.text", duration, invertColor, wristTo, ihi.FNameTo);
                            player.SendConsoleCommand("ddraw.text", duration, color, (wristFrom + wristTo) / 2, (index).ToString() + ":-> " + ihi.FWeaponName);
                        }
                    }
                }
            }
        }

        private void cmdShowAllHitInfo(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && AshTools)
            {
                uint index = 0;
                float duration = FConfigFile.DisplayDuration;
                float distanceMax = FConfigFile.DistanceShowAll;
                DateTime dateFrom = new DateTime(), dateTo = new DateTime();
                bool checkDateFrom = false, checkDateTo = false;
                string dateTimeFormat = "dd/MM/yyyy HH:mm:ss";
                if (args.Length >= 1 && DateTime.TryParseExact(args[0], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateFrom))
                    checkDateFrom = true;
                if (args.Length >= 2 && DateTime.TryParseExact(args[1], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTo))
                    checkDateTo = true;

                List<ImprovedHitInfo> ihiFound = new List<ImprovedHitInfo>();
                Vector3 currentPosition = player.ServerPosition;
                foreach (PlayerData playerData in FLoadedPlayerData)
                {
                    if (playerData.FImprovedHitInfos == null)
                        continue;

                    foreach (ImprovedHitInfo ihi in playerData.FImprovedHitInfos)
                    {
                        if (ihiFound.Find((p) => p.FDate == ihi.FDate && p.FIdFrom == ihi.FIdTo && p.FIdTo == ihi.FIdFrom) != null)
                        {
                            PrintToConsole(player, $"filter ihi {ihi.FDate} {ihi.FIdFrom} {ihi.FIdTo}, already displayed");
                            continue;
                        }

                        if ((Vector3.Distance(currentPosition, ihi.FPositionFrom) < distanceMax || Vector3.Distance(currentPosition, ihi.FPositionTo) < distanceMax) && (!checkDateFrom || DateTime.Compare(ihi.FDate, dateFrom) > 0) && (!checkDateTo || DateTime.Compare(ihi.FDate, dateTo) < 0) && (ihi.FWeaponName != null))
                            ihiFound.Add(ihi);
                    }
                }

                PrintToConsole(player, "\nDate idAttacker nameAttacker positionAttacker weapon distance idVictim nameVictim positionVictim damageType");
                foreach (ImprovedHitInfo ihi in ihiFound.OrderBy((d) => d.FDate))
                {
                    PrintToConsole(player, $"[{index}] {ihi.FDate.ToString("dd/MM/yyyy HH:mm:ss.fff")} {ihi.FIdFrom} {ihi.FNameFrom} {ihi.FPositionFrom.ToString().Replace(",", "")} {ihi.FWeaponName} {ihi.FDistance.ToString("F1")}m {ihi.FIdTo} {ihi.FNameTo} {ihi.FPositionTo.ToString().Replace(",", "")} {ihi.FDamageType.ToString()} ");

                    Color color = Color.green;
                    Color invertColor = Color.red;
                    Vector3 wristFrom = ihi.FPositionFrom; wristFrom[1] += 1;
                    Vector3 wristTo = ihi.FPositionTo; wristTo[1] += 1;

                    player.SendConsoleCommand("ddraw.arrow", duration, color, wristFrom, wristTo, 0.25);
                    player.SendConsoleCommand("ddraw.text", duration, color, wristFrom, ihi.FNameFrom);
                    player.SendConsoleCommand("ddraw.text", duration, invertColor, wristTo, ihi.FNameTo);
                    player.SendConsoleCommand("ddraw.text", duration, color, (wristFrom + wristTo) / 2, (++index).ToString() + ":-> " + ihi.FWeaponName);
                }
            }
        }

        private void cmdListKill(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1 && AshTools)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
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
            if (permission.UserHasPermission(player.UserIDString, FPerm) && args.Length >= 1 && AshTools)
            {
                PlayerData playerData = PlayerData.Find(args[0]);
                if (playerData != null)
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