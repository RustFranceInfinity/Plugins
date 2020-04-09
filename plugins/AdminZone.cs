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
    [Info("AdminZone", "Ash", "0.1")]
    [Description("AdminZone")]

    class AdminZone : RustPlugin
    {
        #region Declaration

        // Config, instance, plugin references
        private static ConfigFile cFile;
        private static AdminZone _instance;

        // define
        private Vector3 InvalidLocation = new Vector3(-1f, -1f, -1f);
        private const float squareSize = 146f;
        private const float midSquare = squareSize / 2;

        private Dictionary<float, float> FOffsetPerMapSize = new Dictionary<float, float>
        {
            [4000f] = 45f,
            [3500f] = 25f
        };

        // Permissions
        private const string _perm = "gotosquare.admin";
        #endregion

        #region Config

        private class ConfigFile
        {
            public float MapSize { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    MapSize = 4000f
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            cFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(cFile);

        #endregion

        #region Methods

        // a valid coordinate is <aplha>[alpha]<numeric>[numeric]
        Vector3 DecipherCoordinate(string parCoordinate)
        {
            Vector3 result = InvalidLocation;

            // find the alpha and numeric part
            int charPos = 0;
            int numericPos = parCoordinate.IndexOfAny("0123456789".ToCharArray(), charPos);

            // check the validity of the string
            if (numericPos < 1 || numericPos > 2)
                return InvalidLocation;

            // translate to valid coordinate
            string upperCoord = parCoordinate.ToUpper();
            int x = numericPos == 1 ? upperCoord[0] - 'A' : upperCoord[1] - 'A' + 26;
            int y = int.Parse(upperCoord.Substring(numericPos));

            float midMap = cFile.MapSize / 2;
            result[0] = (x * squareSize + midSquare) - midMap;
            result[2] = midMap - (y * squareSize + midSquare + (FOffsetPerMapSize.ContainsKey(cFile.MapSize) ? FOffsetPerMapSize[cFile.MapSize] : 0f));
            result[1] = 100f;

            if (result[0] < -midMap || result[0] > midMap || result[2] < -midMap || result[2] > midMap)
                return InvalidLocation;

            return result;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;
            permission.RegisterPermission(_perm, this);
            SaveConfig();
        }

        private void OnServerInitialized()
        {
        }

        #endregion

        #region Plugin API

        [ChatCommand("tp")]
        void GotoSquare(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, _perm) && args.Length == 1)
            {
                Vector3 destination = DecipherCoordinate(args[0]);
                if (destination != InvalidLocation)
                    player.Teleport(destination);
            }
        }
        #endregion
    }
}