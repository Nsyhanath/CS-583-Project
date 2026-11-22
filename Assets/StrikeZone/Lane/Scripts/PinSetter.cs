using System.Linq;
using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// Owns the 10 pins: racks them, sweeps fallen ones, and reports which are standing.
    /// Uses the Pin children already in the scene; if there are none, spawns them from pinPrefab.
    /// </summary>
    public class PinSetter : MonoBehaviour
    {
        public LaneConfig config;
        public Pin pinPrefab;

        public Pin[] Pins { get; private set; }

        void Awake()
        {
            Pins = GetComponentsInChildren<Pin>(true).OrderBy(p => p.number).ToArray();
            if (Pins.Length == 0 && pinPrefab != null)
            {
                Pins = new Pin[10];
                for (int i = 0; i < 10; i++) Pins[i] = Instantiate(pinPrefab, transform);
            }
            for (int i = 0; i < Pins.Length; i++)
            {
                Pins[i].number = i + 1;
                Pins[i].name = "Pin " + (i + 1);
                Pins[i].Home = config.PinHome(i);
            }
        }

        void Start() => RackAll();

        public int StandingCount => Pins.Count(p => !p.IsDown(config));

        public bool[] StandingMask() => Pins.Select(p => !p.IsDown(config)).ToArray();

        public bool AllSettled() => Pins.All(p => p.IsSettled(config));

        public void RackAll()
        {
            foreach (var p in Pins) p.ResetToHome();
        }

        public void ClearFallen()
        {
            foreach (var p in Pins)
            {
                if (p.IsDown(config)) p.gameObject.SetActive(false);
                else p.Straighten();
            }
        }
    }
}
