# Briefing

**Language: [English](README.md) | [한국어](README.kr.md)**

![Briefing screen](media/overview.png)

## Purpose

A client mod that puts everything you check before a raid (**the quests for this map, where the objectives are, the
items to bring and the gear quests ask for**) on a single **Briefing** screen inside the character screen.

The data comes from the [QuestCodex](https://github.com/Viper-9/QuestCodex) server mod, so you get the same results
as QuestCodex's raid prep page in the browser, without leaving the game. Quests changed or added by quest mods show up
exactly as QuestCodex reads them.

## Requirements

- SPT `~4.1.6`
- **[QuestCodex](https://github.com/Viper-9/QuestCodex) 1.9.0 or later** (server mod). It provides the briefing data
  and the map files. Without it, the tab only shows a "QuestCodex is required" notice.

## Installation

1. Install QuestCodex 1.9.0 or later first.
2. Download `Briefing-<version>.zip` for the version you want from [Releases](../../releases).
3. Extract it and overwrite the resulting `BepInEx` folder into your SPT install root (the folder containing
   `EscapeFromTarkov.exe`). (`BepInEx/plugins/Briefing/...` structure)
4. Restart the server and the game.

## Opening it

The **TASKS** tab in the character screen's tab row becomes a dropdown. Click it and pick **Briefing** to open the
briefing screen, or pick **TASKS** to go back to the regular tasks tab.

![Tasks tab dropdown](media/tab-dropdown.png)

The screen follows the game language: Korean and Russian, English for everything else.

## Features

The screen has a **map bar** at the top and three columns below it (quest list · map · supplies/collect/gear).
The number next to each map name is how many objectives you have there. Maps with no objectives can still be picked
to look at the map and its extracts.

### Quest list

<img src="media/quest-list.png" width="420" alt="Quest list">

- Quests to do **on this map**, plus an **Any map** group for quests that work anywhere, with the trader's face,
  an objective summary, progress numbers and a bar.
- The number badge (①②③…) next to a map quest matches the marker number and colour on the map.
- Outside a raid two more groups appear:
  - **Ready to turn in**: at the top. Quests that only need a report to the trader.
  - **Hand in**: quests with item hand-over objectives left. Quests you can hand in right now get a light blue hand
    icon after the name.

### Expanding a quest · hand-overs · turning in

Click a quest row to expand its objectives in game order, with the target item names in small print below.

![Hand-over](media/quest-handover.png)

- **TURN IN** on a hand-over objective submits items the same way the game does. The briefing refreshes as soon as
  it's done.
- **SHOW IN TASKS** switches to the tasks tab with that quest expanded.

![Turn in](media/quest-turn-in.png)

- For a **Ready to turn in** quest, **TURN IN** opens that trader's task screen with the quest selected. You press the
  game's complete button yourself, and closing the trader screen brings you back to the briefing tab.
- The expanded quest is highlighted on the map and the right column narrows to that quest (see
  [Selected quest only](#selected-quest-only) below).

### Map

<img src="media/map.png" width="480" alt="Map">

- Quest markers, quest zones, extracts and locked doors on one map. Hover an extract or a locked door to see its
  name, conditions or key.
- Toggle **Extracts (PMC / Scav / Transit) · Locked doors · Quest zones** with the checkboxes at the top.
- Switch floors with the floor buttons. A floor button gets a red dot when another floor has quest markers.
- Wheel to zoom, drag to pan, double-click to reset, or use the `+` / `−` buttons.
- Hovering a quest row highlights only its markers, and clicking a map marker jumps to that quest in the list.
- The map is QuestCodex's SVG, drawn inside the game. After the first map opens, the other maps are prepared in the
  background.

### Supplies

![Supplies](media/right-supplies.png)

Quest items you need to **bring** into the raid (items to plant and so on), as a grid of game icons. Each cell shows
have/need; red means short, green means covered. The header shows how many rows are short.

### Collect

<img src="media/right-collect.png" width="420" alt="Collect">

Items you need to **find** in raid. The ones found on this map first, then the ones you can get on any map.
FIR and quest item marks use the same icons as the game stash, and `+N` means there are alternative items.
Hover a cell for the full name, the alternatives and the quests that need it.

### Gear

<img src="media/right-gear.png" width="420" alt="Gear">

- **Current gear**: your equipped primaries, holster, helmet and body armor (or rig).
- **Weapons and gear**: weapons, calibers, weapon mods and wearables that quests ask for, checked row by row
  against what you have.
  - ✓ Equipped · ◐ In stash · ⚠ Missing
  - Long lists of alternatives collapse into `+N more ▼`; click it to see them all.
- **Special conditions**: complete in one raid, survive and extract, extract via a specific exit and so on.

### Selected quest only

![Selected quest filter](media/right-filter.png)

When a quest is expanded, all three tabs (supplies, collect, gear) show only that quest. Click **Show all ×** on the
gold bar to drop the filter while keeping the quest expanded.

### Resizing columns

![Resized columns](media/resized-columns.png)

Drag the borders between columns to change the width of the quest list, the map and the right column.
Double-click a border to reset it. Widths are saved and kept across game restarts.

### In raid

- Opening the tab in raid **selects the current map** and marks it as in raid. You can still pick other maps.
- Raid objective progress such as kill counts updates **live**, and finished objectives are greyed out.
- The gear check uses what you have equipped right now, so swapping to a weapon you picked up counts straight away.
- No server requests are made during a raid. It uses the data fetched when the raid started loading.

## Settings

`Layout` section of the BepInEx config file `BepInEx/config/com.viper.briefing.cfg`:

| Setting | Default | Description |
|---|---|---|
| `Side margin` | 20 | Left and right margin of the briefing screen |
| `Top margin` | 8 | Gap between the tab row and the top of the screen |
| `Bottom margin` | 40 | Margin that keeps clear of the bottom menu bar |
| `Left column width` | 0 | Quest list width (0 = default 540). Saved when you drag a column border |
| `Right column width` | 0 | Right column width (0 = default 470). Saved when you drag a column border |

## Known limitations

- **Progress outside a raid comes from the server.** SPT only saves raid progress to the server when the raid ends,
  so the numbers in the menu are as of your last raid (in raid they are read from the game and update live).
- **The gear check works row by row.** The game ties conditions to one weapon ("kill with this weapon fitted with this
  suppressor"), but the briefing checks the weapon and the mod separately. A suppressor fitted to a different weapon
  can still show ✓.
- **Extract names are in English** in every language (tarkov.dev names).
- QuestCodex's limitations carry over. Changes from quest mods that hook responses at runtime don't show up
  (see [QuestCodex known limitations](https://github.com/Viper-9/QuestCodex/blob/main/README.en.md#known-limitations)).
- Not tested with Fika.

## License

Copyright (c) 2026 Viper-9. **All Rights Reserved.**

You may download and use this mod, unmodified, for personal, non-commercial purposes. Redistribution
(including re-uploading elsewhere), modification, derivative works and commercial use are prohibited
without the prior written permission of the copyright holder. See [LICENSE](LICENSE.md) for the full terms.

Third-party components remain under their own licenses. See [THIRD_PARTY_NOTICES](THIRD_PARTY_NOTICES.md).
The map in the screenshots is the one QuestCodex provides; its author and license are shown in the bottom right
corner of the map.
