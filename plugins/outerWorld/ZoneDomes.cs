﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZoneDomes", "k1lly0u correction by Ash @ Rust France Infinity", "0.2.0", ResourceId = 1945)]
    class ZoneDomes : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin ZoneManager;

        private List<BaseEntity> Spheres = new List<BaseEntity>();

        private bool Active;
        private const string SphereEnt = "assets/prefabs/visualization/sphere.prefab";

        ZDData data;
        private DynamicConfigFile Data;
        #endregion

        #region Data
        class ZDData
        {
            public Dictionary<string, ZoneEntry> Zones = new Dictionary<string, ZoneEntry>();
        }
        class ZoneEntry
        {
            public Vector3 Position;
            public float Radius;
        }
        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        } // borrowed from ZoneManager
        void SaveData()
        {
            Data.WriteObject(data);
        }
        void LoadData()
        {
            try
            {
                data = Data.ReadObject<ZDData>();
            }
            catch
            {
                data = new ZDData();
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            Data = Interface.Oxide.DataFileSystem.GetFile("zonedomes_data");
            Data.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };
            lang.RegisterMessages(Messages, this);
        }
        void OnServerInitialized()
        {
            VerifyDependency();
            LoadVariables();
            LoadData();
            RemoveExisting();
            InitializeDomes();

            PrintWarning("IsMute status= " + configData.IsMute);
        }
        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.PrefabName == SphereEnt && !Spheres.Contains(entity))
                Spheres.Add(entity);
        }
        void Unload() => DestroyAllSpheres();
        #endregion

        #region Functions
        private void VerifyDependency()
        {
            if (ZoneManager)
                Active = true;
            else
            {
                PrintWarning(GetMsg("noZM"));
                Active = false;
            }
        }
        private void RemoveExisting()
        {
            for (int i = 0; i < Spheres.Count; i++)
            {
                foreach (var entry in data.Zones)
                {
                    if (Spheres[i] != null)
                    {
                        if (Spheres[i].transform.position == entry.Value.Position)
                        {
                            DestroySphere(Spheres[i]);
                        }
                    }
                }
            }
            Spheres.Clear();
        }
        private void InitializeDomes()
        {
            var removeList = new List<string>();
            foreach (var entry in data.Zones)
            {
                var exists = VerifyZoneID(entry.Key);
                if (exists is string && !string.IsNullOrEmpty((string)exists))
                {
                    CreateSphere(entry.Value.Position, entry.Value.Radius);
                }
                else removeList.Add(entry.Key);
            }
            if (removeList.Count > 0)
            {
                PrintToChat(string.Format(GetMsg("invZone"), removeList.Count));
                foreach (var entry in removeList)
                    data.Zones.Remove(entry);
            }
        }
        private void CreateSphere(Vector3 position, float radius)
        {
            for (int i = 0; i < configData.SphereDarkness; i++)
            {
                BaseEntity sphere = GameManager.server.CreateEntity(SphereEnt, position, new Quaternion(), true);
                SphereEntity ent = sphere.GetComponent<SphereEntity>();
                ent.currentRadius = radius * 2;
                ent.lerpSpeed = 0f;

                sphere.Spawn();
                Spheres.Add(sphere);
            }
        }
        private void DestroySphere(BaseEntity sphere)
        {
            if (sphere != null)
            {
                sphere.KillMessage();
                Spheres.Remove(sphere);
            }
        }
        private void DestroyAllSpheres()
        {
            foreach (var sphere in Spheres)
                if (sphere != null)
                    sphere.KillMessage();
            Spheres.Clear();
        }
        #endregion

        #region ZoneManager Hooks
        private object VerifyZoneID(string zoneid) => ZoneManager?.Call("CheckZoneID", zoneid);
        private object GetZoneLocation(string zoneid) => ZoneManager?.Call("GetZoneLocation", zoneid);
        private object GetZoneRadius(string zoneid) => ZoneManager?.Call("GetZoneRadius", zoneid);
        #endregion

        #region Zone Creation
        [HookMethod("AddNewDome")]
        public bool AddNewDome(BasePlayer player, string ID)
        {
            var zoneid = VerifyZoneID(ID);
            if (zoneid is string && !string.IsNullOrEmpty((string)zoneid))
            {
                if (data.Zones.ContainsKey(ID))
                {
                    SendMsg(player, "", GetMsg("alreadyExists"));
                    return false;
                }
                var pos = GetZoneLocation(ID);
                if (pos != null && pos is Vector3)
                {
                    var radius = GetZoneRadius(ID);
                    if (radius != null && radius is float)
                    {
                        CreateSphere((Vector3)pos, (float)radius);
                        data.Zones.Add(ID, new ZoneEntry { Position = (Vector3)pos, Radius = (float)radius });
                        SaveData();
                        SendMsg(player, ID, GetMsg("newSuccess"));
                        return true;
                    }
                    else
                    {
                        SendMsg(player, ID, GetMsg("noRad"));
                        return false;
                    }
                }
                else
                {
                    SendMsg(player, ID, GetMsg("noLoc"));
                    return false;
                }
            }
            else
            {
                SendMsg(player, ID, GetMsg("noVerify"));
                return false;
            }
        }
        [HookMethod("RemoveExistingDome")]
        public bool RemoveExistingDome(BasePlayer player, string ID)
        {
            if (data.Zones.ContainsKey(ID))
            {
                Vector3 zonePosition = data.Zones[ID].Position;
                List<BaseEntity> sphereToRemove = new List<BaseEntity>();
                for (int i = 0; i < Spheres.Count; i++)
                {
                    if (Spheres[i] != null && Spheres[i].transform.position == zonePosition)
                        sphereToRemove.Add(Spheres[i]);
                }

                if (sphereToRemove.Count == 0)
                {
                    SendMsg(player, ID, GetMsg("noEntity"));
                    SendMsg(player, ID, GetMsg("remInvalid"));
                    return false;
                }

                data.Zones.Remove(ID);
                foreach (BaseEntity sphere in sphereToRemove)
                    DestroySphere(sphere);

                SaveData();
                SendMsg(player, ID, GetMsg("remSuccess"));
            }
            else
            {
                SendMsg(player, ID, GetMsg("noInfo"));
                return false;
            }
            return true;
        }
        #endregion

        #region Chat Commands

        [ChatCommand("zd")]
        private void cmdZoneDome(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                if (args == null || args.Length == 0)
                {
                    SendMsg(player, GetMsg("addSyn"), "/zd add <zoneid>");
                    SendMsg(player, GetMsg("remSyn"), "/zd remove <zoneid>");
                    SendMsg(player, GetMsg("listSyn"), "/zd list");
                    return;
                }
                if (args.Length >= 1)
                {
                    switch (args[0].ToLower())
                    {
                        case "add":
                            if (args.Length == 2)
                            {
                                AddNewDome(player, args[1]);
                            }
                            break;
                        case "remove":
                            if (args.Length == 2)
                            {
                                RemoveExistingDome(player, args[1]);
                            }
                            break;
                        case "list":
                            PrintToChat(GetMsg("listForm"));
                            foreach (var entry in data.Zones)
                                PrintToChat($"{entry.Key} -- {entry.Value.Radius} -- {entry.Value.Position}");
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public int SphereDarkness { get; set; }
            public bool IsMute { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                SphereDarkness = 5,
                IsMute = false
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        private void SendMsg(BasePlayer player, string message, string keyword)
        {
            if (!configData.IsMute)
                SendReply(player, $"<color=orange>{keyword}</color><color=#939393>{message}</color>");
        }
        private string GetMsg(string key) => lang.GetMessage(key, this);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"noInfo", "Unable to find information for: " },
            {"noEntity", "Unable to find the sphere entity with for zone ID: " },
            {"remInvalid", "Removing invalid zone data" },
            {"remSuccess", "You have successfully removed the sphere from zone: " },
            {"noVerify", "Unable to verify with ZoneManager the ID: " },
            {"noLoc", "Unable to retrieve location data from ZoneManager for: " },
            {"noRad", "Unable to retrieve radius data from ZoneManager for: " },
            {"newSuccess", "You have successfully created a sphere for the zone: " },
            {"noZM", "Unable to find ZoneManager, unable to proceeed" },
            {"invZone", "Found {0} invalid zones. Removing them from data" },
            {"listForm", "--- Sphere List --- \n <ID> -- <Radius> -- <Position>" },
            {"addSyn", " - Adds a sphere to the zone <zoneid>" },
            {"remSyn", " - Removes a sphere from the zone <zoneid>" },
            {"listSyn", " - Lists all current active spheres and their position" },
            {"alreadyExists", "This zone already has a dome" }
        };

        #endregion
    }
}