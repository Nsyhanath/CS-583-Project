using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// One bowling pin. Pivot at the center of the base, +Y up.
    /// Keep the art in a child object so the mesh can be swapped without touching physics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pin : MonoBehaviour
    {
        [Tooltip("1-10, standard numbering. Set by PinSetter.")]
        public int number;
        [Tooltip("A low center of mass makes pins wobble and fall like real ones.")]
        public float centerOfMassHeight = 0.14f;

        public Rigidbody Body { get; private set; }
        public bool InPit { get; set; }
        public Vector3 Home { get; set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
        }

        public bool IsDown(LaneConfig config)
        {
            if (!gameObject.activeInHierarchy || InPit) return true;
            if (transform.position.y < -0.05f) return true;
            return Vector3.Angle(transform.up, Vector3.up) > config.downTiltDegrees;
        }

        public bool IsSettled(LaneConfig config)
        {
            if (!gameObject.activeInHierarchy || Body.IsSleeping()) return true;
            return Body.linearVelocity.magnitude < config.pinSettleLinear &&
                   Body.angularVelocity.magnitude < config.pinSettleAngular;
        }

        /// <summary>Back on its spot, upright and still.</summary>
        public void ResetToHome() => PlaceUpright(Home);

        /// <summary>Stand the pin up where it is (what a real pinsetter does with standing pins).</summary>
        public void Straighten() => PlaceUpright(new Vector3(transform.position.x, 0f, transform.position.z));

        void PlaceUpright(Vector3 position)
        {
            gameObject.SetActive(true);
            InPit = false;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.Sleep();
        }
    }
}
