using System.Linq;
using Multiplayer.Common;
using Multiplayer.Common.Networking;
using Multiplayer.Common.Networking.Connection;
using Multiplayer.Common.Networking.Handler;

namespace Multiplayer.Server.Networking.Handler
{
    public class ServerGameplayPacketHandler : MpPacketHandler
    {
        public const int MaxChatMsgLength = 128;

        public ServerGameplayPacketHandler(BaseMultiplayerConnection conn) : base(conn)
        {
        }

        [HandlesPacket(Packet.Client_WorldReady)]
        public void HandleWorldReady(ByteReader data)
        {
            Player.UpdateStatus(ServerPlayer.Status.Playing);
        }

        [HandlesPacket(Packet.Client_Desynced)]
        public void HandleDesynced(ByteReader data)
        {
            Player.UpdateStatus(ServerPlayer.Status.Desynced);
        }

        [HandlesPacket(Packet.Client_Command)]
        public void HandleClientCommand(ByteReader data)
        {
            var cmd = (CommandType) data.ReadInt32();
            var mapId = data.ReadInt32();
            var extra = data.ReadPrefixedBytes(32767);

            // todo check if map id is valid for the player

            var factionId = MultiplayerServer.instance.playerFactions[connection.username];
            MultiplayerServer.instance.SendCommand(cmd, factionId, mapId, extra, Player);
        }

        [HandlesPacket(Packet.Client_Chat)]
        public void HandleChat(ByteReader data)
        {
            var msg = data.ReadString();
            msg = msg.Trim();

            // todo handle max length
            if (msg.Length == 0) return;

            if (msg[0] == '/')
            {
                var cmd = msg.Substring(1);
                var parts = cmd.Split(' ');
                var handler = Server.GetCmdHandler(parts[0]);

                if (handler != null)
                {
                    if (handler.requiresHost && Player.Username != Server.hostUsername)
                        Player.SendChat("No permission");
                    else
                        handler.Handle(Player, parts.SubArray(1));
                }
                else
                {
                    Player.SendChat("Invalid command");
                }
            }
            else
            {
                Server.SendChat($"{connection.username}: {msg}");
            }
        }

        [HandlesPacket(Packet.Client_AutosavedData)]
        [IsFragmented]
        public void HandleAutosavedData(ByteReader data)
        {
            var arbiter = Server.ArbiterPlaying;
            if (arbiter && !Player.IsArbiter) return;
            if (!arbiter && Player.Username != Server.hostUsername) return;

            var maps = data.ReadInt32();
            for (var i = 0; i < maps; i++)
            {
                var mapId = data.ReadInt32();
                Server.mapData[mapId] = data.ReadPrefixedBytes();
            }

            Server.savedGame = data.ReadPrefixedBytes();

            if (Server.tmpMapCmds != null)
            {
                Server.mapCmds = Server.tmpMapCmds;
                Server.tmpMapCmds = null;
            }
        }

        [HandlesPacket(Packet.Client_Cursor)]
        public void HandleCursor(ByteReader data)
        {
            if (Player.lastCursorTick == Server.netTimer) return;

            var writer = new ByteWriter();

            var seq = data.ReadByte();
            var map = data.ReadByte();

            writer.WriteInt32(Player.id);
            writer.WriteByte(seq);
            writer.WriteByte(map);

            if (map < byte.MaxValue)
            {
                var icon = data.ReadByte();
                var x = data.ReadShort();
                var z = data.ReadShort();

                writer.WriteByte(icon);
                writer.WriteShort(x);
                writer.WriteShort(z);

                var dragX = data.ReadShort();
                writer.WriteShort(dragX);

                if (dragX != -1)
                {
                    var dragZ = data.ReadShort();
                    writer.WriteShort(dragZ);
                }
            }

            Player.lastCursorTick = Server.netTimer;

            Server.SendToAll(Packet.Server_Cursor, writer.ToArray(), false, Player);
        }

        [HandlesPacket(Packet.Client_Selected)]
        public void HandleSelected(ByteReader data)
        {
            var reset = data.ReadBool();

            var writer = new ByteWriter();

            writer.WriteInt32(Player.id);
            writer.WriteBool(reset);
            writer.WritePrefixedInts(data.ReadPrefixedInts(100));
            writer.WritePrefixedInts(data.ReadPrefixedInts(100));

            Server.SendToAll(Packet.Server_Selected, writer.ToArray(), excluding: Player);
        }

        [HandlesPacket(Packet.Client_IdBlockRequest)]
        public void HandleIdBlockRequest(ByteReader data)
        {
            var mapId = data.ReadInt32();

            if (mapId == ScheduledCommand.Global)
            {
                //IdBlock nextBlock = MultiplayerServer.instance.NextIdBlock();
                //MultiplayerServer.instance.SendCommand(CommandType.GlobalIdBlock, ScheduledCommand.NoFaction, ScheduledCommand.Global, nextBlock.Serialize());
            }
        }

        [HandlesPacket(Packet.Client_KeepAlive)]
        public void HandleClientKeepAlive(ByteReader data)
        {
            var id = data.ReadInt32();
            var ticksBehind = data.ReadInt32();

            Player.ticksBehind = ticksBehind;

            // Latency already handled by LiteNetLib
            if (connection is MpNetConnection) return;

            if (MultiplayerServer.instance.keepAliveId == id)
                connection.Latency = (int) MultiplayerServer.instance.lastKeepAlive.ElapsedMilliseconds / 2;
            else
                connection.Latency = 2000;
        }

        [HandlesPacket(Packet.Client_SyncInfo)]
        [IsFragmented]
        public void HandleDesyncCheck(ByteReader data)
        {
            var arbiter = Server.ArbiterPlaying;
            if (arbiter && !Player.IsArbiter) return;
            if (!arbiter && Player.Username != Server.hostUsername) return;

            var raw = data.ReadRaw(data.Left);
            foreach (var p in Server.PlayingPlayers.Where(p => !p.IsArbiter))
                p.conn.SendFragmented(Packet.Server_SyncInfo, raw);
        }

        [HandlesPacket(Packet.Client_Pause)]
        public void HandlePause(ByteReader data)
        {
            var pause = data.ReadBool();
            if (pause && Player.Username != Server.hostUsername) return;
            if (Server.paused == pause) return;

            Server.paused = pause;
            Server.SendToAll(Packet.Server_Pause, new object[] {pause});
        }

        [HandlesPacket(Packet.Client_Debug)]
        public void HandleDebug(ByteReader data)
        {
            if (!MpVersion.IsDebug) return;

            Server.PlayingPlayers.FirstOrDefault(p => p.IsArbiter)
                ?.SendPacket(Packet.Server_Debug, data.ReadRaw(data.Left));
        }

        [HandlesPacket(Packet.Client_RequestRemoteStacks)]
        public void ForwardRemoteStacksRequest(ByteReader data)
        {
            var id = data.ReadInt32();
            var requesterId = data.ReadInt32();
            var diffTick = data.ReadInt32();
            var offsetInTick = data.ReadInt32();

            Server.PlayingPlayers.FirstOrDefault(p => p.id == id)?.SendPacket(Packet.Server_RequestRemoteStacks,
                new object[] {requesterId, diffTick, offsetInTick});
        }

        [HandlesPacket(Packet.Client_ResponseRemoteStacks)]
        public void ForwardRemoteStacksResponse(ByteReader data)
        {
            var destId = data.ReadInt32();
            Server.PlayingPlayers.FirstOrDefault(p => p.id == destId)
                ?.SendPacket(Packet.Server_ResponseRemoteStacks, data.ReadRaw(data.Left));
        }
    }
}