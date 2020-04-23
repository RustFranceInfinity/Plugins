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
    [Info("GoToSquare", "Ash @ Rust France Infinity", "1.0.1")]
    [Description("Teleport to the center of the square")]

    class GoToSquare : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        [PluginReference] private Plugin CoordinateToSquare;

        // Permissions
        private const string _perm = "gotosquare.admin";
        #endregion

        #region Hooks

        private void Init()
        {
            // Register univeral chat/console commands
            AddCovalenceCommand("tp", "GotoSquare");
            AddCovalenceCommand("tpa", "GotoSquareAbsolute");
            AddCovalenceCommand("coord", "GetCoordAbsolute");

            // register permission
            permission.RegisterPermission(_perm, this);
        }

        private void OnServerInitialized()
        {
            if (!CoordinateToSquare)
                PrintWarning("GoToSquare won't be working as its translator ('CoordinateToSquare') is not present");
        }

        #endregion

        #region Plugin API

        private void GotoSquare(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 1 && CoordinateToSquare)
            {
                try
                {
                    Vector3 destination = (Vector3)CoordinateToSquare?.Call("SquareToCoordinate", args[0]);
                    player.Teleport(destination);
                }
                catch (ArgumentException e)
                {
                    PrintToChat("{0}: {1}", e.GetType().Name, e.Message);
                }
            }
        }

        private void GotoSquareAbsolute(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 3)
            {
                try
                {
                    Vector3 destination = new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
                    player.Teleport(destination);
                }
                catch (ArgumentException e)
                {
                    PrintToChat("{0}: {1}", e.GetType().Name, e.Message);
                }
                catch (Exception)
                {
                }
            }
        }

        private void GetCoordAbsolute(IPlayer parPlayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)parPlayer.Object;
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 0)
            {
                string square = (string)CoordinateToSquare?.Call("CoordinateToSquare", player.ServerPosition);
                PrintToChat(player, "coordinate= " + player.ServerPosition + " => " + square);
            }
        }
        #endregion
    }
}