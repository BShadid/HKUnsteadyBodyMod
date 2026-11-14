# Developing Unsteady Body

## Building

1. Install the Modding API with Lumafly **before** building. The project
   compiles against the Modding API's `Assembly-CSharp.dll` and
   `MMHOOK_Assembly-CSharp.dll`.
2. Open `UnsteadyBody.csproj` and set `GameDir` to your Hollow Knight folder.
3. Run `dotnet build -c Release`.

The build copies `UnsteadyBody.dll` into
`hollow_knight_Data/Managed/Mods/Unsteady Body/` automatically. The charm icon
(`Resources/UnsteadyBody.png`) is built into the DLL, so the DLL is the only
file the mod needs.

## Logging

With **Detailed logging** on (in the mod's options), `ModLog.txt` gets
step-by-step details: each launch's room-exit catches, momentum carries, flips
and what they updated, the charm menu's state when a flip is refused, the
language picked up, and more. Off, the log only says what the mod found at
start-up (the icon, ItemChanger) and anything that went wrong.

## Settings file

The sounds and the tracker bubble are always on and aren't in the options menu.
They can still be turned off in the mod's global settings file
(`UnsteadyBodyMod.GlobalSettings.json`, next to the game's save files), with
the game closed:

| Setting | Default | What it does |
|---|---|---|
| `Sounds` | `true` | The launch whoosh, the impact thud and any other sounds the mod plays. |
| `TrackerBubble` | `true` | The off-screen bubble. |

## Translations

All player-facing text is in `Strings.cs`, one table per language, using the
game's language codes. Anything missing in a language falls back to English.
The options menu always uses the English entries (the Modding API builds it
once, in a font without Chinese, Japanese or Korean characters); its
translations are kept in the table in case that changes.

## Test plan

Run on the exact build being released, with a fresh save unless noted.

**High risk** (must pass before every release):

1. **Buying it.** Salubra sells Steady Body with its usual icon and description,
   plus the hint "Some say it does the very opposite when turned over."
2. **Turning it over.** At a bench, select the charm: "(Dream Nail) Destabilize"
   appears at the bottom left of the description box. Press it. The charm flips;
   its name, description and icon change in the charm grid, the details panel
   and the equipped row; the prompt reads "Stabilize". Flip it back.
   Selecting an empty notch or another charm shows no prompt and doesn't flip.
3. **It stays turned over.** Save & Quit and reload: the charm is still the side
   you left it. A different save file is unaffected.
4. **Launching.** Unsteady side up and equipped: sideways strikes throw them
   backwards on the ground and in the air; downward strikes launch them
   skywards and stop at ceilings; upward strikes in the air drop them. Steady
   side up, or unequipped, everything is vanilla.
5. **Room exits at top speed.** Launch Strength Ludicrous, Speed Limit Off: 4x.
   Fly out through a left, right, top and bottom exit. Each one loads the next
   room; they never fall forever or lands in the wrong place after a hazard
   respawn.
6. **Randomizer.** In a Randomizer 4 seed, pick up the charm from a randomized
   location: the pop-up shows the right icon for its side, and it can be
   turned over at a bench.
7. **Custom Knight.** With a skin applied, the charm shows the Unsteady icon in
   every place from step 2, including after reopening the menu.

**Medium risk:**

8. **Carry momentum** (option on). A launch still going at an exit continues
   into the next room from the side, top and bottom. Boss arenas that lock you
   in (False Knight) still lock.
9. **Tracker bubble.** Launch far off screen: the bubble shows them live, points
   the right way and shrinks with distance. It disappears when they're back.
10. **Sounds.** A whoosh on launch, a thud on impact with a wall, floor or
    ceiling.
11. **Another language.** Switch the game to one non-English language: the
    charm text and the Destabilize/Stabilize prompt are translated and the
    prompt still lines up with the game's own.
12. **Turning the mod off** in its options, then on again: no errors in the log,
    and the charm behaves as vanilla Steady Body while it's off.
13. **Quiet log.** With Detailed logging off, a few minutes of play adds almost
    nothing to `ModLog.txt`.

<details>
<summary><b>Spoilers: the hidden interaction</b></summary>

With Unsteady Body (Unsteady side up) **and Heavy Blow** both equipped:

14. A low Geiger-counter crackle plays, and a green glow sits at the Knight's
    nail, flickering with each click. It follows them through turning, wall
    clinging, lying down and the Challenge pose (moving to the drawn nail once
    it's out). It hides with them in cutscenes. With `Sounds` off in the
    settings file, the glow still flickers, silently.
15. A nail strike on an enemy sets off an explosion (larger than a jelly's),
    launching them along the true angle away from the enemy. Terrain strikes
    stay four-directional. A grounded upslash doesn't explode.
16. The explosion costs them a mask (two when overcharmed) without stopping their
    flight, with the game's damage flash and muffled sound; the Geiger clicks
    muffle too. A blast that would kill them does kill them normally.
17. Nearby enemies take nail damage and are pushed back harder at higher Launch
    Strength settings.

</details>

## Releasing

1. Set the version in both `UnsteadyBodyMod.cs` (`GetVersion`) and
   `UnsteadyBody.csproj`, as four numbers (`0.1.0.0`).
2. Run the high-risk checks above on that build (and the spoiler checks if
   that code changed).
3. Create a GitHub release tagged `vX.Y` with `UnsteadyBody.dll` zipped as
   `UnsteadyBody.zip`.
4. Open a pull request to [modlinks](https://github.com/hk-modding/modlinks)
   updating the mod's entry: the version, the release link and the zip's
   SHA256 (without the `sha256:` prefix).

## Source files

| File | What it does |
|---|---|
| `UnsteadyBodyMod.cs` | The mod: hooks, charm name and description, options menu. |
| `UnsteadyBodySettings.cs` | Options (global) and which side is up (per save). |
| `Strings.cs` | Every player-facing text, in all ten of the game's languages. |
| `Launch.cs` | Launch strength and speed presets; scales the game's recoil and pogo values. |
| `Glide.cs` | Lets a sideways launch coast to a stop in the air instead of stalling. |
| `LaunchGuard.cs` | Catches room exits skipped at high speed; continuous collision during launches. |
| `Momentum.cs` | Carry momentum: continues an unfinished launch into the next room. |
| `LaunchSounds.cs` | The launch whoosh, impact thud and flip sound. |
| `TrackerBubble.cs` | The off-screen bubble with a live view of the Knight. |
| `CharmIcon.cs` | The Unsteady icon in menus, Salubra's shop, Custom Knight and ItemChanger. |
| `CharmFlipper.cs` | Turning the charm over at a bench. |
| `FlipPrompt.cs` | The Destabilize/Stabilize button prompt in the charm menu. |
| `JellyBlast.cs` | Spoiler: the Heavy Blow explosion and its damage to enemies. |
| `AngledLaunch.cs` | Spoiler: launching along the true angle away from a struck enemy. |
| `BlastHurtsKnight.cs` | Spoiler: the explosion's damage to the Knight, without knockback. |
| `GeigerCounter.cs` | Spoiler: the Geiger-counter clicks and their damage muffle. |
| `RadiationGlow.cs` | Spoiler: the glow at the Knight's nail. |
