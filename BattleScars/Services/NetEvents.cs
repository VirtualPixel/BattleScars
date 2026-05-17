using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BattleScars.Services
{
    // Photon RaiseEvent transport for the two effects vanilla RPCs don't cover:
    // voice pitch overrides on the receiver's local voice chat instance, and
    // spark particle spawns at a world position. Both fire to Others; the
    // sender already applied locally.
    //
    // Codes 188 and 189 are arbitrary picks in the user range (200-255 reserved
    // by Photon). Collisions with other mods are rare in practice.
    public class NetEvents : MonoBehaviour, IOnEventCallback
    {
        public const byte EventVoicePitch = 188;
        public const byte EventSparkSpawn = 189;

        private static readonly RaiseEventOptions ToOthers = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        private static readonly SendOptions Reliable   = new SendOptions { Reliability = true };
        private static readonly SendOptions Unreliable = new SendOptions { Reliability = false };

        private void OnEnable()  => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        public static void SendVoicePitch(int avatarViewID, float pitch, float oscillation, float oscSpeed, float duration)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(EventVoicePitch,
                new object[] { avatarViewID, pitch, oscillation, oscSpeed, duration },
                ToOthers, Reliable);
        }

        public static void SendVoiceCancel(int avatarViewID)
        {
            if (!PhotonNetwork.InRoom) return;
            // Pitch 1.0 with duration 0 is the cancel sentinel on the receive side.
            PhotonNetwork.RaiseEvent(EventVoicePitch,
                new object[] { avatarViewID, 1f, 0f, 0f, 0f },
                ToOthers, Reliable);
        }

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
            switch (photonEvent.Code)
            {
                case EventVoicePitch: HandleVoicePitch(photonEvent.CustomData); break;
                case EventSparkSpawn: HandleSparkSpawn(photonEvent.CustomData); break;
            }
        }

        private static void HandleVoicePitch(object? raw)
        {
            if (raw is not object[] p || p.Length < 5) return;
            if (p[0] is not int viewID) return;
            if (p[1] is not float pitch) return;
            if (p[2] is not float osc) return;
            if (p[3] is not float oscSpeed) return;
            if (p[4] is not float duration) return;

            var view = PhotonView.Find(viewID);
            var avatar = view != null ? view.GetComponent<PlayerAvatar>() : null;
            if (avatar == null || avatar.voiceChat == null) return;

            bool cancel = duration <= 0.01f && Mathf.Approximately(pitch, 1f);
            if (cancel) avatar.voiceChat.OverridePitchCancel();
            else avatar.voiceChat.OverridePitch(pitch, 0.4f, 0.4f, duration, osc, oscSpeed);
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
