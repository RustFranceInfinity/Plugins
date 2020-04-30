using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AdminZone", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("création d'une zone statique dans laquelle les joueurs sont invincibles")]

    class AdminZone : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        [PluginReference] private Plugin ZoneManager, ZoneDomes;

        private static ConfigFile FConfigFile;

        private Dictionary<BasePlayer, Vector3> FAdminZones = new Dictionary<BasePlayer, Vector3>();

        // Timers
        private Timer FTimer;

        // define

        // Permissions
        private const string FPermission = "adminzone.admin";
        #endregion

        #region Config

        private class ConfigFile
        {
            public float DefaultZoneSize { get; set; }
            public float RefreshInterval { get; set; }
            public float RefreshDistance { get; set; }
            public bool UseDynamicZone { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    DefaultZoneSize = 30f,
                    RefreshInterval = 3f,
                    RefreshDistance = 15f,
                    UseDynamicZone = false
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

        #region Methods
        bool CreateOrUpdateAdminZone(BasePlayer parPlayer, float parSize = 0f)
        {
            List<string> arguments = new List<string>() { "name", "adminZone_" + parPlayer.UserIDString, "enter_message", "Administration en cours", "pvpgod", "true" };
            if (parSize != 0f)
            {
                arguments.Add("radius");
                arguments.Add(parSize.ToString());
            }
            return (bool)ZoneManager?.Call("CreateOrUpdateZone", parPlayer.UserIDString, arguments.ToArray(), parPlayer.ServerPosition);
        }

        void ActivateAdminZone(BasePlayer parPlayer, float parSize)
        {
            if (!FAdminZones.ContainsKey(parPlayer))
            {
                bool creationSuccessful = CreateOrUpdateAdminZone(parPlayer, parSize);
                if (creationSuccessful)
                {
                    FAdminZones.Add(parPlayer, parPlayer.ServerPosition);
                    if (ZoneDomes)
                    {
                        bool displaySuccessful = (bool)ZoneDomes?.Call("AddNewDome", parPlayer, parPlayer.UserIDString);
                        if (!displaySuccessful)
                            PrintToChat(parPlayer, "something wrong happens when displaying the dome");
                    }
                }
                else
                    PrintToChat(parPlayer, "something wrong happens when creating the zone");
            }
        }

        void DesactivateAdminZone(BasePlayer parPlayer)
        {
            FAdminZones.Remove(parPlayer);
            if (ZoneDomes)
            {
                bool undisplaySuccessful = (bool)ZoneDomes?.Call("RemoveExistingDome", parPlayer, parPlayer.UserIDString);
                if (!undisplaySuccessful)
                    PrintToChat(parPlayer, "something wrong happens when undisplaying the dome");
            }
            bool eraseSuccessful = (bool)ZoneManager?.Call("EraseZone", parPlayer.UserIDString);
            if (!eraseSuccessful)
                PrintToChat(parPlayer, "something wrong happens when erasing the zone");
        }

        #endregion

        #region Hooks

        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("azone", "ManageAdminZoneCmd");

            permission.RegisterPermission(FPermission, this);
            SaveConfig();

            if (FConfigFile.UseDynamicZone)
                PrintWarning("Dynamic zone enabled, activation distance= " + FConfigFile.RefreshDistance);
        }

        private void Loaded()
        {
            if (!ZoneManager)
                PrintError("ZoneManager not detected, this plugins will not works");
            if (!ZoneDomes)
                PrintWarning("ZoneDomes not detected, this plugins will works but will not display the zones");
        }

        private void OnServerInitialized()
        {
            FTimer?.Destroy();
            if (FConfigFile.UseDynamicZone)
            {
                FTimer = timer.Every(FConfigFile.RefreshInterval, () =>
                {
                    Dictionary<BasePlayer, Vector3> newPos = new Dictionary<BasePlayer, Vector3>();
                    foreach (var playerAndPos in FAdminZones)
                    {
                        BasePlayer currentPlayer = playerAndPos.Key;
                        float distance = Vector3.Distance(playerAndPos.Value, currentPlayer.ServerPosition);
                        if (distance > FConfigFile.RefreshDistance)
                        {
                            if (CreateOrUpdateAdminZone(currentPlayer))
                            {
                                newPos.Add(currentPlayer, currentPlayer.ServerPosition);
                                bool undisplaySuccessful = (bool)ZoneDomes?.Call("RemoveExistingDome", currentPlayer, currentPlayer.UserIDString);
                                if (undisplaySuccessful)
                                {
                                    bool displaySuccessful = (bool)ZoneDomes?.Call("AddNewDome", currentPlayer, currentPlayer.UserIDString);
                                    if (!displaySuccessful)
                                        PrintToChat(currentPlayer, "something wrong happens when undisplaying the dome");
                                }
                                else
                                    PrintToChat(currentPlayer, "something wrong happens when undisplaying the dome");
                            }
                            else
                                PrintToChat(currentPlayer, "something wrong happens when updateing the zone " + currentPlayer.UserIDString);
                        }
                    }
                    foreach (var playerAndPos in newPos)
                        FAdminZones[playerAndPos.Key] = playerAndPos.Value;

                });
            }
        }

        private void Unload()
        {
            foreach (var playerAndPos in FAdminZones)
                DesactivateAdminZone(playerAndPos.Key);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DesactivateAdminZone(player);
        }

        #endregion

        #region Command

        private void ManageAdminZoneCmd(IPlayer parPlayer, string parCommand, string[] parArguments)
        {
            if (!parPlayer.HasPermission(FPermission) || !ZoneManager)
                return;

            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (FAdminZones.ContainsKey(player))
                DesactivateAdminZone(player);
            else
            {
                float radius;
                if (parArguments.Length == 0 || !float.TryParse(parArguments[1], out radius))
                    radius = FConfigFile.DefaultZoneSize;
                ActivateAdminZone(player, radius);
            }
        }
        #endregion
    }
}