using System.Collections.Generic;
using UnityEngine;

namespace Jeju.Yard
{
    // Read positions after animal motion and camera follow, so the selected animal and
    // the speech bubble stay attached to what the player actually sees this frame.
    [DefaultExecutionOrder(100)]
    public sealed class YardProximity : MonoBehaviour
    {
        const float LandReach = 2.1f;
        const float ShoreReach = 4.85f;
        const float CottageReach = 1.9f;
        const float AttentionLeaveMargin = .5f;
        const float FocusMargin = .12f;

        sealed class Resident
        {
            public string modelKey;
            public YardResidentBehaviour actor;
            public float reach;
            public bool near;
        }

        readonly List<Resident> residents = new List<Resident>();
        YardCollectionController collection;
        YardCollectionPanel panel;
        Transform player;
        BoxCollider cottage;
        Camera lens;
        Resident focused;
        bool nearCottage, focusCottage;
        static readonly Vector2 NoBubble = new Vector2(float.NaN, float.NaN);

        public void Initialize(YardCollectionController owner, YardCollectionPanel hud,
            IReadOnlyDictionary<string, GameObject> models)
        {
            collection = owner;
            panel = hud;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<YardWalker>(out var walker)) player = walker.transform;
                if (root.CompareTag("MainCamera")) lens = root.GetComponent<Camera>();
                if (root.name == "Jeju choga" || root.name == "JejuChoga")
                    cottage = root.GetComponent<BoxCollider>();
            }
            foreach (var model in models)
            {
                var actor = model.Value.GetComponentInChildren<YardResidentBehaviour>(true);
                if (actor != null)
                    residents.Add(new Resident { modelKey = model.Key, actor = actor,
                        reach = model.Key == "dolphin" ? ShoreReach : LandReach });
            }
            panel.InteractRequested += Interact;
            panel.HouseExitRequested += ExitAtCottage;
        }

        static float GroundDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void LateUpdate()
        {
            if (panel == null || player == null) return;
            Resolve();
        }

        void Resolve()
        {
            Vector3 position = player.position;
            float best = float.PositiveInfinity;
            Vector2 bubble = NoBubble;
            if (cottage != null && cottage.enabled && cottage.gameObject.activeInHierarchy)
            {
                // ClosestPoint uses the rotated footprint, including the sides of the cottage.
                Vector3 houseCentre = cottage.transform.TransformPoint(cottage.center);
                var atWaist = new Vector3(position.x, houseCentre.y, position.z);
                Vector3 wall = cottage.ClosestPoint(atWaist);
                float distance = GroundDistance(position, wall);
                nearCottage = distance <= CottageReach;
                if (nearCottage)
                {
                    best = distance / CottageReach - (focusCottage ? FocusMargin : 0f);
                    if (lens != null)
                    {
                        Vector3 eave = Vector3.Lerp(houseCentre, wall, .62f);
                        eave.y = cottage.bounds.max.y + 1.15f;
                        Vector3 screen = lens.WorldToScreenPoint(eave);
                        if (screen.z > 0) bubble = screen;
                    }
                }
            }
            else nearCottage = false;

            Resident next = null;
            foreach (var resident in residents)
            {
                Transform point = resident.actor.InteractionPoint;
                bool visible = resident.actor.isActiveAndEnabled && point != null && point.gameObject.activeInHierarchy;
                float distance = visible ? GroundDistance(position, point.position) : float.PositiveInfinity;
                // A resident can keep looking briefly beyond reach. The button follows actual reach.
                bool near = distance <= resident.reach + (resident.near ? AttentionLeaveMargin : 0f);
                if (near != resident.near)
                {
                    resident.actor.SetObserver(near ? player : null);
                    resident.near = near;
                }
                if (distance > resident.reach) continue;
                float score = distance / resident.reach - (focused == resident ? FocusMargin : 0f);
                if (score >= best) continue;
                best = score;
                next = resident;
            }
            focused = next;
            focusCottage = next == null && nearCottage;
            panel.SetNearby(focused != null || focusCottage, focusCottage, bubble);
        }

        void Interact()
        {
            if (panel.IsOpen || player == null) return;
            Resolve();
            if (focused != null) collection.OpenResident(focused.modelKey);
            else if (focusCottage) YardExit.Leave();
        }

        void ExitAtCottage()
        {
            if (panel.IsOpen || player == null) return;
            Resolve();
            if (nearCottage) YardExit.Leave();
        }

        void OnDisable()
        {
            foreach (var resident in residents)
                if (resident.near && resident.actor != null)
                {
                    resident.actor.SetObserver(null);
                    resident.near = false;
                }
            nearCottage = focusCottage = false;
            focused = null;
            if (panel != null) panel.SetNearby(false, false, NoBubble);
        }

        void OnDestroy()
        {
            if (panel == null) return;
            panel.InteractRequested -= Interact;
            panel.HouseExitRequested -= ExitAtCottage;
        }
    }
}
