using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BattleScars.Services
{
    // Photon RaiseEvent transport for spark particle spawns at a world position,
    // the one effect vanilla RPCs don't cover. Fires to Others; the sender
    // already spawned its own locally.
    //
    // Code 189 is an arbitrary pick in the user range (200-255 reserved by
    // Photon). Collisions with other mods are rare in practice.
    public class NetEvents : MonoBehaviour, IOnEventCallback
    {
        public const byte EventSparkSpawn = 189;

        private static readonly RaiseEventOptions ToOthers = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        private static readonly SendOptions Unreliable = new SendOptions { Reliability = false };

        private void OnEnable()  => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        public static void SendSparkSpawn(Vector3 pos, int damage)
        {
            if (!PhotonNetwork.InRoom) return;
            // Unreliable: one-shot visual, a dropped packet costs nothing.
            PhotonNetwork.RaiseEvent(EventSparkSpawn,
                new object[] { pos.x, pos.y, pos.z, damage },
                ToOthers, Unreliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!ConfigService.InActiveScene()) return;
            if (photonEvent.Code == EventSparkSpawn)
                HandleSparkSpawn(photonEvent.CustomData);
        }

        private static void HandleSparkSpawn(object? raw)
        {
            if (raw is not object[] p || p.Length < 4) return;
            if (p[0] is not float x || p[1] is not float y || p[2] is not float z) return;
            if (p[3] is not int damage) return;
            Effects.SpawnSparksAt(new Vector3(x, y, z), damage);
        }
    }
}
