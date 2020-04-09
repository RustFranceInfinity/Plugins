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
    [Info("WhitelistAndCupboard", "Ash @ Rust France Infinity", "1.0.0")]
    [Description("check the white list and the cupbaord authorisation to allow damage on the entity")]

    class WhitelistAndCupboard : RustPlugin
    {
        #region Declaration

        readonly int layerMasks = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");

        // Config, instance, plugin references
        private static ConfigFile cFile;
        private static WhitelistAndCupboard _instance;

        #endregion

        #region Config

        private class ConfigFile
        {
            public List<string> whiteList { get; set; }
            public float DistanceThreshold { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    whiteList = new List<string>(),
                    DistanceThreshold = 20f
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

        List<ulong> FindOwnedCupboardOwnerNear(BaseCombatEntity entity, bool bypassOwner = false)
        {
            var prodOwners = new List<ulong>();
            var entityList = new HashSet<BaseEntity>();
            var checkFrom = new List<Vector3>();
            ulong entityOwner = entity.OwnerID;

            checkFrom.Add(entity.transform.position);

            var current = 0;
            while (true)
            {
                current++;
                if (current > checkFrom.Count)
                    break;

                var entities = Pool.GetList<BuildingPrivlidge>();
                Vis.Entities<BuildingPrivlidge>(checkFrom[current - 1], cFile.DistanceThreshold, entities, layerMasks);

                foreach (var e in entities)
                {
                    if (!entityList.Add(e))
                        continue;
                    checkFrom.Add(e.transform.position);

                    List<ProtoBuf.PlayerNameID> players = e.authorizedPlayers.ToList();
                    foreach (var pnid in players)
                    {
                        if (pnid.userid == entityOwner || bypassOwner)
                        {
                            foreach (var pnidElt in players)
                            {
                                if (!prodOwners.Contains(pnidElt.userid))
                                    prodOwners.Add(pnidElt.userid);
                            }
                            break;
                        }
                    }
                }
                Pool.FreeList(ref entities);
            }

            return prodOwners;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            foreach (var element in cFile.whiteList)
                PrintWarning("whiteListElt= " + element);
        }

        #endregion

        #region Plugin API

        [HookMethod("CanBeDamaged")]
        private bool CanBeDamaged(object entity, object player)
        {
            // convert to BaseCombatEntity and check
            BaseCombatEntity realEntity = null;
            if (entity is BaseCombatEntity)
                realEntity = entity as BaseCombatEntity;
            if (entity == null || realEntity == null)
                throw new ArgumentException("entity");

            // convert to HitInfo and check
            BasePlayer realPlayer = null;
            if (player is BasePlayer)
                realPlayer = player as BasePlayer;
            if (player == null || realPlayer == null)
                throw new ArgumentException("player");

            // white list analysis
            foreach (var element in cFile.whiteList)
            {
                if (realEntity.name.Contains(element))
                    return true;
            }

            // cupboard analysis
            List<ulong> owners = FindOwnedCupboardOwnerNear(realEntity);
            if (owners.Count == 0)
                owners = FindOwnedCupboardOwnerNear(realEntity, true);

            if (owners.Contains(realPlayer.userID))
                return true;

            return false;
        }

        #endregion
    }
}