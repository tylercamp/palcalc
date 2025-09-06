# PalSchema Architecture Analysis & Data Location Index

## Overview

PalSchema is a sophisticated mod system for Palworld that enables JSON-based customization of in-game constants without directly editing game files. It uses function hooking, Unreal Engine reflection, and structured data table modification to achieve conflict-free modding.

## Core Architecture

### Hook-Based Interception System

PalSchema uses SafetyHook to intercept critical Unreal Engine functions:

- **`UDataTable::HandleDataTableChanged`** - Catches when game data tables are loaded/modified
- **`AGameModeBase::InitGameState`** - Initializes the mod system when the game starts  
- **`UClass::PostLoad`** - Handles blueprint modifications after they're loaded
- **`FPakPlatformFile::GetPakFolders`** - Adds custom pak file locations

### JSON Processing Pipeline

The system processes JSON files through specialized loaders organized in the `Mods/PalSchema/mods/` directory:

```
mods/
├── YourModName/
│   ├── pals/          # Monster/Pal modifications
│   ├── items/         # Item data changes  
│   ├── buildings/     # Building modifications
│   ├── raw/           # Direct data table edits
│   ├── blueprints/    # Blueprint modifications
│   ├── translations/  # Language/text changes
│   ├── enums/         # Custom enum values
│   └── paks/          # Custom pak files (assets)
```

## Data Table Location Patterns

### Base Path Structure
All Palworld data tables follow this consistent pattern:
```
/Game/Pal/DataTable/[Category]/DT_[TableName].DT_[TableName]
```

### Category Organization

#### Character/Pal Data (`/Game/Pal/DataTable/Character/`)
- `DT_PalMonsterParameter` - Core Pal stats (HP, Attack, Defense, etc.)
- `DT_PalCharacterIconDataTable` - Pal icon references
- `DT_PalBPClass` - Blueprint class assignments
- `DT_PalDropItem` - Loot drop tables
- `DT_PalCharacterIconDataTable_SkinOverride` - Skin icon overrides

#### Text/Localization (`/Game/Pal/DataTable/Text/`)
- `DT_PalNameText` - Pal display names
- `DT_PalShortDescriptionText` - Short descriptions
- `DT_PalLongDescriptionText` - Detailed descriptions
- `DT_ItemNameText` - Item names
- `DT_ItemDescriptionText` - Item descriptions
- `DT_UI_Common_Text` - UI text elements
- `DT_MapObjectNameText` - Building names
- `DT_BuildObjectDescText` - Building descriptions
- `DT_TechnologyNameText` - Technology names
- `DT_TechnologyDescText` - Technology descriptions

#### Item Data (`/Game/Pal/DataTable/Item/`)
- `DT_ItemRecipeDataTable` - Crafting recipes

#### Abilities/Moves (`/Game/Pal/DataTable/Waza/`)
- `DT_WazaMasterLevel` - Abilities learned by level

#### Building/World Objects (`/Game/Pal/DataTable/MapObject/`)
- `DT_MapObjectAssignData` - Object assignment data
- `DT_MapObjectFarmCrop` - Farm crop data
- `DT_MapObjectItemProductDataTable` - Production data
- `DT_MapObjectMasterDataTable` - Master object definitions

#### Building-Specific (`/Game/Pal/DataTable/MapObject/Building/`)
- `DT_BuildObjectDataTable` - Building definitions
- `DT_BuildObjectIconDataTable` - Building icons

#### Technology/Research (`/Game/Pal/DataTable/Technology/`)
- `DT_TechnologyRecipeUnlock` - Technology unlock conditions

#### Character Creation (`/Game/Pal/DataTable/CharacteCreation/`)
- `DT_CharacterCreationMeshPresetTable_Hair` - Hair options
- `DT_CharacterCreationMeshPresetTable_Head` - Head options
- `DT_CharacterCreationEyeMaterialPresetTable` - Eye materials
- `DT_CharacterCreationMeshPresetTable_Body` - Body types
- `DT_CharacterCreationMakeInfoPreset` - Preset configurations
- `DT_CharacterCreationColorPresetTable` - Color presets
- `DT_CharacterCreationMeshPresetTable_Equipments` - Equipment options

## File Index for Data Table References

### Source Code Index Files

#### Loader Header Files (Declare data table references)
- **`include/Loader/PalMonsterModLoader.h:38-45`** - Monster/Pal data tables (8 tables)
- **`include/Loader/PalItemModLoader.h:33-36`** - Item data tables (3 tables)
- **`include/Loader/PalBuildingModLoader.h:26-36`** - Building data tables (11 tables)
- **`include/Loader/PalAppearanceModLoader.h:27-33`** - Character creation data tables (7 tables)
- **`include/Loader/PalSkinModLoader.h:25-27`** - Skin data tables (2 tables)

#### Loader Implementation Files (Contain actual data table paths)
- **`src/Loader/PalMonsterModLoader.cpp:23-45`** - 8 data tables for Pals
- **`src/Loader/PalItemModLoader.cpp:29-36`** - 3 data tables for Items
- **`src/Loader/PalBuildingModLoader.cpp:20-51`** - 11 data tables for Buildings
- **`src/Loader/PalAppearanceModLoader.cpp:29-48`** - 7 data tables for Appearance
- **`src/Loader/PalSkinModLoader.cpp:31-35`** - 2 data tables for Skins

### Documentation Index Files

#### Type Documentation (Shows which data tables use specific property types)
- **`website/docs/types/softobjectptr.md:11`** - References `DT_PalCharacterIconDataTable`
- **`website/docs/types/softclassptr.md:11`** - References `DT_PalBPClass`
- **`website/docs/types/numericproperty.md:7`** - References `DT_PalMonsterParameter`
- **`website/docs/types/ftext.md:7`** - References `DT_ItemNameText`
- **`website/docs/types/arrayproperty.md:7`** - References `DT_ItemShopCreateData`

#### Guide Documentation (Shows practical usage examples)
- **`website/docs/guides/rawtables/intro.md:27`** - References `DT_PalDropItem`
- **`website/docs/guides/translations/intro.md:35`** - References `DT_PalNameText`
- **`website/docs/guides/buildings/craftingstation.md:197`** - References `DT_MapObjectAssignData` and `DT_BuildObjectIconDataTable`

## Blueprint System

### Blueprint Generated Classes (`_C` suffix)

PalSchema handles blueprint modifications through a specialized system that operates on Blueprint Generated Classes. These are the runtime/compiled versions of blueprints.

#### Key Blueprint: `BP_PalGameSetting_C`

**Location**: `Pal/Content/Pal/Blueprint/System/BP_PalGameSetting.uasset`

**Contains**: Game-wide settings and constants including:
- **SprintSP** - Stamina consumption for sprinting
- **CharacterRankUpRequiredNumMap** - Pal star level requirements
- **Various gameplay constants** that affect core game mechanics

**Documentation References**:
- **`website/docs/guides/blueprints/intro.md:24-35`** - Shows SprintSP modification
- **`website/docs/types/mapproperty.md:11-18`** - Shows CharacterRankUpRequiredNumMap modification

#### Blueprint Loading System

1. **Asset Path Translation**: Blueprints use the `_C` suffix for Blueprint Generated Classes
2. **Loading Method**: Non-`/Game/` paths use `LoadSafe()` method for deferred loading
3. **Application**: Modifications applied via `PostLoad` hook when blueprint loads
4. **Registry**: Stored in `BPModRegistry` for deferred application

**Implementation Files**:
- **`src/Loader/PalBlueprintModLoader.cpp`** - Main blueprint modification logic
- **`include/Loader/PalBlueprintModLoader.h`** - Blueprint loader interface
- **`src/Loader/Blueprint/PalBlueprintMod.cpp`** - Individual blueprint mod handling
- **`include/Loader/Blueprint/PalBlueprintMod.h`** - Blueprint mod data structure

## Raw Table Modification System

### Direct Data Table Access

The `PalRawTableLoader` allows direct modification of any Unreal Engine data table by name:

**Features**:
- **Row operations**: Add, edit, or delete table rows
- **Wildcard filtering**: Apply changes to multiple rows using `*` patterns
- **Property mapping**: Maps JSON keys to Unreal Engine property names

**Example JSON Structure**:
```json
{
  "DT_PalMonsterParameter": {
    "Lamball": {
      "HP": 100,
      "Attack": 50,
      "Defense": 30
    },
    "*": {
      "CaptureRateCorrect": 1.5
    }
  }
}
```

**Implementation**: `src/Loader/PalRawTableLoader.cpp`

## Property System Integration

### Unreal Engine Reflection

The core mechanism uses Unreal Engine's reflection system via `PropertyHelper`:

```cpp
PropertyHelper::CopyJsonValueToContainer(Row, Property, JsonValue);
```

**Functions**:
- Inspects target property's type using UE reflection
- Converts JSON values to appropriate C++ types
- Handles complex types like soft object references, arrays, and structs

### Memory Management

- Uses `FMemory::Malloc/Free` for UE-compatible allocation
- Properly initializes/destroys UE structs using `InitializeStruct/DestroyStruct`
- Handles reference counting for UE objects

## Auto-Reload System

### File Watching

```cpp
filewatch::FileWatch<std::wstring>(modsPath, std::wregex(L".*\\.(json|jsonc)"), callback);
```

Enables hot-reloading of JSON files without restarting the game.

## Pak File Integration

### Custom Asset Support

PalSchema extends Unreal Engine's pak loading system:

```cpp
void PalMainLoader::GetPakFolders(const TCHAR* CmdLine, TArray<FString>* OutPakFolders)
{
    GetPakFolders_Hook.call(CmdLine, OutPakFolders);
    auto ModsFolderPath = fs::path(...) / "Mods" / "PalSchema" / "mods";
    OutPakFolders->Add(FString(AbsolutePathWithSuffix.c_str()));
}
```

**Enables**:
- Custom textures and models
- New audio files  
- Additional game assets
- Bundled content alongside JSON modifications

## Key Files for Understanding the System

1. **`src/Loader/PalMainLoader.cpp`** - Coordinates all loaders and hook management
2. **`src/Loader/PalRawTableLoader.cpp`** - Direct data table modification by name
3. **`include/Loader/PalMainLoader.h:35-44`** - Declares all specialized loaders
4. **`src/dllmain.cpp`** - Entry point and initialization
5. **`src/Utility/Config.cpp`** - Configuration system

## Complete Data Table Registry

### Monster/Pal Tables (8 tables)
```cpp
// From src/Loader/PalMonsterModLoader.cpp:23-45
m_dataTable                 // DT_PalMonsterParameter
m_iconDataTable             // DT_PalCharacterIconDataTable  
m_palBpClassTable           // DT_PalBPClass
m_wazaMasterLevelTable      // DT_WazaMasterLevel
m_palDropItemTable          // DT_PalDropItem
m_palNameTable              // DT_PalNameText
m_palShortDescTable         // DT_PalShortDescriptionText
m_palLongDescTable          // DT_PalLongDescriptionText
```

### Item Tables (3 tables)
```cpp
// From src/Loader/PalItemModLoader.cpp:29-36
m_itemRecipeTable           // DT_ItemRecipeDataTable
m_nameTranslationTable      // DT_ItemNameText
m_descriptionTranslationTable // DT_ItemDescriptionText
```

### Building Tables (11 tables)
```cpp
// From src/Loader/PalBuildingModLoader.cpp:20-51
m_mapObjectAssignData       // DT_MapObjectAssignData
m_mapObjectFarmCrop         // DT_MapObjectFarmCrop
m_mapObjectItemProductDataTable // DT_MapObjectItemProductDataTable
m_mapObjectMasterDataTable  // DT_MapObjectMasterDataTable
m_mapObjectNameTable        // DT_MapObjectNameText
m_buildObjectDataTable      // DT_BuildObjectDataTable
m_buildObjectIconDataTable  // DT_BuildObjectIconDataTable
m_buildObjectDescTable      // DT_BuildObjectDescText
m_technologyRecipeUnlockTable // DT_TechnologyRecipeUnlock
m_technologyNameTable       // DT_TechnologyNameText
m_technologyDescTable       // DT_TechnologyDescText
```

### Character Creation Tables (7 tables)
```cpp
// From src/Loader/PalAppearanceModLoader.cpp:29-48
m_hairTable                 // DT_CharacterCreationMeshPresetTable_Hair
m_headTable                 // DT_CharacterCreationMeshPresetTable_Head
m_eyesTable                 // DT_CharacterCreationEyeMaterialPresetTable
m_bodyTable                 // DT_CharacterCreationMeshPresetTable_Body
m_presetTable               // DT_CharacterCreationMakeInfoPreset
m_colorPresetTable          // DT_CharacterCreationColorPresetTable
m_equipmentTable            // DT_CharacterCreationMeshPresetTable_Equipments
```

### Skin Tables (2 tables)
```cpp
// From src/Loader/PalSkinModLoader.cpp:31-35
m_skinIconTable             // DT_PalCharacterIconDataTable_SkinOverride
m_skinTranslationTable      // DT_UI_Common_Text
```

This architecture provides a powerful, flexible way to modify Palworld's game constants without directly editing game files, making it mod-friendly and reducing conflicts between different modifications.