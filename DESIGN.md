---
name: Astral Alchemy XR
description: Current cinematic title menu and spatial gameplay HUD, captured 2026-09-08.
colors:
  star-gold: "rgb(100% 68% 15%)"
  cyan: "rgb(12% 85% 100%)"
  menu-idle-text: "rgb(85% 88% 93%)"
  menu-focus-text: "rgb(100% 82% 43%)"
  menu-page: "rgb(1.5% 2.8% 6.5% / 98%)"
  hud-body: "rgb(93% 94% 100%)"
---

# Design System: Astral Alchemy XR

## Overview

This document records the implemented title menu and gameplay HUD only. The title is an all-English cinematic observatory cover: midnight-blue artwork carries the scene on the right, while a separately generated gold ASTRAL ALCHEMY logo and live menu occupy the left. The gameplay HUD retains its existing dark, gold, and cyan illustrated frames and Chinese copy.

The implementation authority is `Assets/Scripts/Editor/AstralAlchemySceneBuilder.cs` (`BuildTitleScene`, `UpgradeHud`, and `ArrangeWraparoundHud`), with behavior in `TitleMenuController.cs`, `TitleMenuButton.cs`, and `ArenaGameController.cs`. Measurements below are Unity canvas units unless explicitly stated otherwise. They describe this implementation, not a general responsive web layout.

## Colors

Star gold emphasizes titles, score, and help headings. Cyan identifies the help steps. Pale neutral text stays legible against dark artwork. Menu focus uses the lighter gold token; press feedback uses white. The runtime menu background is warm brown `(0.24, 0.18, 0.075)` in Unity RGB, with alpha `0.12` at rest, `0.62` on focus, and `0.8` during a press.

HUD status messages retain their event or element hue but blend it toward white by `0.58`. Empty element slots use a dim Star sigil at alpha `0.16`; selected slots show their full-color generated icon.

## Typography

Live text uses `Assets/Art/UI/Fonts/ArialUnicode.ttf` (Arial Unicode), with Unity `LegacyRuntime.ttf` as the code fallback. It is normal weight. The generated logo is an image asset; it is not live typesetting and has no assigned runtime display font.

Title menu labels are 32 units. Help heading is 36, step labels 26, body 25; the tagline and controller instruction are 23. Title text uses a subtle outline offset `(0.5, -0.5)` and wraps inside its measured rectangle.

HUD text generally fits within 20–25 units; recipe detail fits 21–24, selected-element text 20–22, and the central instruction 21–22. HUD outlines use offset `(0.85, -0.85)`. Tutorial status copy explicitly uses three lines, with word wrapping and vertical truncation enabled. Keep instructional copy short enough to fit the existing region.

## Layout

The title uses a world-space canvas of `1600 × 900`, scale `0.006`, at world position `(0, 1.62, 3.2)`. The opening eye is `(0, 1.62, -2.8)`; the desktop camera field of view is 58 degrees. Background art extends to `2200 × 1257` for full-bleed framing. The separate logo is `670 × 315`, centered at `(-425, 217)` in the canvas. The three main-menu rows center at `(-435, -85)`, `(-435, -180)`, and `(-435, -275)`.

Help opens on the right in a `760 × 710` region centered at `(410, 10)`. Quit confirmation uses `720 × 470` centered at `(410, 0)`. Opening either hides the main-menu rows while preserving the cover background, logo, and tagline.

The gameplay HUD is a stationary world-space object on the upper rear arc of the arena, at `(0, 4.65, 2.1)` with scale `0.0057`. It is not attached to the camera. The opening gameplay eye is `(0, 2.65, -4.7)`; desktop field of view is 64 degrees. The side boards yaw inward around that eye position, with no pitch, roll, or runtime following.

| HUD region | Size | Canvas center | Interior |
| --- | --- | --- | --- |
| Recipe board | `380 × 380` | `(-500, 42)` | Recipe text width `306`; title y `113`, detail y `46` |
| Timer and score board | `380 × 380` | `(500, 42)` | Text width `310`; tide bar `260 × 20` |
| Core health | `640 × 119` | `(0, 206)` | Label width `560`; bar `540 × 25` |
| Central status | `600 × 140` | `(0, 45)` | Text `500 × 96`, centered: 50-unit horizontal and 22-unit vertical margins |
| Result panel | `760 × 263` | `(0, 0)` | Open star field with no inner gray card; title `610 × 52` at y `78`; detail `560 × 126` at y `-34`, line spacing `1.22` |

Selected icons are `60 × 60`, centered at `(-66, -116)` and `(66, -116)` within the recipe board. The plus sign sits between them. Their bounds leave 44 units below the icons and generous separation from the outer frame; preserve this padding when replacing artwork.

## Elevation & Depth

The cover obtains depth from its painted observatory and layered logo. Menu pages use nearly opaque dark surfaces. The HUD obtains depth from real world placement, inward yaw, generated frame artwork, and restrained text outlines. Its placement keeps the cauldron and star core unobstructed.

## Shapes

Menu hit areas are simple rectangles (`570 × 76`), with a `520 × 60` text region centered at `(8, 0)`. The gameplay panels preserve the incumbent illustrated ornamental frames. The central status frame uses Unity sliced rendering with sprite borders `(42, 18, 42, 18)` so the taller interior does not stretch the frame thickness.

## Components

**Main menu.** START GAME loads `AstralAlchemyArena`. HOW TO PLAY opens SELECT / COMBINE / CAST instructions, and BACK returns to the main menu. QUIT opens confirmation; CANCEL returns and QUIT GAME exits the player (or stops Play mode in the Editor). All six commands are live controls.

**Pointer and button feedback.** Either tracked PICO controller can ray-hover a button and activate it with a new trigger press. Each hand must point at the button it activates. Rays change from cyan to gold over a target. Buttons scale to `1.025` on focus and `0.98` during a `0.16`-second press, with exponential smoothing at rate `18`. Activation sends a supported controller a short haptic impulse. Input arms after 1.25 seconds and release of held activation controls. Desktop mouse and arrow-key / Enter / Space navigation also exist; Escape returns from a secondary page or opens quit confirmation.

**Gameplay feedback.** The recipe board binds the current recipe and selected element icons. The right board binds countdown, tide, score, and defeated count. `StarCoreWorldHealthBar` drives the top health fill, damage trail, and numeric value. Status text reports tutorial, recipe, and combat events; its color is lightened at runtime for readability.

**Asset provenance.** User cover references are at `/Users/ld/Desktop/游戏封面`. The global ange-embed v2 collections/facets/search workflow was consulted first; returned assets were unrelated and lacked license metadata, so none were imported. The title assets were generated through Tripo Nano Banana, at 5 credits per task:

| Asset | Project-local source | Generation task ID |
| --- | --- | --- |
| Observatory background | `Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Background.png` | `59415ce6-d061-4847-b213-b5bca906640c` |
| Separate title logo | `Assets/Art/UI/GeneratedUI/TitleRedesign20260908/Logo.png` | `ff710c02-5178-4a2a-a74d-de99dee606f2` |

The retained HUD artwork is under `Assets/Art/UI/GeneratedUI/NanoBanana_Refined/`: `SidePanelClean/SidePanelClean_Cropped.png`, `StatusPanel/StatusPanel_Cropped.png`, `TitlePanel/TitlePanel_Cropped.png`, and `ElementIcons/`.

Desktop evidence lives under `Preview/CoverRefinement/`: `Title_Main.png`, `Title_Help.png`, `Title_Quit.png`, `HUD_Tutorial.png`, and `HUD_SelectedElements.png`. Screenshots are evidence snapshots; the source values above govern rebuilding. Automated menu behavior and gameplay regression passed in the implementation session. Real-headset appearance and interaction remain unverified.

## Do's and Don'ts

- Do keep the title cover, tagline, controls, help, and quit confirmation in English.
- Do retain the separate generated background and logo, with interactive menu labels rendered live.
- Do preserve the gameplay panel artwork, raised world placement, inward yaw, readable text, and selected-icon margins.
- Do verify the tutorial and both occupied selection slots when changing HUD layout.
- Don't replace button interaction with lettering baked into the cover image.
- Don't attach the gameplay boards to the camera or mirror their readable face.
- Don't infer headset validation from desktop screenshots or automated tests.

The central instruction text is optically raised 14 layout units inside its panel to balance visible glyph padding above and below.
