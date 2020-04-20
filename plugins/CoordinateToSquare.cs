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
    [Info("CoordinateToSquare", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("translate coordinate to square an square to coordinate")]

    class CoordinateToSquare : RustPlugin
    {
        #region Declaration

        private Vector3 InvalidLocation = new Vector3(-1f, -1f, -1f);
        private float mapSize;
        private float squareSize = 146;
        private float midSquare = 73;

        private Dictionary<float, float> FOffsetPerMapSize = new Dictionary<float, float>
        {
            [4000f] = 50f,
            [3500f] = -10f,
            [3000f] = 75f
        };

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            mapSize = World.Size;
            float offset = FOffsetPerMapSize.ContainsKey(mapSize) ? FOffsetPerMapSize[mapSize] : 0f;
            int nbSquare = (int)((mapSize - offset) / squareSize);
            squareSize = (mapSize - offset) / nbSquare;
            PrintWarning("mapSize= " + mapSize + ", offset = " + offset + ", nbSquare= " + nbSquare + ", squareSize recalibrated to= " + squareSize);
        }

        #endregion

        #region Methods

        public static string NumberToLetter(int parX)
        {
            int rem = Mathf.FloorToInt((float)(parX / 26));
            int quot = parX % 26;
            string result = string.Empty;
            if (rem > 0)
            {
                for (int i = 0; i < rem; i++)
                    result += Convert.ToChar(65 + i);
            }
            return result + Convert.ToChar(65 + quot).ToString();
        }

        #endregion

        #region Plugin API

        [HookMethod("CoordinateToSquare")]
        private string CoordinateToSquareCmd(object parPosition)
        {
            // convert to BaseCombatEntity and check
            Vector3 realPosition = InvalidLocation;
            if (parPosition is Vector3)
                realPosition = (Vector3)parPosition;
            if (parPosition == null || realPosition == InvalidLocation)
                throw new ArgumentException("parPosition");

            // translate from coordinate to square
            float midMap = mapSize / 2;
            float offset = (FOffsetPerMapSize.ContainsKey(mapSize) ? FOffsetPerMapSize[mapSize] : 0f);

            int x = (int)((midMap + realPosition.x) / squareSize);
            int y = (int)((midMap - offset - realPosition.z) / squareSize);

            return $"{NumberToLetter(x)}{y}";
        }

        [HookMethod("SquareToCoordinate")]
        private Vector3 SquareToCoordinateCmd(object parSquare)
        {
            // convert to BaseCombatEntity and check
            string realSquare = null;
            if (parSquare is string)
                realSquare = parSquare as string;
            if (parSquare == null || realSquare == null)
                throw new ArgumentException("parSquare is invalid: " + parSquare);

            // translate from square to coordinate
            Vector3 result = InvalidLocation;

            // find the alpha and numeric part
            int charPos = 0;
            int numericPos = realSquare.IndexOfAny("0123456789".ToCharArray(), charPos);

            // check the validity of the string
            if (numericPos < 1 || numericPos > 2)
                throw new ArgumentException("Invalid location provided, bad numeric information in the location: " + numericPos);

            // translate to valid coordinate
            string upperCoord = realSquare.ToUpper();
            int x = numericPos == 1 ? upperCoord[0] - 'A' : upperCoord[1] - 'A' + 26;
            int y = int.Parse(upperCoord.Substring(numericPos));

            float midMap = mapSize / 2;
            float offset = (FOffsetPerMapSize.ContainsKey(mapSize) ? FOffsetPerMapSize[mapSize] : 0f);
            result[0] = (x * squareSize + midSquare) - midMap;
            result[2] = midMap - (y * squareSize + midSquare + offset);
            result[1] = 100f;

            if (result[0] < -midMap || result[0] > midMap || result[2] < -midMap || result[2] > midMap)
                throw new ArgumentException("Invalid location provided, location is outside of the map: " + result);

            return result;
        }

        #endregion
    }
}