# MonitorSwitcher

A small Risk of Rain 2 mod that lets you choose which monitor the game runs on, right from the in-game settings.

## What it does

- Adds a **Display Monitor** option to **Options → Video**.
- Pick a monitor and the game window moves there immediately — no restart required.
- Your choice is saved and applied automatically the next time you launch the game.

## How to use

1. Open **Options → Video** and find **Display Monitor** at the bottom of the list.
2. Select the monitor you want (shown as `Display 1 (2560 x 1440)`, `Display 2 (3840 x 2160)`, etc.).
3. The game moves to that monitor right away. A 10-second confirmation dialog lets you keep the change or revert it, just like the resolution setting.
4. That's it — the choice sticks for future launches.

## Performance

Nothing runs in the background. The setting is applied once when the game starts and once when you change it; there is no continuous overhead or measurable impact on frame rate.

## Notes

- **Windows only.** Multi-monitor setup required.
- The **Resolution** dropdown updates to list the resolutions of the monitor you're currently on, so you can switch to a 4K monitor and select 4K without restarting.
- If the saved monitor isn't connected anymore, the game falls back to your primary display.
- Advanced: you can also set it from the developer console with `display_monitor DISPLAY1` (values are `DISPLAY1`, `DISPLAY2`, ...).
