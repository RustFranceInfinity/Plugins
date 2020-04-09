using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AdminZone", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("création d'une zone statique dand laquelle les joueurs sont invincibles")]

    class AdminZone : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        [PluginReference] private Plugin ZoneManager, ZoneDomes;

        private static ConfigFile FConfigFile;
        private static AdminZone FInstance;

        private HashSet<BasePlayer> FAdminZones = new HashSet<BasePlayer>();

        // define

        // Permissions
        private const string FPermission = "adminzone.admin";
        #endregion

        #region Config

        private class ConfigFile
        {
            public float DefaultZoneSize { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    DefaultZoneSize = 25f
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
        void ActivateAdminZone(BasePlayer parPlayer, float parSize)
        {
            if (!FAdminZones.Contains(parPlayer))
            {
                string[] arguments = { "name", "adminZone_" + parPlayer.UserIDString, "enter_message", "Administration en cours", "radius", "", "pvpgod", "true" };
                arguments[5] = parSize.ToString();
                bool creationSuccessful = (bool)ZoneManager?.Call("CreateOrUpdateZone", parPlayer.UserIDString, arguments, parPlayer.ServerPosition);
                if (creationSuccessful)
                {
                    FAdminZones.Add(parPlayer);
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
            if (FAdminZones.Contains(parPlayer))
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
        }

        #endregion

        #region Hooks

        private void Init()
        {
            FInstance = this;
            permission.RegisterPermission(FPermission, this);
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            if (!ZoneManager)
                PrintError("ZoneManager not detected, this plugins will not works");
            if (!ZoneDomes)
                PrintWarning("ZoneDomes not detected, this plugins will works but will not display the zones");
        }

        private void Unload()
        {
            foreach (BasePlayer user in FAdminZones)
                DesactivateAdminZone(user);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DesactivateAdminZone(player);
        }

        #endregion

        #region Plugin API

        // azone <on|off> [size]
        [ChatCommand("azone")]
        void CreateAdminZone(BasePlayer parPlayer, string parCommand, string[] parArguments)
        {
            if (permission.UserHasPermission(parPlayer.UserIDString, FPermission) && parArguments.Length >= 1 && ZoneManager)
            {
                if (parArguments[0] == "on")
                    ActivateAdminZone(parPlayer, (parArguments.Length > 1 ? float.Parse(parArguments[1]) : FConfigFile.DefaultZoneSize));
                else if (parArguments[0] == "off")
                    DesactivateAdminZone(parPlayer);
            }
        }
        #endregion
    }
}