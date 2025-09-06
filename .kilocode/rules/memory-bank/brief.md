# PalCalc Technical Reference

## Project Overview
**PalCalc** is a C#/.NET breeding optimization tool for Palworld with a modular architecture consisting of 7 core sub-projects and external dependencies.

## Core Sub-Projects

### 1. **PalCalc.Model** 
- **Purpose**: Core domain models and data contracts
- **Key Components**: [`PalDB`](PalCalc.Model/PalDB.cs), [`PalInstance`](PalCalc.Model/), [`PassiveSkill`](PalCalc.Model/), game entities
- **Dependencies**: None (base layer)
- **Exports**: Data models, enums, game constants

### 2. **PalCalc.SaveReader**
- **Purpose**: Palworld save file parsing engine
- **Key Components**: [`LevelSaveFile`](PalCalc.SaveReader/SaveFile/LevelSaveFile.cs), GVAS parser, visitor pattern implementation
- **Dependencies**: PalCalc.Model
- **Exports**: Save file data extraction, Steam/Xbox format support
- **Pattern**: Visitor pattern for memory-efficient parsing

### 3. **PalCalc.Solver**
- **Purpose**: Multi-threaded breeding optimization algorithms
- **Key Components**: [`BreedingSolver`](PalCalc.Solver/BreedingSolver.cs), [`WorkingSet`](PalCalc.Solver/WorkingSet.cs), pruning algorithms, probability calculations
- **Dependencies**: PalCalc.Model, PalCalc.SaveReader
- **Exports**: Optimal breeding paths, effort estimation
- **Pattern**: Strategy pattern for pruning, custom thread management

### 4. **PalCalc.GenDB**
- **Purpose**: Game asset extraction and database generation
- **Key Components**: Asset parsers, CUE4Parse integration, localization extraction
- **Dependencies**: PalCalc.Model
- **Exports**: Generated [`db.json`](PalCalc.Model/db.json), pal icons, game data
- **Pattern**: Pipeline pattern for data transformation

### 5. **PalCalc.UI**
- **Purpose**: WPF presentation layer
- **Key Components**: MVVM ViewModels, Views, GraphSharp integration
- **Dependencies**: PalCalc.Model, PalCalc.Solver, PalCalc.SaveReader
- **Exports**: User interface, visualization, settings management
- **Pattern**: MVVM with Community Toolkit

### 6. **CLI Projects** (Testing/Development)
- **PalCalc.SaveReader.CLI**: Save file parsing tests
- **PalCalc.Solver.CLI**: Solver algorithm testing
- **Dependencies**: Respective core projects
- **Purpose**: Development and debugging tools

### 7. **PalCalc.Solver.Tests**
- **Purpose**: Unit tests for solver algorithms
- **Dependencies**: PalCalc.Solver
- **Key Tests**: Probability calculations, breeding logic, IV inheritance

## External Dependencies

### **GraphSharp** & **GraphSharp.Controls**
- **Purpose**: Breeding tree visualization
- **Integration**: PalCalc.UI for graph rendering
- **Type**: Embedded library (defunct project)

### **AdonisUI** & **AdonisUI.ClassicTheme**
- **Purpose**: WPF theming system
- **Integration**: PalCalc.UI styling
- **Type**: Embedded library

### **DotNetKit.Wpf.AutoCompleteComboBox**
- **Purpose**: Enhanced WPF controls
- **Integration**: PalCalc.UI user input
- **Type**: Embedded library

## Data Flow Architecture

```
Game Assets → PalCalc.GenDB → db.json → PalCalc.Model
Save Files → PalCalc.SaveReader → PalInstance → PalCalc.Solver → Breeding Paths → PalCalc.UI
```

## Key Relationships

1. **PalCalc.Model** serves as the foundation for all other projects
2. **PalCalc.SaveReader** transforms save files into Model objects
3. **PalCalc.Solver** processes Model objects to find optimal breeding paths
4. **PalCalc.UI** orchestrates SaveReader and Solver, presents results
5. **PalCalc.GenDB** generates the embedded database used by Model
6. **CLI projects** provide isolated testing environments

## Build Dependencies (Solution Structure)

```
PalCalc.Model (base)
├── PalCalc.SaveReader
├── PalCalc.GenDB  
├── PalCalc.Solver (depends on SaveReader)
│   ├── PalCalc.Solver.CLI
│   └── PalCalc.Solver.Tests
├── PalCalc.UI (depends on Solver, SaveReader)
└── PalCalc.SaveReader.CLI
```

## Additional Components

### **Supporting Directories**
- **adonis-ui/**: Source code for AdonisUI theming library (embedded)
- **DotNetKit.Wpf.AutoCompleteComboBox/**: AutoComplete ComboBox control source (embedded)
- **GraphSharp/** & **GraphSharp.Controls/**: Graph visualization library source (embedded)
