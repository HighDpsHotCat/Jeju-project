#if JEJU_YARD_VISITS
using System;

namespace Jeju.Yard
{
    // A future connection adapter delivers complete messages on Unity's main thread.
    public interface IYardVisitPeer
    {
        event Action<string> SnapshotReceived;
        void SendSnapshot(string json);
    }

    public sealed class YardVisitSession : IDisposable
    {
        IYardVisitPeer peer;
        YardCollectionController yard;
        Action<YardVisitSnapshot> onVisit;

        public YardVisitSession(IYardVisitPeer peer, YardCollectionController yard,
            Action<YardVisitSnapshot> onVisit = null)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (yard == null) throw new ArgumentNullException(nameof(yard));
            this.peer = peer;
            this.yard = yard;
            this.onVisit = onVisit;
            if (onVisit != null) peer.SnapshotReceived += Receive;
        }

        public bool SendCurrentYard()
        {
            if (peer == null || yard == null) return false;
            peer.SendSnapshot(yard.CaptureVisitSnapshot().ToJson());
            return true;
        }

        void Receive(string json)
        {
            if (yard != null && yard.TryReadVisitSnapshot(json, out var snapshot))
                onVisit?.Invoke(snapshot);
        }

        public void Dispose()
        {
            if (peer == null) return;
            if (onVisit != null) peer.SnapshotReceived -= Receive;
            peer = null;
            yard = null;
            onVisit = null;
        }
    }
}
#endif
