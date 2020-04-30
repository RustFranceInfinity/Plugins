using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Simple Mp plugin", "Ash", 0.1)]
    [Description("Send a mp to an active player")]

    public class SimpleMpPlugin : RustPlugin
    {
        [ChatCommand("mp")]
        void SpeakTo(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 1)
            {
                string receiverName = args[0];
                string msg = player.displayName + "> ";
                for (int i = 1; i < args.Length; ++i)
                    msg += args[i] + " ";

                List<BasePlayer> closeOnes = new List<BasePlayer>();
                BasePlayer receiver = null;
                foreach (var activePlayer in BasePlayer.activePlayerList)
                {
                    if (activePlayer.displayName == receiverName || activePlayer.UserIDString == receiverName)
                    {
                        receiver = activePlayer;
                        break;
                    }
                    else if (activePlayer.displayName.Contains(receiverName))
                    {
                        closeOnes.Add(activePlayer);
                    }
                }

                if (receiver != null || closeOnes.Count == 1)
                {
                    SendMessage(receiver != null ? receiver : closeOnes[0], msg);
                }
                else
                {
                    SendMessage(player, "Unknown player '" + receiverName + "'");
                    if (closeOnes.Count > 0)
                    {
                        SendMessage(player, "Closest player name found");
                        foreach (BasePlayer str in closeOnes)
                            SendMessage(player, "\t" + str.displayName);
                    }
                }
            }
            else
            {
                SendMessage(player, "Usage: /mp <playerName> <message>");
            }
        }

        void SendMessage(BasePlayer player, string msg, params object[] args)
        {
            PrintToChat(player, msg, args);
        }
    }
}
