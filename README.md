# Physics Hands Test

A Unity test project for making tracked VR hands **stop at solid objects** (including objects that move on their own, like an animated animal) instead of clipping through them, while the fingers keep matching the real hand pose exactly.

- Unity 6 (6000.4.3f1), URP
- Meta XR All-in-One SDK, OVRInteraction Comprehensive rig
- Tested in the Meta XR Simulator (OpenXR)

---

## The problem

Meta's tracking overwrites the hand's transform every frame, so adding colliders to the tracked hand does nothing: the hand passes through everything. A Rigidbody hand that chases the tracked hand with forces does work, but it fights the surface (flicker/jitter) because the follower pushes in while depenetration pushes out.

## The approach

Keep **two** hands per side and separate tracking from rendering:

1. **Tracked hand (invisible).** The original SDK hand visual. It follows the real hand and is never blocked. It is only used as the *target pose*.
2. **Surface hand (visible).** A copy of the hand visual that is moved by `SurfaceHand.cs`. Each frame the script takes the tracked pose, and places the visible hand at the closest valid position that is not inside any obstacle. There are no forces and no Rigidbody, so there is nothing to fight.

The fingers still match the real hand because the copy reads the same hand data source; only its **root pose** (wrist position and rotation) is overridden by the script.

This is the same idea as Meta's poke limiter (the visual is clamped while the real hand keeps going), generalized to arbitrary surfaces and lateral motion.

---

## Scene setup

### 1. Layers
Create two layers: `Hands` and `Animals`. The `SurfaceHand` script uses its own **Obstacle Mask** instead of the physics matrix, so the collision matrix only needs to keep `Animals` from colliding with things you don't want.

### 2. Obstacles ("animals")
- Put each obstacle on the `Animals` layer.
- Give it a **primitive or convex collider** (cylinder/capsule/box). For a real skinned animal, put capsules or boxes on the main bones. Non-convex mesh colliders do not work with the penetration queries used here.
- Add a **kinematic Rigidbody** (Is Kinematic on) if the object moves.
- Move it in `FixedUpdate` with `rb.MovePosition`/`MoveRotation`, or set the Animator **Update Mode** to **Animate Physics**.

### 3. Tracked hand (the target)
For each of the left and right hand visuals that come with the rig (`OVRHandVisualLeft` / `OVRHandVisualRight`):
- Leave **Update Root Pose** **on**, so it keeps following your real hand.
- Disable its **Skinned Mesh Renderer** so it is invisible.
- Inside it there is an `openxrlefthand` / `openxrrighthand` object with the correct root pose. That object is the `Target` for the script below.

### 4. Surface hand (the visible one)
For each side, duplicate the hand visual and:
1. Move the duplicate to the **scene root** (outside `OVRCameraRig`).
2. Turn **Update Root Pose** **off** on the duplicate, so the SDK only updates the finger joints.
3. Keep its renderer **on**.
4. Set the layer to `Hands` (including children).
5. **Remove any Rigidbody.** Nothing physical drives this hand.
6. Add a **Box Collider** sized to the palm, with **Is Trigger** ticked. The script only uses it for its size, center and orientation.
7. Add the `SurfaceHand` component and set:

| Field | Value |
|---|---|
| Target | the `openxr...hand` object under the **original** (invisible) hand visual |
| Pos Offset / Rot Offset Euler | fixes a constant mismatch between the target's pivot and the hand mesh pivot; leave at 0 unless the hand looks shifted or twisted |
| Obstacle Mask | `Animals` |
| Box | the hand's Box Collider (auto-filled if empty) |
| Skin | small gap kept at the surface (default `0.002`) |
| Max Slides | slide iterations per frame (default `4`) |
| Snap Distance | snap straight to the goal if it is farther than this and free (default `0.3`) |

Keep the hand's scale at `1`; the script reads the box size without applying scale.

Do this once per hand (left and right).

### 5. Quick test
Add one or two cylinders on `Animals` with a kinematic Rigidbody and a small ping-pong mover script, then push the hands into them from different angles.

---

## What `SurfaceHand.cs` does

It runs in `LateUpdate` (with a late `DefaultExecutionOrder`) so the SDK has already updated the target, and it works as a **collide-and-slide** solver, like a character controller.

Each frame, per hand:

1. **Read the goal pose** from `Target` (plus offsets). The goal rotation is applied directly, so the hand always rotates like the real wrist.
2. **Snap check.** If the goal is more than `Snap Distance` away *and* is not inside an obstacle (tracking jump, or the real hand has come out the far side of an object), jump straight to it.
3. **Depenetrate.** If the hand's box, at its new rotation, overlaps an obstacle, push it out along the contact normal (`Physics.ComputePenetration`). This handles a wrist twist rotating the box into a surface, and a moving obstacle running into the hand.
4. **Slide toward the goal.** Box-cast from the current position toward the goal.
   - On a hit, stop at the surface (minus `Skin`), remove the part of the remaining motion that points into the surface, and slide the remainder along it. Repeat up to `Max Slides` times, which handles corners.
   - With no hit, move straight to the goal.
5. **Depenetrate again** for safety, then apply the final position and rotation to the transform.

### Consequences

- If the real hand is **at or inside** an object, the visible hand sits exactly on the surface and slides along it as the real hand moves underneath.
- The box is cast at the hand's actual rotation, so a **flat hand** puts the whole palm face on the surface, and a **sideways hand** puts only the edge on it.
- The solver always starts from a valid (outside) position, so the hand cannot flip to the far side of an object.
- There is no spring or lag in free space: the hand matches the real hand exactly when nothing is in the way.

### Limitations

- Only the palm box blocks. Fingers can still visually clip into objects.
- Obstacles need primitive or convex colliders (`BoxCast` and `ComputePenetration` limitation).
- When the real hand passes through a thick object, the visible hand slides around its surface until the goal comes out free, then snaps. For very large objects, lower `Snap Distance` or blend toward the goal.

---

## Troubleshooting

- **Hands don't move at all:** the tracked hand must keep **Update Root Pose** on, and `Target` must be the object that actually changes in Play mode. Select it while moving your hands and watch its Transform.
- **Hand passes through objects:** check that `Obstacle Mask` includes the obstacle's layer (a LayerMask defaults to Nothing), and that the obstacle has a primitive/convex collider.
- **Visible hand is offset or rotated wrongly:** adjust `Pos Offset` and `Rot Offset Euler` (rotation in 90 degree steps first, then small position steps).
- **Meta XR Simulator doesn't open:** use the OpenXR plug-in provider (XR Plug-in Management, on both the Android and PC tabs), enable the Meta XR feature group, then activate the simulator from the toolbar next to the Play button.
