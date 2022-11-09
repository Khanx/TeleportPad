using BlockEntities;
using BlockEntities.Helpers;
using Chatting;
using Pipliz;
using System.Collections.Generic;

namespace TeleportPad
{
    [BlockEntityAutoLoader]
    public class TeleportPadTracker : IEntityManager, IMultiBlockEntityMapping, ILoadedWithDataByPositionType, IChangedWithType
    {
        public class TeleportPad : IBlockEntityKeepLoaded, IBlockEntity, IBlockEntitySerializable
        {
            public Vector3Int pair = Vector3Int.invalidPos;

            public TeleportPad()
            {
                this.pair = Vector3Int.invalidPos;
            }

            public TeleportPad(Vector3Int pair)
            {
                this.pair = pair;
            }

            public EKeepChunkLoadedResult OnKeepChunkLoaded(Vector3Int blockPosition)
            {
                return EKeepChunkLoadedResult.YesLong;
            }

            public ESerializeEntityResult SerializeToBytes(Chunk chunk, Vector3Byte blockPosition, ByteBuilder builder)
            {
                builder.WriteVariable(pair);
                return ESerializeEntityResult.WroteData | ESerializeEntityResult.LoadChunkOnStartup;
            }
        }

        public IEnumerable<ItemTypes.ItemType> TypesToRegister { get { return types; } }

        readonly ItemTypes.ItemType[] types = new ItemTypes.ItemType[]
        {
                ItemTypes.GetType("Khanx.TeleportPad"), //Enabled
                ItemTypes.GetType("Khanx.TeleportPadD") //Disabled
        };

        public InstanceTracker<TeleportPad> Positions
        {
            get;
            protected set;
        }

        public TeleportPadTracker()
        {
            Positions = new InstanceTracker<TeleportPad>();
            lastPlaced = new List<(Players.PlayerID, Vector3Int)>();
        }

        public void OnLoadedWithDataPosition(Chunk chunk, Vector3Int blockPosition, ushort type, ByteReader reader)
        {
            if (reader != null)
            {
                TeleportPad teleportPad = new TeleportPad(reader.ReadVariableVector3Int());
                if (chunk.GetOrAllocateEntities().Add(blockPosition, teleportPad))
                {
                    Positions.TryAdd(blockPosition, teleportPad);
                }
            }
        }

        public static List<(Players.PlayerID, Vector3Int)> lastPlaced;

        public void OnChangedWithType(Chunk chunk, BlockChangeRequestOrigin requestOrigin, Vector3Int blockPosition, ItemTypes.ItemType typeOld, ItemTypes.ItemType typeNew)
        {
            if (requestOrigin.Type != BlockChangeRequestOrigin.EType.Player)
                return;

            Players.Player player = requestOrigin.AsPlayer;

            //OnRemove
            if (typeNew == BlockTypes.BuiltinBlocks.Types.air)
            {
                int i = lastPlaced.FindIndex(x => x.Item2 == blockPosition);

                if (i != -1)    //Removed lastPlaced
                    lastPlaced.RemoveAt(i);
                else if (Positions.TryGetValue(blockPosition, out TeleportPad teleportPad1))    //Removed a teleportPad from a pair
                {
                    if (Positions.TryGetValue(teleportPad1.pair, out TeleportPad teleportPad2))
                    {
                        teleportPad2.pair = Vector3Int.invalidPos;

                        lastPlaced.Add((player.ID, teleportPad1.pair));
                        Chat.Send(player, "Removed Teleport Pad, next time that you place a Teleport Pad without disconnecting will connect the other pair of the one that you removed.");
                        ServerManager.TryChangeBlock(teleportPad1.pair, ItemTypes.GetType("Khanx.TeleportPadD"), player);   //Disabled Skin
                    }
                    else
                        Chat.Send(player, "Removed Teleport Pad");
                }

                Positions.TryRemove(blockPosition, out _);
                chunk.GetEntities()?.Remove(blockPosition);
            }

            //OnAdd
            if (typeOld == BlockTypes.BuiltinBlocks.Types.air)
            {
                int i = lastPlaced.FindIndex(x => x.Item1 == player.ID);

                TeleportPad teleportPad = new TeleportPad();

                if (i == -1) //First pad placed
                {
                    lastPlaced.Add((player.ID, blockPosition));
                    Chat.Send(player, "You need to place a second Teleport Pad without disconnecting to pair both pads.");
                }
                else  //Second pad placed
                {
                    Positions.TryGetValue(lastPlaced[i].Item2, out TeleportPad teleportPad2);

                    teleportPad.pair = lastPlaced[i].Item2;
                    teleportPad2.pair = blockPosition; 

                    lastPlaced.RemoveAt(i);
                    Chat.Send(player, "Teleport Pads paired.");

                    //Enabled Skin
                    ServerManager.TryChangeBlock(teleportPad.pair, ItemTypes.GetType("Khanx.TeleportPad"), player);
                    ServerManager.TryChangeBlock(teleportPad2.pair, ItemTypes.GetType("Khanx.TeleportPad"), player);

                    World.GetChunk(teleportPad.pair.ToChunk()).SetDirty();
                    World.GetChunk(teleportPad2.pair.ToChunk()).SetDirty();
                }

                if (chunk.GetOrAllocateEntities().Add(blockPosition, teleportPad))
                {
                    Positions.TryAdd(blockPosition, teleportPad);
                }
            }
        }

    }
}
