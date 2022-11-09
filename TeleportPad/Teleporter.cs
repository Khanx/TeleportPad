using BlockEntities;
using Chatting;
using ModLoaderInterfaces;
using Pipliz;
using System.Collections.Generic;

namespace TeleportPad
{
    [ModLoader.ModManager]
    public class Teleporter : IOnPlayerMoved
    {
        public static readonly long timeBetweenTeleport = 5000L;
        public static Dictionary<Players.PlayerID, (ServerTimeStamp, bool)> lastTeleport = new Dictionary<Players.PlayerID, (ServerTimeStamp, bool)>();


        private static TeleportPadTracker tracker = null;
        public static TeleportPadTracker teleportPadTracker { get { if (tracker == null) { ServerManager.BlockEntityCallbacks.TryGetAutoLoadedInstance(out tracker); } return tracker; } }


        public void OnPlayerMoved(Players.Player player, UnityEngine.Vector3 oldLocation)
        {
            Vector3Int posDown = new Vector3Int(player.Position) + Vector3Int.down;

            if (!teleportPadTracker.Positions.TryGetValue(posDown, out TeleportPadTracker.TeleportPad teleportPad))
                return;

            if (teleportPad.pair == Vector3Int.invalidPos)
            {
                Chat.Send(player, "Teleport pad not paired.");

                return;
            }

            if (lastTeleport.TryGetValue(player.ID, out var time) && time.Item1.TimeSinceThis < timeBetweenTeleport)
            {
                if (time.Item2 == false)    //Send the message only one time
                {
                    Chat.Send(player, "There is a cooldown of 5s between teleports.");

                    lastTeleport.Remove(player.ID);
                    lastTeleport.Add(player.ID, (time.Item1, true));
                }

                return;
            }

            Chatting.Commands.Teleport.TeleportTo(player, (teleportPad.pair + Vector3Int.up).Vector);
            Chat.Send(player, "Teleported");

            if (lastTeleport.ContainsKey(player.ID))
                lastTeleport.Remove(player.ID);

            lastTeleport.Add(player.ID, (ServerTimeStamp.Now, false));
        }
    }
}
