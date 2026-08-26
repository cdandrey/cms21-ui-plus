# CMS21 UI+

**CMS21 UI+** is a Car Mechanic Simulator 2021 mod focused on interface clarity,
inventory usability, vehicle information and small usability improvements.

- Display name: **CMS21 UI+**
- Short name: **CMS21 UI+**
- Technical name and assembly: `CMS21UIPlus`
- DLL: `CMS21UIPlus.dll`
- Runtime directory: `Mods\CMS21UIPlus\`
- Main configuration: `Mods\CMS21UIPlus\CMS21UIPlus.cfg`
- Repository name: `cms21-ui-plus`
- Questions: `cdandrey@gmail.com` — include `CMS21UI+` in the subject line

## Relationship to QoLmod

CMS21 UI+ is a substantially refactored and reduced derivative of **QoLmod** by
**Meitzi**, originally published at <https://www.nexusmods.com/carmechanicsimulator2021/mods/105>
and licensed under GNU GPL v3. Selected QoLmod feature ideas are retained; the retained and
removed feature lists are summarized in [QoLmod origin](#qolmod-origin).

## Features and settings

All switches below are stored under `[CMS21UIPlus.Settings]` in
`CMS21UIPlus.cfg`. Displayed names and apply modes are taken from the in-game settings
manifest. Indicator and quick-filter switches currently use `immediate`; the remaining listed
switches use `restartGame`.

### Inventory

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Part repairability badge** | `showPartRepairabilityIndicators` | `true` | Adds a condition-coloured wrench to repairable loose-part rows in the inventory, warehouse, barn, junkyard, scrap, scrap-upgrade and mechanical/body repair lists. The badge follows the effective repairability of the part. When CMS21 Gameplay+ is also installed, its optional brake-drum repair support is reflected without making either mod a required dependency of the other. |
| **Owned-part count badge** | `showOwnedPartCountIndicators` | `true` | Shows how many matching loose parts or complete assemblies are owned across the normal inventory and every unlocked warehouse. Counts are grouped by condition, wheel identity includes ET/profile/size/width, and components inside a stored assembly are not counted again as loose copies. |
| **Mount/unmount part card badges** | `showMountPartCardIndicators` | `true` | Adds repairability and owned-part availability badges directly to the hovered part card while mounting or removing parts. Repairability is shown in the upper-left corner; availability is shown in the upper-right corner with the same condition-coloured owned-count breakdown used by inventory and warehouse rows, or a red warehouse badge when no matching part is owned. |
| **Hide body-part paint badges** | `hideBodyPartPaintColorBadges` | `true` | Hides the vanilla paint-colour `Color` badge on supported body-part cards. It does not alter the part image, paint data or livery icons. |
| **Remember sorting** | `rememberInventorySorting` | `true` | Saves and restores the selected sorting mode independently for the normal inventory, warehouse inventory and warehouse contents. Values are stored per player profile for all four profile slots and reapplied after opening or switching the relevant tabs. |

### Inventory filters

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Inventory quick filters** | `addInventoryQuickFilters` | `true` | Adds condition, repairability and quality filters to garage inventory and warehouse lists, plus owned/missing filtering in barn and junkyard. |
| **Reset inventory quick filters on exit** | `resetInventoryQuickFiltersOnExit` | `true` | Restores the existing garage inventory/warehouse behavior by clearing their quick-filter state when the window closes. Disabling it keeps those filter modes for the next opening in the same scene. |
| **Move filtered parts** | `moveFilteredPartsBetweenInventoryAndWarehouse` | `true` | In the warehouse window, moves every loose part and complete assembly remaining after the active warehouse tab, category, native search and quick filters. The bulk action uses the configured modifier with Enter or a modified left click, suppresses per-item movement popups and refreshes the window once after the transaction. The feature is independent from `addInventoryQuickFilters`: when quick filters are disabled, it still moves the result produced by the native tab, category and search controls. |
| **Scrap inventory filters** | `addScrapInventoryFilters` | `true` | Adds localized-name search plus condition, repairability and quality filters to the scrap inventory. |
| **Reset scrap filters on exit** | `resetScrapInventoryFiltersOnExit` | `true` | Clears scrap condition, repairability, quality and search state when the scrap window closes. Disabling it keeps the state for the next opening in the same scene. |
| **Bulk scrap shortcut** | `addBulkScrapShortcut` | `true` | Adds a native hold-`Space` action and matching footer mouse action for scrapping all unlocked loose parts in the current Scrap result across every page. The bulk-scrap hint is shown only on the Scrap tab, not on Upgrade. Releasing before the hold completes cancels without selecting a part; the confirmation reports the exact quantity and the game's own bulk-scrap calculation performs the final update. This switch works independently from `addScrapInventoryFilters`. |
| **Repair inventory filters** | `addRepairInventoryFilters` | `true` | Adds an immediate localized-name search plus condition and quality filters to both the mechanical-part repair inventory and the body-panel repair inventory. The filtered result is rebuilt after a repair animation completes, and its state is independent from the scrap filters. |
| **Reset repair filters on exit** | `resetRepairInventoryFiltersOnExit` | `true` | Clears repair condition, quality and search state when the repair window closes. Disabling it keeps the state for the next opening in the same scene. |
| **Spring clamp quick filters** | `addSpringClampInventoryFilters` | `true` | Adds condition, quality and localized-name search to spring-clamp assembly and disassembly selection. `LeftAlt` resets the filters. |
| **Reset spring-clamp filters on exit** | `resetSpringClampInventoryFiltersOnExit` | `false` | When enabled, clears spring-clamp condition, quality and search state when the selection window closes. The default keeps the existing persistent behavior. |
| **Tire changer quick filters** | `addTireChangerInventoryFilters` | `true` | Adds condition, quality and localized-name search to tire-changer assembly and disassembly selection. Complete wheels are filtered by aggregate condition, while quality matching uses their contained rim/tire parts. Filtered-out entries cannot remain selected, empty results keep the chooser open, and `LeftAlt` resets the filters. |
| **Reset tire-changer filters on exit** | `resetTireChangerInventoryFiltersOnExit` | `false` | When enabled, clears tire-changer condition, quality and search state when the selection window closes. |
| **Wheel balancer quick filters** | `addWheelBalancerInventoryFilters` | `true` | Adds condition, quality and localized-name search to the wheel-balancer selection. Complete wheels are filtered by aggregate condition, while quality matching uses their contained rim/tire parts. Filtered-out entries cannot remain selected or submitted, empty results keep the chooser open, and `LeftAlt` resets the filters. |
| **Reset wheel-balancer filters on exit** | `resetWheelBalancerInventoryFiltersOnExit` | `false` | When enabled, clears wheel-balancer condition, quality and search state when the selection window closes. |
| **Brake lathe quick filters** | `addBrakeLatheInventoryFilters` | `true` | Adds condition, quality and localized-name search to the brake-lathe part selection window. Filtered-out entries cannot remain selected or submitted, empty results keep the chooser open, and `LeftAlt` resets the filters. |
| **Reset brake-lathe filters on exit** | `resetBrakeLatheInventoryFiltersOnExit` | `false` | When enabled, clears brake-lathe condition, quality and search state when the selection window closes. |
| **Mount part-selection quick filters** | `addMountPartSelectionFilters` | `true` | Adds condition, quality and localized-name search to the inventory chooser opened while mounting a part. The native chooser receives only matching entries, so filtered-out parts cannot remain selected. `LeftAlt` resets all mount-selection filters. |
| **Reset mount-selection filters on exit** | `resetMountPartSelectionFiltersOnExit` | `false` | When enabled, clears mount-selection condition, quality and search state when the selection window closes. |

#### Filter cycles and condition ranges

Condition filtering uses the game's current repair/junk threshold instead of duplicating it as a fixed mod value.

- Garage inventory and both warehouse tabs: off → red (below the repair threshold) → orange (normally 15–49%) → yellow (50–79%) → green ring (80–99%) → green (100%).
- Spring clamp, tire changer, wheel balancer and mount part selection: off → **white** (repair threshold through 100%) → green (100%) → green ring (80–99%) → yellow (50–79%) → orange (normally 15–49%) → red (below the repair threshold).
- Brake lathe: off → **white** (repair threshold through 100%) → green ring (80–99%) → yellow (50–79%) → orange (normally 15–49%) → red (below the repair threshold); the 100% state is omitted because perfect brake parts do not need machining.
- Repair inventories: off → orange (normally 15–49%) → yellow (50–79%) → green ring (80–99%). Their native lists already exclude parts below the repair threshold.
- Scrap: off → red (below the repair threshold) → orange (normally 15–49%) → yellow (50–79%) → green ring (80–99%). Upgrade has no condition quick filter.
- Barn and junkyard: off → **white** (repair threshold through 100%) → red (normally 0–14%) → orange (normally 15–49%) → yellow (50–79%) → green ring (80–99%).
- Repairability in every window that exposes this control: off → repairable only → non-repairable only.
- Quality in every window that exposes this control: off → upgraded (1–3 stars) → 1 star → 2 stars → 3 stars → not upgraded (0 stars).
- Barn and junkyard ownership: off → owned → missing. A matching copy at 50% condition or better counts as owned for this filter.

Left click advances every quick-filter cycle; right click moves through the same states in reverse.

The additional search fields in scrap, repair, spring-clamp, tire-changer and mount part-selection inventories match the localized displayed part name immediately and combine with the active quick filters. The game's existing inventory and warehouse search fields are not replaced.

Garage inventory and warehouse lists treat complete assemblies separately from loose parts. Complete wheels, assembled shock absorbers and other `GroupItem` entries have aggregate condition and therefore participate in the condition filter, but they have no independent repairability or quality value; when a repairability or quality filter is active in these garage-style lists, such assemblies are excluded. In barn and junkyard lists, group entries remain eligible when at least one contained part matches the active filters.

Special map/case inventory objects are never removed by quick filters in barn and junkyard. In garage-style inventory contexts they remain visible only while no quick filter is active because they do not expose normal part condition, repairability or quality properties.

On the spring clamp, `LeftAlt` resets condition, quality and text filters. By default the spring clamp, tire changer, wheel balancer, brake lathe and mount-selection filters keep their state between openings; their individual reset-on-exit switches can change that behavior. If a spring-clamp filter leaves the current selection stage empty, the chooser stays open. During assembly, only the active part card shows the native no-items state; during disassembly, the stale part preview is hidden and a centered native no-items state is shown instead. The tire changer follows the same assembly/disassembly empty-state behavior; filtered complete wheels are matched by aggregate condition and by the quality of their contained parts. Mount part selection uses the same native no-items presentation when its filtered result is empty, and `LeftAlt` resets its condition, quality and text filters.

The bulk warehouse transfer and bulk scrap features do not require their corresponding quick-filter switches. They operate on the current native result, and when quick filters are enabled they additionally respect the filtered result produced by those controls.

#### Owned counts and indicator colors

Owned counts include normal inventory items and assemblies plus items and assemblies from every unlocked warehouse. Complete assemblies are identified by their component identity and overall condition. The display uses these ranges:

- green: 100%;
- yellow: 50–99%;
- orange: the game's repair threshold through 49%;
- red: below the repair threshold.

When owned copies span several ranges, a white total is shown above the coloured breakdown. The repairability and owned-count badges are independent of each other and remain available when quick filters are disabled.

On first startup, an existing `showInventoryPartIndicators` value is copied to both current
indicator switches and the legacy line is removed. The older
`showOwnedPartCountInInventoryAndWarehouse`, `addWrenchIconToRepairableParts` and
`showOwnedPartCountInBarnAndJunkyard` keys are not used.

### Shopping list

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Auto-remove purchased parts from list** | `removePartsFromShoppingList` | `true` | After a shop purchase actually reduces player money, subtracts the bought quantity from the shopping-list entry currently linked to the single shop search result. The entry is removed when the remaining amount reaches zero. Licence plates keep their exact plate-name matching path. |
| **Autofill tire/rim/part purchase window** | `wheelShopListPurchaseHelper` | `true` | When the current parts-shop search contains exactly one item that matches a shopping-list entry, prefills that entry's requested quantity within the shop's allowed range. Tires also receive width/profile/size, rims receive size/ET, and tire controls are presented in Width/Profile/Size order while preserving selected values during option changes. |
| **Improved shopping list** | `addShoppingListSorting` | `true` | Enables shop-category quick filters, a compact inventory-style card grid, `-` / `+` quantity controls, repairability and warehouse-availability badges, persistent sorting menu and hold-`Space` bulk purchase for the current shop. Sorting supports name, warehouse availability, unit price and repairability in both directions. |
| **Filter by current shop on open** | `filterShoppingListByCurrentShopOnOpen` | `true` | When the shopping list opens inside a specialized shop, enables only that shop category. Disabling it opens the list with all shop categories enabled. |

With **Improved shopping list** enabled, twelve compact shop-category icons filter the list by Main, Body, Rims, Tires, Interior, Licence Plates, Gearbox, Tuning, Body Tuning, Electronics, Community and Addons; enabled categories are white and disabled categories are grey. The icons reuse the native shop-selection sprites. With `filterShoppingListByCurrentShopOnOpen` enabled, opening the list inside a specific parts shop enables only that shop category; otherwise the list opens with all categories enabled, as it does from inventory or the shop hub. Pressing `Alt` toggles all categories on or off. The shopping list is presented as a compact inventory-style card grid sized to 75% of the native inventory card. The number of columns is calculated from the available list width with a 2-pixel gap between cards and equal leftover margins on the left and right; when the whole list fits on one row, cards start from the left with the same 2-pixel edge gap. Each card shows the part thumbnail and localized name without a condition bar; tire and rim parameters are displayed directly on the card. Quantity, `-` / `+` controls and the native delete action are placed directly below the card; `-` is disabled when only one item remains. The same quantity changes are available from the `-` / `+` keyboard keys while a shopping-list card is selected. Inside a specific parts shop, holding `Space` opens a native confirmation for buying every compatible shopping-list entry sold by that shop and reports the matching part count and discounted cost before purchase. The action is hidden on the shop hub; tire and rim dimensions are preserved, tuning shops buy available tuning variants, and successfully purchased quantities are removed from the list. Repairability and warehouse indicators are drawn in the upper-left and upper-right corners of each card. Owned quantities are always shown vertically below the warehouse icon as total, 100%, 50–99%, 15–49% and below-15% counts, omitting empty condition groups; missing parts keep only the red warehouse icon.

Shopping-list ownership sorting uses the same shared part-identity rules as the ownership badges, including wheel dimensions. The purchase controller synchronizes against the current single shop result and the live shopping-list data. Clicking a shopping-list entry preserves its exact tire/rim dimensions for the resulting search; manual one-result searches bind to the first matching list entry. Clearing or changing the search drops the previous link, while reopening the shop or closing the shopping list re-synchronizes it from the current result.

### Jobs and controls

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Unmark completed job parts** | `unmarkFinishedParts` | `true` | After a marked mechanical part is mounted and reaches the condition required by the active job, removes the required-part highlight automatically. It does not complete unrelated job requirements or alter the part condition. |
| **Mark required body parts** | `markBodyParts` | `true` | Extends the job marking workflow to required body panels, including the cyan visual highlight and completion callback path used when the correct panel is installed. Existing game callbacks are preserved and invoked. |
| **Quick mount-mode switch** | `quickSwitchMountModes` | `true` | On release of either configured key, switches only between the matching assemble/disassemble pair for interior, part selection, bonus/workbench and garage modes. It does nothing in unrelated game modes or outside the garage. |

Keyboard bindings are stored separately in `KeyBindings.cfg`:

```toml
quickSwitchMountModesPrimary = "LeftAlt"
quickSwitchMountModesSecondary = "RightAlt"
filteredWarehouseTransferModifierPrimary = "LeftShift"
filteredWarehouseTransferModifierSecondary = "RightShift"
filteredWarehouseTransferActionPrimary = "Return"
filteredWarehouseTransferActionSecondary = "KeypadEnter"
```

Values use `UnityEngine.KeyCode` names. `None` disables a binding. Invalid values are
reported and replaced by the safe default. The filtered-transfer modifier also applies to
left mouse clicks, and the warehouse footer shows the active bulk-transfer shortcut.

### Interface and state

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Livery file names** | `showLiveryFileNames` | `true` | Replaces bare numeric livery entries with normalized livery filenames in the supported livery selection interface, making similarly numbered variants identifiable without changing the selected texture. |
| **Vehicle information on map** | `showCarConditionOnMap` | `true` | Adds total vehicle condition and rear licence-plate information to supported map vehicle cards and parking panels. Condition is calculated from body panels, mechanical parts, body and interior data while ignoring non-car/dummy elements handled by the game. |
| **Parking sorting** | `addParkingSorting` | `true` | Adds the native rearrangement command and sorting window to the garage parking browser. The selected mode physically compacts and rearranges all cars across every unlocked parking alley, so the new slot order persists in the game save. Arrival order is preserved per profile even while a known car is temporarily outside parking. Name sorting groups identical names by condition from best to worst; condition sorting uses the current vehicle data. |
| **Confirm dyno start automatically** | `autoConfirmDynoStart` | `true` | Automatically accepts only the localized redundant dyno-start confirmation, with its confirmation sound disabled. Other confirmation windows are not affected. The dyno window blur is controlled separately by CMS21 Immersion+. |
| **Exit game in garage menu** | `addExitGameToGaragePauseMenu` | `true` | Enables the reserved Exit Game action in the normal garage pause menu and routes it through the game's own exit confirmation instead of adding a separate custom shutdown path. |

### Startup

| In-game setting | Config flag | Default | Detailed behavior |
|---|---|---:|---|
| **Skip startup videos** | `skipStartupVideosTotally` | `true` | Skips the startup intro scenes and proceeds to the normal startup/menu flow without changing later loading screens. |
| **Continue startup automatically** | `autoContinueStartupPrompt` | `true` | Waits for the actual startup continuation prompt, confirms it through the prompt's own action and stops after a bounded timeout. It does not send a global synthetic Enter key. |

## In-game mod settings

The mod adds an in-game interface for configuring its own settings. Other mods can
also integrate with this menu and expose their settings through the shared UI.

### Integrating another mod

Integration is declarative. A mod does not reference `CMS21UIPlus.dll`, implement an
interface or expose a provider class. The menu reads a JSON manifest and the declared
TOML configuration file; it does not call into the third-party mod DLL.

For an assembly named `ExampleMod.dll`, provide a manifest named
`ExampleMod.ui-settings.json` in either supported location:

```text
<Game>\Mods\ExampleMod.ui-settings.json
<Game>\Mods\ExampleMod\ExampleMod.ui-settings.json
```

The manifest `modId` must match the assembly name without `.dll`, ignoring case. A
relative `config.path` is resolved from the manifest directory. Declared settings can
use boolean, number, string or enum values stored in one TOML section and can provide
localization for their names, descriptions and enum state labels.

Minimal example:

```json
{
  "modId": "ExampleMod",
  "displayName": "Example Mod",
  "config": {
    "path": "ExampleMod.cfg",
    "format": "toml",
    "section": "ExampleMod.Settings"
  },
  "groups": [
    {
      "id": "general",
      "name": "General",
      "order": 10
    }
  ],
  "settings": [
    {
      "id": "FID_Enabled",
      "key": "enabled",
      "group": "general",
      "nameKey": "LOC_SettingEnabledName",
      "descriptionKey": "LOC_SettingEnabledDescription",
      "type": "boolean",
      "default": true,
      "applyMode": "restartGame",
      "order": 10
    }
  ],
  "localization": {
    "en": {
      "LOC_SettingEnabledName": "Enable feature",
      "LOC_SettingEnabledDescription": "Enable the example feature."
    },
    "ru": {
      "LOC_SettingEnabledName": "Включить функцию",
      "LOC_SettingEnabledDescription": "Включить пример функции."
    }
  }
}
```

The corresponding configuration may contain other sections and keys; they are
preserved when the UI writes declared settings:

```toml
[ExampleMod.Settings]
enabled = true
```

Manifest fields:

- `modId`: technical assembly name and unique UI identifier;
- `displayName`: card title;
- `config.path`: TOML file path, normally relative to the manifest;
- `config.format`: currently only `toml`;
- `config.section`: TOML section containing the declared keys;
- `groups`: category definitions; `id` values must be unique and begin with a Latin
  letter;
- `settings`: setting definitions; `id` and `key` values must be unique;
  TOML keys use letters, digits and underscores and cannot begin with a digit;
- feature identifiers use the `FID_` prefix; localization identifiers use `LOC_`;
- `name`: group title;
- `nameKey` and `descriptionKey`: required localization keys for settings;
- `localization`: optional fixed-name block containing `en` and `ru` translations
  for the declared setting names and descriptions;
- `order`: category or setting order; equal values keep manifest order;
- `default`: value used when the key is absent and by category reset;
- `type`: `boolean`, `number`, `string` or `enum`;
- `step`: optional positive increment for `number`; defaults to `1`;
- `enums`: reusable enum definitions. Each definition contains parallel `ids`, `en` and/or `ru` arrays; at least one localization array is required;
- `enum`: reusable enum definition id used by a setting whose `type` is `enum`;
- `enumValues`: optional inline enum definition with the same `ids`, `en` and `ru` arrays; use either `enum` or `enumValues`, not both;
- `dependency` and `dependencyWarningKey`: optional pair for a runtime dependency exposed by the target mod. When an enabled boolean setting is unavailable, its warning is shown directly in the setting row;
- `dependencyPartialWarningKey`: optional warning shown when the dependency is only partially available;
- `dependencyDefaultWarningKey`: optional warning used when the dependency is unavailable under the target mod's default/vanilla state;
- `dependencySwitchKey` and `dependencyWhenFalse`: optional pair that selects an alternate dependency id while another boolean setting is `false`; warnings are refreshed immediately when that setting changes in the draft;
- `applyMode`: `immediate`, `reopenWindow`, `reloadLocation` or `restartGame`.

Enum ids are the string values written to TOML and understood by the target mod.
Localization is positional: every present language array must have the same length as
`ids`. Either `en` or `ru` may be omitted; the available language is then used as the
fallback. Example:

```json
{
  "enums": [
    {
      "id": "ProcessingDuration",
      "ids": ["Off", "Fast", "Default"],
      "en": ["Off", "Fast", "Default"],
      "ru": ["Откл", "Быстро", "Дефолтно"]
    }
  ],
  "settings": [
    {
      "id": "FID_ProcessingDuration",
      "key": "processingDuration",
      "group": "general",
      "type": "enum",
      "enum": "ProcessingDuration",
      "default": "Fast",
      "applyMode": "restartGame",
      "order": 20,
      "nameKey": "LOC_ProcessingDurationName",
      "descriptionKey": "LOC_ProcessingDurationDescription"
    }
  ]
}
```

Both language objects must contain the same C#-style identifiers:

```json
{
  "LOC_SettingEnabledName": "Enable feature",
  "LOC_SettingEnabledDescription": "Enable the example feature."
}
```

English is used for every game language except Russian.

Target mods can publish dependency state through the optional public bridge
`Cms21UiPlus.ModSettingDependencyRegistry.SetStatus(providerId, dependencyId, status)`, where
`status` is `available`, `partial`, `unavailable` or `unavailableByDefault`. The existing
`SetAvailable(providerId, dependencyId, available)` bridge remains supported for simple
two-state dependencies. An unpublished dependency is treated as available, so manifests remain
usable without a hard runtime dependency on another mod.

`applyMode` is descriptive: CMS21 UI+ writes the configuration but does not notify,
reload or invoke the other mod. Authors should use `restartGame` unless their own mod
independently observes file changes or reloads the configuration at the stated point.
An absent manifest leaves the card marked as not supporting UI settings. An invalid
manifest does the same and writes the validation reason to the CMS21 UI+ log.

The complete working reference is `configs/CMS21UIPlus.ui-settings.json`.

## Configuration and runtime files

Current templates and UI manifest:

- `configs/CMS21UIPlus.cfg` — primary feature switches;
- `configs/CMS21UIPlus.ui-settings.json` — in-game settings groups, labels and metadata;
- `resources/Localization/en.json` and `resources/Localization/ru.json` — built-in UI text embedded in the DLL and parsed by the mod;
- `configs/KeyBindings.cfg` — keyboard bindings.

At runtime they are installed under:

```text
<Game>\Mods\CMS21UIPlus\
```

`ProfileMemory.dat` is generated in that directory and stores profile-specific
inventory/warehouse sorting plus the persistent parking-arrival history used by global rearrangement.
Parking rearrangement changes the game's physical parking slots, so the resulting order is saved
by the game. `CMS21UIPlus.cfg.bak` can exist temporarily during an in-game
settings save and contains the previous configuration. It is deleted when the Mods menu closes,
but can remain after an abnormal termination. Do not commit or package either generated file.

## QoLmod origin

CMS21 UI+ retains the following feature concepts from QoLmod by **Meitzi**:

- completed job-part unmarking and required body-part marking: `unmarkFinishedParts`,
  `markBodyParts`;
- shopping-list removal and purchase assistance: `removePartsFromShoppingList`,
  `wheelShopListPurchaseHelper`;
- quick assemble/disassemble mode switching: `quickSwitchMountModes`;
- garage pause-menu exit: `addExitGameToGaragePauseMenu`;
- startup video skipping and automatic prompt continuation: `skipStartupVideosTotally`,
  `autoContinueStartupPrompt`;
- inventory sorting persistence: `rememberInventorySorting`;
- livery filenames and map vehicle information: `showLiveryFileNames`,
  `showCarConditionOnMap`;
- automatic acceptance of the redundant dyno-start confirmation, split from the former
  streamlined dyno feature: `autoConfirmDynoStart`.

## Repository layout

```text
cms21-ui-plus/
├─ configs/
│  ├─ CMS21UIPlus.cfg
│  ├─ CMS21UIPlus.ui-settings.json
│  └─ KeyBindings.cfg
├─ resources/
│  ├─ AssetSources/         # editable sources; never installed
│  ├─ InventoryIndicators/
│  └─ Localization/
├─ libs/                    # local reference DLLs, not tracked by Git
├─ scripts/
│  ├─ build.ps1
│  ├─ build-install.ps1
│  └─ restore-libs.ps1
├─ src/
│  ├─ Features/
│  ├─ Infrastructure/
│  ├─ UI/
│  ├─ Config.cs
│  └─ Main.cs
├─ CMS21UIPlus.csproj
├─ LICENSE.md
├─ README-install.md
└─ README.md
```

## Build

Requirements:

- Windows;
- .NET Framework 4.7.2 Developer Pack;
- Visual Studio Build Tools/MSBuild;
- game, Unity, MelonLoader, Tomlet and Harmony assemblies in `libs`.

### Restoring reference libraries

The DLL files under `libs` are local development dependencies and are not tracked by Git.
Restore them from the installed game and MelonLoader directories:

```powershell
.\scripts\restore-libs.ps1 `
    -GamePath "D:\SteamLibrary\steamapps\common\Car Mechanic Simulator 2021"
```

The script reads the required `libs\*.dll` entries from `CMS21UIPlus.csproj`, creates the
`libs` directory when necessary and preserves existing DLLs unless `-Force` is used. It also
validates the game layout and reports the selected source path and assembly version for each
restored DLL.

### Compiling and installing

From the repository root:

```powershell
.\scripts\build.ps1 -Target Rebuild -Configuration Release
```

Build, create the explicit install payload and install it:

```powershell
.\scripts\build-install.ps1
```

A destination can be supplied directly:

```powershell
.\scripts\build-install.ps1 `
    -Destination "D:\SteamLibrary\steamapps\common\Car Mechanic Simulator 2021"
```

See `README-install.md` for accepted destination paths and installation behavior.

## Licence

GNU General Public License v3.0. See `LICENSE.md`.
