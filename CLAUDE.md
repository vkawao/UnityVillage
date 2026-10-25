# VR Village — building house quests

Course final project (KMITL, *Fundamentals of AR, VR and Mixed Reality*). The group builds one VR village; this user (Wikorn) owns **5 houses**. Each house is an interactive building with **10 actions**. Houses so far: **Guitar Shop** (finished, the reference implementation) and **Sakura Park** (vending corner; built, being play-tested).

**Group 15, village name "Mai Ruu Muu Baan"** (gate text `VillageDecorBuilder.WelcomeText`; APK: Company Name `Group 15`, Product Name `MaiRuuMuuBaan`, package `com.Group15.MaiRuuMuuBaan`). Export guide: `C:\Users\support\Downloads\Unity Export Guide for Final Project.pdf`: delete the `XR Interaction Simulator` object before every headset build; Build Profiles: only `Village.unity`, platform Android.

## Grading rules (they drive every design decision)

- Every house needs **≥10 distinct actions**. The same action *type* is fine; the same *procedure* is not.
- Every house needs **≥1 UI element**, **≥1 item moved/rotated by script** and **≥1 VR grab or teleport**. Missing any one scores the whole house 0.
- **The APK is the source of truth.** Target: Meta Quest 3S. An action that doesn't work in the build scores 0.
- In the live demo, a random classmate plays **unbriefed**, and the whole group shares ~10 minutes. Actions must explain themselves and be quick.

## Blender models (the other half of the grade)

Course outline: `C:\Users\support\Downloads\VR_Village_Final_Project_Outline.pdf`. Models are 40 of the 100 individual points.

- **≥8 self-modeled models per house**, each with **UVs and a material**. A plain primitive doesn't count; a model reused in a later house counts only in the first. Recoloured copies count once. A building made of boxes is listed as **one** model.
- **AI-generated models are allowed (user, 2026-10-24)**, so Claude may build models directly. The outline PDF said they score 0, so still list AI help (models, code, materials) in the PDF's declared-assets section to be safe.
- Submission per person: `StudentID_Nickname.apk`, `.pdf` (house image, image + explanation of each model, close-up + description of each action, declared assets), `_blend.zip`.

**The user's style:** beginner-simple (cube/cylinder + extrude, inset, loop cut), flat colours with no textures, **no bevels**, plain material names without prefixes, PascalCase object names.

**Files** (reorganised 2026-10-24): each house's working .blend + exported FBX lives in `Documents\VRVillage\Houses\<House>\` (`GuitarShop`, `SakuraPark`, `Cafe`, `Laundromat`, `Arcade`). Everything else (`.blend1` backups, old/scrapped files, unused previews, `SplitDryers.py`, `BlendBackups\`) is in `Documents\VRVillage\Other\`. Don't leave new project files loose in `Documents`.

| House | Working .blend (`VRVillage\Houses\<House>\`) | In Unity |
|---|---|---|
| Guitar Shop | `GuitarCenterShop.blend`. On 2026-10-22 the AI-made wall AC units, roof AC units and roof vents (12 objects) were deleted from it at the user's request; pre-removal backup in `VRVillage\Other\BlendBackups\`. The FBX in Unity still contains those meshes; the scene objects are deleted by hand in Unity. The old `GuitarCenter.blend` (12 Oct, now in `Other`) is outdated; don't submit it. | `Assets/GuitarCenter.fbx` |
| Sakura Park | `VendingMachinePark.blend` | `Assets/VendingMachinePark.blend` (unpacked in the scene, but its meshes are still used; see below) |
| Cafe | `Cafe.blend` | `Assets/Cafe.fbx` |
| Laundromat | `Laundromat.blend` + `Laundromat.fbx` (Claude-built, declare it; renamed from `LaundromatPreview` 2026-10-24; the older scrapped files are `Other\LaundromatOld.blend/.fbx`) | `Assets/Laundromat.fbx` |
| Arcade | `Arcade.blend` (Claude-built, the user now models in it; declare it; renamed from `ArcadePreview` 2026-10-24) + `Arcade.fbx` | `Assets/Arcade.fbx` (linked prefab instance `Arcade`) |

**Working with Blender:**
- The Blender MCP (port 9876) reaches **one** Blender window. For any other file, inspect it read-only with headless Blender: `"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe" --background <file> --python <script> -- <out.json>`.
- **The user edits scenes by hand.** Never re-run a build script over their scene, and never wipe or rebuild their objects. Make targeted edits, verify them with a render, and ask before anything destructive.
- **Material-slot trap:** `mesh.materials.pop()` / `clear()` shift polygon material indices. Finish the slot list first, `foreach_set("material_index")` last, then check every face's material is unchanged. The `.blend1` backup is the recovery source.
- `bpy.data.libraries.load` fills the very list you assign to `dst.meshes`; pass a copy.
- **FBX export for Unity:** Mesh only, Scale 1, Apply Scalings = FBX Units Scale, -Z Forward, Y Up, **Apply Transform** on, Smoothing = Face, no animation. Transparent glass needs Surface Type = Transparent on the extracted material in Unity.

## Tech stack

| Thing | Version / location |
|---|---|
| Unity | 6000.3.19f1 (`C:\Program Files\Unity\Hub\Editor\6000.3.19f1`) |
| Render pipeline | URP 17.3.0 |
| XR Interaction Toolkit | 3.5.1. Source: `Library/PackageCache/com.unity.xr.interaction.toolkit@63ca3b7ce776/Runtime/` |
| UI | uGUI 2.0 + TextMeshPro (Inter font: `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF.asset`) |
| Other | OpenXR 1.17.1, Meta OpenXR 2.5.1, XR Hands 1.8.1, Input System 1.19.0 |

- **Scene:** `Assets/Village.unity`. The XR Origin + XR Interaction Simulator rig is already there and works.
- **No asmdefs:** everything compiles into `Assembly-CSharp`, so VR Template scripts like `Unity.VRTemplate.XRKnob` are usable directly.

## Hard rules

1. **Never hand-edit `Village.unity`.** All scene wiring is done by Editor menu scripts (`VR Village/...`) that the user runs. Write runtime scripts, then extend a setup utility that builds and wires objects through `SerializedObject` and saves the scene.
2. **Check XRI APIs against the installed source before using them.** Grep the package `Runtime/` folder; never write member names from memory. Known traps:
   - Interactables: `interactorsSelecting` / `firstInteractorSelecting` (NOT `interactorsSelected`).
   - Interactors (sockets): `interactablesSelected`, `hasSelection`.
   - Namespaces: `UnityEngine.XR.Interaction.Toolkit.Interactables`, `...Interactors`, `...UI` (`XRUIInputModule`, `TrackedDeviceGraphicRaycaster`); event args live in `UnityEngine.XR.Interaction.Toolkit`.
   - Interactable collider list: serialized `m_Colliders`. Grab: `useDynamicAttach`, `movementType`, `throwOnDetach`. Unity 6 uses `Rigidbody.linearVelocity`.
3. **One compile error anywhere hides the whole `VR Village` menu.** Editor scripts don't load until everything compiles.
4. **Don't disturb other houses.** Teammates build houses in the same village with their own guidance methods.
   - All of a house's objects go under that house's root (e.g. `GuitarShopQuest`).
   - Nothing goes on the shared XR rig or camera; scripts may only *read* `Camera.main`.
   - Changes to shared scripts (e.g. `DoorMover`) must be **opt-in toggles, off by default**, switched on by this house's setup.
   - Guidance (HUD, arrow, dings) only runs while the player is inside the house's area box. Keep areas from overlapping neighbours.
5. **No audio files.** All sounds are synthesized at runtime in `SynthAudio` (and `QuestCelebration` / `GuitarTuner`). Add new sounds there as cached static clips.
6. **The setup must stay idempotent and safe to re-run.** Reuse existing objects. Keep anchors the user moved (set rotation only if still identity). Rebuild generated UI/FX so layout changes apply. For generated parts (`QuestParts`), restore the user's hand placement after a rebuild.

## Verifying a compile without the Editor

The open Editor holds the project lock, so batch mode can't run. Compile-check with Unity's bundled Roslyn instead:

- Run `NetCoreRuntime/dotnet.exe DotNetSdkRoslyn/csc.dll @file.rsp` from the Unity `Editor/Data` folder.
- Build the `.rsp` from `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` in the project root:
  - references: each `HintPath`, plus each `ProjectReference` mapped to `Library/ScriptAssemblies/<name>.dll`
  - the csproj's `DefineConstants`
  - flags `-langversion:9.0 -nostdlib+ -target:library`
  - sources: glob `Assets/Scripts/**/*.cs`, because the csproj lists go stale for new files
- **Run the compiler with the project root as the working directory**; HintPaths are relative.
- Compile the runtime assembly first, then the editor assembly referencing the fresh runtime dll.
- You can also read `%LOCALAPPDATA%\Unity\Editor\Editor.log` for the real Editor's compile results and `[Quest]` play-mode logs.

**Shell quirk:** heredocs that contain an apostrophe break in the Bash tool. Write files and patch scripts with the Write tool, then run them.

## Architecture (namespace `VRVillage`, `Assets/Scripts/`)

### Reusable quest framework (`Assets/Scripts/Quest/`)

```
<House>Quest root      QuestManager: runs steps in order, auto-numbers "N. instruction"
  QuestPanel           big static world-space panel; takes the anchor's position AND rotation
  Anchors/QuestAnchor_N_*   where the panel appears per step (readable side faces the anchor's -Z)
  Steps/StepN_*        one QuestStep subclass per action
  FX/Celebration_*     QuestCelebration: particle burst + synthesized fanfare
  Guide/               QuestGuide + HouseArea box + GuideHUD
```

| Component | Role |
|---|---|
| `QuestStep` (abstract) | Fields: `m_Instruction`, `m_PanelAnchor`, `m_Celebration` (plays on Complete), `m_ShowWhileActive` (objects shown only during the step) + `m_HideDelay`, `m_ShortInstruction` (guide task line), `m_GuideTarget`. Override `OnBegin` / `OnEnd`; call `Complete()` and `SetProgress(richText)`. `virtual GuideTarget` lets a step point the arrow somewhere dynamic. Context menu: "Debug: Force Complete". |
| `QuestManager` | Hides panel → moves it to the anchor → shows "N. text" (+ progress line, 65% size, gold; inner `<color>` tags override) → waits for Complete. Events `StepStarted`, `AllStepsCompleted`. |
| `QuestPanel` | Static (the user explicitly wants no turning to face the player), big (1600×640 at 0.001 scale), fades/pops. |
| `QuestGuide` | Per-house guidance, only inside the house's area box: a lazily following task line, an arrow toward `step.GuideTarget` (hidden when in view, left/right when behind), and a ding from the target on each new step. |
| `QuestCelebration` | `Play()` = burst + fanfare at its position; `Sparkle(pos, n)` = silent small burst. Style (gold sparkle / confetti / green) chosen at build time. |
| `SynthAudio` | Static cached clips: amp buzz/hum, buzzer, cash register, click, ding, door open/close, unplugged/clean strum, distorted lead lick. |
| `GrabUtil.IsHeldByHand` | Held by a hand, not a socket. |
| `CounterZone` / `CounterItem` | Polled box that detects items placed (released + settled) in an area; items carry a name and price. |
| `QuestBob` | Bobbing marker (e.g. "Here!"). |
| `ItemTrigger` | Stateless box volume: `Contains(rigidbody)` for slots, bin openings, card readers. Its -Z faces the player. |
| `InsertItemStep` | Put an item into a slot (held or not) → sound, item vanishes. |
| `DisposeItemStep` | Drop an item into the right bin; a wrong bin buzzes, shows a hint and pops it back out. |
| `ConsumeStep` | Drink (hold at mouth N seconds: gulps + sigh) or Eat (N munches, then the item flattens). |
| `SitStep` | Sit at a seat on purpose: press its "Sit down" button (fade out, move onto the seat, creak, fade in) or teleport onto its `TeleportationAnchor`. Walk-up snap is off (`m_SnapRadius` = 0). |
| `ScreenFader` | House-owned black panel held in front of the camera only while fading (`BuildScreenFader` in the kit). |
| `HipHolster` + `HolsterSocket` | Hip pocket that follows the head (yaw smoothed, ignores looking down); the socket only accepts listed items; visual only inside the house area. `Store(item)` docks an item. |
| `QuestHideAtStart` | Hides items at play start (visible in the editor for placement); steps reveal them. |
| `IQuestItemSource` / `QuestItems` | A step that *chooses* an item (drink, snack) feeds later steps through their `m_ItemSource` field. |
| `SynthSound` + `SynthAudio.Get/PlayAt` | Pick synthesized sounds from the inspector; `SynthAudio` is `partial` (park sounds in `SynthAudio.Park.cs`). |

Interaction patterns already solved, worth copying:
- **Strum / shake detection:** speed relative to the head, so joystick walking doesn't count (`GuitarTuner`, `TryGearStep`).
- **XRKnob volume knob** on a grabbable object (`AmpUnit`). Limit the grab interactable's `m_Colliders` to its own box so child interactables (knob, footswitch) aren't swallowed.
- **World-space UI buttons:** the canvas needs `TrackedDeviceGraphicRaycaster`, and the scene needs an EventSystem with `XRUIInputModule`; the setup adds one if missing. Hook `onClick` at runtime in the step, not as persistent listeners.
- **Toggle by grabbing a small `XRSimpleInteractable`, or by trigger (`activated`) while holding** (`EffectPedal`).
- **Gate a final action on a checklist** and point the guide arrow at what's missing (`PurchaseStep`).

### Setup utilities (`Assets/Scripts/Editor/`)

- `VillageSetupUtility`: the original door/guitar/rack setup. It is `[InitializeOnLoad]` and **auto-runs once per editor session** (saves the scene), so scene changes it makes can "come back".
- `QuestBuildKit` (static, shared by every house; use it with `using static VRVillage.Editor.QuestBuildKit;`):
  - `FindOrCreate`, `FindOrCreateAnchor`, `GetOrAdd`, `SetArray`, `StepSO`, `SetGuideTexts`
  - `NewWorldCanvas`, `AddButton`, `AddRounded`, `AddText`, `BuildPanel`, `BuildHereMarker(anchor, text)`
  - `BuildCelebration` + `CelebrationStyle`, `BuildGuide(root, frame, floorBounds, margin)`
  - `SetupGrabbable`, `SetupCounterItem`, `EnsureEventSystem`, `EnsureStaticCollider`
  - `RebuildParts` / `KeepPlacement`, `Primitive`, `GetOrCreateMaterial`, `FindMaterial`, `ReplaceMaterial`
  - **Model features by material:** `EnsureReadable` (turns on Read/Write for the model), `FrontFaces(renderer, material, front, frame)` (frontmost faces of one material, e.g. a slot inset, screen, metal plate), `MaterialBounds` (e.g. a dispenser tray), `BuildItemTrigger`
  - the `BuildingFrame` struct: building-aligned axes, frame-space bounds, snapping directions to the walls
- `QuestSetupUtility` (partial) + `QuestSetupUtility.Shop.cs`: menu **VR Village → Setup Guitar Shop Quest** (shop-specific code only).
- `SakuraParkQuestSetupUtility`: menu **VR Village → Setup Sakura Park Quest**.

## Guitar Shop (done): reference for the 10 actions

Root `GuitarShopQuest`, building `GuitarCenter` (from `Assets/GuitarCenter.fbx`).

| # | Action | Step class |
|---|---|---|
| 1 | Enter the store: the door swings (script-moved item) with a latch/creak/bell sound | `DoorOpenStep` |
| 2 | Hang the electric guitars on rack 1 (sockets) | `RackGuitarsStep` |
| 3 | Hang the acoustic guitars on rack 2, sparkle + fanfare | `RackGuitarsStep` |
| 4 | Tune the acoustics by picking up or shaking them | `TuneGuitarsStep` + `GuitarTuner` |
| 5 | Bring the Les Paul to the counter ("Here!" marker) | `ItemToCounterStep` |
| 6 | Bring any effect pedal to the counter | `ItemToCounterStep` |
| 7 | Turn amp knobs (3 buzz, 1 hums), press the button under the working amp: wrong = buzzer + red, right = confetti | `AmpTestStep` + `AmpUnit` |
| 8 | Bring the working amp to the counter | `WorkingAmpToCounterStep` |
| 9 | Try the gear (unplugged / clean / distorted by amp + pedal state), press Done! | `TryGearStep` + `EffectPedal` |
| 10 | Purchase on the permanent cashier UI, only when all gear rests on the counter: items vanish, register sound, green burst | `PurchaseStep` + `CashierUI` |

## Sakura Park: the vending corner

Root `SakuraParkQuest`. The model is `Assets/VendingMachinePark.blend`; the user has **unpacked** its scene object `VendingMachinePark` completely (no prefab instance left, checked 2026-10-22). About 60 scene meshes still reference sub-assets of that `.blend`, so **never delete or replace it**. Copying a newer `.blend` over it updates mesh shapes only: new objects don't appear and moved objects don't move. Its source file is `C:\Users\support\Documents\VendingMachinePark.blend`. Features are found by material: mail slot `BlackTorii`, coin plate and card reader `BrushedSteel`, white button panel `SmartMachine` (on `VendingMachine`), screen `TealScreen`, trays `Black`. The bins are `RecyeleBin` (sic) and `TrashBin`.

| # | Action | Step class |
|---|---|---|
| 1 | Post the mail in the mailbox slot: swish, mail vanishes | `InsertItemStep` |
| 2 | Coin into the vending machine's coin slot: coin rattle | `InsertItemStep` |
| 3 | Press Coca Cola / Pepsi / Sprite on the white panel: can clunks into the tray, then rolls toward the bench | `VendingChoiceStep` |
| 4 | Sit on the bench: "Sit down" button (fade + creak) or teleport anchor | `SitStep` + `ScreenFader` |
| 5 | Hold the can at your mouth: gulps, sigh | `ConsumeStep` (Drink) |
| 6 | Can into the Recycle bin only | `DisposeItemStep` |
| 7 | Pick a snack on the smart machine's screen UI | `SnackChoiceStep` + `SmartMachineScreen` |
| 8 | Card appears in the hip pocket; tap it on the reader, which fails twice then approves; the snack drops | `CardPaymentStep` + `HipHolster` |
| 9 | Hold the bag at your mouth: munches, the bag flattens | `ConsumeStep` (Eat) |
| 10 | Bag into the General bin only: confetti + fanfare | `DisposeItemStep` |

## Cafe: barista shift (all 10 actions built, being play-tested)

Root `CafeQuest`, model `Assets/Cafe.fbx` as a **linked prefab instance** named `Cafe` (not unpacked: components are added onto its objects, nothing is reparented). Source `Documents\VRVillage\Houses\Cafe\Cafe.blend`. Menu **VR Village → Setup Cafe Quest** (`CafeQuestSetupUtility`); scripts in `Assets/Scripts/Cafe/`, sounds in `SynthAudio.Cafe.cs`.
- `CafeDoor`'s origin is on its hinge, so `DoorMover` rotates the door itself (outward swing). `GrinderCrank`'s origin is on the grinder shaft. `Portafilter` is modelled locked in `EspressoMachine`; that pose is the machine socket.

| # | Action | Step class |
|---|---|---|
| 1 | Open the door | `DoorOpenStep` |
| 2 | Touch the OPEN sign (`OpenNeon`, off at start): flicker + buzz | `TouchSignStep` + `NeonSign` |
| 3 | Each play, 4 random indoor chairs are scattered to random clear floor spots (physics-checked, one maybe knocked over). Out-of-place chairs pulse yellow with a "Move me!" marker; empty spots show a green ghost chair; fanfare | `ArrangeChairsStep` |
| 4 | Portafilter into the grinder socket, turn the crank 3 times: grounds fill | `GrindStep` + `CrankHandle` |
| 5 | Lock the portafilter into the machine socket: metal clack | `SocketItemStep` + `SocketSound` |
| 6 | Any `Cup` into the cup socket on the machine's drip tray | `SocketItemStep` (chosen cup = `ChosenItem`) |
| 7 | Espresso / Latte / Cappuccino on the machine's `White` panel (UI): pump hum, coffee stream fills the cup | `BrewStep` + `CupFill` + `StreamFx` |
| 8 | Cup out of the machine, tip `MilkPitcher` over it: milk stream, cup turns the drink's colour | `PourMilkStep` |
| 9 | Cup on a `Saucer`, a `Cake` from the pastry case on another (a saucer holds one item) | `PlateStep` + `PlateHolder` / `PlateItem` |
| 10 | Both plates set down at the front counter's "Here!" zone: confetti + fanfare | `ServeStep` |

- `PastryCase` has no collider on purpose: hands reach through the glass; cakes rest (kinematic) until first grabbed.

## Laundromat: self-service laundry (all 10 actions built, being play-tested)

Root `LaundromatQuest`, model `Assets/Laundromat.fbx` as a **linked prefab instance** named `Laundromat` (source `Documents\VRVillage\Houses\Laundromat\Laundromat.blend`, Claude-built, must be declared). Menu **VR Village → Setup Laundromat Quest** (`LaundromatQuestSetupUtility`); scripts in `Assets/Scripts/Laundromat/`, sounds in `SynthAudio.Laundry.cs`. `BuildHolster` now lives in `QuestBuildKit` (shared with Sakura Park).
- Washers (+ change/queue machines) face out from the back wall, dryers from the side wall; fronts come from row averages, not `InwardDir` (corner machines snap wrong). Washers number 1-8 left to right; dryers 1-3 bottom row, 4-6 top. Number tags under `Machines/Tags`.
- Card and coins are copies of Sakura Park's `CreditCard` / `Coin` meshes (`Items/`), so those models count once.
- `LaundryContainer` + `LaundryItem`: a shirt let go of in the container's opening is packed (shrunk, kinematic child, collisions with the container ignored); grabbing takes it out. Shirt grabs have `retainTransformParent` off.
- The user kept their own spec for 5-10 (they declined making 3/6/8/10 less alike).
- `LaundryMachine` per washer/dryer (door, drum spin that stops on a full turn, drum `LaundryContainer` for washers, run loop). `MachineDoor` swings about world up through the hinge origin. `QueueStep.Machine(dryer)` gives the booked one (number 1 if none booked).
- `MachineAnchor`: panel anchors, zones, "Here!" markers and the finale FX are placed around washer 1 / dryer 1 (under `FollowBooking`) and move to the booked machine at runtime; `m_KeepHeight` for floor zones and dryer-stack things.
- Dryer doors: split and in the FBX (checked 2026-10-24: `Assets/Laundromat.fbx` contains `DryerDoor`). Layout (`DryerDoor[.00N]` / `DryerTopDoor[.00N]`, children of each dryer, origin on the hinge, shared mesh `DryerDoor`; backup `Documents\VRVillage\Other\BlendBackups\LaundromatPreview_before_dryer_doors_2026-10-23.blend`). Without them in the FBX the dryers work doorless (setup warns). The dryers have a real drum cavity (boolean on the body box only; an exact boolean on the whole mesh dropped the loose display panel). Backup before: `BlendBackupsaundromatpreview_before_dryer_drums_2026-10-23.blend`.
- Step 6 also takes a basket spill: basket held, tipped past 65 degrees near the washer door, shirts move into the drum one every 0.15 s. Packing never resizes a shirt: an XR grab keeps the scale it was picked up at, so any pack-time shrink stuck and compounded. Instead the setup sets the shirts to 0.6x the model's size once (`ShirtScale`, from the prefab source, so re-runs don't stack). Washer drums take a shirt even while held (`m_TakeWhileHeld`, cancels the hand's selection) and their opening reaches 0.35 m out of the door. Basket and carts only pack shirts from their own step on (`m_Accepting` off at start). Their grab collider is a shell (slab under the inside floor + 4 thin walls, `ShellColliders`), so a hand reaching inside grabs the shirts, not the container. Container floors/openings are NOT KeepPlacement'd: the first run's wrong cart floor (at the wheels) kept being restored and masked every fix. The inside floor is the highest flat mesh face under the container's middle (raycasts against a probe failed for the cart).
- Carts: grabbable, `FreezePositionY` + freeze X/Z rotation, no throw, so they slide like pushed carts. Wet shirts = darker via property block (`LaundryItem.SetWet`).

| # | Action | Step class |
|---|---|---|
| 1 | Press `SwitchButton`: button pushes in, 3 `CeilingFan`s spin up | `SwitchStep` + `CeilingFan` |
| 2 | Card from the hip pocket onto the change machine's right grey pad: approved, coins drop on the tray; card back in the pocket = put away | `CardChangeStep` |
| 3 | Pick a washer and a dryer on the `QueueMachine` screen (UI over `DarkGlass`), coins into its `Chrome` slot | `QueueStep` |
| 4 | All 6 shirts into the blue `LaundryBasket` | `FillContainerStep` + `LaundryContainer` |
| 5 | Basket set down on top of the booked washer ("Here!"): its door opens | `PlaceOnMachineStep` |
| 6 | Shirts into its drum + tip `DetergentBottle` over the door (stream, 2 s): door shuts, drum spins, slosh loop | `WasherLoadStep` |
| 7 | Empty bottle into `TrashBin`: drum winds down, end tune (original, LG-style), door opens, shirts wet | `FinishWashStep` |
| 8 | Push a `LaundryCart` to the "Here!" zone in front of the washer: the shirts move from the drum into it one by one | `CartLoadStep` |
| 9 | Push that cart to the booked dryer: its door opens | `CartToMachineStep` |
| 10 | Cart locks in front of the dryer (ungrabbable, kinematic); shirts into the dryer door: door shuts, hum, confetti + fanfare | `DryerLoadStep` |

(3 and 4 were swapped on the user's request so the player doesn't walk back and forth; the setup renames the old step/anchor objects.)

## Arcade: game night (all 10 actions built 2026-10-24, being play-tested)

Root `ArcadeQuest`, model `Assets/Arcade.fbx` as a **linked prefab instance** named `Arcade` (at x 52.5 in the village). Menu **VR Village → Setup Arcade Quest** (`ArcadeQuestSetupUtility`); scripts in `Assets/Scripts/Arcade/`, sounds in `SynthAudio.Arcade.cs`.
- Card mechanic: `ArcadeGameStep` (base for games 3-8). Tap the arcade card (held in hand) on the machine's `CardReader`-material box; the card is pulled from the hand back into the hip pocket, "READY... GO!", then the subclass runs; `ShowScore` puts a live score on the machine's screen, `Win()` finishes. No tickets, no prices (user's call).
- The card is a pink copy of Sakura Park's `CreditCard` mesh (`Items/ArcadeCard`). It comes out of / goes back into the **grey `Steel` strip on the staff side of `PrizeCounter`** (in front of the register; the register's `Screen` faces the staff side too). `FreeAfterGrab` makes the presented (kinematic) card a normal physics item once picked up.
- Building neon (`NeonPink/Cyan/Green` slots) is dark until the door first opens (`NeonFlicker`, property blocks).
- A machine's front = the side its card reader is on (`ReaderFront`); the basketball machine is long, so its centre gives the wrong side.
- Rhythm game buttons: the 8 separate `Yellow` parts of `MaimaiMachine`, found by clustering triangles; touch boxes + glow discs under its `QuestParts`. Score on the round `Screen` inside the ring.

| # | Action | Step class |
|---|---|---|
| 1 | Open the door (neon flickers on) | `DoorOpenStep` + `NeonFlicker` |
| 2 | Press **Get card** on the register screen (UI); the card slides out flat (horizontally) from the grey slot's staff-side face (`Counter/CardOutFlat`); grab it | `GetCardStep` |
| 3 | Rhythm game: card on the reader, touch the glowing button 8 times (notes climb the scale) | `RhythmGameStep` |
| 4 | Blue `ArcadeCabinet`: grab `Joystick` by its red ball (touch box `Joystick/QuestParts/Grip`; tilts about its base), steer the ship on the screen UI, catch 5 falling stars | `CabinetGameStep` + `JoystickLever` |
| 5 | Hoops: throw `Basketball`s through the `Orange` rim (crossing it downward), 3 baskets; aim assist for throws within 30 deg; balls return to their ramp spots | `HoopsGameStep` |
| 6 | Whack-a-mole: `Mole`s pop between the lowest and highest modelled heights; hammer head (`Red` part) near a popped mole while swinging = hit; 5 hits | `WhackGameStep` |
| 7 | Air hockey: red `Pusher` hits the `Puck` (no gravity, height locked) into the gap in the far rail; generated box colliders (surface, rails, goal back) under the table's `QuestParts`; no score screen (the table has none, user's call); 1 goal | `AirHockeyStep` + `PuckSound` |
| 8 | Claw: LEFT/BACK/FRONT/RIGHT/DROP panel on the ledge (or the red button), the claw drifts onto the nearest `Plush`, carries it to the grey chute, the machine spits it out of the black flap onto the floor (no hollow in the model; `Machines/Claw/PrizeOut`, `m_SpitDirection`) | `ClawGameStep` |
| 9 | Set the won plush down in `Counter/PlushZone` (customers' half, far end from the register, "Here!") | `PlushToCounterStep` |
| 10 | Card (held) into `Counter/CardReturnZone`: it slides back flat into the grey slot and is gone, register says thanks, confetti + fanfare | `ReturnCardStep` |

## Teammates' houses (imported 2026-10-24)

- **Phumiphat** (5 houses): `.unitypackage` from github.com/Phumiphatrk/VRVillage-5Houses, extracted into `Assets/RamenVR` (namespace `RamenVR`). The user drags the 5 prefabs from `Assets/RamenVR/Prefabs` into the village. Never click the `Ramen VR` menu (it rebuilds houses). Their restart buttons act as Close buttons in the village. Their 32 canvases were lowered from `dynamicPixelsPerUnit` 10 to 4 on 2026-10-25 (the legacy font texture overflowed); the ramen shop's pink stools/bowls are Phumiphat's own flat colours (`beaw`, `goat bolw naja`), not broken materials.
- **Narin** (5 houses incl. the Backrooms): source github.com/NarinKong/Finals, a whole project whose houses are generated by an editor script. Adapted by Claude into `Assets/Narin` (Houses/Blender assets with original GUIDs, namespace `NarinVillage`, script `Assets/Narin/Editor/NarinHousesSetupTool.cs`, menu **VR Village → Setup Narin Houses**; debug items under `VR Village/Narin Debug`). It builds into `Village.unity` under the root `NarinHouses`; the village parts sit under `NarinHouses/NarinVillageArea` (default (20, 0, -60), rotated 180°; a re-run keeps where the user moved it), the Backrooms dimension stays at (2000, 0, 2000). Changes from Narin's original: no rig changes (no `VRPlayerCollisionEnforcer`, no CharacterController edits, no wrist UI, no simulator/Player tag), no teleport at start, no forced mouse UI input or `PhysicsRaycaster` on the camera, no 140 m ground cube or directional-light edits. All scene-wide searches (`Trash`/`Bin`, `Almond`, `Cash Register`, canvases, mouse/E-key clicks) go through `NarinScope` so they only touch objects under `NarinHouses`. Narin's "gather 5 Almond Waters at the spawn desk" pass is removed (stale positions, never worked; reviving it made a pile at the spawn): bottles come from the maze; `ConfigureAlmondBottle` now also sets the Rigidbody pose, because interpolated copies of the template bottle snapped back to the template by the spawn desk (the actual pile). Backrooms fixes: `TeleportToBackrooms` teleports first, then starts the generator (it builds no chunks/floors while the player is near the village, so the old order left the player falling into the void); the manager puts the player back at the Backrooms spawn if they drop below y -3; the in-Backrooms terminal keeps only flashlight / drink / emergency teleport. The village's XR Origin sits under the scene object `Other`, so Narin's `transform.root` moved that whole group: teleports, the generator's player and the monster's "is this the player" checks now use the XROrigin (`NarinScope.PlayerRig`). `GrabAndPlaceItem` only counts a placement after the item was grabbed once (items starting on their targets auto-completed 6 tasks at play start). The generator switches every directional light off while inside the Backrooms and back on when leaving (Narin's project hid the sun with 45 m shadows; ours renders 2.5 m). Narin's project also had point lights enabled in `Performance URP Config` (ours: `m_AdditionalLightsRenderingMode` 0 = off), so the ceiling lights don't render here. Their wallpaper texture (`high_quality_backroom_s_wallpaper...jpg`) was lost to the Windows path limit when cloning and restored from the git blob on 2026-10-25. World canvases use `dynamicPixelsPerUnit` 3 (10 overflowed the shared legacy font texture: "Failed to update dynamic font (LegacyRuntime)").

## Village layout (presentation plaza, 2026-10-25)

Defined once in `VillageLayout` (`Assets/Scripts/Editor/VillageLayoutArranger.cs`); menu **VR Village → Arrange Village Layout** (one undo step, re-runnable, does not save) places all 15 houses, the spawn and Narin's board/return spot, then rebuilds decor and lights. Backup of the scene before this layout: `Documents\VRVillage\Other\SceneBackups\Village_before_plaza_2026-10-25.unity`.
- **Presentation plaza** (x 2..26, z -11.5..8.5) in the middle of the block with the 5 demo houses facing in: Sakura Park + Arcade (south side, facing north), Ramen (north), chicken farm (west), the Backrooms house (east). Spawn = plaza centre (14, -1.5) facing Sakura/Arcade; escaping the Backrooms lands next to it. Demo order: Sakura, Arcade (Wikorn) → Ramen, farm (Phumiphat) → Backrooms (Narin).
- **Ring road** (asphalt x -23..56, z -32..28, 6 m + 2 m sidewalks) round the block; the other 10 face it from outside: Guitar, Cafe, Laundromat (south), mini-mart, house, mechanic (north), storage + park (west), fish + flower (east). Narin's debug board stands on the east road.
- Wikorn's houses only slide (building + quest root, `NeverRotate`); their quest scripts store only directions/sizes/local offsets, so sliding is safe. Re-run the Sakura / Arcade (and Cafe / Laundromat) setups after a move so guide areas re-fit neighbours. Entrances: Wikorn's and Phumiphat's (except Ramen, -Z) face local +Z; Narin's are fixed per house from Narin's own build script: storage, house and Backrooms house enter on local -Z, mini-mart and mechanic on local -X (guessing from doors turned the storage, mini-mart, mechanic and Backrooms house wrong). `VillageLayout.WorldBounds` skips hidden renderers (Narin keeps the whole Backrooms kit hidden inside its house); the light groups keep the old include-hidden measure (`WorldBounds(t, true)`) they were pinned with.
- **VR Village → Rebuild Village Decor** (`VillageDecorBuilder`, under `VillageDecor`, materials `Assets/Materials/Decor`): roads, sidewalks, dashes, zebra crossings where the 4 walkways (`VillageLayout.Walkways`) meet the ring, paved plaza + walkways, grass on the rest of the block cut round houses, fountain + benches (NW corner), stone lanterns at walkway ends, lantern canopy on red poles over the plaza, street lamps (not at walkway mouths), power lines on the south road, red torii welcome gate over the NW walkway (`WelcomeText`), corner signposts + "Plaza" pointers, a name sign + flower pot per house, bus stop on the west road, farm fence (gate toward the plaza), trees outside the ring and a grove on the block (sakura near Sakura Park, dead trees by the Backrooms). Primitives, static batching, no shadows; anything landing in a house is skipped.
- **VR Village → Rebuild House Lights** (`VillageLightingBuilder`, also run by Arrange): `VillageLighting/Lights_<House>` per indoor house; a house without a group gets 1-4 soft point lights (grid over the footprint, intensity 1.4, colour per house; a building receives max 4 per-object lights). **Existing groups are the user's hand-tuned lights (Cafe 10, Laundromat 6, Arcade, Flower, Guitar edited 2026-10-25) and are never rebuilt**; Arrange pins them to their building (`AnchorExisting`) before moving anything and re-anchors after, so they travel with the house. Delete a group to regenerate it. Narin's 16-18 m `Interior_PointLight`s are switched off. Point lights were enabled in `Performance URP Config` on 2026-10-25 (per pixel, 4 per object; shadows still 2.5 m); check the Quest frame rate. The village's only sun is `GuitarCenter/Directional Light` inside the Guitar Shop model.
- **Ground:** Arrange lifts `Other/Plane` so its top meets the houses' bases (median), never the other way round (lowering houses by hand left their quest roots behind, 2026-10-25). The plane used to sit 0.42 m below the houses.
- `VR Village → Report House Footprints` writes `Logs/VillageLayout.txt`. Narin's setup keeps each house's placement on re-runs and no longer builds their plaza/trees.

## Building the next house

0. **Models first, in Blender, by the user:** plan the ≥8 models with them (see "Blender models"), then check UVs, materials, applied scale and naming before it goes to Unity.
1. **Get the spec from the user first.** They describe each action as **panel text → what the player does → completes when → feedback**. If something is unspecified, pick something sensible and say what you picked.
2. **Inspect the scene before writing code.** Grep `Village.unity` for `m_Name:` to find the building root and object names; the scene is ~15k lines, so never read it whole. Building meshes come in as unpacked FBX objects with no colliders or components; positions come from renderer bounds at setup time.
3. **Reuse `QuestBuildKit` and the generic steps** before writing new ones. Locate model features by material name with `FrontFaces` / `MaterialBounds`; check them first with the Blender connection or a headless Blender script.
4. **Give each house its own:**
   - root object (`<House>Quest`)
   - setup file (`<House>QuestSetupUtility.cs`) with its own menu item `VR Village/Setup <House> Quest`
   - `QuestManager`, `QuestPanel`, `QuestGuide` + `HouseArea`
   - step classes, only when an existing one doesn't fit; prefer the generic ones (`ItemToCounterStep`-style zones, socket steps, button steps)
5. **Satisfy the grading checklist** (UI, script-moved item, grab/teleport, 10 distinct actions), and make every action readable without a briefing:
   - a short panel text
   - a progress line with clear status, not cramped: one instruction line, a status row with green/grey states, and one hint line
   - an obvious completion signal
   - fill `m_ShortInstruction` and the guide targets
6. **Compile-check offline**, then tell the user exactly what to do in Unity:
   - let it recompile
   - run the setup menu
   - what to test
   - which inspector fields to tweak

## Working with the user

- They're time-pressed: keep replies short and practical, and implement one batch at a time; they test in the XR Interaction Simulator between batches.
- They decide feel and UX: the static big panel, muted guitar noise during action 2, the "Here!" marker, and so on.
- They adjust placement by hand in the Scene view. Never overwrite anchors or `QuestParts` placement they've moved.
- Later: make sure an Android/Quest build works; the APK is what's graded.
- `HANDOFF.md` is an older snapshot from before actions 5–10 were added. This file supersedes it.
