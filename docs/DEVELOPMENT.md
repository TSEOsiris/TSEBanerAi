# Development Guide

## Setup Development Environment

### 1. Prerequisites

- **Visual Studio 2022 Community** (or higher)
  - Workload: ".NET desktop development"
  - Component: ".NET Framework 4.7.2 targeting pack"
- **Git** (optional but recommended)
- **Mount & Blade II Bannerlord** (copy in `C:\TSEBanerAi\Mount & Blade II Bannerlord`)

### 2. Open Project

1. Open `C:\TSEBanerAi\TSEBanerAi\TSEBanerAi.sln` in Visual Studio
2. Wait for NuGet packages to restore
3. Build the solution (Ctrl+Shift+B)

### 3. Configure Debugging

#### Launch Settings

Create `.vs\TSEBanerAi\Config\applicationhost.config` or use these steps:

1. Right-click on `TSEBanerAi` project → Properties
2. Go to **Debug** tab
3. Set:
   - **Start Action:** Start external program
   - **Start external program:** `C:\TSEBanerAi\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Bannerlord.exe`
   - **Command line arguments:** 
     ```
     /singleplayer _MODULES_*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*TSEBanerAi*_MODULES_
     ```
   - **Working directory:** `C:\TSEBanerAi\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

4. **Save** (Ctrl+S)

#### Alternative: launchSettings.json

Create `src\TSEBanerAi\Properties\launchSettings.json`:

```json
{
  "profiles": {
    "Bannerlord": {
      "commandName": "Executable",
      "executablePath": "C:\\TSEBanerAi\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\Bannerlord.exe",
      "commandLineArgs": "/singleplayer _MODULES_*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*TSEBanerAi*_MODULES_",
      "workingDirectory": "C:\\TSEBanerAi\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\"
    }
  }
}
```

### 4. Build and Deploy

The project is configured to **auto-deploy** on build:
- DLL is copied to `Modules\TSEBanerAi\bin\Win64_Shipping_Client\`
- PDB (debug symbols) is copied in Debug mode
- SubModule.xml is copied automatically

### 5. Start Debugging

Press **F5** or click **Start Debugging**

The game will launch with your mod loaded!

---

## Debugging Tips

### Breakpoints

1. Set breakpoints in your code (F9)
2. Start debugging (F5)
3. Trigger the code in-game
4. Visual Studio will pause at breakpoints

### Hot Reload

In Debug mode, you can modify code while game is running:
1. Make changes
2. Press **Ctrl+Alt+F5** (Hot Reload)
3. Changes apply without restarting

### Logs

View logs in:
- **In-game console:** Press `` ` `` (tilde)
- **File:** `C:\TSEBanerAi\Mount & Blade II Bannerlord\logs\rgl_log_*.txt`

### Common Issues

#### "Could not load file or assembly"
- Check that all TaleWorlds DLLs are referenced correctly
- Verify game path in `.csproj`

#### "Module not loading"
- Check `SubModule.xml` syntax
- Verify dependencies in `SubModule.xml`
- Check logs for errors

#### "Breakpoints not hitting"
- Ensure you're building in **Debug** mode
- Check that PDB file is deployed
- Disable "Just My Code" in VS: Tools → Options → Debugging → General

---

## Project Structure

```
src/
└── TSEBanerAi/
    ├── TSEBanerAi.csproj    # Project file
    ├── SubModule.xml         # Mod manifest
    ├── SubModule.cs          # Main entry point
    ├── Core/                 # Core systems
    │   ├── Config.cs
    │   └── Logger.cs
    ├── LLM/                  # LLM integration
    │   ├── ILLMProvider.cs
    │   ├── OllamaProvider.cs
    │   └── PromptBuilder.cs
    ├── Dialogue/             # Dialogue system
    │   ├── DialogueManager.cs
    │   └── ContextBuilder.cs
    └── Actions/              # Action execution
        └── ActionExecutor.cs
```

---

## Git Workflow

### Initial Setup

```bash
cd C:\TSEBanerAi\TSEBanerAi
git init
git add .
git commit -m "Initial commit"
```

### Daily Workflow

```bash
# Before starting work
git pull

# After making changes
git add .
git commit -m "Description of changes"
git push
```

### Branching

```bash
# Create feature branch
git checkout -b feature/dialogue-system

# Work on feature...

# Merge back to main
git checkout main
git merge feature/dialogue-system
```

---

## Testing

### Manual Testing

1. Build project (Ctrl+Shift+B)
2. Start debugging (F5)
3. Load a campaign
4. Test features in-game

### Unit Testing (TODO)

```csharp
// Add MSTest or xUnit project
// Write tests for core logic
```

---

## Performance

### Profiling

Use Visual Studio Profiler:
1. Debug → Performance Profiler
2. Select "CPU Usage"
3. Start game
4. Analyze results

### Memory

Monitor with:
- Visual Studio Diagnostic Tools (Debug → Windows → Show Diagnostic Tools)
- dotMemory (JetBrains)

---

## Next Steps

1. Implement `DialogueManager` - intercept NPC conversations
2. Create `PromptBuilder` - generate optimized prompts
3. Add `OllamaProvider` - connect to LLM
4. Test with simple dialogue

See [ARCHITECTURE.md](ARCHITECTURE.md) for system design.




