# PowerPlatformToolBox (PPTB) Porting Plan
## Dataverse to Power BI Semantic Model Generator

**Version:** 1.0  
**Date:** February 2026  
**Status:** Planning Phase

---

## Executive Summary

This document outlines a comprehensive plan to port the **Dataverse to Power BI Semantic Model Generator** from XrmToolBox (.NET Framework 4.8, Windows Forms) to **PowerPlatformToolBox** (Electron, TypeScript, cross-platform). The port will maintain feature parity while adapting to PPTB's web-based architecture and leveraging its cross-platform capabilities.

**Key Metrics:**
- **Current Codebase:** ~15 C# files, ~4,400 lines in SemanticModelBuilder alone, ~160KB PluginControl.cs
- **Target Platform:** TypeScript/JavaScript, Electron-based, cross-platform (Windows/macOS/Linux)
- **Architecture:** Three-layer design - Shared Core Logic (TypeScript), PPTB Adapter, React UI
- **Estimated Effort:** 6-8 weeks for full feature parity
- **Recommended Framework:** React with TypeScript (best tooling, PPTB samples, component ecosystem)

---

## Table of Contents

1. [Component Portability Analysis](#1-component-portability-analysis)
2. [Proposed Architecture](#2-proposed-architecture)
3. [Technical Challenges](#3-technical-challenges)
4. [Implementation Approach](#4-implementation-approach)
5. [Phased Implementation Plan](#5-phased-implementation-plan)
6. [Risk Assessment & Mitigation](#6-risk-assessment--mitigation)
7. [Success Criteria](#7-success-criteria)
8. [Appendices](#appendices)

---

## 1. Component Portability Analysis

### 1.1 Current Architecture Overview

```
DataverseToPowerBI.Core (Framework-agnostic .NET)
  ├── IDataverseConnection interface
  └── DataModels.cs (all data models)

DataverseToPowerBI.XrmToolBox (.NET Framework 4.8)
  ├── UI Layer (Windows Forms)
  │   ├── PluginControl.cs (160KB, main orchestrator)
  │   ├── FactDimensionSelectorForm.cs (78KB, star-schema wizard)
  │   ├── TableSelectorForm.cs
  │   ├── FormViewSelectorForm.cs
  │   ├── CalendarTableDialog.cs
  │   ├── SemanticModelChangesDialog.cs
  │   ├── SemanticModelSelectorDialog.cs
  │   └── TmdlPreviewDialog.cs
  │
  ├── Business Logic Layer
  │   ├── SemanticModelBuilder.cs (4,357 lines, TMDL generation engine)
  │   ├── FetchXmlToSqlConverter.cs (FetchXML → SQL translation)
  │   └── DebugLogger.cs
  │
  ├── Persistence Layer
  │   └── SemanticModelManager.cs (JSON config management)
  │
  └── Dataverse Integration
      └── XrmServiceAdapterImpl.cs (IOrganizationService wrapper)
```

### 1.2 Portability Classification

#### ✅ **HIGHLY PORTABLE** (Can be reused with minimal changes)

| Component | Type | Lines | Portability | Notes |
|-----------|------|-------|-------------|-------|
| **DataModels.cs** | Models | ~500 | 95% | Convert C# classes to TypeScript interfaces/types. JSON serialization attributes map to standard JSON. |
| **IDataverseConnection** | Interface | ~200 | 90% | Maps directly to TypeScript interface. Async patterns already present. |
| **SemanticModelBuilder Logic** | Business Logic | ~4,357 | 80% | Core TMDL generation logic is platform-agnostic. File I/O and string manipulation translate well to Node.js. |
| **FetchXmlToSqlConverter** | Business Logic | ~800 | 85% | XML parsing and string manipulation. Use DOMParser or xml2js in TypeScript. |
| **SemanticModelManager** | Persistence | ~400 | 90% | JSON serialization/deserialization. Node.js fs module handles file operations. |

#### ⚠️ **REQUIRES ADAPTATION** (Needs significant rewrite)

| Component | Type | Lines | Effort | Notes |
|-----------|------|-------|--------|-------|
| **XrmServiceAdapterImpl** | Integration | ~800 | Medium | Replace with `window.dataverseAPI` calls. PPTB provides similar methods. |
| **PluginControl.cs** | UI | ~3,500 | High | Windows Forms → React components. Complex state management. |
| **FactDimensionSelectorForm** | UI | ~1,800 | High | Multi-step wizard with tree views, checkboxes, grouping logic. |
| **All Dialog Forms** | UI | ~1,500 | Medium | Modal dialogs map to React modal components. |

#### ❌ **PLATFORM-SPECIFIC** (Must be replaced)

| Component | Replacement Strategy |
|-----------|---------------------|
| **Windows Forms Controls** | React components + UI library (e.g., Ant Design, Material-UI) |
| **IOrganizationService SDK** | `window.dataverseAPI` methods from PPTB |
| **.NET File I/O** | Node.js `fs` module via `window.toolboxAPI.utils.saveFile()` |
| **WorkAsync pattern** | Standard async/await with React state management |
| **XrmToolBox settings storage** | PPTB settings API (if available) or local JSON files |

### 1.3 PPTB API Coverage Analysis

#### ✅ **Available in PPTB** (Based on React sample analysis)

| Feature | PPTB API | Coverage |
|---------|----------|----------|
| **Dataverse Queries** | `window.dataverseAPI.fetchXmlQuery()` | ✅ Full |
| **Entity Metadata** | `window.dataverseAPI.getEntityMetadata()` | ✅ Full |
| **All Entities Metadata** | `window.dataverseAPI.getAllEntitiesMetadata()` | ✅ Full |
| **CRUD Operations** | `window.dataverseAPI.create/update/delete()` | ✅ Full |
| **Connection Info** | `window.toolboxAPI.connections.getActiveConnection()` | ✅ Full |
| **Notifications** | `window.toolboxAPI.utils.showNotification()` | ✅ Full |
| **Clipboard** | `window.toolboxAPI.utils.copyToClipboard()` | ✅ Full |
| **File Save** | `window.toolboxAPI.utils.saveFile()` | ✅ Full |
| **Theme Detection** | `window.toolboxAPI.utils.getTheme()` | ✅ Full |
| **Events** | `window.toolboxAPI.events.on()` | ✅ Full |

#### ⚠️ **Needs Verification** (May require custom implementation)

| Feature | Requirement | Workaround/Solution |
|---------|-------------|---------------------|
| **Solution Tables Query** | Get tables by solution ID | Use FetchXML query against `solutioncomponent` entity |
| **Form Metadata with XML** | Get FormXML | Use Web API: `/systemforms(guid)?$select=formxml` |
| **View Metadata with FetchXML** | Get view FetchXML | Use Web API: `/savedqueries(guid)?$select=fetchxml` |
| **Relationship Metadata** | Get lookup relationships | Use `EntityMetadata.ManyToOneRelationships` from metadata API |
| **Choice/Optionset Labels** | Get display names for choices | Available in `AttributeMetadata` |
| **Multi-file PBIP Generation** | Create folder with multiple files | Use `window.toolboxAPI.utils.saveFile()` multiple times or zip creation |

#### ❌ **Not Available** (Requires new implementation)

| Gap | Impact | Mitigation |
|-----|--------|------------|
| **Folder Picker Dialog** | Users select output folder | Use file save dialog for initial file, derive folder path |
| **Advanced TreeView Controls** | Complex hierarchical selection UI | Implement custom React tree component (e.g., `react-arborist`, `rc-tree`) |
| **DataGridView Sorting/Filtering** | Rich table interactions | Use React table library (e.g., `@tanstack/react-table`) |
| **Direct File System Access** | Write multiple TMDL files to folder | Use PPTB file save API iteratively or create zip archive |

---

## 2. Proposed Architecture

### 2.1 Three-Layer Architecture

Maintain the current philosophy: **Keep core logic separate from UI and platform-specific code.**

```
┌─────────────────────────────────────────────────────────────────┐
│                         PPTB Tool Package                       │
│                    pptb-dataverse-semanticmodel                 │
└─────────────────────────────────────────────────────────────────┘
         │
         ├─── 📱 UI Layer (React + TypeScript)
         │    ├── App.tsx (main orchestrator)
         │    ├── components/
         │    │   ├── ConnectionStatus.tsx
         │    │   ├── ModelConfigurationPanel.tsx
         │    │   ├── TableSelector/ (folder)
         │    │   │   ├── SolutionSelector.tsx
         │    │   │   └── TableListView.tsx
         │    │   ├── StarSchemaWizard/ (folder)
         │    │   │   ├── FactTableSelector.tsx
         │    │   │   ├── DimensionSelector.tsx
         │    │   │   └── RelationshipManager.tsx
         │    │   ├── AttributeSelector.tsx
         │    │   ├── FormViewSelector.tsx
         │    │   ├── CalendarTableDialog.tsx
         │    │   ├── TmdlPreview.tsx
         │    │   └── ChangePreview.tsx
         │    ├── hooks/
         │    │   ├── useDataverseMetadata.ts
         │    │   ├── useModelConfiguration.ts
         │    │   └── useSemanticModelBuilder.ts
         │    └── styles/
         │        └── main.css
         │
         ├─── 🔧 Adapter Layer (PPTB Integration)
         │    ├── DataverseAdapter.ts
         │    │   └── Implements IDataverseConnection using window.dataverseAPI
         │    ├── StorageAdapter.ts
         │    │   └── Handles configuration persistence
         │    └── FileSystemAdapter.ts
         │        └── Wraps PPTB file APIs for PBIP generation
         │
         └─── 🧠 Core Logic Layer (Platform-Agnostic TypeScript)
              ├── models/
              │   ├── DataModels.ts (ported from C#)
              │   └── IDataverseConnection.ts (ported interface)
              ├── services/
              │   ├── SemanticModelBuilder.ts (MAIN - ported from C#)
              │   ├── FetchXmlToSqlConverter.ts (ported from C#)
              │   ├── TmdlGenerator.ts (TMDL string generation)
              │   └── ChangeAnalyzer.ts (change detection logic)
              └── utils/
                  ├── XmlParser.ts
                  ├── StringUtils.ts
                  └── Logger.ts
```

### 2.2 Data Flow

```
User Interaction (React UI)
    ↓
React Components + Hooks
    ↓
Adapter Layer (PPTB APIs)
    ↓
Core Logic (SemanticModelBuilder, FetchXmlToSqlConverter)
    ↓
Adapter Layer (File I/O)
    ↓
PBIP Output (Folder with TMDL files)
```

### 2.3 Key Design Principles

1. **Core Logic Isolation**: `SemanticModelBuilder.ts`, `FetchXmlToSqlConverter.ts`, and models have ZERO dependencies on React or PPTB APIs
2. **Interface Abstraction**: `IDataverseConnection` interface allows swapping implementations (PPTB, mock, future platforms)
3. **Dependency Injection**: Core services receive adapters via constructor, enabling testing and flexibility
4. **Immutable State**: React state management with immutability for predictable UI updates
5. **Type Safety**: Full TypeScript coverage with strict mode enabled

### 2.4 Technology Stack

| Layer | Technology | Justification |
|-------|-----------|---------------|
| **Framework** | React 18 + TypeScript | Best PPTB sample support, large ecosystem, team familiarity |
| **Build Tool** | Vite | Fast dev server, optimized builds, PPTB compatibility proven |
| **UI Components** | Ant Design or Material-UI | Rich component library (TreeSelect, Table, Modal, Form) |
| **Table Component** | @tanstack/react-table | Powerful sorting, filtering, grouping for attribute grids |
| **Tree Component** | rc-tree or react-arborist | Hierarchical selection for star-schema wizard |
| **State Management** | React Context + Hooks | Simpler than Redux for this use case, sufficient for form-heavy UI |
| **XML Parsing** | DOMParser (native) | Built-in browser API, no dependencies for FetchXML parsing |
| **File Generation** | window.toolboxAPI.utils.saveFile() | PPTB-provided API for file operations |
| **Testing** | Vitest + React Testing Library | Vite-native testing, component testing best practices |

---

## 3. Technical Challenges

### 3.1 Major Challenges

#### Challenge 1: TMDL Generation Engine Port (4,357 lines)

**Complexity:** High  
**Effort:** 2-3 weeks  

**C# Specifics:**
- Heavy use of `StringBuilder` for large string construction
- Regex patterns for TMDL parsing and user code preservation
- File I/O with UTF-8 without BOM
- Dictionary-based state management
- LINQ queries for metadata filtering

**TypeScript Solution:**
- Template literals and array joins for string building
- Same regex patterns (JavaScript regex is similar)
- Node.js `fs` module with explicit UTF-8 encoding
- JavaScript `Map` and `Object` for state
- Array methods (`filter`, `map`, `reduce`) instead of LINQ

**Code Reuse Strategy:**
- Port method-by-method, maintaining same structure
- Create TypeScript interfaces matching C# models exactly
- Unit tests for each conversion method
- Side-by-side validation: Generate TMDL from same config in both versions, compare output

#### Challenge 2: Multi-Dialog Workflow → Single-Page React App

**Complexity:** Medium-High  
**Effort:** 1-2 weeks  

**XrmToolBox Approach:**
- Separate modal dialogs for each step
- State passed via dialog results
- Linear workflow enforced by dialog sequence

**PPTB Solution:**
- Single-page app with wizard-style stepper or tabbed interface
- Centralized state management via React Context
- Enable non-linear navigation (jump to any step)
- Visual progress indicator

**Design Pattern:**
```tsx
<AppContext.Provider value={{ modelConfig, dataverseAdapter }}>
  <Layout>
    <Sidebar /> {/* Navigation, model selector */}
    <MainPanel>
      <Routes>
        <Route path="/tables" element={<TableSelector />} />
        <Route path="/star-schema" element={<StarSchemaWizard />} />
        <Route path="/attributes" element={<AttributeSelector />} />
        <Route path="/relationships" element={<RelationshipManager />} />
        <Route path="/calendar" element={<CalendarTableDialog />} />
        <Route path="/preview" element={<TmdlPreview />} />
      </Routes>
    </MainPanel>
  </Layout>
</AppContext.Provider>
```

#### Challenge 3: Windows Forms Controls → React Components

**Complexity:** Medium  
**Effort:** 2 weeks  

| Windows Forms Control | React Replacement | Library |
|-----------------------|-------------------|---------|
| **DataGridView** (sortable, multi-select) | `@tanstack/react-table` | Custom component |
| **TreeView** (hierarchical checkboxes) | `rc-tree` with custom render | rc-tree |
| **CheckedListBox** | `Checkbox.Group` | Ant Design |
| **TabControl** | `Tabs` | Ant Design |
| **ToolStrip** | Custom toolbar with `Button` | Ant Design |
| **SplitContainer** | CSS Grid or `react-split-pane` | react-split-pane |
| **ProgressBar** | `Progress` | Ant Design |

**Key Differences:**
- React components are declarative; state drives rendering
- No manual event wire-up; use `onChange` handlers
- Sorting/filtering built into table libraries vs. manual ListView comparison

#### Challenge 4: File System Operations

**Complexity:** Medium  
**Effort:** 1 week  

**XrmToolBox:**
- Direct folder picker via Windows API
- Write multiple files to selected folder
- UTF-8 without BOM encoding

**PPTB:**
- No native folder picker (file-based dialogs only)
- `window.toolboxAPI.utils.saveFile()` for single files
- May need to create ZIP archive for multi-file PBIP projects

**Solution Options:**

**Option A: Individual File Save (Simple but tedious)**
```typescript
// User picks a folder via initial file save
const firstFilePath = await window.toolboxAPI.utils.saveFile({
  defaultFileName: 'model.tmdl',
  content: tmdlContent
});
// Derive folder from first file path
const folder = path.dirname(firstFilePath);
// Write remaining files programmatically if API allows folder write
```

**Option B: ZIP Archive (Better UX)**
```typescript
// Generate all TMDL files in memory
const files = generateAllTmdlFiles(modelConfig);
// Create ZIP archive using jszip library
const zip = new JSZip();
files.forEach(file => zip.file(file.path, file.content));
// Save single .zip file
const zipBlob = await zip.generateAsync({ type: 'blob' });
await window.toolboxAPI.utils.saveFile({
  defaultFileName: 'SemanticModel.pbip.zip',
  content: zipBlob
});
// User extracts ZIP to desired location
```

**Recommendation:** Start with Option A, add Option B if user feedback indicates need.

#### Challenge 5: Change Detection & Preview

**Complexity:** Medium  
**Effort:** 1 week  

**Current Implementation:**
- Parse existing TMDL files from disk
- Compare with new config
- Classify changes (Safe, Additive, Moderate, Destructive)
- Show TreeView with color-coded impacts

**Port Strategy:**
- Use `DOMParser` or `xml-js` for TMDL parsing (TMDL is structured like XML)
- Diff algorithm: Deep object comparison (e.g., `lodash.isEqual`)
- React Tree component with custom node rendering for color coding
- Same impact categories, same logic

### 3.2 Minor Challenges

| Challenge | Complexity | Solution |
|-----------|-----------|----------|
| **Timezone Handling** | Low | JavaScript `Date`, `Intl.DateTimeFormat` |
| **Regex Patterns** | Low | JavaScript regex syntax nearly identical to .NET |
| **Logging** | Low | Console API or structured logger (e.g., `winston`) |
| **FetchXML Parsing** | Low | DOMParser (browser native) or `xml2js` library |
| **JSON Serialization** | Very Low | Native `JSON.stringify/parse` |

---

## 4. Implementation Approach

### 4.1 Project Structure

```
pptb-dataverse-semanticmodel/
├── package.json                  # PPTB tool manifest
├── tsconfig.json                 # TypeScript configuration
├── vite.config.ts                # Vite build configuration
├── index.html                    # Entry point
├── README.md                     # Tool documentation
│
├── src/
│   ├── main.tsx                  # React entry point
│   ├── App.tsx                   # Main app component
│   ├── vite-env.d.ts             # Type definitions for PPTB APIs
│   │
│   ├── components/               # React UI components
│   │   ├── layout/
│   │   │   ├── AppLayout.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   └── Header.tsx
│   │   ├── configuration/
│   │   │   ├── ModelSelector.tsx
│   │   │   └── SettingsPanel.tsx
│   │   ├── tables/
│   │   │   ├── SolutionSelector.tsx
│   │   │   ├── TableList.tsx
│   │   │   └── TableSelector.tsx
│   │   ├── star-schema/
│   │   │   ├── FactTablePicker.tsx
│   │   │   ├── DimensionSelector.tsx
│   │   │   └── RelationshipTree.tsx
│   │   ├── attributes/
│   │   │   ├── AttributeSelector.tsx
│   │   │   ├── FormPicker.tsx
│   │   │   └── ViewPicker.tsx
│   │   ├── calendar/
│   │   │   └── CalendarTableDialog.tsx
│   │   ├── preview/
│   │   │   ├── TmdlPreview.tsx
│   │   │   └── ChangePreview.tsx
│   │   └── common/
│   │       ├── LoadingSpinner.tsx
│   │       ├── ErrorBoundary.tsx
│   │       └── StatusMessage.tsx
│   │
│   ├── hooks/                    # Custom React hooks
│   │   ├── useConnection.ts
│   │   ├── useDataverse.ts
│   │   ├── useModelConfig.ts
│   │   ├── useSemanticModel.ts
│   │   └── useEventLog.ts
│   │
│   ├── adapters/                 # PPTB integration adapters
│   │   ├── DataverseAdapter.ts
│   │   ├── StorageAdapter.ts
│   │   └── FileSystemAdapter.ts
│   │
│   ├── core/                     # Platform-agnostic core logic
│   │   ├── models/
│   │   │   ├── IDataverseConnection.ts
│   │   │   ├── DataModels.ts
│   │   │   └── ConfigurationModels.ts
│   │   ├── services/
│   │   │   ├── SemanticModelBuilder.ts    # MAIN ENGINE
│   │   │   ├── FetchXmlToSqlConverter.ts
│   │   │   ├── TmdlGenerator.ts
│   │   │   ├── ChangeAnalyzer.ts
│   │   │   └── RelationshipDetector.ts
│   │   └── utils/
│   │       ├── XmlParser.ts
│   │       ├── StringUtils.ts
│   │       ├── Logger.ts
│   │       └── ValidationUtils.ts
│   │
│   ├── context/                  # React Context for global state
│   │   ├── AppContext.tsx
│   │   └── ModelConfigContext.tsx
│   │
│   └── styles/                   # CSS styles
│       ├── main.css
│       └── components.css
│
└── tests/                        # Unit and integration tests
    ├── core/
    │   ├── SemanticModelBuilder.test.ts
    │   ├── FetchXmlToSqlConverter.test.ts
    │   └── TmdlGenerator.test.ts
    ├── adapters/
    │   └── DataverseAdapter.test.ts
    └── components/
        └── TableSelector.test.tsx
```

### 4.2 Build Configuration (`vite.config.ts`)

```typescript
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';

function fixHtmlForPPTB(): Plugin {
  return {
    name: 'fix-html-for-pptb',
    enforce: 'post',
    transformIndexHtml(html) {
      html = html.replace(/\s*type="module"/g, '');
      html = html.replace(/\s*crossorigin/g, '');
      html = html.replace(/\s+>/g, '>');
      
      const scriptRegex = /(<script[^>]*src="[^"]*"[^>]*><\/script>)/g;
      const scripts: string[] = [];
      html = html.replace(scriptRegex, (match) => {
        scripts.push(match);
        return '';
      });
      
      if (scripts.length > 0) {
        const scriptsHtml = '\n  ' + scripts.join('\n  ');
        html = html.replace('</body>', scriptsHtml + '\n</body>');
      }
      
      return html;
    },
  };
}

export default defineConfig({
  plugins: [react(), fixHtmlForPPTB()],
  base: './',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    rollupOptions: {
      output: {
        format: 'iife',
        inlineDynamicImports: true,
        manualChunks: undefined,
      },
    },
  },
  resolve: {
    alias: {
      '@': '/src',
      '@core': '/src/core',
      '@adapters': '/src/adapters',
      '@components': '/src/components',
      '@hooks': '/src/hooks',
    },
  },
});
```

### 4.3 Package Configuration (`package.json`)

```json
{
  "name": "pptb-dataverse-semanticmodel",
  "version": "2.0.0",
  "displayName": "Dataverse Semantic Model Generator",
  "description": "Generate Power BI semantic models (PBIP/TMDL) from Dataverse metadata",
  "contributors": [
    {
      "name": "Your Name",
      "url": "https://github.com/yourusername"
    }
  ],
  "license": "GPL-3.0",
  "main": "index.html",
  "repository": {
    "type": "git",
    "url": "https://github.com/yourusername/dataverse-semantic-model.git"
  },
  "engines": {
    "node": ">=18.0.0"
  },
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview",
    "test": "vitest",
    "test:ui": "vitest --ui",
    "lint": "eslint src --ext ts,tsx",
    "type-check": "tsc --noEmit"
  },
  "dependencies": {
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "antd": "^5.22.0",
    "@tanstack/react-table": "^8.20.0",
    "rc-tree": "^5.10.0",
    "jszip": "^3.10.1",
    "lodash-es": "^4.17.21"
  },
  "devDependencies": {
    "@pptb/types": "^1.0.19",
    "@types/react": "^18.3.12",
    "@types/react-dom": "^18.3.1",
    "@types/lodash-es": "^4.17.12",
    "@vitejs/plugin-react": "^4.3.4",
    "typescript": "^5.6.3",
    "vite": "^7.1.11",
    "vitest": "^2.1.0",
    "@testing-library/react": "^16.0.0",
    "@testing-library/jest-dom": "^6.6.0",
    "eslint": "^9.0.0",
    "@typescript-eslint/eslint-plugin": "^8.0.0",
    "@typescript-eslint/parser": "^8.0.0"
  },
  "files": [
    "dist"
  ]
}
```

### 4.4 Development Workflow

1. **Local Development**
   ```bash
   npm install
   npm run dev
   # Opens Vite dev server (APIs won't work until loaded in PPTB)
   ```

2. **Build for PPTB**
   ```bash
   npm run build
   # Outputs to dist/ folder
   ```

3. **Install in PPTB**
   - Open PowerPlatformToolBox
   - Tools → Install Tool
   - Point to project folder or publish to npm

4. **Testing in PPTB**
   - Connect to Dataverse environment
   - Test end-to-end workflows

5. **Unit Testing**
   ```bash
   npm run test
   # Run core logic tests (SemanticModelBuilder, converters)
   npm run test:ui
   # Visual test UI
   ```

### 4.5 Incremental Migration Strategy

**Option A: Big Bang (Recommended)**
- Build complete PPTB tool from scratch
- Maintain XTB version until PPTB version reaches feature parity
- Switch users over at v2.0 release

**Pros:**
- Clean break, no hybrid maintenance
- Freedom to redesign UX for web platform
- Faster overall timeline

**Cons:**
- No incremental user feedback
- Higher upfront effort before release

**Option B: Parallel Development**
- Release PPTB version as "beta" early
- Gather user feedback while maintaining XTB version
- Deprecate XTB version after 6 months

**Pros:**
- Early user validation
- Smoother transition

**Cons:**
- Dual maintenance burden
- Feature drift between versions

**Recommendation:** Option A (Big Bang) — this is a platform shift, clean break makes sense.

---

## 5. Phased Implementation Plan

### Phase 0: Foundation (Week 1)

**Goal:** Set up project infrastructure and validate PPTB integration

**Tasks:**
- [ ] Initialize Vite + React + TypeScript project
- [ ] Configure PPTB compatibility (vite.config.ts with IIFE plugin)
- [ ] Add dependencies (Ant Design, @tanstack/react-table, rc-tree, etc.)
- [ ] Create project structure (folders, base files)
- [ ] Implement basic App.tsx with PPTB API connection test
- [ ] Verify tool loads in PowerPlatformToolBox
- [ ] Set up Vitest for unit testing
- [ ] Create `DataverseAdapter.ts` wrapping `window.dataverseAPI`
- [ ] Test fetching entity metadata

**Deliverable:** Running skeleton app in PPTB showing connection status

---

### Phase 1: Core Logic Port (Week 2-3)

**Goal:** Port platform-agnostic business logic to TypeScript

**Tasks:**
- [ ] Port `DataModels.cs` → `DataModels.ts` (interfaces/types)
- [ ] Port `IDataverseConnection.cs` → `IDataverseConnection.ts`
- [ ] Port `FetchXmlToSqlConverter.cs` → `FetchXmlToSqlConverter.ts`
  - [ ] Write unit tests for each operator type
  - [ ] Validate against XTB version (same input → same output)
- [ ] Port `SemanticModelBuilder.cs` → `SemanticModelBuilder.ts`
  - [ ] Break into smaller modules:
    - `TmdlGenerator.ts` - String generation utilities
    - `RelationshipDetector.ts` - Lookup relationship logic
    - `ChangeAnalyzer.ts` - Change detection
  - [ ] Write comprehensive unit tests
  - [ ] Side-by-side validation: Generate TMDL from test config in both versions
- [ ] Port `SemanticModelManager.cs` → Configuration persistence logic
- [ ] Implement `FileSystemAdapter.ts` for PBIP output

**Deliverable:** Core logic fully ported and tested, ready for UI integration

**Validation:**
```typescript
// Test case: Generate TMDL for same configuration
const testConfig: SemanticModelConfig = { /* ... */ };
const tmdlCSharp = generateWithXTB(testConfig);
const tmdlTypeScript = semanticModelBuilder.generate(testConfig);
assert.equal(tmdlCSharp, tmdlTypeScript); // Should match exactly
```

---

### Phase 2: Basic UI & Navigation (Week 4)

**Goal:** Implement app shell and navigation

**Tasks:**
- [ ] Create `AppLayout.tsx` with sidebar and main panel
- [ ] Implement `ModelSelector.tsx` (configuration management)
  - New model, load model, save model
- [ ] Create navigation structure (tabs or stepper)
- [ ] Implement `ConnectionStatus.tsx` (port from React sample)
- [ ] Set up React Context for global state
  - `AppContext` - connection, current model
  - `ModelConfigContext` - table selections, relationships, etc.
- [ ] Implement `StorageAdapter.ts` for config persistence
- [ ] Add notifications for user feedback

**Deliverable:** App shell with working model configuration management

---

### Phase 3: Table Selection (Week 4-5)

**Goal:** Implement solution and table selection UI

**Tasks:**
- [ ] Create `SolutionSelector.tsx`
  - Fetch solutions from Dataverse
  - Display in dropdown or list
  - Filter by solution
- [ ] Create `TableList.tsx`
  - Display tables from selected solution
  - Multi-select with checkboxes
  - Search/filter functionality
- [ ] Implement `useDataverse.ts` hook
  - `useSolutions()` - fetch and cache solutions
  - `useTables(solutionId)` - fetch tables for solution
  - `useTableMetadata(tableName)` - fetch attributes, relationships
- [ ] Connect to ModelConfigContext to save selections

**Deliverable:** Working table selection matching XTB functionality

---

### Phase 4: Star-Schema Wizard (Week 5-6)

**Goal:** Implement fact/dimension selection and relationship management

**Tasks:**
- [ ] Create `FactTablePicker.tsx`
  - Radio button selection from selected tables
- [ ] Create `DimensionSelector.tsx`
  - Tree view of relationships from fact table
  - Checkbox selection of dimensions
  - Grouping by target table (handle multiple relationships)
  - Active/Inactive toggle for relationships
  - Search and filter functionality
- [ ] Implement relationship detection logic
  - Parse `ManyToOneRelationships` from metadata
  - Build relationship graph
- [ ] Create `RelationshipTree.tsx` component
  - Custom tree rendering with `rc-tree`
  - Grouped relationship display
  - Active/Inactive visual indicators
- [ ] Implement "Solution tables only" filter
- [ ] Add snowflake dimension support (dimension → parent dimension)

**Deliverable:** Full star-schema wizard matching XTB feature set

**UI Example:**
```
Fact Table: salesorder
Dimensions:
  ├─ 📊 account (3 relationships)
  │   ├─ ✓ customerid (Active)
  │   ├─ ☐ partnerid (Inactive)
  │   └─ ☐ originatingleadid (Inactive)
  ├─ ✓ contact.primarycontactid (Active)
  └─ ✓ product.productid (Active)
```

---

### Phase 5: Attribute & Form/View Selection (Week 6-7)

**Goal:** Implement detailed attribute configuration for each table

**Tasks:**
- [ ] Create `AttributeSelector.tsx`
  - Table-by-table attribute selection
  - Display as table with sortable columns (use `@tanstack/react-table`)
  - Select All / Deselect All
  - Form-based preset (load attributes from selected form)
- [ ] Create `FormPicker.tsx` modal
  - Fetch forms for table
  - Display form names
  - Parse FormXML to extract field list
- [ ] Create `ViewPicker.tsx` modal
  - Fetch views for table
  - Display view names
  - Extract FetchXML for filtering
- [ ] Implement attribute display name override
  - Double-click to edit display name
  - Conflict detection (highlight duplicates in red)
  - Show asterisk (*) for overridden names
- [ ] Add storage mode per-table dropdown

**Deliverable:** Complete attribute configuration UI

**UI Example:**
```
Table: account
Form: [Main Form (v)] | View: [Active Accounts (v)] | Storage: [DirectQuery (v)]

| Select | Logical Name       | Display Name      | Type      |
|--------|--------------------|-------------------|-----------|
| ✓      | accountid          | Account ID        | Guid      |
| ✓      | name               | Account Name*     | String    | <- Override
| ✓      | revenue            | Annual Revenue    | Money     |
| ☐      | description        | Description       | Memo      |
```

---

### Phase 6: Calendar Table & Advanced Features (Week 7)

**Goal:** Implement date dimension and advanced configuration

**Tasks:**
- [ ] Create `CalendarTableDialog.tsx`
  - Date range selection (start year, end year)
  - Fiscal calendar configuration
  - Timezone offset selection
  - Preview generated date table
- [ ] Implement date table generation logic
  - Generate date range
  - Fiscal year calculations
  - Holiday support (optional)
- [ ] Add connection mode selection (TDS vs FabricLink)
- [ ] Implement FabricLink-specific settings
  - SQL endpoint URL
  - Database name
  - Choice label table configuration
- [ ] Add global storage mode selector

**Deliverable:** Calendar table generation and advanced settings

---

### Phase 7: TMDL Preview & Generation (Week 8)

**Goal:** Implement TMDL preview and PBIP file generation

**Tasks:**
- [ ] Create `TmdlPreview.tsx`
  - Tree view of all TMDL files
  - Syntax-highlighted code preview (use `react-syntax-highlighter`)
  - Copy individual table TMDL
  - Save all TMDL files to folder
- [ ] Implement TMDL generation pipeline
  - Call `SemanticModelBuilder.generate(config)`
  - Display each table TMDL
  - Show relationships.tmdl, expressions.tmdl, model.tmdl
- [ ] Create `FileSystemAdapter.ts`
  - Option A: Multiple `saveFile()` calls for each TMDL file
  - Option B: Create ZIP archive with jszip
- [ ] Implement PBIP folder structure generation
  - `.pbip` file
  - `.SemanticModel/` folder
  - `.Report/` folder
  - `.platform` file

**Deliverable:** Full TMDL preview and PBIP generation

---

### Phase 8: Change Detection & Preview (Week 8-9)

**Goal:** Implement incremental update with change analysis

**Tasks:**
- [ ] Create `ChangePreview.tsx`
  - Tree view of changes (Added, Modified, Removed)
  - Color-coded impact levels (Safe, Additive, Moderate, Destructive)
  - Expandable nodes showing details
- [ ] Implement `ChangeAnalyzer.ts`
  - Parse existing TMDL files from disk
  - Deep comparison with new configuration
  - Classify changes by impact
  - Detect user measure preservation needs
- [ ] Add "Preview Changes" workflow
  - User clicks "Update Model"
  - Show change preview dialog
  - User confirms or cancels
  - Apply changes with user code preservation

**Deliverable:** Change detection matching XTB functionality

**UI Example:**
```
Changes Preview:
  ├─ ✅ Tables (2 added, 1 modified)
  │   ├─ + campaign (Safe - New table)
  │   ├─ + lead (Safe - New table)
  │   └─ ⚠ account (Moderate - 3 columns added, 1 removed)
  ├─ ✅ Relationships (1 added)
  │   └─ + salesorder.customerid → account.accountid (Safe)
  └─ ✅ Measures (Preserved)
      └─ ℹ 5 user measures will be preserved
```

---

### Phase 9: Testing & Polish (Week 9-10)

**Goal:** Comprehensive testing and UX refinement

**Tasks:**
- [ ] Write integration tests for end-to-end workflows
- [ ] Test on all platforms (Windows, macOS, Linux)
- [ ] Performance testing with large metadata sets (100+ tables)
- [ ] Error handling improvements
  - Network errors
  - Invalid configurations
  - PPTB API failures
- [ ] UX polish
  - Loading states for all async operations
  - Better error messages
  - Keyboard shortcuts
  - Accessibility (ARIA labels, keyboard navigation)
- [ ] Documentation
  - In-app help tooltips
  - README with screenshots
  - Migration guide from XTB to PPTB
- [ ] Beta testing with 3-5 users

**Deliverable:** Production-ready v2.0.0

---

### Phase 10: Release & Documentation (Week 10)

**Goal:** Publish tool and create comprehensive documentation

**Tasks:**
- [ ] Publish to npm (if distributing via npm)
- [ ] Create release notes
- [ ] Record demo video
- [ ] Write migration guide for XTB users
- [ ] Update GitHub repository
  - Add PPTB version to README
  - Tag v2.0.0 release
- [ ] Announce release
  - Blog post
  - Twitter/LinkedIn
  - PowerPlatform community forums

**Deliverable:** Public v2.0.0 release

---

## 6. Risk Assessment & Mitigation

### High-Risk Items

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **TMDL generation logic differs subtly** | Medium | High | Rigorous unit tests, side-by-side validation with XTB version |
| **PPTB file API insufficient for multi-file output** | Medium | Medium | Have ZIP fallback ready (jszip library) |
| **Complex UI components (tree, table) have poor performance** | Low | Medium | Use virtualization (react-window) for large lists, lazy loading |
| **FetchXML parsing differences** | Low | Low | Comprehensive test suite with real-world FetchXML examples |

### Medium-Risk Items

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Change detection misses edge cases** | Medium | Medium | Extensive testing with various existing models, incremental updates |
| **Display name conflict detection has bugs** | Low | Low | Unit tests for conflict detection logic, validation before build |
| **Relationship detection incomplete** | Low | Medium | Test with various relationship types (1:N, N:1, self-referencing) |
| **Timezone conversion errors** | Low | Low | Use well-tested libraries (e.g., date-fns), unit tests |

### Low-Risk Items

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **UI rendering differences across platforms** | Low | Low | Test on Windows, macOS, Linux; use CSS normalization |
| **Configuration serialization issues** | Very Low | Low | JSON is standard, same format as XTB |

---

## 7. Success Criteria

### Functional Parity

- [ ] All 12 key features from XTB version work in PPTB
- [ ] Generated TMDL is byte-for-byte identical for same configuration
- [ ] Incremental updates preserve user code correctly
- [ ] Change detection matches XTB accuracy

### Performance

- [ ] Initial load < 2 seconds
- [ ] Table selection for 100+ tables < 3 seconds
- [ ] TMDL generation for 50 tables < 5 seconds
- [ ] UI remains responsive during all operations

### Quality

- [ ] 80%+ test coverage for core logic
- [ ] Zero critical bugs in beta testing
- [ ] All supported browsers work (Chrome, Edge, Safari, Firefox)
- [ ] Accessible (WCAG 2.1 AA)

### User Adoption

- [ ] 50+ PPTB installs in first month
- [ ] Positive user feedback (>4.0 rating)
- [ ] Migration guide helps XTB users switch smoothly

---

## Appendices

### Appendix A: Technology Evaluation

#### Why React over Vue/Svelte?

| Framework | Pros | Cons | Score |
|-----------|------|------|-------|
| **React** | Largest ecosystem, best PPTB samples, Ant Design/Material-UI, team familiarity | Verbose (JSX), boilerplate | 9/10 |
| **Vue** | Simpler syntax, good reactivity, Vuetify components | Smaller ecosystem, less PPTB samples | 7/10 |
| **Svelte** | Smallest bundle, fast, elegant syntax | Smallest ecosystem, fewest PPTB samples, limited component libraries | 6/10 |
| **Plain HTML/TS** | No framework overhead, simple | Manual state management, no component ecosystem | 5/10 |

**Decision:** React - Best balance of ecosystem, tooling, and PPTB compatibility.

#### UI Component Library

| Library | Pros | Cons | Score |
|---------|------|------|-------|
| **Ant Design** | Comprehensive (TreeSelect, Table, Modal, Form), enterprise-grade, good docs | Large bundle size (~500KB) | 9/10 |
| **Material-UI** | Popular, well-maintained, modern design | Similar bundle size, less suited for data-heavy UIs | 8/10 |
| **Chakra UI** | Modern, accessible, smaller bundle | Missing some advanced components (TreeSelect) | 7/10 |
| **Custom** | Full control, minimal bundle | High development effort, reinvent wheel | 4/10 |

**Decision:** Ant Design - Best for data-heavy enterprise tools, has TreeSelect and Table.

### Appendix B: C# to TypeScript Conversion Guide

| C# Pattern | TypeScript Equivalent |
|------------|----------------------|
| `class SemanticModelBuilder` | `export class SemanticModelBuilder` |
| `private readonly string _field;` | `private readonly field: string;` |
| `public async Task<List<T>>` | `public async methodName(): Promise<T[]>` |
| `Dictionary<string, T>` | `Map<string, T>` or `{ [key: string]: T }` |
| `List<T>` | `T[]` |
| `StringBuilder` | `string` with `+=` or array with `join()` |
| `string.Format("Hello {0}", name)` | Template literal: `` `Hello ${name}` `` |
| `Path.Combine(a, b)` | `path.join(a, b)` (Node.js path module) |
| `File.WriteAllText(path, content, encoding)` | `fs.writeFileSync(path, content, 'utf8')` |
| `Regex.Replace(input, pattern, replacement)` | `input.replace(new RegExp(pattern), replacement)` |
| `XDocument.Parse(xml)` | `new DOMParser().parseFromString(xml, 'text/xml')` |
| `JsonConvert.SerializeObject(obj)` | `JSON.stringify(obj)` |

### Appendix C: PPTB API Reference Summary

Based on React sample analysis:

**Dataverse API**
```typescript
window.dataverseAPI.getAllEntitiesMetadata(): Promise<{ value: EntityMetadata[] }>
window.dataverseAPI.getEntityMetadata(entityName: string): Promise<EntityMetadata>
window.dataverseAPI.fetchXmlQuery(fetchXml: string): Promise<{ value: any[] }>
window.dataverseAPI.create(entityName: string, record: any): Promise<string>
window.dataverseAPI.update(entityName: string, id: string, record: any): Promise<void>
window.dataverseAPI.delete(entityName: string, id: string): Promise<void>
```

**ToolBox API**
```typescript
window.toolboxAPI.connections.getActiveConnection(): Promise<DataverseConnection>
window.toolboxAPI.utils.showNotification(options: NotificationOptions): Promise<void>
window.toolboxAPI.utils.copyToClipboard(text: string): Promise<void>
window.toolboxAPI.utils.saveFile(options: SaveFileOptions): Promise<string>
window.toolboxAPI.utils.getTheme(): Promise<'light' | 'dark'>
window.toolboxAPI.events.on(handler: (event, payload) => void): void
```

### Appendix D: Estimated Lines of Code

| Component | Current (C#) | Estimated (TS) | Ratio |
|-----------|-------------|----------------|-------|
| **Core Logic** | ~5,000 | ~4,500 | 0.9x |
| **UI Components** | ~5,000 (WinForms) | ~3,500 (React) | 0.7x |
| **Adapters** | ~1,000 | ~800 | 0.8x |
| **Tests** | 0 | ~2,000 | New |
| **Total** | ~11,000 | ~10,800 | 0.98x |

TypeScript version is slightly smaller due to:
- More concise syntax (template literals, destructuring)
- Less boilerplate (no async wrappers for sync code)
- React components more declarative than WinForms

### Appendix E: Sample Code Snippets

#### SemanticModelBuilder.ts (Excerpt)

```typescript
export class SemanticModelBuilder {
  constructor(
    private readonly connection: IDataverseConnection,
    private readonly config: SemanticModelConfig,
    private readonly fileAdapter: FileSystemAdapter,
    private readonly logger: Logger
  ) {}

  async generatePBIPProject(): Promise<void> {
    this.logger.info('Starting PBIP generation...');
    
    // Generate all TMDL content
    const tables = await this.generateTableTMDL();
    const relationships = this.generateRelationshipsTMDL();
    const model = this.generateModelTMDL(tables);
    const expressions = this.config.connectionMode === 'FabricLink' 
      ? this.generateExpressionsTMDL() 
      : null;

    // Write to file system
    await this.fileAdapter.writeSemanticModel({
      modelName: this.config.modelName,
      tables,
      relationships,
      model,
      expressions,
    });

    this.logger.info('PBIP generation complete!');
  }

  private generateTableTMDL(): Promise<TmdlTable[]> {
    return Promise.all(
      this.config.selectedTables.map(async (table) => {
        const attributes = await this.getAttributesForTable(table);
        const partition = this.generatePartition(table, attributes);
        return {
          name: table.displayName,
          tmdl: this.renderTableTMDL(table, attributes, partition),
        };
      })
    );
  }

  private generatePartition(table: TableConfig, attributes: AttributeMetadata[]): string {
    const columns = attributes.map(attr => attr.logicalName).join(', ');
    const tableName = table.logicalName;
    const whereClause = table.viewFilter 
      ? this.fetchXmlConverter.convert(table.viewFilter) 
      : '';

    if (this.config.connectionMode === 'DataverseTDS') {
      return `
        SELECT ${columns}
        FROM ${tableName}
        ${whereClause ? `WHERE ${whereClause}` : ''}
      `.trim();
    } else {
      // FabricLink mode - different SQL structure
      return this.generateFabricLinkPartition(tableName, columns, whereClause);
    }
  }
}
```

#### DataverseAdapter.ts

```typescript
export class DataverseAdapter implements IDataverseConnection {
  private connection: ToolBoxAPI.DataverseConnection | null = null;

  async authenticate(clearCredentials: boolean = false): Promise<string> {
    // PPTB handles authentication, we just get the connection
    this.connection = await window.toolboxAPI.connections.getActiveConnection();
    return this.connection?.url ?? '';
  }

  async getSolutionsAsync(): Promise<DataverseSolution[]> {
    const fetchXml = `
      <fetch>
        <entity name="solution">
          <attribute name="solutionid" />
          <attribute name="uniquename" />
          <attribute name="friendlyname" />
          <attribute name="version" />
          <filter>
            <condition attribute="isvisible" operator="eq" value="true" />
          </filter>
          <order attribute="friendlyname" />
        </entity>
      </fetch>
    `;

    const result = await window.dataverseAPI.fetchXmlQuery(fetchXml);
    return result.value.map(solution => ({
      id: solution.solutionid,
      uniqueName: solution.uniquename,
      friendlyName: solution.friendlyname,
      version: solution.version,
    }));
  }

  async getTableMetadataAsync(logicalName: string): Promise<TableMetadata> {
    const metadata = await window.dataverseAPI.getEntityMetadata(logicalName);
    return {
      logicalName: metadata.LogicalName,
      displayName: metadata.DisplayName?.UserLocalizedLabel?.Label ?? logicalName,
      primaryIdAttribute: metadata.PrimaryIdAttribute,
      primaryNameAttribute: metadata.PrimaryNameAttribute,
    };
  }

  // ... other methods
}
```

### Appendix F: Testing Strategy

#### Unit Tests (Core Logic)

```typescript
// tests/core/SemanticModelBuilder.test.ts
import { describe, it, expect } from 'vitest';
import { SemanticModelBuilder } from '@core/services/SemanticModelBuilder';
import { MockDataverseAdapter } from '../mocks/MockDataverseAdapter';

describe('SemanticModelBuilder', () => {
  it('generates correct TMDL for simple table', async () => {
    const adapter = new MockDataverseAdapter();
    const config = {
      modelName: 'TestModel',
      selectedTables: [{ logicalName: 'account', displayName: 'Account' }],
      connectionMode: 'DataverseTDS',
    };
    const builder = new SemanticModelBuilder(adapter, config);

    const tmdl = await builder.generateTableTMDL('account');

    expect(tmdl).toContain('table Account');
    expect(tmdl).toContain('partition');
    expect(tmdl).toContain('SELECT');
  });

  it('handles view filters correctly', async () => {
    const adapter = new MockDataverseAdapter();
    const config = {
      selectedTables: [{
        logicalName: 'account',
        viewFilter: '<filter><condition attribute="statecode" operator="eq" value="0" /></filter>',
      }],
    };
    const builder = new SemanticModelBuilder(adapter, config);

    const partition = builder.generatePartition(config.selectedTables[0]);

    expect(partition).toContain('WHERE');
    expect(partition).toContain('statecode');
  });
});
```

#### Integration Tests (Component + Adapter)

```typescript
// tests/components/TableSelector.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { TableSelector } from '@components/tables/TableSelector';
import { MockDataverseAdapter } from '../mocks/MockDataverseAdapter';

describe('TableSelector', () => {
  it('loads and displays tables from solution', async () => {
    const adapter = new MockDataverseAdapter();
    adapter.mockTables = [
      { logicalName: 'account', displayName: 'Account' },
      { logicalName: 'contact', displayName: 'Contact' },
    ];

    render(<TableSelector adapter={adapter} />);

    await waitFor(() => {
      expect(screen.getByText('Account')).toBeInTheDocument();
      expect(screen.getByText('Contact')).toBeInTheDocument();
    });
  });
});
```

---

## Conclusion

This plan provides a comprehensive roadmap for porting the Dataverse to Power BI Semantic Model Generator to PowerPlatformToolBox. The phased approach balances risk, maintains the current architecture philosophy of separating core logic from UI/platform code, and ensures feature parity with the XrmToolBox version.

**Key Success Factors:**
1. **Rigorous testing** of core TMDL generation logic (side-by-side validation)
2. **React component library** choice (Ant Design recommended)
3. **Incremental validation** at each phase
4. **User feedback** during beta testing

**Timeline Summary:**
- **Phase 0:** 1 week (Foundation)
- **Phases 1-2:** 2 weeks (Core logic + basic UI)
- **Phases 3-6:** 4 weeks (Full feature implementation)
- **Phases 7-8:** 2 weeks (Preview, generation, change detection)
- **Phases 9-10:** 2 weeks (Testing, polish, release)
- **Total:** 10-11 weeks with buffer

**Next Steps:**
1. Review and approve this plan
2. Set up development environment
3. Begin Phase 0 (Foundation)
4. Schedule weekly progress reviews

---

**Document Version:** 1.0  
**Last Updated:** February 15, 2026  
**Author:** GitHub Copilot CLI  
**Status:** Draft - Awaiting Approval
