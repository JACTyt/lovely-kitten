# Lovely Kitten

A cozy pixel-art roguelike about a cat catching birds. Each day is a timed round with a coin goal; at nightfall
the kitten curls up to sleep and you spend the day's coins in the Dream Market. Miss a goal and the run ends —
but the stars you earned unlock new items and cosmetics for the next run.

Built with **Godot 4.7 (.NET / C#)**.

## Features

- **Hand-tuned jump** — analytic arc (no physics engine), hold-to-jump-higher, jump buffering, air control,
  faster falls, screen-edge wrap.
- **Roguelike runs** — 120 s days (90 s day + 30 s dusk with swooping owls), rising daily goals, a 3-item night
  shop with re-rolls, run-over summary.
- **Active skills** — Pounce Dash, Tail Swipe, Dive Bomb, Meow, Yarn Ball and Cat Time, each with its own key and
  cooldown. Catches made mid-skill give bonus combo.
- **12 passive run items** — Spring Paws, Double Jump, Bird Magnet, Catnip, Lucky Coin, Glide, Nine Lives and more.
- **Bird events** — Flock!, Golden hour and Windy.
- **Combos** — chained mid-air catches multiply coins up to ×4.
- **Meta progression** — earn stars per run; unlock items and buy cat furs, hats, scarves, glasses, bird skins and
  backgrounds (Street, Rooftops, Park — each with a night version).
- **Juice** — feather bursts, floating coin text, screen shake, procedurally generated chiptune music and SFX.

## Controls

| Key | Action |
|---|---|
| A / D, ← / → | Move |
| Space / W / ↑ | Jump (hold for higher) |
| C | Nap — birds fly low and slow |
| Shift | Pounce Dash |
| E | Tail Swipe |
| S / ↓ (in air) | Dive Bomb |
| Q | Meow |
| F | Yarn Ball |
| R | Cat Time |
| Esc | Pause |

Skills must be bought in the night shop during a run.

## Getting started

Requirements: [Godot 4.7 .NET edition](https://godotengine.org/download) and the .NET 8 SDK.

1. Open `project.godot` in Godot.
2. Let the editor import assets and build the C# solution.
3. Press **F5**.

Command-line build check:

```sh
dotnet build LovelyKitten.csproj
```

### Dev launch arguments

Pass after `--` (e.g. `godot --path . -- --dev-day=10`):

| Argument | Effect |
|---|---|
| `--dev-day=<seconds>` | Shorter days |
| `--dev-coins=<n>` | Start a run with coins (counts toward the day goal) |
| `--dev-items=dash,meow,...` | Start a run with items (ids in `autoload/Catalog.cs`) |

## Regenerating art & audio

All generated sprites, backgrounds, UI, sound effects and music come from one deterministic script:

```sh
pip install pillow
python tools/art/generate.py
```

Outputs go to `sprites/generated/`, `audio/generated/` and `entity/cat/CatAnchors.g.cs`. Reopen the editor (or run
`godot --headless --import`) afterwards so Godot re-imports them.

## Project layout

```
autoload/   GameState (meta + save), RunState (run loop), Catalog (items & cosmetics), Audio, SaveGame
entity/     cat (movement, clothes) and bird (creature kinds)
scene/      main scene, spawning & catching, skills, camera, background, effects
ui/         HUD, night shop, run over, collection, menus, shared UI kit
shaders/    cat palette swap, HSV tint
tools/art/  asset generator
docs/       design specs and implementation plan
```

Save data lives in `user://save.cfg` (on Windows: `%APPDATA%\Godot\app_userdata\LovelyKitten\`).

## License

[MIT](LICENSE) © 2026 Dmytro Maistruk
