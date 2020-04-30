using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                    Name = "ConfigurationDetail",
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

            FDottedConfiguration = GetPropertyValueNames(FConfiguration);
        }

        // Only called if the config file does not already exist
        protected override void LoadDefaultConfig()
        {
            FConfiguration = Configuration.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(FConfiguration);

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
                ["Debug"] = "<color=gray>[DEBUG]</color> {0}",
                ["Log"] = "<color=white>[LOG]</color> {0}",
                ["Warning"] = "<color=orange>[WARNING]</color> {0}",
                ["Error"] = "<color=red>[ERROR]</color> {0}",
                ["OxideDebug"] = "[DEBUG] [{0}] {1}",
                ["OxideLog"] = "[LOG] [{0}] {1}",
                ["OxideWarning"] = "[WARNING] [{0}] {1}",
                ["OxideError"] = "[ERROR] [{0}] {1}",
                ["BadUsage"] = "The given parameters did not fit the required ones",
                ["Usage" + ModifyDefaultConfigurationCommandName] = "Usage> " + ModifyDefaultConfigurationActionName + " [list|show|change|add|del]\n- list : display all the fields with their type and value \n- show <field> : display the value of the field\n- modify <field> [<key>] <value> : modify the value of the field, if the field is a dictionary, the key is required\n- add <field> [<key>] <value> add a new value to the field, if the field is a dictionary, the key is required\n- del <field> <value> : delete a value (or a key/value) from a field",
                ["Usage" + ModifySpecificConfigurationCommandName] = "Usage> " + ModifySpecificConfigurationActionName + " [create|list|show|change|add|del]\n-create <configName> : create a new config if not already present and duplicate the default value \n- list <configName> : display all the fields with their type and value <configName>\n- show <configName> <field> : display the value of the field\n- modify <configName> <field> [<key>] <value> : modify the value of the field, if the field is a dictionary, the key is required\n- add <configName> <field> [<key>] <value> add a new value to the field, if the field is a dictionary, the key is required\n- del <configName> <field> <value> : delete a value (or a key/value) from a field"
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

        List<DottedFieldDescription> FDottedConfiguration = new List<DottedFieldDescription>();

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
        private const string PluginNameLowered = "basicframework";
        private const string AdminPermission = PluginNameLowered + ".admin";

        private const string ModifyDefaultConfigurationActionName = "defaultConfig";
        private const string ModifyDefaultConfigurationCommandName = "ModifyDefaultConfiguration";

        private const string ModifySpecificConfigurationActionName = "specificConfig";
        private const string ModifySpecificConfigurationCommandName = "modifySpecificConfiguration";

        // Called when a plugin is being initialized
        private void Init()
        {
            // register chat/console command
            AddCovalenceCommand(ModifyDefaultConfigurationActionName, ModifyDefaultConfigurationCommandName);
            AddCovalenceCommand(ModifySpecificConfigurationActionName, ModifySpecificConfigurationCommandName);

            // register permissions
            RegisterPermissionWithOverwriteFromConfiguration();
        }

        // Called after the server startup has been completed and is awaiting connections
        void OnServerInitialized(bool parServerInitialized)
        {
        }

        // Called when a plugin is being unloaded => Save data, nullify static variables, etc.
        private void Unload()
        {
        }

        #endregion Initialization

        #region Commands

        private void ModifyDefaultConfiguration(IPlayer parIPlayer, string parCommandName, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parIPlayer.Object;

            // check the player conversion
            if (player == null)
            {
                SendMessage(LogLevel.Error, GetFormettedAndTranslatedText("NotAPlayer", parIPlayer.Id));
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

            ConfigurationData configuration = GetConfiguration();
            LogConfigurationData logConfiguration = configuration.LogConfiguration;
            if (parArguments[0] == "list")
            {
                foreach (DottedFieldDescription element in FDottedConfiguration)
                    SendMessage(LogLevel.Log, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}");

                //// clone data
                //SendMessage(LogLevel.Log, "On clone les data");
                //ConfigurationData data = Clone(configuration);
                //FConfiguration.UserConfiguration.Add(player.UserIDString, data);
                //dottedConfig = GetPropertyValueNames(FConfiguration);
                //foreach (DottedFieldDescription element in dottedConfig)
                //    SendMessage(LogLevel.Log, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}");
                return;
            }

            SendMessageToPlayer(LogLevel.Log, player, "HasPermission", player.UserIDString, player.displayName, player.userID);
            SendMessage(LogLevel.Log, $"value before {logConfiguration.LogLevelToDisplay}");
            if (ModifyConfigurationData(player, parArguments, configuration))
                SaveConfig();
            SendMessage(LogLevel.Log, $"value after {logConfiguration.LogLevelToDisplay}");
        }

        private void modifySpecificConfiguration(IPlayer parIPlayer, string parCommandName, string[] parArguments)
        {
            BasePlayer player = (BasePlayer)parIPlayer.Object;

            // check the player conversion
            if (player == null)
            {
                SendMessage(LogLevel.Error, GetFormettedAndTranslatedText("NotAPlayer", parIPlayer.Id));
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
                SendMessageToPlayer(LogLevel.Log, player, "Usage" + ModifySpecificConfigurationCommandName, parIPlayer.Id);
                return;
            }

            ConfigurationData configuration = GetConfiguration();
            LogConfigurationData logConfiguration = configuration.LogConfiguration;
            if (parArguments[0] == "list")
            {
                foreach (DottedFieldDescription element in FDottedConfiguration)
                    SendMessage(LogLevel.Log, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}");

                //// clone data
                //SendMessage(LogLevel.Log, "On clone les data");
                //ConfigurationData data = Clone(configuration);
                //FConfiguration.UserConfiguration.Add(player.UserIDString, data);
                //dottedConfig = GetPropertyValueNames(FConfiguration);
                //foreach (DottedFieldDescription element in dottedConfig)
                //    SendMessage(LogLevel.Log, $"{element.Name} Type={element.Type} IsContainer={element.IsContainer} IsClass={element.IsClass} Value={element.Value}");
                return;
            }

            SendMessageToPlayer(LogLevel.Log, player, "HasPermission", player.UserIDString, player.displayName, player.userID);
            SendMessage(LogLevel.Log, $"value before {logConfiguration.LogLevelToDisplay}");
            if (ModifyConfigurationData(player, parArguments, configuration))
                SaveConfig();
            SendMessage(LogLevel.Log, $"value after {logConfiguration.LogLevelToDisplay}");
        }

        #endregion Commands

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

        private bool ModifyConfigurationData(BasePlayer player, string[] parArguments, ConfigurationData outConfigurationData)
        {
            outConfigurationData.LogConfiguration.LogLevelToDisplay = LogLevel.Debug;
            return true;
        }

        private string GetFormettedAndTranslatedText(string parLangKey, string parPlayerIdString = null, params object[] parArguments)
        {
            return string.Format(lang.GetMessage(parLangKey, this, parPlayerIdString), parArguments);
        }

        // send a message through oxide console
        private void SendMessage(LogLevel parLevel, string parText)
        {
            // fallback in case of problem
            if (FConfiguration == null || GetConfiguration() == null)
            {
                Puts(format: $"[Fallback] {parLevel.ToString()}> {parText}");
                return;
            }

            if (GetConfiguration().LogConfiguration.LogLevelToDisplay > parLevel)
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

        private void RegisterPermissionWithOverwriteFromConfiguration()
        {
            // check the new permissions
            Dictionary<string, string> changedPermission = new Dictionary<string, string>();
            List<string> permissionToRegister = new List<string>() { AdminPermission };
            PermissionConfigurationData permissionConfiguration = GetConfiguration().PermissionConfiguration;
            foreach (var permKeyAndValue in permissionConfiguration.OverwritedCommandsPermission)
            {
                string permissionName = permKeyAndValue.Value;
                if (!permissionName.StartsWith(PluginNameLowered))
                {
                    permissionName = PluginNameLowered + "." + permissionName;
                    changedPermission[permKeyAndValue.Key] = permissionName;
                }

                SendMessage(LogLevel.Log, $"Command= '{permKeyAndValue.Key}', overridedPermission= '{permissionName}'");
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
                SendMessage(parLevel, GetFormettedAndTranslatedText("OverwriteWithPlayer", parPlayer?.UserIDString, parPlayer?.displayName, formattedAndTranslatedText));
                return;
            }

            if (logConfiguration.LogLevelToDisplay > parLevel)
                return;

            if (logConfiguration.DoNotDisplayMsgToPlayer.Contains(parText))
            {
                if (logConfiguration.DisplayToConsoleIfPlayerIsNotAllowToSeeMsg)
                    SendMessage(parLevel, GetFormettedAndTranslatedText("OverwriteWithPlayer", parPlayer?.UserIDString, parPlayer?.displayName, formattedAndTranslatedText));
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