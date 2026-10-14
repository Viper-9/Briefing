# Third-party notices

## Hysocs-OperatorTraits (Apache License 2.0)

`client/Briefing.Client/Tab/TabRowLayout.cs` is adapted from the tab-row layout of Operator Traits by Hysocs (median
tab spacing, first-tab x, row-width rule) and modified for this project. The inventory tab injection and selection
bridging approach (cloning a native `Tab`, bridging selection with the native `TabGroup`) follows its design.

Licensed under the Apache License, Version 2.0. Full text: [LICENSES/Apache-2.0.txt](LICENSES/Apache-2.0.txt)
(also at https://www.apache.org/licenses/LICENSE-2.0). The files carry modifications by the Briefing authors.

## LiberationSans (SIL Open Font License 1.1)

`briefing.bundle` embeds `Placeholder SDF`, a small static signed-distance-field atlas (128x128, digits, Latin letters and a few
punctuation marks) generated from LiberationSans by the prefab generator. It is an editor preview font only; at run time every text uses
the game's own fonts. The LiberationSans font file itself is not shipped. License text:
`unity/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` (shipped in the release zip as `LICENSES/OFL-1.1-LiberationSans.txt`).

## TextMesh Pro Essential Resources (Unity Companion License)

The TextMesh Pro Essential Resources in `unity/Assets/TextMesh Pro` are provided by Unity Technologies under the
Unity Companion License. `briefing.bundle` includes the TextMesh Pro `TMP_SDF-Mobile` shader from them.

## SPT-DynamicMaps (MIT License)

The map projection, floor selection and floor display rules in `client/Briefing.Core/Map/MapProjection.cs` are ported from
SPT-DynamicMaps (`MapView`, `MapLayer`) by way of QuestCodex's `web/src/wiki/mapProjection.ts`, and the SVG rendering path in
`client/Briefing.Client/Map/SvgSprites.cs` (import options, tessellation presets, vertex budget, sprite options) follows its `SvgUtils`.
`client/Briefing.Tests/Fixtures/map.bigmap.json` is QuestCodex's conversion of SPT-DynamicMaps' map calibration data (commit 4944764),
used as test data only. https://github.com/acidphantasm/SPT-DynamicMaps

MIT License

Copyright (c) 2025 Michael P. Starkweather

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## tarkov-dev (MIT License)

The extract and transit marker icons `unity/Assets/Briefing/Icons/extract_{pmc,shared,scav,transit}.png` (embedded in
`briefing.bundle`) are copied unmodified from `public/maps/interactive/` in tarkov-dev (added by Shebuka in commit bb40b16), by way of
QuestCodex. `client/Briefing.Tests/Fixtures/map.interchange-manimal.json` is QuestCodex's conversion of tarkov-dev's
`src/data/maps.json` calibration for the expanded Interchange, used as test data only. https://github.com/the-hideout/tarkov-dev

MIT License

Copyright (c) 2019 Oskar Risberg

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Locked door icon (CC BY 3.0)

`unity/Assets/Briefing/Icons/door_with_lock.png` (embedded in `briefing.bundle`, tinted red at display time) is the "Locked door"
icon by Delapouite, game-icons.net (https://game-icons.net/1x1/delapouite/locked-door.html), licensed under CC BY 3.0
(https://creativecommons.org/licenses/by/3.0/). SPT-DynamicMaps scaled it down to 60x60 and added a black outline; Briefing ships that
PNG unmodified, by way of QuestCodex.

## Map SVGs (not distributed)

Briefing does not ship any map SVG. It downloads them at run time from the QuestCodex server mod, which carries their licenses
(tarkov.dev maps by Shebuka, CC BY-NC-SA 4.0); the map panel shows the author and license from `map.json` `attribution`.
