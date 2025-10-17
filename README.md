# BatchScrapping

A BepInEx mod for MycoPunk that adds batch scrapping functionality to the gear upgrade system.

## Description

This mod enhances the upgrade management system in MycoPunk by allowing players to mark upgrades for bulk scrapping and providing convenient buttons to scrap multiple upgrades at once. It introduces a 'T' key to mark/unmark upgrades (favorites cannot be marked) and adds hold-to-confirm buttons for scrapping either marked upgrades or all non-favorite upgrades.

The mod uses Harmony to patch various gear window methods, adding new UI buttons and keyboard functionality. It processes upgrades in configurable batches to ensure smooth performance and provides proper resource refunds based on upgrade rarity.

## Getting Started

### Dependencies

* MycoPunk (base game)
* [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible
* .NET Framework 4.8

### Building/Compiling

1. Clone this repository
2. Open the solution file in Visual Studio, Rider, or your preferred C# IDE
3. Build the project in Release mode

Alternatively, use dotnet CLI:
```bash
dotnet build --configuration Release
```

### Installing

**Option 1: Via Thunderstore (Recommended)**
1. Download and install using the Thunderstore Mod Manager
2. Search for "BatchScrapping" under MycoPunk community
3. Install and enable the mod

**Option 2: Manual Installation**
1. Ensure BepInEx is installed for MycoPunk
2. Copy `BatchScrapping.dll` from the build folder
3. Place it in `<MycoPunk Game Directory>/BepInEx/plugins/`
4. Launch the game

### Executing program

Once the mod is loaded, new functionality is available in gear upgrade windows:

1. **Marking Upgrades for Scrapping:**
   - Hover over any upgrade
   - Press the 'T' key to mark/unmark it (red icon indicates marked status)
   - Note: Favorite upgrades cannot be marked for scrapping

2. **Scrapping Marked Upgrades:**
   - Hold down the gray "Scrap Marked" button on the right side of gear windows
   - Hold for ~1 second to confirm scrapping
   - Only marked upgrades will be scrapped for resources

3. **Scrapping All Non-Favorite Upgrades:**
   - Hold down the red "Scrap All Non-Favorite" button
   - Hold for ~1 second to confirm scrapping
   - All non-favorite upgrades will be scrapped

Resources are refunded immediately, and the UI updates automatically after scrapping.

## Help

* **'T' key not working?** Make sure you're hovering over the upgrade in a gear details window
* **Cannot mark favorite upgrades?** This is intentional to protect valuable upgrades
* **Buttons not showing?** Try closing and reopening the gear window
* **Incompatibility issues?** This mod uses Harmony patches. If you have other mods that modify GearDetailsWindow or GearUpgradeUI, conflicts may occur
* **Performance issues?** If scrapping large numbers of upgrades causes lag, try reducing the batch size (currently 100)
* **Logs:** Check the BepInEx console for detailed scrapping information and any errors

## Authors

* Sparroh
* funlennysub (original mod template)
* [@DomPizzie](https://twitter.com/dompizzie) (README template)

## License

* This project is licensed under the MIT License - see the LICENSE.md file for details
