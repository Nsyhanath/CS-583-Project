# Strike Zone: Lane & Ball Spec (`feat/the-lane`)

Owner: Perfect · Branch: `feat/the-lane` (cut from `title-screen`) · Last updated: 2026-11-21

This branch builds the core of the game: the bowling alley scene, the lane, the ball, the pins, and every collision between them. The other features (release meter, pin design, scoreboard) plug into the public API in [section 6](#6-public-api-what-teammates-build-on). Nobody should need to open lane internals to use it.

---

## 1. Where the project is today

| Item | State |
|---|---|
| Unity version | **6000.6.2f1** (Unity 6), URP 17.6, Input System 1.20, Timeline, uGUI + TextMeshPro |
| Game name in the repo | **Strike Zone** (this spec uses that name) |
| `main` | Only `README.md`. No Unity project yet. |
| `title-screen` | Unity project + `TitleScreen.unity` title menu. Menu buttons are PLAY, settings, high scores, how to play. Every button shows a "coming soon" toast because each one's `sceneToLoad` is empty. |
| Build scene list | Only `Assets/Scenes/SampleScene.unity`. `TitleScreen.unity` isn't in it yet. |
| Physics settings | Defaults: fixed timestep 0.02 s, solver iterations 6/1, max angular speed 50 rad/s. These are too low for bowling; see [section 5](#5-physics-setup). |
| Code style | Namespace `StrikeZone`, scripts under `Assets/StrikeZone/...` |

> **Branch note:** `feat/the-lane` starts from `title-screen` because `main` has no Unity project yet. Merge `title-screen` into `main` soon, then open this branch's PR against `main`.

---

## 2. Scope of this branch

### In scope
1. **PLAY loads the alley.** Pressing PLAY on the title screen opens `BowlingAlley.unity` (the request from the title-screen author).
2. **Alley scene:** approach, lane, gutters, pin deck, side kickbacks, pit, back cushion. Real-world scale, placeholder art.
3. **Ball:** Rigidbody, launch from a board position with a speed, an angle and a hook amount, optional steering after release, gutter detection.
4. **Pins:** a placeholder pin prefab at the correct size and mass, racked by code in the standard 10-pin triangle, with a "pin is down" rule.
5. **Throw resolution:** wait for everything to settle, then report one `ThrowResult` per throw.
6. **Pinsetter actions:** `RackAll()` for a full reset and `ClearFallen()` to sweep fallen pins between balls.
7. **Debug thrower and basic camera,** so the lane can be tested and filmed before the meter and Cinemachine exist.

### Out of scope (other branches build on this one)
| Feature | Owner | What they use from this branch |
|---|---|---|
| Release / power / accuracy meter | Player controls | `BowlingLane.PlaceBall()`, `Throw()`, `Steer()` |
| Pin model and art | Pin design | Swap the mesh inside `Pin.prefab` and keep the prefab contract in [section 4.4](#44-pin-prefab-contract) |
| Scoreboard and 10-frame scoring | Game flow | `ThrowResolved` event, `RackAll()`, `ClearFallen()` |
| Ball select (weights) | Game flow / controls | `BallSpec` ScriptableObject in [section 6.3](#63-ballspec-scriptableobject) |
| Cinemachine cameras, lighting, duck obstacle | Camera / lighting | Ball `Transform`, `BallHitPins` event |

---

## 3. Milestones

Halfway review is **Nov 24**, so M0 and M1 must be working by **Nov 23** to record the gameplay video.

| # | Milestone | Done when | Target |
|---|---|---|---|
| M0 ✅ | PLAY loads the alley | Clicking PLAY on the title screen opens `BowlingAlley`. Esc in the alley returns to the title screen. | Nov 22 |
| M1 (code in, tuning pending) | Throwable lane | Debug thrower rolls the ball, pins scatter, an on-screen debug label shows pins down | Nov 23 |
| M2 | Full rules | Gutter, pit, settle detection, one `ThrowResolved` per throw, `RackAll` and `ClearFallen` work across a full 10-frame game | Nov 28 |
| M3 | Hook and steering | Hook and steer forces in place, pocket hits strike often, everything tuned | Dec 1 |
| M4 | API freeze | Teammates are building on the API. After this, only bug fixes and tuning. | Dec 1 |

---

## 4. World layout

### 4.1 Coordinate system
- **1 Unity unit = 1 meter.**
- **Origin = center of the foul line**, top surface of the lane at **y = 0**.
- The lane runs along **+Z** (the bowler looks down +Z). **+X is the bowler's right.**
- Board numbers count from the **right** edge, as right-handed bowlers do. Board 1 is the far right and board 20 is the center.

### 4.2 Dimensions
| Thing | Real | Unity value | Constant |
|---|---|---|---|
| Lane length (foul line to head pin center) | 60 ft | 18.288 | `HeadPinZ` |
| Head pin to pit edge | 34.19 in | 0.868 | `PinDeckDepth` |
| Lane end / pit edge | | 19.156 | `PitEdgeZ` |
| Lane width | 41.5–42 in | 1.0668 (42 in, team choice) | `LaneWidth` (half = 0.5334) |
| Boards | 39 standard | 39 boards of 0.02735 | `BoardCount`, `BoardWidth` |
| Gutter width / depth | 9.25 in / 1.875 in | 0.235 / 0.048 | `GutterWidth`, `GutterDepth` |
| Approach length | 15 ft | 4.572 (z from -4.572 to 0) | `ApproachLength` |
| Aiming arrows | 12–16 ft past foul line | z 3.66–4.88, boards 5, 10, 15, 20, 25, 30, 35 | (lane texture) |
| Guide dots | 7 ft | z 2.13 | (lane texture) |
| Ball diameter | 8.595 in | 0.2183 (radius 0.109) | `BallRadius` |
| Ball mass | 6–16 lb | 2.72–7.26 kg | `BallSpec.massKg` |
| Typical ball speed | 16–20 mph | 7–9 m/s | `BallSpec.speedRange` |
| Pin height | 15 in | 0.381 | |
| Pin widest diameter | 4.766 in | 0.121 | |
| Pin base diameter | 2.03 in | 0.0516 | |
| Pin mass | 3 lb 6 oz – 3 lb 10 oz | 1.58 kg | |
| Pin spacing (center to center) | 12 in | 0.3048 | `PinSpacing` |
| Row spacing (12 in × cos 30°) | 10.39 in | 0.26397 | `RowSpacing` |

All constants live in one `LaneConfig` ScriptableObject (`Assets/StrikeZone/Lane/LaneConfig.asset`) so nobody types magic numbers.

**Board to X:** `x = HalfLaneWidth - (board - 0.5) * BoardWidth`. Board 20 gives x ≈ 0, and board 10 gives x ≈ +0.26 (right side).

### 4.3 Pin positions
Rows `r = 0..3`, slots `i = 0..r` from left to right as the bowler sees them:

```
x = (i - r / 2) * 0.3048
z = 18.288 + r * 0.26397
pinNumber = r * (r + 1) / 2 + i + 1
```

```
 7   8   9   10      r = 3  (back row, z = 19.080)
   4   5   6         r = 2
     2   3           r = 1
       1             r = 0  (head pin, z = 18.288)
   bowler -> +Z
```

The **pocket** for right-handers is between pins 1 and 3 (ball center at about x = +0.06 at impact, entering at 4–6° toward −X). For left-handers it's between pins 1 and 2.

### 4.4 Pin prefab contract
Pin design can restyle the pin freely as long as these stay true:
- Pivot at the **center of the base**, +Y up, height 0.381.
- Root has `Rigidbody` (mass 1.58), the `Pin` component, and colliders.
- Colliders: a **capsule for the body plus a sphere for the head** (compound collider). Avoid a mesh collider; it's slower and less stable.
- Center of mass lowered to y ≈ 0.14 (set in `Pin.Awake` with `rb.centerOfMass`).
- The visual mesh is a **child** object, so swapping art never touches physics.

### 4.5 Alley geometry (placeholder primitives)
| Object | Shape | Center (x, y, z) | Size (x, y, z) | Notes |
|---|---|---|---|---|
| Approach | Cube | (0, −0.025, −2.286) | (1.54, 0.05, 4.572) | Light wood material |
| Lane bed | Cube | (0, −0.025, 9.578) | (1.0668, 0.05, 19.156) | `LaneSurface` physics material, lane texture |
| Gutter L / R | Cube | (∓0.6509, −0.073, 9.80) | (0.235, 0.05, 19.6) | Top at −0.048, runs into the pit |
| Lane dividers L / R | Cube | (∓0.86, 0.0, 9.0) | (0.183, 0.06, 23.0) | Fills the gap to the neighbor lane's gutter |
| Side kickbacks L / R | Cube | (∓0.82, 0.25, 18.75) | (0.05, 0.6, 1.7) | Pins bounce back in for mix |
| Pit floor | Cube | (0, −0.50, 19.70) | (2.0, 0.05, 1.10) | Below the lane |
| Pit trigger | Box trigger | (0, −0.25, 19.70) | (2.0, 0.5, 1.10) | Ball or pins inside = out of play |
| Back cushion | Cube | (0, 0.10, 20.25) | (2.0, 1.2, 0.10) | `Cushion` physics material, low bounce |
| Masking unit | Cube | (0, 1.0, 19.40) | (2.0, 0.9, 0.15) | Visual only. Home for a neon sign later. |
| Neighbor lanes | Lane prefab copies | x = ±1.72 | | Visual only, no pins, colliders off |

---

## 5. Physics setup

### 5.1 Project settings to change
| Setting | Default now | Set to | Why |
|---|---|---|---|
| Time › Fixed Timestep | 0.02 | **0.01** | 100 physics steps per second, so a fast ball doesn't skip through a thin pin |
| Physics › Default Solver Iterations | 6 | **12** | Stable stacks of pins knocking each other |
| Physics › Default Solver Velocity Iterations | 1 | **4** | Better bounces between pins |
| Physics › Bounce Threshold | 2 | **1** | Slower pin-on-pin hits still bounce |
| Physics › Default Max Angular Speed | 50 | **100** | A rolling ball at 7.6 m/s spins at about 70 rad/s, so 50 caps it |

### 5.2 Rigidbodies
| | Ball | Pin |
|---|---|---|
| Mass | from `BallSpec` (3.6 / 5.4 / 7.3 kg) | 1.58 kg |
| Collision detection | **Continuous Dynamic** | Continuous Speculative |
| Interpolation | Interpolate | Interpolate |
| Linear / angular damping | 0 / 0.05 | 0.1 / 0.3 |
| Collider | Sphere r = 0.109 | Capsule + sphere (see 4.4) |

Unity 6 renamed some Rigidbody properties: use `rb.linearVelocity` (not `velocity`) and `rb.linearDamping` (not `drag`).

### 5.3 Physics materials (starting values, tune in M3)
| Material | Dynamic / static friction | Bounciness | Used on |
|---|---|---|---|
| `LaneSurface` | 0.04 / 0.04 | 0 | Lane bed, approach |
| `Ball` | 0.10 / 0.10 | 0.05 | Ball |
| `Pin` | 0.35 / 0.40 | 0.40 | Pins |
| `Kickback` | 0.20 / 0.20 | 0.50 | Side kickbacks |
| `Cushion` | 0.60 / 0.60 | 0.05 | Back cushion, pit floor |

### 5.4 Layers
`Ball`, `Pin`, `LaneGeometry`, `Triggers`. The ball and pins collide with everything. `Triggers` only reports overlaps.

---

## 6. Public API (what teammates build on)

All scripts are in `Assets/StrikeZone/Lane/Scripts/`, namespace `StrikeZone`. Teammates only touch **`BowlingLane`**, which is the one entry point. Get it with an inspector reference or `FindFirstObjectByType<BowlingLane>()`.

### 6.1 `BowlingLane` (facade on the scene root)
```csharp
public class BowlingLane : MonoBehaviour
{
    // State
    public LaneState State { get; }          // Ready, Rolling, Resolving
    public BallSpec CurrentBall { get; set; }
    public int StandingCount { get; }
    public bool[] StandingMask { get; }      // index 0 = pin 1 ... index 9 = pin 10

    // Before the throw (player controls)
    public void PlaceBall(int board);         // move ball along the foul line, boards 1-39
    public void Throw(ThrowParams p);         // only works when State == Ready

    // During the roll (player controls)
    public void Steer(float input);           // -1 (left) .. +1 (right); ignored past SteerLimitZ

    // Pinsetter (game flow decides when)
    public void RackAll();                    // fresh rack of 10, ball back to the approach
    public void ClearFallen();                // sweep down pins, keep standing ones, ball back

    // Events
    public event Action BallReleased;
    public event Action BallEnteredGutter;
    public event Action BallHitPins;          // first ball-to-pin contact (camera cut, sound)
    public event Action<ThrowResult> ThrowResolved;  // fires exactly once per throw
}
```

### 6.2 Data types
```csharp
[Serializable]
public struct ThrowParams
{
    public float speed;        // m/s, clamped to CurrentBall.speedRange
    public float angleDeg;     // launch direction, 0 = straight down the lane, + = right
    public float hook;         // -1..1, amount of back-end curve (+ curves left, for right-handers)
}

public struct ThrowResult
{
    public int knockedThisThrow;   // pins that went down on this ball
    public int standingAfter;      // pins still standing
    public bool[] standingMask;    // which pins are standing (for split display)
    public bool gutter;
    public float impactSpeed;      // m/s at first pin contact, 0 if none
    public float entryAngleDeg;    // ball direction at first pin contact
}
```

### 6.3 `BallSpec` ScriptableObject
The design doc's Force / Speed / Control stats come from the ball weight, so one asset holds all of them:

```csharp
[CreateAssetMenu(menuName = "Strike Zone/Ball Spec")]
public class BallSpec : ScriptableObject
{
    public string displayName;     // "12 lb"
    public float massKg;           // Force
    public Vector2 speedRange;     // Speed, m/s
    public float control = 1f;     // Control: multiplies steer strength (meters read it too)
    public float hookStrength = 1f;
    public Material material;
}
```

Ship three assets: `Ball_08lb` (3.6 kg, 7–9 m/s, control 1.25), `Ball_12lb` (5.4 kg, 6–8.5, 1.0), `Ball_16lb` (7.3 kg, 5–8, 0.75).

### 6.4 How teammates connect
```csharp
// Release meter, when the player finishes the accuracy meter:
lane.Throw(new ThrowParams { speed = powerToSpeed, angleDeg = aim + accuracyError, hook = spin });

// Scoreboard / game flow:
lane.ThrowResolved += r =>
{
    scoring.RecordThrow(r.knockedThisThrow);
    if (scoring.FrameOver) lane.RackAll(); else lane.ClearFallen();
};
```

---

## 7. Behaviour details

### 7.1 Throw
1. `Throw()` puts the ball at its board position, `z = -0.3`, `y = BallRadius + 0.001`.
2. It sets `linearVelocity` from speed and angle, and sets `angularVelocity` to a matching rolling spin (`speed / radius`) so the ball doesn't skid oddly on the oiled lane.
3. `State` becomes `Rolling` and `BallReleased` fires.

### 7.2 Hook and steering (applied in `FixedUpdate` as sideways force)
| Force | When | Strength |
|---|---|---|
| **Hook** | Ball z between `BreakpointZ` (11.0) and `HeadPinZ` | `hook × HookAccel (0.6 m/s²) × spec.hookStrength` |
| **Steer** | Ball z < `SteerLimitZ` (12.2, about 2/3 of the lane) | `input × SteerAccel (0.8 m/s²) × spec.control` |

A full hook from z = 11 moves the ball about 0.3 m (about 11 boards) and gives an entry angle of about 4–5°, which is the strike window. This fakes the hook on purpose; don't simulate oil friction.

### 7.3 Gutter
If the ball center gets past `|x| > HalfLaneWidth` before `z = HeadPinZ`, the ball is in the gutter:
- `BallEnteredGutter` fires.
- Hook and steer stop. The ball's x is pinned to the gutter center, and it rolls into the pit without touching the pins.
- `ThrowResult.gutter = true`.

### 7.4 When a pin is down
A pin is **down** if any of these is true:
- It tilts more than **20°** from upright (`Vector3.Angle(pin.up, Vector3.up) > 20`)
- Its base drops below `y = -0.05` (it fell into the pit or gutter)
- It's inside the pit trigger

A pin that slides but stays upright is still **standing**, which matches the real rules.

### 7.5 Settle and resolve
The throw resolves when **all** of these are true:
- The ball is in the pit, **or** its speed has been under 0.05 m/s for 0.5 s
- Every pin has stayed under 0.02 m/s linear and 0.1 rad/s angular speed for **0.75 s**

Fallbacks: resolve anyway **4 s after first pin contact** or **8 s after release**, so a pin wobbling forever can't stall the game. Then:
1. Build the `ThrowResult` and fire `ThrowResolved` **once**.
2. `State` becomes `Resolving`. It stays there until game flow calls `RackAll()` or `ClearFallen()`, which sets it back to `Ready`.

### 7.6 Pinsetter
- `RackAll()`: return every pin to its home spot (zero velocity, upright, active) and put the ball on the approach.
- `ClearFallen()`: deactivate down pins and leave standing pins where they are. Real pinsetters set them back down in the same place they stood.
- Pins are spawned once by `PinSetter` from `Pin.prefab` using the formula in 4.3. Don't place pins by hand.

---

## 8. PLAY → bowling alley (M0)

**Built:** run **Strike Zone › Build Bowling Alley Scene** in Unity (`Assets/StrikeZone/Editor/BowlingAlleySetup.cs`). It does steps 1–3 and 5 below, and builds the greybox alley from section 4.5 with placeholder pins, ball, lane texture, lights and a neon sign. Step 4 is already done in code.

1. Create `Assets/Scenes/BowlingAlley.unity`.
2. In **File › Build Profiles › Scene List**, add `TitleScreen` at index 0 and `BowlingAlley` at index 1. Untick or remove `SampleScene`.
3. Set the PLAY button's **Menu Button › Scene To Load** to `BowlingAlley` in `TitleScreen.unity`.
4. **Also** put `BowlingAlley` in the PLAY entry of `Assets/StrikeZone/Editor/StrikeZoneSetup.cs` (the `items` list around line 173, plus a `sceneToLoad` field passed to `MakeButton`). Re-running **Strike Zone › Build Title Screen Scene** rebuilds the scene from scratch and would wipe the setting otherwise.
5. In the alley, **Esc** loads `TitleScreen` (a small `ReturnToTitle` script).
6. Later polish: a short fade-to-black between scenes.

`TitleScreen.unity` belongs to the title-screen author. Change **only** the PLAY button field, and tell them first to avoid a merge conflict on the scene file.

---

## 9. Testing tools

### 9.1 `DebugThrower` (in the alley until the meter lands)
| Key | Action |
|---|---|
| ← / → | Move one board |
| Space | Throw with the inspector values (speed, angle, hook) |
| 1 / 2 / 3 | Switch to the 8 / 12 / 16 lb ball |
| R | `RackAll()` |
| C | `ClearFallen()` |

It shows a debug label with the state, pins down, impact speed and entry angle. It runs a simple "two balls or a strike, then rack" loop so a full game can be played. Disable it once the real meter and scoring arrive.

### 9.2 `LaneCamera` (placeholder until Cinemachine)
It has three views: **Behind** (aiming), **Follow** (stays about 1.9 m behind the ball, stops at z ≈ 13.5), and **Pin cam** (moves in close about 1.5 s after first contact). It's swapped for Cinemachine by the camera owner later.

### 9.3 Acceptance checklist
- [ ] Measure in the scene: head pin at z = 18.288, pin spacing 0.3048, ball diameter 0.218
- [ ] Ball at 7.5 m/s reaches the head pin in about 2.4 s
- [ ] Straight hit on the head pin at 7.5 m/s with the 16 lb ball knocks over at least 6 pins
- [ ] Pocket hit (x ≈ +0.06, 4–6° entry) gives a strike in **at least 5 of 10** tries with the 12–16 lb balls
- [ ] Ball thrown at board 1 with a hook away from the pins goes in the gutter, scores 0, and pins stay up
- [ ] 10 m/s throw never passes through pins (tunnelling)
- [ ] `ThrowResolved` fires exactly once per throw, across a full 21-ball game
- [ ] `RackAll` after a messy strike puts all 10 pins exactly back on their spots
- [ ] PLAY on the title screen opens the alley, and Esc goes back
- [ ] Steady 60 fps in the editor with all 10 pins moving

---

## 10. Files this branch adds
```
Assets/Scenes/BowlingAlley.unity
Assets/StrikeZone/Lane/
  LaneConfig.asset
  Balls/Ball_08lb.asset, Ball_12lb.asset, Ball_16lb.asset
  Physics/LaneSurface, Ball, Pin, Kickback, Cushion  (.physicsMaterial)
  Materials/  (placeholder colors, plus the lane texture slot)
  Textures/LanePlaceholder.png
  Meshes/PinPlaceholder.asset, PinCollider.asset
  Prefabs/Ball.prefab, Pin.prefab
  Scripts/
    BowlingLane.cs      facade + state machine
    LaneConfig.cs       ScriptableObject with every dimension
    BallSpec.cs         ScriptableObject per ball weight
    BowlingBall.cs      launch, hook, steer, gutter
    Pin.cs              down rule, reset, lowered center of mass
    PinSetter.cs        spawn, rack, clear
    PitTrigger.cs       marks ball and pins out of play
    ThrowTypes.cs       ThrowParams, ThrowResult, LaneState
    DebugThrower.cs     keyboard test thrower + debug label
    LaneCamera.cs       placeholder camera
    ReturnToTitle.cs    Esc back to the title screen
```
Changed: `TitleScreen.unity` (PLAY button field only), `StrikeZoneSetup.cs` (PLAY scene name), `ProjectSettings/TimeManager.asset` and `DynamicsManager.asset` (section 5.1), `EditorBuildSettings.asset` (scene list).

---

## 11. Art swap guide

**Golden rule:** change art in the **assets** under `Assets/StrikeZone/Lane/`, never in the `BowlingAlley` scene. The builder (**Strike Zone › Build Bowling Alley Scene**) rebuilds the scene from scratch, but it only *creates* materials, textures, meshes, prefabs and config assets when they're missing. It never overwrites one that exists, so your edits survive every rebuild.

| What | Where | How to change it |
|---|---|---|
| Lane boards, arrows, dots | `Textures/LanePlaceholder.png`, used by `Materials/LaneSurface.mat` | Paint a new image and drop it in `Textures/`, then drag it onto **Base Map** of `LaneSurface.mat`. Or overwrite the PNG with the same name. Layout: **bottom of the image = foul line, top = pit edge, right edge = board 1.** Keep it tall and narrow, at about 1 : 18 (for example 512 × 8192 or 312 × 4096). Set Wrap Mode to Clamp. |
| Neighbor lanes | `Materials/LaneSurfaceDim.mat` | Same texture, darker tint. Change the tint with **Base Color**. |
| Gutters (the trenches) | `Materials/Gutter.mat` | Base Color, Metallic, Smoothness, or a Base Map (brushed metal or plastic) |
| Rails between lanes (ball-return caps) | `Materials/Divider.mat` | Same as above |
| Side kickback walls by the pins | `Materials/Kickback.mat` | Same as above |
| Approach, floor, pit, back cushion, masking unit, back wall | `Approach.mat`, `Floor.mat`, `Pit.mat`, `Cushion.mat`, `MaskingUnit.mat`, `BackWall.mat` | Same as above |
| Ball color per weight | `Materials/Ball08.mat`, `Ball12.mat`, `Ball16.mat` (assigned in `Balls/Ball_XXlb.asset` › Material) | Edit the material, or point the BallSpec at a new one |
| Ball model | `Prefabs/Ball.prefab` › `Visual` | Double-click the prefab and replace `Visual/Body` (and the finger holes) with your model, scaled to **0.218 m across**. Then drag its renderer onto **Bowling Ball › Visual Renderer** on the prefab root. Leave the root's Rigidbody and Sphere Collider alone. |
| Pin model | `Prefabs/Pin.prefab` › `Visual` | Replace `Visual` with your model: **pivot at the base, 0.381 m tall, +Y up**. Leave the root and the `Collider` child alone. |
| Lane width, hook strength, pin-down angle, timings | `LaneConfig.asset` | Change values in the Inspector (gameplay tuning, not art) |

**Importing from Blender:** export FBX with *Apply Transform* on and 1 unit = 1 m. In Unity, check the model's size against the numbers above (the scale gizmo shows meters). URP materials use the **Universal Render Pipeline/Lit** shader, which takes Base Map, Normal Map, Metallic and Smoothness.

**Starting over on one asset:** delete it, run the builder again, and the placeholder comes back.

The masking unit above the pins holds the **STRIKE ZONE** neon sign (a TextMeshPro text in the scene builder). It's the spot for real signage later.

## 12. Open questions
1. Game name: the repo says **Strike Zone**. Is that final?
2. Lane width: 42 in (team spec) versus the official 41.5 in. The spec uses 42 in, and the difference doesn't matter for gameplay.
3. Should a strike-assist nudge (Easy difficulty only) live in `BowlingLane`, or in game flow?
4. Git LFS isn't set up yet. The title background PNG is committed directly. Set up LFS before adding models and audio.
