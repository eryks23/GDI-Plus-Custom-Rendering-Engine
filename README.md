# GDI-Plus-Custom-Rendering-Engine

**Snake v2.0 — A classic Snake game with a fully custom GDI+ rendering pipeline built in C# Windows Forms.**

No third-party libraries. Every pixel — gradient overlays, anti-aliased mine graphics, animated leaderboard panels — is drawn directly via `System.Drawing` and `System.Drawing.Drawing2D`. The project demonstrates how far you can push GDI+ within a vanilla .NET Framework Windows Forms application.

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

---

## Table of Contents

1. [Features](#features)
2. [Tech Stack](#tech-stack)
3. [Requirements](#requirements)
4. [Build & Run](#build--run)
5. [Controls](#controls)
6. [Gameplay](#gameplay)
7. [Configuration](#configuration)
8. [Rendering Architecture](#rendering-architecture)
9. [Leaderboard File Format](#leaderboard-file-format)
10. [Project Structure](#project-structure)
11. [Contributing](#contributing)
12. [Author](#author)
13. [License](#license)

---

## Features

- **Custom GDI+ rendering** — all graphics drawn with `Graphics`, `LinearGradientBrush`, `SmoothingMode.AntiAlias`, and layered alpha compositing; zero WinForms designer visuals at runtime.
- **Double-buffered, flicker-free canvas** — `DoubleBuffered = true` ensures smooth redraws at every timer tick.
- **Responsive grid** — cell size recalculated on every resize; the game area fills the window while respecting a 12 px minimum cell floor.
- **Dynamic mine system** — up to 12 mines spawn, relocate, and cycle every 40 ticks; new mines are rejected if placed within Manhattan distance 4 of the snake's head.
- **Progressive difficulty** — the game timer starts at 130 ms and drops by 10 ms every 50 points, bottoming out at 60 ms.
- **Persistent leaderboard** — scores are stored in a pipe-delimited flat file next to the executable; up to 500 entries, sorted descending on load.
- **Nickname deduplication** — duplicate names receive an auto-appended suffix (`_2`, `_3`, …) rather than silently overwriting existing entries.
- **Zero-score confirmation** — saving a 0-point run requires a second deliberate click to prevent accidental entries.
- **Scrollable Hall of Fame overlay** — the top-N leaderboard panel supports mouse-wheel and arrow-key scrolling with a proportional scroll thumb.
- **Gold / Silver / Bronze rank colours** — top-3 rows use distinct metallic tones; the current player's row is highlighted in yellow.
- **Keyboard-first UX** — `Enter` restarts, `Escape` closes the leaderboard, WASD + arrow keys steer the snake.

---

## Tech Stack

| Component       | Technology                                          |
|-----------------|-----------------------------------------------------|
| Language        | C# 7 (targeting .NET Framework 4.7.2)               |
| UI framework    | Windows Forms (`System.Windows.Forms`)              |
| 2-D graphics    | GDI+ (`System.Drawing`, `System.Drawing.Drawing2D`) |
| Build system    | MSBuild 15.0 / Visual Studio 2017+                  |
| Persistence     | Pipe-delimited plain-text file (`snake_scores.txt`) |
| External deps   | None — pure .NET Framework BCL                      |

---

## Requirements

- **OS**: Windows 7 SP1 or later (Windows 10/11 recommended)
- **Runtime**: [.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472) or newer (pre-installed on Windows 10 1803+)
- **Build tools** (choose one):
  - [Visual Studio 2017+](https://visualstudio.microsoft.com/) with the **.NET desktop development** workload
  - [Build Tools for Visual Studio 2017+](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) (command-line only)

---

## Build & Run

### Visual Studio (recommended)

```
1. Clone or download the repository.
2. Open WindowsFormsApp3.csproj in Visual Studio.
3. Select the desired configuration (Debug / Release).
4. Press F5 to build and run, or Ctrl+F5 to run without the debugger.
```

### MSBuild (command line)

```powershell
# Clone
git clone https://github.com/eryks23/GDI-Plus-Custom-Rendering-Engine.git
cd GDI-Plus-Custom-Rendering-Engine

# Debug build
msbuild WindowsFormsApp3.csproj /p:Configuration=Debug

# Release build
msbuild WindowsFormsApp3.csproj /p:Configuration=Release /p:Optimize=true

# Run the compiled executable
.\bin\Release\WindowsFormsApp3.exe
```

> **Note**: `msbuild.exe` must be on your `PATH`. The typical location is:
> `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\`

### Pre-built binary

A Debug build (`WindowsFormsApp3.exe`) is included in `bin/Debug/`. You can run it directly on any machine with .NET Framework 4.7.2 installed — no additional setup required.

---

## Controls

| Key              | Action                                |
|------------------|---------------------------------------|
| `↑` / `W`        | Move up                               |
| `↓` / `S`        | Move down                             |
| `←` / `A`        | Move left                             |
| `→` / `D`        | Move right                            |
| `Enter`          | Restart game (from game-over screen)  |
| `Escape`         | Close leaderboard overlay             |
| `↑` / `↓`        | Scroll leaderboard (arrow keys)       |
| Mouse wheel      | Scroll leaderboard                    |

---

## Gameplay

The snake starts with 3 segments at position `(5, 12)` moving right on a 30 × 25 grid.

**Scoring**

Each piece of food eaten awards **10 points**. The food respawns in a random cell that overlaps neither the snake nor any mine.

**Speed escalation**

| Score threshold | Timer interval |
|-----------------|----------------|
| 0               | 130 ms         |
| 50              | 120 ms         |
| 100             | 110 ms         |
| …               | …              |
| 700 +           | 60 ms (floor)  |

**Mines**

Mines appear after the first 40 ticks and are updated every 40 ticks thereafter. At each update one mine is either added (if fewer than 12 exist) or swapped for a new one at a random position ≥ 4 cells (Manhattan distance) from the snake's head. Colliding with a mine ends the game.

**Game over triggers**

- Snake head crosses the grid boundary (wall collision)
- Snake head overlaps any body segment (self collision)
- Snake head moves onto a mine

---

## Configuration

All tunable constants are defined at the top of `Form1.cs`. Edit and rebuild to change game behaviour.

```csharp
// Form1.cs — tunable constants
private const int GridWidth        = 30;   // columns in the play area
private const int GridHeight       = 25;   // rows in the play area
private const int MineTickInterval = 40;   // game ticks between mine updates
private const int MaxMines         = 12;   // maximum simultaneous mines
private const int MinSeg           = 12;   // minimum cell width/height in pixels
private const int BottomBar        = 50;   // height of the status bar in pixels
private const int LbVisible        = 9;    // visible rows in the leaderboard panel
```

**Initial speed** is set in `StartGame()`:

```csharp
gameTimer.Interval = 130; // milliseconds per tick at game start
```

**Speed floor** is enforced in `GameTimer_Tick()`:

```csharp
if (score % 50 == 0 && gameTimer.Interval > 60)
    gameTimer.Interval -= 10;
```

**Scores file location** resolves at runtime to the same directory as the executable:

```csharp
private static readonly string ScoresFile =
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snake_scores.txt");
```

The file is created automatically on the first save. No `.env` file or registry keys are used.

---

## Rendering Architecture

All drawing happens inside the `Form1_Paint` handler, which is triggered by `this.Invalidate()` on every timer tick and every state change. The paint pass executes the following layers in order:

```
Form1_Paint()
│
├── Grid lines          — semi-transparent white Pen (alpha 25)
│
├── DrawMines()         — per-mine: filled ellipse body + outline + fuse + 8 directional spikes + specular highlight
│
├── Snake segments      — LimeGreen head, rgb(0,175,0) body; 1-px inset rectangle per cell
│
├── Food                — filled Crimson ellipse, radius scales with CellSize
│
├── Bottom status bar   — LinearGradientBrush fill + separator line + score/length/mines string
│
└── [if !gameRunning]
    ├── DrawGameOverOverlay()     — semi-opaque gradient fill + bordered box + score + rank text
    └── DrawLeaderboardOverlay()  — panel with shadow, gradient bg, double border, scrollable rows, scroll thumb
```

`SmoothingMode.AntiAlias` is active for all non-rectangular shapes. It is reset to `Default` before the pixel-aligned grid and status bar to avoid sub-pixel bleeding.

---

## Leaderboard File Format

The leaderboard persists to `snake_scores.txt` as a pipe-delimited UTF-8 text file. Each line represents one entry:

```
Name|Score|Date
```

**Field rules**

| Field  | Type   | Notes                                          |
|--------|--------|------------------------------------------------|
| `Name` | string | 1–20 chars; `|`, `\n`, `\r` stripped on save   |
| `Score`| int    | Non-negative; lines with non-integer score are skipped on load |
| `Date` | string | `dd.MM.yyyy` format; optional (legacy entries may omit it) |

**Example**

```
Anonim_28|370|29.04.2026
PlayerOne|280|19.06.2026
Anonymous|0|19.06.2026
```

The file is sorted descending by score on every write and capped at 500 entries. It is safe to edit manually; malformed lines are silently skipped on the next load.

---

## Project Structure

```
GDI-Plus-Custom-Rendering-Engine/
│
├── Form1.cs                        # Main game window, game loop, GDI+ rendering engine
├── Form1.Designer.cs               # Auto-generated Windows Forms initialisation code
├── Program.cs                      # Application entry point — bootstraps Form1
├── App.config                      # Runtime target framework binding
├── WindowsFormsApp3.csproj         # MSBuild project file (.NET Framework 4.7.2)
│
├── Properties/
│   ├── AssemblyInfo.cs             # Assembly version, GUID, copyright metadata
│   ├── Resources.Designer.cs       # Auto-generated resource accessor class
│   ├── Resources.resx              # Embedded resource manifest
│   ├── Settings.Designer.cs        # Auto-generated application settings class
│   └── Settings.settings           # Application settings definition
│
├── bin/
│   ├── Debug/
│   │   ├── WindowsFormsApp3.exe    # Debug build executable
│   │   ├── WindowsFormsApp3.pdb    # Debug symbols
│   │   └── snake_scores.txt        # Leaderboard data (auto-created; committed as sample)
│   └── Release/                    # Release build output (generated by MSBuild)
│
└── obj/                            # Intermediate build artefacts (not committed)
```

**Key classes**

| Class / type  | File       | Responsibility                                                  |
|---------------|------------|-----------------------------------------------------------------|
| `Program`     | Program.cs | Static entry point; calls `Application.Run(new Form1())`        |
| `ScoreEntry`  | Form1.cs   | DTO — `Name` (string), `Score` (int), `Date` (string)          |
| `Form1`       | Form1.cs   | Window, game state machine, GDI+ render loop, leaderboard I/O  |

---

## Contributing

1. Fork the repository and create a feature branch:
   ```powershell
   git checkout -b feature/your-feature-name
   ```
2. Make your changes. Keep each commit focused on a single concern.
3. Verify the project builds without warnings in both Debug and Release:
   ```powershell
   msbuild WindowsFormsApp3.csproj /p:Configuration=Release /warnaserror
   ```
4. Open a pull request against `main` with a clear description of what changed and why.

**Code style conventions used in this project**

- 4-space indentation, no tabs.
- `camelCase` for private fields and local variables; `PascalCase` for types, properties, and methods.
- Inline comments on non-obvious logic — the codebase is intended to be readable as a learning resource.
- No external NuGet packages; keep the dependency surface as pure .NET Framework BCL.

---

## Author

**eryks23**
GitHub: [https://github.com/eryks23](https://github.com/eryks23)
Repository: [https://github.com/eryks23/GDI-Plus-Custom-Rendering-Engine](https://github.com/eryks23/GDI-Plus-Custom-Rendering-Engine)

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.
