using System;
using JetBrains.Annotations;

// kicken / ban in API?
// internally handle playernames not as unique identifiers, but generate GUID(?)
// take steam ID when playing with Steam as sort of unique identifier, if not, generate one
// Steam allows saving of small settings file in steam account itself
// Expose 

namespace Multiplayer.API
{
    [PublicAPI]
    public class ApiEventService
    {
        public static readonly ApiEventService Instance = new ApiEventService();
        
//        #region Abstracted Events
//        
//        public event EventHandler<PlayerConnectedEventArgs> PlayerConnected;
//        
//        public event EventHandler<PlayerDisconnectedEventArgs> PlayerDisconnected;
//
//        public event EventHandler<PlayerDesyncedEventArgs> PlayerDesynced;
//
//        public event EventHandler PlayerListChanged;
//        
//        #endregion
//
//        #region Advanced Events
//
//        public event EventHandler ProtocolHandle;
//
//        #endregion
    }

//    [PublicAPI]
//    public class PlayerListChangedEventArgs : EventArgs
//    {
//        public string PlayerName { get; set; }
//        public string SteamName { get; set; }
//        // client / server / arbiter? enum 
//        // client ID (def. GUID)
//        // enum (EventType / connectiontype) - connected, disconnected, desynced
//    }
}