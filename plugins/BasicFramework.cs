using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Oxide.Plugins
{
    [Info("BasicFramework", "Ash @ RustFranceInfinity", "0.0.1")]
    [Description("Basic framework used for plugins")]
    public class BasicFramework : RustPlugin
    {
        #region Log
        public class LogLevelConverter : TypeConverter
        {
            // Overrides the CanConvertFrom method of TypeConverter.
            // The ITypeDescriptorContext interface provides the context for the
            // conversion. Typically, this interface is used at design time to
            // provide information about the design-time container.
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                if (sourceType == typeof(string))
                {
                    BasicFramework.Instance.SendMessage(LogLevel.Debug, "LogLevelConverter::CanConvertFrom", null);
                    return true;
                }
                return base.CanConvertFrom(context, sourceType);
            }

            // Overrides the ConvertFrom method of TypeConverter.
            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (value is string)
                {
                    BasicFramework.Instance.SendMessage(LogLevel.Debug, $"LogLevelConverter::ConvertFrom {value}", null);
                    string strValue = value as string;
                    if (strValue == "Debug")
                        return LogLevel.Debug;
                    else if (strValue == "Log")
                        return LogLevel.Log;
                    else if (strValue == "Warning")
                        return LogLevel.Warning;
                    else if (strValue == "Error")
                        return LogLevel.Error;
                    else
                        return LogLevel.Nothing;
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        [TypeConverter(typeof(EnumConverter))]
        public enum LogLevel
        {
            Debug = 0,
            Log,
            Warning,
            Error,
            Nothing
        }

        #endregion

        #region Configuration
        public Configuration FConfiguration;

        [Serializable]
        public class LogConfigurationData
        {
            public LogLevel LogLevelToDisplay { get; set; }
            public bool NeverLogToPlayer { get; set; }
            public bool LogToConsoleOverChat { get; set; }
            public List<string> DoNotDisplayMsgToPlayer { get; set; }
            public bool DisplayToConsoleIfPlayerIsNotAllowToSeeMsg { get; set; }
            public static LogConfigurationData DefaultConfig()
            {
                return new LogConfigurationData
                {
                    LogLevelToDisplay = LogLevel.Log,
                    NeverLogToPlayer = false,
                    LogToConsoleOverChat = true,
                    DoNotDisplayMsgToPlayer = new List<string>(),
                    DisplayToConsoleIfPlayerIsNotAllowToSeeMsg = true
                };
            }
        }

        [Serializable]
        public class PermissionConfigurationData
        {
            public Dictionary<string, string> OverwritedCommandsPermission { get; set; }
            public static PermissionConfigurationData DefaultConfig()
            {
                return new PermissionConfigurationData
                {
                    OverwritedCommandsPermission = new Dictionary<string, string>()
                };
            }
        }

        [Serializable]
        public class ConfigurationData
        {
            public string Name { get; set; }
            public LogConfigurationData LogConfiguration { get; set; }
            public PermissionConfigurationData PermissionConfiguration { get; set; }

            public static ConfigurationData DefaultConfig()
            {
                return new ConfigurationData
                {
                    Name = "Default",
                    LogConfiguration = LogConfigurationData.DefaultConfig(),
                    PermissionConfiguration = PermissionConfigurationData.DefaultConfig()
                };
            }
        }

        [Serializable]
        public class Configuration
        {
            public ConfigurationData DefaultConfiguration { get; set; }
            public Dictionary<string, ConfigurationData> UserConfiguration { get; set; }
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    DefaultConfiguration = ConfigurationData.DefaultConfig(),
                    UserConfiguration = new Dictionary<string, ConfigurationData>()
                };
            }
        }

        private ConfigurationData GetConfiguration(string parPlayerIdString = null)
        {
            if (parPlayerIdString == null || !FConfiguration.UserConfiguration.ContainsKey(parPlayerIdString))
                return FConfiguration.DefaultConfiguration;

            return FConfiguration.UserConfiguration[parPlayerIdString];
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                FConfiguration = Config.ReadObject<Configuration>();
                if (FConfiguration == null)
                    LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }

            FDottedDefaultConfiguration = GetPropertyValueNames(FConfiguration.DefaultConfiguration);
            foreach (var userConfigurationIterator in FConfiguration.UserConfiguration)
                FDottedUserConfiguration[userConfigurationIterator.Key] = GetPropertyValueNames(userConfigurationIterator.Value);
        }

        // Only called if the config file does not already exist
        protected override void LoadDefaultConfig()
        {
            FConfiguration = Configuration.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(FConfiguration);

        public void SaveConfig(string parConfigurationName)
        {
            SaveConfig();
            if (parConfigurationName == "Default")
                FDottedDefaultConfiguration = GetPropertyValueNames(FConfiguration.DefaultConfiguration);
            else
                FDottedUserConfiguration[parConfigurationName] = GetPropertyValueNames(FConfiguration.UserConfiguration[parConfigurationName]);
        }
        #endregion Configuration

        #region Localization

        // Called when the localization for a plugin should be registered
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have the permission to do this command",
                ["HasPermission"] = "You have the permission to do this command, name= <color=orange>{0}</color>, id= <color=red><size=18>{1}</size></color>",
                ["NotAPlayer"] = "The given parameter is not a player",
                ["OverwriteWithPlayer"] = "@{0}: {1}",
                ["Debug"] = "<color=green>[DEBUG]</color> {0}",
                ["Log"] = "<color=white>[LOG]</color> {0}",
                ["Warning"] = "<color=orange>[WARNING]</color> {0}",
                ["Error"] = "<color=red>[ERROR]</color> {0}",
                ["OxideDebug"] = "[DEBUG] [{0}] {1}",
                ["OxideLog"] = "[LOG] [{0}] {1}",
                ["OxideWarning"] = "[WARNING] [{0}] {1}",
                ["OxideError"] = "[ERROR] [{0}] {1}",
                ["BadUsage"] = "The given parameters did not fit the required ones",
                ["Usage" + ModifyDefaultConfigurationCommandName] = "Usage> " + ModifyDefaultConfigurationActionName + " [list|show|modify|add|del]\n- list : display all the fields with their type and value \n- show <field> : display the value of the field\n- modify <field> [<key>] <value> : modify the value of the field, if the field is a dictionary, the key is required\n- add <field> [<key>] <value> add a new value to the field, if the field is a dictionary, the key is required\n- del <field> <value> : delete a value (or a key/value) from a field",
                ["Usage" + ModifySpecificConfigurationCommandName] = "Usage> " + ModifySpecificConfigurationActionName + " [list|show|modify|add|del]\n-create <configName> : create a new config if not already present and duplicate the default value \n- list <configName> : display all the fields with their type and value <configName>\n- show <configName> <field> : display the value of the field\n- modify <configName> <field> [<key>] <value> : modify the value of the field, if the field is a dictionary, the key is required\n- add <configName> <field> [<key>] <value> add a new value to the field, if the field is a dictionary, the key is required\n- del <configName> <field> <value> : delete a value (or a key/value) from a field",
                ["Usage" + GuiManagementCommandName] = "Usage> " + GuiManagementActionName + " [add]\n-add a [panel|button|label|icon] ont the [panel] at the given relative [position]"
            }, this);
        }

        #endregion Localization

        #region DottedConfiguration
        public class DottedFieldDescription
        {
            public string Name { get; set; }
            public bool IsContainer { get; set; } = false;
            public bool IsClass { get; set; } = false;
            public string Type { get; set; }
            public string Value { get; set; } = "";
        }

        List<DottedFieldDescription> FDottedDefaultConfiguration = new List<DottedFieldDescription>();
        Dictionary<string, List<DottedFieldDescription>> FDottedUserConfiguration = new Dictionary<string, List<DottedFieldDescription>>();

        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
                throw new ArgumentException("The type must be serializable.", "source");

            // Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
                return default(T);

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }

        #endregion

        #region Initialization
        private static BasicFramework Instance { get; set; }

        private const string PluginNameLowered = "basicframework";
        private const string AdminPermission = PluginNameLowered + ".admin";

        private const string ModifyDefaultConfigurationActionName = "defaultConfig";
        private const string ModifyDefaultConfigurationCommandName = "ModifyDefaultConfiguration";

        private const string ModifySpecificConfigurationActionName = "specificConfig";
        private const string ModifySpecificConfigurationCommandName = "ModifySpecificConfiguration";

        private const string GuiManagementActionName = "gui";
        private const string GuiManagementCommandName = "GuiManagement";

        // Called when a plugin is being initialized
        private void Init()
        {
            // register chat/console command
            AddCovalenceCommand(ModifyDefaultConfigurationActionName, ModifyDefaultConfigurationCommandName);
            AddCovalenceCommand(ModifySpecificConfigurationActionName, ModifySpecificConfigurationCommandName);
            AddCovalenceCommand(GuiManagementActionName, GuiManagementCommandName);

            // register permissions
            RegisterPermissionWithOverwriteFromConfiguration(null);
        }

        // Called after the server startup has been completed and is awaiting connections
        void OnServerInitialized(bool parServerInitialized)
        {
        }

        // Called when a plugin is being unloaded => Save data, nullify static variables, etc.
        private void Unload()
        {
        }

        void Unloaded()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                RemoveAllGui(player);
        }

        #endregion Initialization

        #region Commands

        private void ModifyDefaultConfiguration(IPlayer parIPlayer, string parCommandName, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parIPlayer.Object;

            // check the player conversion
            if (player == null)
            {
                SendMessage(LogLevel.Error, GetFormettedAndTranslatedText("NotAPlayer", parIPlayer.Id), player.UserIDString);
                return;
            }

            // check the player permission
            if (!HasPermission(parIPlayer, ModifyDefaultConfigurationCommandName))
            {
                SendMessageToPlayer(LogLevel.Warning, player, "NoPermission", parIPlayer.Id);
                return;
            }

            // main part
            if (parArguments.Length < 1)
            {
                SendMessageToPlayer(LogLevel.Warning, player, "BadUsage", parIPlayer.Id);
                SendMessageToPlayer(LogLevel.Log, player, "Usage" + ModifyDefaultConfigurationCommandName, parIPlayer.Id);
                return;
            }

            // all is clear
            ConfigurationInteraction(player, parCommandName, parArguments, GetConfiguration());
        }

        private void ModifySpecificConfiguration(IPlayer parIPlayer, string parCommandName, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parIPlayer.Object;

            // check the player conversion
            if (player == null)
            {
                SendMessage(LogLevel.Error, GetFormettedAndTranslatedText("NotAPlayer", parIPlayer.Id), null);
                return;
            }

            // check the player permission
            if (!HasPermission(parIPlayer, ModifySpecificConfigurationCommandName))
            {
                SendMessageToPlayer(LogLevel.Warning, player, "NoPermission", parIPlayer.Id);
                return;
            }

            // main part
            if (parArguments.Length < 1)
            {
                SendMessageToPlayer(LogLevel.Warning, player, "BadUsage", parIPlayer.Id);
                SendMessageToPlayer(LogLevel.Log, player, "Usage" + ModifySpecificConfigurationCommandName, parIPlayer.Id);
                return;
            }

            // still clear
            if (!FConfiguration.UserConfiguration.ContainsKey(player.UserIDString))
            {
                FConfiguration.UserConfiguration[player.UserIDString] = Clone(FConfiguration.DefaultConfiguration);
                SaveConfig(player.UserIDString);
            }
            ConfigurationInteraction(player, parCommandName, parArguments, GetConfiguration(player.UserIDString));
        }

        private void GuiManagement(IPlayer parIPlayer, string parCommandName, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parIPlayer.Object;

            // check the player conversion
            if (player == null)
            {
                SendMessage(LogLevel.Error, GetFormettedAndTranslatedText("NotAPlayer", parIPlayer.Id), null);
                return;
            }

            // check the player permission
            if (!HasPermission(parIPlayer, GuiManagementCommandName))
            {
                SendMessageToPlayer(LogLevel.Warning, player, "NoPermission", parIPlayer.Id);
                return;
            }

            // main part
            if (parArguments.Length < 1)
            {
                SendMessageToPlayer(LogLevel.Warning, player, "BadUsage", parIPlayer.Id);
                SendMessageToPlayer(LogLevel.Log, player, "Usage" + GuiManagementCommandName, parIPlayer.Id);
                return;
            }

            if (parArguments[0] == "add")
                AddGui(player, parArguments);
            else if (parArguments[0] == "remove_all")
                RemoveAllGui(player);
        }

        void ConfigurationInteraction(BasePlayer parPlayer, string parCommandName, string[] parArguments, ConfigurationData parConfiguration)
        {
            List<DottedFieldDescription> dottedFieldDescriptions = parConfiguration.Name == "Default" ? FDottedDefaultConfiguration : FDottedUserConfiguration[parConfiguration.Name];
            if (parArguments.Length > 0 && parArguments[0] == "list")
            {
                foreach (DottedFieldDescription element in dottedFieldDescriptions)
                    SendMessageToPlayer(LogLevel.Log, parPlayer, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}", parConfiguration.Name == "Default" ? null : parConfiguration.Name);
            }
            else if (parArguments.Length > 1 && parArguments[0] == "show")
            {
                foreach (DottedFieldDescription element in dottedFieldDescriptions)
                    if (element.Name == parArguments[1])
                        SendMessageToPlayer(LogLevel.Log, parPlayer, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}", parConfiguration.Name == "Default" ? null : parConfiguration.Name);
            }
            else if (parArguments.Length > 2 && parArguments[0] == "modify")
            {
                if (ModifyConfigurationData(parPlayer, parArguments[1], parArguments[2], parConfiguration))
                    SaveConfig(parConfiguration.Name);
            }
        }

        #endregion Commands

        #region Gui

        private void RemoveAllGui(BasePlayer parPlayer)
        {
            foreach (var ui in FUsedUI)
                ui.Destroy(parPlayer);
        }

        private void AddGui(BasePlayer parPlayer, string[] parArguments)
        {
            if (FCurrentUi == null)
                FCurrentUi = new UIObject();
            else
                FCurrentUi.Destroy(parPlayer);

            string uiElementName = parArguments[2];
            if (parArguments[1] == "panel" && !FCurrentUi.Contains(uiElementName))
            {
                string panelName = FCurrentUi.AddPanel(uiElementName, Double.Parse(parArguments[3]), Double.Parse(parArguments[4]), Double.Parse(parArguments[5]), Double.Parse(parArguments[6]), new UIColor(0, 0, 0, 0.9), true, "Overlay");
                SendMessageToPlayer(LogLevel.Debug, parPlayer, $"panelName= {panelName}");
            }
            else if (parArguments[1] == "buttonClose" && FCurrentUi != null && FCurrentUi.Contains(parArguments[2]) && !FCurrentUi.Contains("buttonClose" + uiElementName))
            {
                string close = FCurrentUi.AddButton("buttonClose" + uiElementName, Double.Parse(parArguments[3]), Double.Parse(parArguments[4]), Double.Parse(parArguments[5]), Double.Parse(parArguments[6]), new UIColor(1, 0, 0, 0), "", parArguments[2], parArguments[2]);
                FCurrentUi.AddText("buttonClose" + parArguments[2] + "_Text", 0, 0, 1, 1, new UIColor(1, 0, 0, 1), "Fermer", 19, close, 3);
                SendMessageToPlayer(LogLevel.Debug, parPlayer, $"buttonName= {close}");
            }
            //ui.AddText("label4", 0.590775770456961, 0.163398692810458, 0.0935175345377258, 0.0610021786492375, new UIColor(128, 0, 0, 1), "<color=#850606>Morts</color>", 24, panel, 7);
            //ui.AddText("label3", 0.360361317747078, 0.163398692810458, 0.1722635494155154, 0.0610021786492375, new UIColor(166, 24, 40, 1), "<color=#008000>Éliminations</color>", 24, panel, 7);
            //ui.AddText("label2", 0.0786397449521785, 0.163398692810458, 0.3767587672688629, 0.0610021786492375, new UIColor(1, 0, 0, 1), "<color=#C0C0C0>Nom du joueur</color>", 24, panel, 7);
            //ui.AddText("label1", 0.775876726886291, 0.163398692810458, 0.125398512221041, 0.0610021786492375, new UIColor(1, 0, 0, 1), "<color=#C0C0C0>E/M Ratio</color>", 24, panel, 7);
            //ui.AddText("label0", 0.355876726886291, 0.045398692810458, 0.305398512221041, 0.0610021786492375, new UIColor(1, 1, 1, 1), "<color=blue>Rust</color> France <color=red>Infinity™</color> <color=#af8700>Stats</color>", 33, panel, 7);

            if (FCurrentUi != null)
            {
                FCurrentUi.Draw(parPlayer);
                FUsedUI.Add(FCurrentUi);
            }
        }

        // UI Classes - Created by LaserHydra
        UIObject FCurrentUi = null;
        List<UIObject> FUsedUI = new List<UIObject>();

        class UIColor
        {
            double red;
            double green;
            double blue;
            double alpha;

            public UIColor(double red, double green, double blue, double alpha)
            {
                this.red = red;
                this.green = green;
                this.blue = blue;
                this.alpha = alpha;
            }

            public override string ToString()
            {
                return $"{red.ToString()} {green.ToString()} {blue.ToString()} {alpha.ToString()}";
            }
        }

        class UIObject
        {
            List<object> FUiElements = new List<object>();
            List<string> FUiElementsName = new List<string>();

            public UIObject()
            {
            }

            public bool Contains(string parElementName)
            {
                foreach (string elementName in FUiElementsName)
                {
                    if (elementName == parElementName)
                        return true;
                }
                return false;
            }

            public void Draw(BasePlayer player)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", JsonConvert.SerializeObject(FUiElements).Replace("{NEWLINE}", Environment.NewLine));
            }

            public void Destroy(BasePlayer player)
            {
                foreach (string uiName in FUiElementsName)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", uiName);
            }

            public string AddPanel(string name, double left, double top, double width, double height, UIColor color, bool mouse = false, string parent = "Overlay")
            {
                // name = name + RandomString();

                string type = "";
                if (mouse) type = "NeedsCursor";

                FUiElements.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Image"},
                                {"color", color.ToString()}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            },
                            new Dictionary<string, string> {
                                {"type", type}
                            }
                        }
                    }
                });

                FUiElementsName.Add(name);
                return name;
            }

            public string AddText(string name, double left, double top, double width, double height, UIColor color, string text, int textsize = 15, string parent = "Overlay", int alignmode = 0)
            {
                //name = name + RandomString();
                text = text.Replace("\n", "{NEWLINE}");
                string align = "";

                switch (alignmode)
                {
                    case 0: { align = "LowerCenter"; break; };
                    case 1: { align = "LowerLeft"; break; };
                    case 2: { align = "LowerRight"; break; };
                    case 3: { align = "MiddleCenter"; break; };
                    case 4: { align = "MiddleLeft"; break; };
                    case 5: { align = "MiddleRight"; break; };
                    case 6: { align = "UpperCenter"; break; };
                    case 7: { align = "UpperLeft"; break; };
                    case 8: { align = "UpperRight"; break; };
                }

                FUiElements.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Text"},
                                {"text", text},
                                {"fontSize", textsize.ToString()},
                                {"color", color.ToString()},
                                {"align", align}
                            },
                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                FUiElementsName.Add(name);
                return name;
            }

            public string AddButton(string name, double left, double top, double width, double height, UIColor color, string command = "", string parent = "Overlay", string closeUi = "")
            {
                // name = name + RandomString();

                FUiElements.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"close", closeUi},
                                {"command", command},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString()} {((1 - top) - height).ToString()}"},
                                {"anchormax", $"{(left + width).ToString()} {(1 - top).ToString()}"}
                            }
                        }
                    }
                });

                FUiElementsName.Add(name);
                return name;
            }

            public string AddImage(string name, double left, double top, double width, double height, UIColor color, string url = "http://oxidemod.org/data/avatars/l/53/53411.jpg?1427487325", string parent = "Overlay")
            {
                FUiElements.Add(new Dictionary<string, object> {
                    {"name", name},
                    {"parent", parent},
                    {"components",
                        new List<object> {
                            new Dictionary<string, string> {
                                {"type", "UnityEngine.UI.Button"},
                                {"sprite", "assets/content/textures/generic/fulltransparent.tga"},
                                {"url", url},
                                {"color", color.ToString()},
                                {"imagetype", "Tiled"}
                            },

                            new Dictionary<string, string> {
                                {"type", "RectTransform"},
                                {"anchormin", $"{left.ToString().Replace(",", ".")} {((1 - top) - height).ToString().Replace(",", ".")}"},
                                {"anchormax", $"{(left + width).ToString().Replace(",", ".")} {(1 - top).ToString().Replace(",", ".")}"}
                            }
                        }
                    }
                });

                FUiElementsName.Add(name);
                return name;
            }
        }

        #endregion

        #region Methods
        private static List<DottedFieldDescription> GetGenericValueNames(Object obj, DottedFieldDescription parentInfo)
        {
            List<DottedFieldDescription> valueNames = new List<DottedFieldDescription>();
            Type type = obj.GetType();

            PropertyInfo count = type.GetProperty("Count");
            if (count != null)
                parentInfo.Value = count.GetValue(obj).ToString();

            MethodInfo enumerater = type.GetMethod("GetEnumerator");
            if (enumerater != null)
            {
                int index = 0;
                IEnumerable myEnum = obj as IEnumerable;
                IEnumerator myEnumerator = myEnum.GetEnumerator();
                while (myEnumerator.MoveNext())
                {
                    DottedFieldDescription currentInfo = new DottedFieldDescription();
                    currentInfo.Name = "_" + (index++).ToString();
                    Object currentElement = myEnumerator.Current;
                    Type currentType = currentElement.GetType();
                    if (currentType.IsPrimitive || (!currentType.IsGenericType && currentType.IsSecurityTransparent))
                    {
                        currentInfo.Type = currentType.Name;
                        currentInfo.Value = currentElement.ToString();
                        valueNames.Add(currentInfo);
                    }
                    else
                    {
                        foreach (DottedFieldDescription info in GetPropertyValueNames(currentElement))
                        {
                            info.Name = currentInfo.Name + "." + info.Name;
                            valueNames.Add(info);
                        }
                    }
                }
            }

            return valueNames;
        }

        private static List<DottedFieldDescription> GetPropertyValueNames(Object obj)
        {
            List<DottedFieldDescription> valueNames = new List<DottedFieldDescription>();
            Type type = obj.GetType();
            PropertyInfo[] props = type.GetProperties();
            foreach (var prop in props)
            {
                DottedFieldDescription currentDottedInformation = new DottedFieldDescription();
                currentDottedInformation.Name = prop.Name;
                currentDottedInformation.Type = prop.PropertyType.Name;
                if (prop.PropertyType.IsPrimitive)
                {
                    currentDottedInformation.Value = prop.GetValue(obj).ToString();
                    valueNames.Add(currentDottedInformation);
                }
                else if (!prop.PropertyType.IsGenericType)
                {
                    if (prop.PropertyType.IsSealed)
                    {
                        currentDottedInformation.Value = prop.GetValue(obj).ToString();
                        valueNames.Add(currentDottedInformation);
                    }
                    else
                    {
                        currentDottedInformation.IsClass = true;
                        valueNames.Add(currentDottedInformation);
                        foreach (DottedFieldDescription info in GetPropertyValueNames(prop.GetValue(obj)))
                        {
                            info.Name = currentDottedInformation.Name + "." + info.Name;
                            valueNames.Add(info);
                        }
                    }
                }
                else if (prop.PropertyType.IsGenericType)
                {
                    currentDottedInformation.IsContainer = true;
                    valueNames.Add(currentDottedInformation);
                    foreach (DottedFieldDescription info in GetGenericValueNames(prop.GetValue(obj), currentDottedInformation))
                    {
                        info.Name = currentDottedInformation.Name + info.Name;
                        valueNames.Add(info);
                    }
                }
                else if (prop.PropertyType.IsClass)
                {
                    currentDottedInformation.IsClass = true;
                    valueNames.Add(currentDottedInformation);
                    foreach (DottedFieldDescription info in GetPropertyValueNames(prop.GetValue(obj)))
                    {
                        info.Name = currentDottedInformation.Name + "." + info.Name;
                        valueNames.Add(info);
                    }
                }
            }
            return valueNames;
        }

        private bool ModifyConfigurationData(BasePlayer parPlayer, string parName, string parNewValue, ConfigurationData outConfigurationData)
        {
            string[] tokens = parName.Split('.');
            Object currentObject = outConfigurationData;
            for (uint i = 0; i < tokens.Length; ++i)
            {
                string token = tokens[i];
                if (!token.Contains("_"))
                {
                    Type type = currentObject.GetType();
                    PropertyInfo property = type.GetProperty(token);

                    if (property == null)
                    {
                        currentObject = null;
                        break;
                    }

                    if (i == tokens.Length - 1)
                    {
                        SendMessageToPlayer(LogLevel.Debug, parPlayer, $"property.PropertyType.IsEnum= {property.PropertyType.IsEnum}");
                        if (property.PropertyType.IsEnum)
                            SendMessageToPlayer(LogLevel.Debug, parPlayer, $"value= {Enum.Parse(property.PropertyType, parNewValue)}");

                        property.SetValue(currentObject, property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, parNewValue) : Convert.ChangeType(parNewValue, property.PropertyType), null);
                        return true;
                    }
                    else
                        currentObject = property.GetValue(currentObject);
                }
            }

            return false;
        }

        private string GetFormettedAndTranslatedText(string parLangKey, string parPlayerIdString = null, params object[] parArguments)
        {
            return string.Format(lang.GetMessage(parLangKey, this, parPlayerIdString), parArguments);
        }

        // send a message through oxide console
        private void SendMessage(LogLevel parLevel, string parText, string parPlayerIdString)
        {
            // fallback in case of problem
            if (FConfiguration == null || GetConfiguration(parPlayerIdString) == null)
            {
                Puts(format: $"[Fallback] {parLevel.ToString()}> {parText}");
                return;
            }

            if (GetConfiguration(parPlayerIdString).LogConfiguration.LogLevelToDisplay > parLevel)
                return;

            string text = GetFormettedAndTranslatedText("Oxide" + parLevel.ToString(), null, System.DateTime.Now, parText);
            if (text == null)
                return;

            switch (parLevel)
            {
                case LogLevel.Debug:
                case LogLevel.Log:
                    Puts(text);
                    break;
                case LogLevel.Warning:
                    PrintWarning(text);
                    break;
                case LogLevel.Error:
                    PrintError(text);
                    break;
                default:
                    break;
            }
        }

        private void RegisterPermissionWithOverwriteFromConfiguration(string parPlayerIdString = null)
        {
            // check the new permissions
            Dictionary<string, string> changedPermission = new Dictionary<string, string>();
            List<string> permissionToRegister = new List<string>() { AdminPermission };
            PermissionConfigurationData permissionConfiguration = GetConfiguration(parPlayerIdString).PermissionConfiguration;
            foreach (var permKeyAndValue in permissionConfiguration.OverwritedCommandsPermission)
            {
                string permissionName = permKeyAndValue.Value;
                if (!permissionName.StartsWith(PluginNameLowered))
                {
                    permissionName = PluginNameLowered + "." + permissionName;
                    changedPermission[permKeyAndValue.Key] = permissionName;
                }

                SendMessage(LogLevel.Log, $"Command= '{permKeyAndValue.Key}', overridedPermission= '{permissionName}'", parPlayerIdString);
                if (!permissionToRegister.Contains(permissionName))
                    permissionToRegister.Add(permissionName);
            }

            // Register permissions for commands
            foreach (string permissionName in permissionToRegister)
                permission.RegisterPermission(permissionName, this);


            // update the config with the new permission
            if (changedPermission.Count > 0)
            {
                foreach (var newPerm in changedPermission)
                    permissionConfiguration.OverwritedCommandsPermission[newPerm.Key] = newPerm.Value;
                SaveConfig();
            }
        }

        private void SendMessageToPlayer(LogLevel parLevel, BasePlayer parPlayer, string parText, params object[] parArguments)
        {
            string formattedAndTranslatedText = GetFormettedAndTranslatedText(parText, parPlayer?.UserIDString, parArguments);
            LogConfigurationData logConfiguration = GetConfiguration(parPlayer?.UserIDString).LogConfiguration;
            if (parPlayer == null || logConfiguration.NeverLogToPlayer)
            {
                SendMessage(parLevel, GetFormettedAndTranslatedText("OverwriteWithPlayer", parPlayer?.UserIDString, parPlayer?.displayName, formattedAndTranslatedText), parPlayer?.UserIDString);
                return;
            }

            if (logConfiguration.LogLevelToDisplay > parLevel)
                return;

            if (logConfiguration.DoNotDisplayMsgToPlayer.Contains(parText))
            {
                if (logConfiguration.DisplayToConsoleIfPlayerIsNotAllowToSeeMsg)
                    SendMessage(parLevel, GetFormettedAndTranslatedText("OverwriteWithPlayer", parPlayer?.UserIDString, parPlayer?.displayName, formattedAndTranslatedText), parPlayer?.UserIDString);
                return;
            }

            if (logConfiguration.LogToConsoleOverChat)
                PrintToConsole(parPlayer, GetFormettedAndTranslatedText(parLevel.ToString(), parPlayer.UserIDString, formattedAndTranslatedText));
            else
                PrintToChat(parPlayer, GetFormettedAndTranslatedText(parLevel.ToString(), parPlayer.UserIDString, formattedAndTranslatedText));
        }

        private bool HasPermission(IPlayer parIPlayer, string parCommandName)
        {
            string permissionToTest;
            PermissionConfigurationData permissionConfiguration = GetConfiguration(parIPlayer?.Id).PermissionConfiguration;
            if (!permissionConfiguration.OverwritedCommandsPermission.TryGetValue(parCommandName, out permissionToTest))
                permissionToTest = AdminPermission;

            return parIPlayer.HasPermission(permissionToTest);
        }
        #endregion

        #region Public Hook

        #endregion

    }
}