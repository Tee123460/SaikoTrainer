# Saiko Trainer

A **free, open-source** MelonLoader mod / trainer for the single-player horror game
**Saiko no Sutoka** (by Habupain).

> Single-player use only. The game has no online/multiplayer mode.

---

## Features

**Combat**
- God Mode
- Full Heal

**ESP / Info**
- ESP for Saiko (box, distance, AI state)
- Key ESP (find keys + highlights the exit door)

**Movement / World**
- No Clip (WASD + Space/Ctrl)
- Freeze AI (Saiko stands still)
- Unlock Exit Door (skip the key hunt)
- Teleport To Saiko / Teleport Saiko To You

**Player**
- Speed Hack (x1.8)
- Infinite Stamina

**Time**
- Adjustable time scale
- Infinite Time

---

User Interface
<img width="286" height="634" alt="image" src="https://github.com/user-attachments/assets/bb1642c2-52c4-47c5-afc0-85d06668c788" />


## Requirements

- **Saiko no Sutoka** on Steam (Windows)
- **MelonLoader** installed into the game folder — https://melonloader.co

## Install (ready-made release)

1. Install MelonLoader and point it at `Saiko no sutoka.exe`. If you have never run it,
   launch the game once so MelonLoader creates the `Mods` folder, then close it.
2. Copy `Mods\SaikoTrainer.dll` from the release zip into
   `<Steam>\steamapps\common\Saiko no sutoka\Mods\`
3. Start the game and press **Insert** to open the menu.

## Usage / Keys

| Key | Action |
|-----|--------|
| Insert | Open / close menu |
| Drag title bar | Move the menu |
| WASD / Space / Ctrl | No Clip movement |
| Mouse wheel | (in game) |

---

## Build from source

Requires the **.NET 6 SDK**.

```powershell
dotnet build -c Release -p:GameDir="D:\Steam\steamapps\common\Saiko no sutoka"
```

Instead of passing `-p:GameDir`, you can set the environment variable once:

```powershell
setx SAIKO_GAME_DIR "D:\Steam\steamapps\common\Saiko no sutoka"
```

Then:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1     # build + deploy to Mods
powershell -ExecutionPolicy Bypass -File .\package.ps1   # build + make release zip
```

The release zip is written to `dist\SaikoTrainer_v<version>.zip`.

> Note: building requires the game's assemblies, which are **not** included in this repo.
> Only the mod's own source code is distributed here.

---

## Disclaimer

- This is a single-player trainer. Do not use it to harass other people.
- Not affiliated with or endorsed by the game's developer. **No game files are included.**
- Some antivirus tools flag trainers as false positives — build from source if you prefer.
- Game updates may break the mod until it is updated.

## License

MIT — see [LICENSE](LICENSE).

---

## ภาษาไทย (ย่อ)

โมดเทรนเนอร์**ฟรี**สำหรับเกมแนวเล่นคนเดียว **Saiko no Sutoka**

**ติดตั้ง:** ลง MelonLoader ในโฟลเดอร์เกม → เปิดเกมหนึ่งครั้งแล้วปิด (เพื่อสร้างโฟลเดอร์ `Mods`)
→ ก๊อป `SaikoTrainer.dll` ไปวางใน `Saiko no sutoka\Mods\` → เปิดเกม กด **Insert**

**บิลด์เอง:** ต้องมี .NET 6 SDK แล้วรัน
`dotnet build -c Release -p:GameDir="พาธเกมของคุณ"`

สัญญาอนุญาต MIT — ใช้งาน/แก้ไข/แจกต่อได้ ฟรี
