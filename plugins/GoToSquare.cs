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
    [Info("GoToSquare", "Ash @ Rust France Infinity", "1.0.5")]
    [Description("Teleport to the center of the square")]

    class GoToSquare : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        [PluginReference] private Plugin AshTools;

        private static ConfigFile FConfigFile;

        // Permissions
        private const string _perm = "gotosquare.admin";
        #endregion

        #region Configuration
        private class ConfigFile
        {
            public float ZValue { get; set; }
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    ZValue = 0f
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

        #region Hooks

        private void Init()
        {
            // Register universal chat/console commands
            AddCovalenceCommand("tp", "GotoSquare");
            AddCovalenceCommand("tpa", "GotoSquareAbsolute");
            AddCovalenceCommand("coord", "GetCoordAbsolute");

            // register permission
            permission.RegisterPermission(_perm, this);
        }

        private void OnServerInitialized()
        {
            if (!AshTools)
                PrintError("AshTools is not present, this plugins will not works");
        }

        #endregion

        #region Plugin API

        private void GotoSquare(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 1 && AshTools)
            {
                try
                {
                    Vector3 destination = (Vector3)AshTools?.Call("SquareToCoordinate", args[0]);
                    destination[1] = FConfigFile.ZValue;
                    player.Teleport(destination);
                }
                catch (ArgumentException e)
                {
                    PrintToChat(player, "{0}: {1}", e.GetType().Name, e.Message);
                }
            }
        }

        private void GotoSquareAbsolute(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 3 && AshTools)
            {
                try
                {
                    Vector3 destination = new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
                    PrintToChat(player, $"destination: {destination}");
                    player.Teleport(destination);
                }
                catch (ArgumentException e)
                {
                    PrintToChat(player, "{0}: {1}", e.GetType().Name, e.Message);
                }
                catch (Exception)
                {
                }
            }
        }

        private void GetCoordAbsolute(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 0 && AshTools)
            {
                string square = (string)AshTools?.Call("CoordinateToSquare", player.ServerPosition);
                PrintToChat(player, "coordinate= " + player.ServerPosition + " => " + square);
            }
        }
        #endregion
    }
}