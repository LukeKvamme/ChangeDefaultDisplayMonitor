# MonitorSwitcher

A small Risk of Rain 2 mod that lets you choose which monitor the game runs on, right from the in-game settings.

## What it does

- Adds a **Display Monitor** option to **Options → Video**.
- Pick a monitor and the game window moves there immediately — no restart required.
- Your choice is saved and applied automatically the next time you launch the game.

## How to use

1. Open **Options → Video** and find **Display Monitor** at the bottom of the list.
2. Select the monitor you want (shown as `Display 1 (2560 x 1440)`, `Display 2 (3840 x 2160)`, etc.).
3. The game moves to that monitor right away. A 10-second confirmation dialog lets you keep the change or revert it, just like the vanilla resolution setting.
4. The choice sticks for future launches.

## Performance

Nothing runs in the background. The setting is applied once when the game starts and once when you change it; there is no continuous overhead or measurable impact on frame rate because this mod is not polling, or doing anything per-frame to keep the game on the selected monitor, or anything else like that. 

It is a set-and-forget, event-driven mod that runs on start-up and when you choose to change the display.

## Notes

- **Windows only** because I don't have Linux to test with, and my quick googling showed that Thunderstore is Windows-based anyways. If there is Linux interest maybe I can look at that later.
- (obviously) **Multi-monitor setup required**. With a single monitor the setting just shows your one display and selecting it does not do anything. But really, if you have 1 monitor I don't know why you would download this mod lol.
- The **Resolution** dropdown updates to list the resolutions of the monitor you're currently on, so you can switch to a 4K monitor and select 4K without restarting.
- If the saved monitor isn't connected anymore, the game falls back to your primary display.
- **Advanced/Devs:** You can also set the display from the developer console with `display_monitor DISPLAY1` (values are `DISPLAY1`, `DISPLAY2`, ... -- NOT the name of the monitor (the weird like AWQXXYZ32 naming monitor companies like to use)).
