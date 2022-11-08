using BlockEntities;
using ModLoaderInterfaces;
using Newtonsoft.Json;
using Pipliz;
using System.Collections.Generic;
using System.IO;

using Chatting;
using Chatting.Commands;
using System.Linq;

namespace TeleportPad
{
    [BlockEntityAutoLoader]
    [ModLoader.ModManager]
    public class Teleporter : IAfterWorldLoad, IOnQuit, IOnAutoSaveWorld, IChangedWithType, ISingleBlockEntityMapping, IOnPlayerMoved
    {
        public string teleportPadFile;

        public static List<(Players.PlayerID, Vector3Int)> lastPlaced = new List<(Players.PlayerID, Vector3Int)>();
        public static Dictionary<Vector3Int, Vector3Int> padPair = new Dictionary<Vector3Int, Vector3Int>();

        public static readonly long timeBetweenTeleport = 5000L;
        public static Dictionary<Players.PlayerID, ServerTimeStamp> lastTeleport = new Dictionary<Players.PlayerID, ServerTimeStamp>();

        ItemTypes.ItemType ISingleBlockEntityMapping.TypeToRegister => ItemTypes.GetType("Khanx.TeleportPad");

        public void OnChangedWithType(Chunk chunk, BlockChangeRequestOrigin requestOrigin, Vector3Int blockPosition, ItemTypes.ItemType typeOld, ItemTypes.ItemType typeNew)
        {
            //OnRemove
            if (typeNew == BlockTypes.BuiltinBlocks.Types.air)
            {
                int i = lastPlaced.FindIndex(x => x.Item2 == blockPosition);

                if (i != -1)    //Removed lastPlaced
                    lastPlaced.RemoveAt(i);
                else if (padPair.TryGetValue(blockPosition, out Vector3Int pairedPad))
                {
                    padPair.Remove(blockPosition);
                    padPair.Remove(pairedPad);
                    Chat.SendToConnected("Removed teleport pairs");
                }
            }

            //OnAdd
            if (typeOld == BlockTypes.BuiltinBlocks.Types.air)
            {
                if (requestOrigin.Type != BlockChangeRequestOrigin.EType.Player)
                    return;

                Players.Player player = requestOrigin.AsPlayer;

                int i = lastPlaced.FindIndex(x => x.Item1 == player.ID);

                if(i == -1) //First pad placed
                {
                    lastPlaced.Add((player.ID, blockPosition));
                    Chat.Send(player, "You need to place a second pad teleported without disconecting to pair both pads.");
                }
                else //Second pair placed
                {
                    Vector3Int pairedPair = lastPlaced[i].Item2;
                    lastPlaced.RemoveAt(i);

                    padPair.Add(blockPosition, pairedPair);
                    padPair.Add(pairedPair, blockPosition);
                    Chat.Send(player, "Pads paired.");
                }
            }
        }

        //Add a time between messages?
        public void OnPlayerMoved(Players.Player player, UnityEngine.Vector3 oldLocation)
        {
            Vector3Int posDown = new Vector3Int(player.Position) + Vector3Int.down;

            if (!padPair.TryGetValue(posDown, out Vector3Int pairedPad))
            {
                if (World.TryGetTypeAt(posDown, out ItemTypes.ItemType type) && type.Name.Equals("Khanx.TeleportPad"))
                {
                    Chat.Send(player, "Teleport pad not paired.");
                }
                return;
            }

            if (lastTeleport.TryGetValue(player.ID, out ServerTimeStamp time) && time.TimeSinceThis < timeBetweenTeleport)
            {
                Chat.Send(player, "There is a cooldown of 5s between teleports.");
                return;
            }

            Teleport.TeleportTo(player, (pairedPad + Vector3Int.up).Vector);
            Chat.Send(player, "Teleported");

            if (lastTeleport.ContainsKey(player.ID))
                lastTeleport.Remove(player.ID);

            lastTeleport.Add(player.ID, ServerTimeStamp.Now);
        }

        public void AfterWorldLoad()
        {
            teleportPadFile = "./gamedata/savegames/" + ServerManager.WorldName + "/teleportPad.json";

            if (!File.Exists(teleportPadFile))
                return;

            var tempPair = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(teleportPadFile));
            padPair = tempPair.ToDictionary(pair => Vector3Int.Parse(pair.Key), pair => Vector3Int.Parse(pair.Value));
        }

        public void Save()
        {
            if (File.Exists(teleportPadFile))
                File.Delete(teleportPadFile);

            if (padPair.Count == 0)
                return;

            string json = JsonConvert.SerializeObject(padPair.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()));

            File.WriteAllText(teleportPadFile, json);
            Log.Write("<color=red>Saved Teleport Pads</color>");
        }

        public void OnQuit()
        {
            Save();
        }

        public void OnAutoSaveWorld()
        {
            Save();
        }
    }
}
