# CS464 Assignment 02 · Noor Muhammad · BSCS24088

- **Video:**[Google Drive Walkthrough Link](https://drive.google.com/file/d/19qSr5fiSz-H10vJBuI_NCsrIObwFQ0B4/view?usp=sharing)
- **Input:** Legacy Input with Active Input Handling set to Both in `ProjectSettings.asset` (with native `InputSystem.actions` fallback support)
- **Ground check:** `Physics.CheckSphere` at `feet.position` on the `Ground` layer (`1 << 6`), visualized in Scene view with `Gizmos.DrawWireSphere`
- **Camera:** Exact exponential smoothing (`1 - e^(-sharpness * dt)`) with velocity look-ahead (`0.35 s`) in `LateUpdate`
- **Juice:** Volume-preserving squash on landing (`y = 0.7`, `x, z = 1.195`), driven by `Ease.OutBack`, triggered on real landing after `> 0.1 s` in the air

---

## Tuning

| Setting | Value | Why this number |
|---|---|---|
| Speed | 5.0 m/s | At 5 m/s, navigating 20–30 m greybox corridors and arenas feels brisk and responsive without overshooting narrow walkways, and diagonal clamping maintains uniform speed at all angles. |
| Jump height | 1.8 m | Allows the player to easily clear 1.1 m–1.5 m cover blocks and platforms across all five levels with comfortable clearance while fitting beneath overhead structures. |
| Time to apex | 0.61 s | Derived analytically from $t_{\text{up}} = \frac{v_0}{g} = \frac{\sqrt{2 \cdot 9.81 \cdot 1.8}}{9.81} \approx 0.606\text{ s} \approx 0.61\text{ s}$, creating a clear, readable upward trajectory. |
| Fall multiplier | 2.2× | Shortens downward descent from 0.61 s to $t_{\text{down}} = \frac{0.606}{\sqrt{2.2}} \approx 0.41\text{ s}$, eliminating floaty hang time and making landings crisp and decisive. |
| Coyote time | 0.1 s | At 5 m/s, 0.1 s translates to exactly $5 \times 0.1 = 0.5\text{ m}$ of grace distance past a ledge edge, forgiving late human reaction without causing noticeable mid-air walking. |
| Jump buffer | 0.12 s | At 60 fps, 0.12 s buffers an early Space press up to ~7 frames before touchdown, ensuring players anticipating landings never drop inputs. |
| Camera smoothing | 5 (sharpness) | Exact exponential blending factor $k = 5\text{ s}^{-1}$ yields a frame-rate independent tracking lag of $\frac{\text{speed}}{k} = \frac{5}{5} = 1.0\text{ m}$ at steady state, smooth without rubber-banding. |
| Look-ahead or dead zone | 0.35 s | Advances camera aim point by $\text{flatVelocity} \times 0.35\text{ s} = 5 \times 0.35 = 1.75\text{ m}$ ahead of the player, revealing upcoming turns and hazards early. |

---

## Four answers

1. **The jump, in numbers.**  
   From our settings ($jumpHeight = 1.8\text{ m}$, $g = 9.81\text{ m/s}^2$, $m = 1.0\text{ kg}$, $fallMultiplier = 2.2\times$):  
   - Launch speed: $v_0 = \sqrt{2gh} = \sqrt{2 \times 9.81 \times 1.8} \approx 5.94\text{ m/s}$.  
   - Jump impulse: $J = m \cdot v_0 = 1.0 \times 5.94 = 5.94\text{ N}\cdot\text{s}$, applied via `ForceMode.Impulse` once vertical velocity is zeroed.  
   - Time to apex: $t_{\text{up}} = \frac{v_0}{g} = \frac{5.9427}{9.81} \approx 0.61\text{ s}$.  
   - Fall time: $t_{\text{down}} = \sqrt{\frac{2h}{g \cdot fallMultiplier}} = \frac{0.6058}{\sqrt{2.2}} \approx 0.41\text{ s}$.  
   - Total air time: $T = t_{\text{up}} + t_{\text{down}} = 0.61 + 0.41 = 1.02\text{ s}$.  
   - Horizontal reach: At 5.0 m/s horizontal running speed, one jump carries the player $d = 5.0\text{ m/s} \times 1.014\text{ s} \approx 5.07\text{ m}$. Combined with 0.1 s coyote time adding up to 0.5 m off the edge, total clearable distance is $\approx 5.57\text{ m}$. This comfortably clears our widest gap (the 3.0 m gap in Level01 and 2.5 m gap in Level05) with over 2 m of safety margin.

2. **Update and FixedUpdate.**  
   - `Update()` runs once per rendered frame (variable $\Delta t$). It reads raw user input (`Input.GetAxisRaw` / `InputSystem.actions`), flattens camera forward/right to build camera-relative movement clamped to length 1, performs the visible `Physics.CheckSphere` ground check, updates coyote time and jump buffer timestamps, queues the jump when both conditions are met, and triggers the squash coroutine on landing.  
   - `FixedUpdate()` runs at a locked 50 Hz physics rate (every 0.02 s). It sets `rb.linearVelocity` (applying horizontal speed while preserving vertical velocity), zeroes vertical velocity and applies the one-shot impulse when `jumpQueued` is true, and adds extra downward acceleration (`Physics.gravity * (fallMultiplier - 1f)`) only while falling.  
   - *What breaks if swapped:* If `GetKeyDown` is polled in `FixedUpdate`, at 144 fps about 2 out of 3 frames do not run `FixedUpdate`, causing ~65% of jump presses to be silently dropped. If physics movement and impulses run in `Update`, motion becomes frame-rate dependent, collision detection stutters, and scaling velocity by `Time.deltaTime` mistakenly produces $0.1\text{ m/s}$ rather than $5\text{ m/s}$.

3. **Vector3.Dot.**  
   In our ground check and slope handling, `Vector3.Dot(contact.normal, Vector3.up)` calculates the cosine of the angle between the contact normal and the world up vector:  
   `float slopeDot = Vector3.Dot(collision.GetContact(0).normal, Vector3.up);`  
   `if (slopeDot > 0.5f) isGrounded = true;`  
   - *Sign and value meaning:* If `Vector3.Dot > 0`, the surface points into the upper hemisphere; specifically `> 0.5f` corresponds to a surface slope under $60^\circ$ ($\cos(60^\circ) = 0.5$), which counts as walkable ground. A value of `0` denotes a completely vertical surface ($90^\circ$ wall), while `Vector3.Dot < 0` denotes a downward-facing ceiling or overhang, which must never reset the jump window.

4. **The juice.**  
   - Effect & Curve: A volume-preserving squash on landing, driven by `Ease.OutBack(t)` through a coroutine on child `Body`.  
   - Duration: Exactly 0.25 s (which equals 15 frames at 60 fps, or 36 frames at 144 fps).  
   - *Why this curve and duration:* 0.25 s (15 frames) is brief enough that the player retains full responsive control without visual lag, yet long enough for the deformation to read clearly. `Ease.OutBack` overshoots past rest pose ($p \approx 1.08$) before rebounding to $1.0$, producing an energetic bounce. Using `Vector3.LerpUnclamped(squashed, rest, p)` preserves the overshoot rebound. The squash only triggers if the player was in the air for $> 0.1\text{ s}$, preventing continuous stutter while walking, and only scales the child `Body` so the parent `CapsuleCollider` stays completely rigid.

---

## Levels

| Level | What the controller must do there | What I changed for this jump |
|---|---|---|
| Level01 | Traverse the spawn corridor, jump across the 3.0 m gap (z=22 to z=25), weave past low cover blocks, and touch the goal. | Verified jump reaches 5.07 m carry distance; the 3.0 m gap is cleared cleanly with ~2 m safety margin without modifying geometry. |
| Level02 | Steer through the split route corridor, choosing between the main path or detouring into the side room for the reward. | Verified camera-relative movement and tight diagonal clamping allow seamless cornering and obstacle evasion. |
| Level03 | Climb the 3.0 m elevation ramp, balance across the narrow 1.5 m bridge at y=3 m, and jump onto the final platform to reach the goal. | Clamped diagonal movement prevents accidental slide-off on the narrow 1.5 m bridge; camera smoothing keeps the bridge centered. |
| Level04 | Navigate a tight 1.5 m pinch corridor before emerging into a 30 m arena, navigating around multiple staggered cover blocks. | Verified coyote time forgives late presses when stepping off cover edges; 0.35 s camera look-ahead reveals arena layout early. |
| Level05 | Sprint down the main corridor, clear the 2.5 m gap (z=20 to z=22.5), choose between the raised ramp-bridge or ground route to the illuminated goal. | Gap of 2.5 m is cleared with ease; camera follow maintains smooth vertical tracking as the player transitions between elevations. |

---

## Project Structure

```
CS464-A2-BSCS24088/
├── .gitignore
├── README.md
├── Assets/
│   ├── Editor/
│   │   ├── Assignment02PlayModeTest.cs
│   │   ├── Assignment02Tests.cs
│   │   ├── LevelBuilder.cs
│   │   └── SetupAssignment02.cs
│   ├── Materials/
│   │   ├── Player.mat
│   │   ├── PlayerFrictionless.physicMaterial
│   │   └── ...
│   ├── Prefabs/
│   │   ├── Cover.prefab
│   │   ├── Floor.prefab
│   │   ├── Goal.prefab
│   │   ├── Landmark.prefab
│   │   ├── MainCamera.prefab
│   │   ├── Marker.prefab
│   │   ├── Player.prefab
│   │   ├── Ramp.prefab
│   │   ├── Reward.prefab
│   │   ├── SpawnPad.prefab
│   │   └── Wall.prefab
│   ├── Scenes/
│   │   ├── Level01.unity
│   │   ├── Level02.unity
│   │   ├── Level03.unity
│   │   ├── Level04.unity
│   │   └── Level05.unity
│   └── Scripts/
│       ├── CameraFollow.cs
│       ├── Ease.cs
│       └── PlayerController.cs
├── Packages/
│   └── manifest.json
└── ProjectSettings/
    ├── ProjectSettings.asset (activeInputHandler: 2)
    └── TagManager.asset (Layer 6: Ground)
```
