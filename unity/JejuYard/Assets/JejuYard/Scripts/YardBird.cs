using UnityEngine;

namespace Jeju.Yard
{
    public sealed class YardBird : YardResidentBehaviour
    {
        public enum Species { Seagull, Sparrow }
        public Species species;
        public Transform flightFrame, torso, head, leftWing, rightWing, feet;
        public Transform leftPrimaries, rightPrimaries;
        public Vector3 perchNormal = Vector3.up;

        public const float GullCycle = 26f, SparrowCycle = 18f;
        static readonly Vector3 HopEnd = new Vector3(1.05f, 0f, -.38f);
        static readonly Vector3 FlightControlA = new Vector3(2.36f, 0f, .25f);
        static readonly Vector3 FlightControlB = new Vector3(-1.15f, 0f, 1.44f);
        float elapsed;
        Vector3 landingSpot, landingUp = Vector3.up;
        public override Transform InteractionPoint => flightFrame;

        protected override void ChooseRestSpot()
        {
            // Over the surf, approach the nearest strip of solid sand toward the home perch.
            var parent = flightFrame.parent;
            landingSpot = Vector3.zero;
            landingUp = parent.TransformDirection(perchNormal);
            for (int step = 0; step <= 4; step++)
            {
                Vector3 spot = Vector3.Lerp(flightFrame.position, parent.position, step * .25f);
                if (Physics.Raycast(spot + Vector3.up * .5f, Vector3.down, out var hit, 5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    hit.collider is MeshCollider && hit.normal.y > .7f)
                {
                    landingSpot = parent.InverseTransformPoint(hit.point);
                    landingUp = hit.normal;
                    break;
                }
            }
        }

        void Update()
        {
            elapsed = Mathf.Repeat(elapsed + AdvanceAttention(Time.deltaTime), species == Species.Seagull ? GullCycle : SparrowCycle);
            ApplyPose(elapsed, Attention);
            FaceObserver(landingUp);
        }

        public void PoseAt(float seconds) => ApplyPose(seconds, 0f);

        protected override void ApplyPetPose(float elapsedSeconds, float strength)
        {
            bool gull = species == Species.Seagull;
            float flutter = Mathf.Sin(elapsedSeconds * (gull ? 6f : 16f));
            float spread = (gull ? 20f + 8f * flutter : 8f + 5f * flutter) * strength;
            rightWing.localRotation = Quaternion.Euler(0f, -spread, spread * .65f) * rightWing.localRotation;
            leftWing.localRotation = Quaternion.Euler(0f, spread, -spread * .65f) * leftWing.localRotation;
            head.localRotation *= gull
                ? Quaternion.Euler(-12f * strength, 0f, 4f * flutter * strength)
                : Quaternion.Euler(-6f * strength, 0f, (15f + 5f * flutter) * strength);
        }

        void ApplyPose(float seconds, float rest)
        {
            if (species == Species.Seagull) Gull(Mathf.Repeat(seconds, GullCycle), rest);
            else Sparrow(Mathf.Repeat(seconds, SparrowCycle), rest);
        }

        void Gull(float time, float rest)
        {
            const float takeoff = 12f, landing = 22f;
            float t = Mathf.InverseLerp(takeoff, landing, time);
            float angle = Mathf.SmoothStep(0f, 1f, t) * (Mathf.PI * 2f);
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            float aloft = Mathf.SmoothStep(0f, 1f, Mathf.Min(time-takeoff, landing-time) / 1.65f);
            float height = aloft * (1.92f + .25f * Mathf.Sin(2f*angle));
            var point = new Vector3(2.9f*sin, height, -2.65f*(1f-cos) - .95f*sin);
            flightFrame.localPosition = Vector3.Lerp(point, landingSpot, rest);
            aloft *= 1f - rest;
            float heading = Yaw(new Vector3(2.9f*cos, 0f, -2.65f*sin-.95f*cos));
            var bank = Quaternion.AngleAxis(aloft*(6f + 10f*sin*sin), Vector3.forward);
            var slope = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(perchNormal, Vector3.up, aloft));
            flightFrame.localRotation = slope * Quaternion.Euler(0f, heading, 0f) * bank;

            float open = Mathf.SmoothStep(0, 1, (time-takeoff+.60f)/.85f)
                * Mathf.SmoothStep(0, 1, (landing-time+.60f)/.85f);
            float effort = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.25f,.63f,Mathf.Cos(2f*angle)));
            float stroke = time * 2f * Mathf.PI * 1.65f;
            float rise = Mathf.Lerp(38f, 6f + 39f*Mathf.Sin(stroke)*effort, aloft);
            open *= 1f - rest;
            Wings(open, rise, 5f, -3f-18f*Mathf.Cos(stroke)*aloft*effort);

            float breath = Mathf.Sin(time * 2f*Mathf.PI*7f/GullCycle);
            float standing = Mathf.Max(aloft, open);
            torso.localPosition = Vector3.up * (Mathf.Lerp(.232f,.30f,standing) + .006f*breath*(1f-standing));
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(-4f, -2f*effort*Mathf.Sin(stroke), aloft),0,0);
            head.localRotation = Quaternion.Euler(3f*Mathf.Lerp(breath, Mathf.Sin(Time.time*2.1f),rest),
                13f*Mathf.Sin(time*4f*Mathf.PI/GullCycle)*(1f-rest), 0);
            Feet(.185f, Mathf.Lerp(.81f,1f,standing), 72f*aloft);
        }

        void Sparrow(float time, float rest)
        {
            const float hopStart = 6f, takeoff = 11.4f, landing = 15.2f;
            float hopYaw = Yaw(HopEnd);
            float takeoffYaw = Yaw(FlightControlA - HopEnd);
            float landYaw = Yaw(-FlightControlB);
            Vector3 position = Vector3.zero;
            float yaw = hopYaw, pitch = 0f, roll = 0f, airborne = 0f, peck = 0f, hopLift = 0f;
            if (time < hopStart)
            {
                peck = Peck(time, 3.8f);
            }
            else if (time < hopStart + 2.1f)
            {
                float step = (time-hopStart)/.7f;
                float within = Mathf.Min(1f, Mathf.Repeat(step, 1f) / .76f);
                hopLift = 4f*within*(1f-within);
                position = HopEnd * ((Mathf.Floor(step) + within) / 3f);
                position.y = .18f*hopLift;
                pitch = 5f*hopLift;
            }
            else if (time < takeoff)
            {
                float pause = time-hopStart-2.1f;
                position = HopEnd;
                yaw = Mathf.LerpAngle(hopYaw, takeoffYaw, Mathf.SmoothStep(0, 1, (pause-1.6f)/1.4f));
                peck = Peck(pause,.55f) + Peck(pause,1.2f);
            }
            else if (time < landing)
            {
                float u = Mathf.InverseLerp(takeoff, landing, time);
                float v = 1f-u;
                position = v*v*v*HopEnd + 3f*v*v*u*FlightControlA + 3f*v*u*u*FlightControlB;
                position.y = 1.28f*Mathf.Sin(Mathf.PI*u);
                Vector3 tangent = v*v*(FlightControlA-HopEnd) + 2f*v*u*(FlightControlB-FlightControlA) - u*u*FlightControlB;
                yaw = Yaw(tangent);
                airborne = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f-u) / .105f);
                pitch = -16f*Mathf.Cos(Mathf.PI*u)*airborne;
                roll = 9f*Mathf.Sin(Mathf.PI*u);
            }
            else
            {
                float pause = time-landing;
                yaw = Mathf.LerpAngle(landYaw, hopYaw, Mathf.SmoothStep(0,1,pause/2.2f));
                peck = Peck(pause,.82f) + Peck(pause,1.39f);
            }

            flightFrame.localPosition = Vector3.Lerp(position, landingSpot, rest);
            airborne *= 1f-rest;
            hopLift *= 1f-rest;
            peck *= 1f-rest;
            pitch *= 1f-rest;
            roll *= 1f-rest;
            flightFrame.localRotation = Quaternion.Euler(0f,yaw,0f);
            float standing = Mathf.Max(Mathf.Max(airborne,hopLift),peck);
            float breath = Mathf.Sin(time*10f*Mathf.PI/SparrowCycle);
            torso.localPosition = Vector3.up*(Mathf.Lerp(.179f,.235f,standing) + .004f*breath*(1f-standing));
            torso.localRotation = Quaternion.Euler(pitch+26f*peck,0f,roll);
            head.localRotation = Quaternion.Euler(39f*peck + 2f*Mathf.Lerp(breath, Mathf.Sin(Time.time*2.8f),rest)*(1f-standing),
                (1f-peck)*(1f-airborne)*12f*Mathf.Sin(time*Mathf.PI)*(1f-rest), 0);
            float beat = time * (2f*Mathf.PI*4f);
            Wings(airborne, 10f + 48f*Mathf.Sin(beat), 9f + 6f*Mathf.Sin(beat), -24f*Mathf.Cos(beat));
            Feet(.14f, Mathf.Lerp(.68f,1f,standing),56f*airborne);
        }

        static float Peck(float time, float center)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(time-center)/.25f);
            return t*t*(3f-2f*t);
        }

        static float Yaw(Vector3 heading) => Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;

        void Feet(float length, float extension, float tuck)
        {
            if (feet == null) return;
            feet.localPosition = new Vector3(0,length*extension,-.025f);
            feet.localScale = new Vector3(1,extension,1);
            feet.localRotation = Quaternion.Euler(tuck,0,0);
        }

        void Wings(float open, float rise, float sweep, float flex)
        {
            bool gull = species == Species.Seagull;
            // Bring each wrist in against the flank and overlap the outer feathers above the tail.
            float span = Mathf.Lerp(gull ? .58f : .67f,1f,open);
            float twist = (1f-open)*51f;
            float yaw = Mathf.Lerp(87f,sweep,open);
            float roll = Mathf.Lerp(-5f,rise,open);
            var socket = new Vector3(gull ? .105f : .093f, gull ? .079f : .063f, gull ? .035f : .047f);
            socket += new Vector3(gull ? .060f : .050f, .018f, gull ? .105f : .043f)*(1f-open);
            var scale = new Vector3(span,1,Mathf.Lerp(.80f,1,open));
            rightWing.SetLocalPositionAndRotation(socket,Quaternion.Euler(twist,yaw,roll));
            leftWing.SetLocalPositionAndRotation(new Vector3(-socket.x,socket.y,socket.z),Quaternion.Euler(twist,-yaw,-roll));
            rightWing.localScale = leftWing.localScale = scale;
            float featherSweep = (1f-open)*13f;
            float bend = Mathf.Lerp(-3f,flex,open);
            rightPrimaries.localRotation = Quaternion.Euler(0,featherSweep,bend);
            leftPrimaries.localRotation = Quaternion.Euler(0,-featherSweep,-bend);
        }
    }
}
