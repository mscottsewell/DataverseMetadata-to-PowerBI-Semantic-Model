# PPTB Implementation Quick Start Guide

This guide helps you get started with implementing the PowerPlatformToolBox port.

## Prerequisites

### Required Software

1. **PowerPlatformToolBox Desktop App**
   - Download from: https://powerplatformtoolbox.com
   - Install and verify it runs
   - Create a test Dataverse connection

2. **Node.js 18+**
   ```bash
   node --version  # Should be 18.0.0 or higher
   npm --version
   ```

3. **Development Tools**
   - VS Code (recommended) or your preferred editor
   - Git

### Recommended VS Code Extensions

- ESLint
- Prettier
- TypeScript and JavaScript Language Features
- ES7+ React/Redux/React-Native snippets

## Phase 0: Project Initialization (Day 1)

### Step 1: Create PPTB Tool Project

```bash
# Option A: Use PPTB generator (if available)
npx yo pptb

# Option B: Manual setup (based on React sample)
mkdir dataverse-to-powerbi-pptb
cd dataverse-to-powerbi-pptb
npm init -y
```

### Step 2: Install Dependencies

```bash
# Core dependencies
npm install react@^18.3.1 react-dom@^18.3.1

# UI component library (choose one)
npm install antd  # Recommended: Ant Design
# OR
npm install @mui/material @emotion/react @emotion/styled

# Table/Tree components
npm install @tanstack/react-table rc-tree

# Dev dependencies
npm install --save-dev \
  @pptb/types@^1.0.17 \
  @types/react@^18.3.12 \
  @types/react-dom@^18.3.1 \
  @vitejs/plugin-react@^4.3.4 \
  typescript@^5.6.3 \
  vite@^7.1.11 \
  vitest@latest \
  @testing-library/react@latest \
  @testing-library/user-event@latest
```

### Step 3: Create Project Structure

```bash
mkdir -p src/{components,hooks,adapters,core,types,utils}
mkdir -p src/components/{wizard,dialogs,common}
mkdir -p src/core/{tmdl,converters,models}
touch src/App.tsx
touch src/main.tsx
touch index.html
touch vite.config.ts
touch tsconfig.json
```

**Directory Structure:**
```
dataverse-to-powerbi-pptb/
├── index.html                 # Entry point
├── package.json
├── tsconfig.json
├── vite.config.ts            # PPTB IIFE bundling config
├── vitest.config.ts
└── src/
    ├── main.tsx              # React root
    ├── App.tsx               # Main app component
    ├── types/
    │   ├── DataModels.ts     # Port from DataverseToPowerBI.Core
    │   └── pptb.d.ts         # PPTB API type augmentations
    ├── adapters/
    │   ├── DataverseAdapter.ts      # window.dataverseAPI wrapper
    │   └── FileSystemAdapter.ts     # window.toolboxAPI.utils wrapper
    ├── core/
    │   ├── models/
    │   │   └── SemanticModelConfig.ts
    │   ├── tmdl/
    │   │   ├── SemanticModelBuilder.ts    # Port from XTB
    │   │   ├── TmdlGenerator.ts
    │   │   └── ChangeAnalyzer.ts
    │   └── converters/
    │       └── FetchXmlToSqlConverter.ts  # Port from XTB
    ├── hooks/
    │   ├── useDataverse.ts
    │   ├── useModelConfig.ts
    │   └── useNotifications.ts
    ├── components/
    │   ├── common/
    │   │   ├── ConnectionStatus.tsx
    │   │   └── Notification.tsx
    │   ├── wizard/
    │   │   ├── ModelSelector.tsx
    │   │   ├── TableSelector.tsx
    │   │   ├── StarSchemaWizard.tsx
    │   │   ├── AttributeSelector.tsx
    │   │   └── RelationshipManager.tsx
    │   └── dialogs/
    │       ├── CalendarTableDialog.tsx
    │       ├── TmdlPreviewDialog.tsx
    │       └── ChangePreviewDialog.tsx
    └── utils/
        ├── logger.ts
        └── validation.ts
```

### Step 4: Configure Vite for PPTB

**vite.config.ts:**
```typescript
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

// Custom plugin to remove module attributes for PPTB compatibility
function pptbCompatibility() {
  return {
    name: 'pptb-compatibility',
    transformIndexHtml(html: string) {
      return html
        .replace(/type="module"/g, '')
        .replace(/crossorigin/g, '')
        .replace(/<script/g, '<script defer');
    },
  };
}

export default defineConfig({
  plugins: [react(), pptbCompatibility()],
  build: {
    outDir: 'dist',
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
      },
      output: {
        format: 'iife',
        entryFileNames: 'assets/[name].js',
        chunkFileNames: 'assets/[name].js',
        assetFileNames: 'assets/[name].[ext]',
      },
    },
    minify: 'terser',
    sourcemap: false,
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
    },
  },
});
```

**tsconfig.json:**
```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "baseUrl": ".",
    "paths": {
      "@/*": ["./src/*"]
    }
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

**package.json (key fields):**
```json
{
  "name": "dataverse-to-powerbi-pptb",
  "version": "2.0.0",
  "displayName": "Dataverse to Power BI Semantic Model",
  "description": "Generate optimized Power BI semantic models from Dataverse metadata",
  "main": "index.html",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview",
    "test": "vitest",
    "test:ui": "vitest --ui",
    "lint": "eslint . --ext ts,tsx",
    "format": "prettier --write \"src/**/*.{ts,tsx}\""
  }
}
```

### Step 5: Create Minimal App Shell

**index.html:**
```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Dataverse to Power BI</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

**src/main.tsx:**
```typescript
import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './index.css'; // If using

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

**src/App.tsx:**
```typescript
import React, { useState, useEffect } from 'react';

function App() {
  const [connection, setConnection] = useState<any>(null);
  const [apiAvailable, setApiAvailable] = useState(false);

  useEffect(() => {
    // Check if PPTB APIs are available
    if (typeof window !== 'undefined' && 'toolboxAPI' in window && 'dataverseAPI' in window) {
      setApiAvailable(true);

      // Subscribe to connection changes
      window.toolboxAPI.connection.onConnectionChange((conn) => {
        setConnection(conn);
      });

      // Get current connection
      window.toolboxAPI.connection.getCurrentConnection().then(setConnection);
    }
  }, []);

  if (!apiAvailable) {
    return (
      <div style={{ padding: '20px' }}>
        <h2>⚠️ PPTB APIs Not Available</h2>
        <p>This tool must be loaded within PowerPlatformToolBox.</p>
      </div>
    );
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Dataverse to Power BI Semantic Model Generator</h1>
      
      <h2>Connection Status</h2>
      {connection ? (
        <div style={{ background: '#e6f7e6', padding: '10px', borderRadius: '4px' }}>
          ✅ Connected to: <strong>{connection.organizationFriendlyName}</strong>
          <br />
          Environment: {connection.url}
        </div>
      ) : (
        <div style={{ background: '#ffe6e6', padding: '10px', borderRadius: '4px' }}>
          ❌ No connection. Please create a connection in PPTB.
        </div>
      )}

      <h2>Next Steps</h2>
      <ol>
        <li>Create a Dataverse connection in PPTB</li>
        <li>Implement DataverseAdapter</li>
        <li>Build Model Selector UI</li>
        <li>Port SemanticModelBuilder</li>
      </ol>
    </div>
  );
}

export default App;
```

### Step 6: Build and Test

```bash
# Build the tool
npm run build

# The dist/ folder should contain:
# - index.html
# - assets/main.js (IIFE bundle)
# - assets/main.css (if using)
```

### Step 7: Install in PPTB

1. Open PowerPlatformToolBox
2. Go to **Tools** section
3. Click **"Install Tool"** or **"Load Local Tool"**
4. Browse to the `dist/` folder (or project root if PPTB auto-detects)
5. The tool should appear in the tool list
6. Click to launch and verify connection status appears

## Phase 1: Core Logic Port (Week 2-3)

### Step 1: Port Data Models

**src/types/DataModels.ts:**
```typescript
// Port from DataverseToPowerBI.Core/Models/DataModels.cs

export interface SemanticModelConfig {
  modelName: string;
  workingFolder: string;
  connectionMode: 'DataverseTDS' | 'FabricLink';
  defaultStorageMode: 'DirectQuery' | 'Import' | 'Dual';
  tables: TableConfig[];
  relationships: RelationshipConfig[];
  // ... other properties
}

export interface TableConfig {
  logicalName: string;
  displayName: string;
  schemaType: 'Fact' | 'Dimension' | 'Other';
  storageMode?: 'DirectQuery' | 'Import' | 'Dual';
  attributes: AttributeConfig[];
  viewFilter?: ViewFilterConfig;
}

export interface AttributeConfig {
  logicalName: string;
  displayName: string;
  dataType: string;
  isSelected: boolean;
  displayNameOverride?: string;
}

// ... continue porting all interfaces
```

### Step 2: Create Dataverse Adapter

**src/adapters/DataverseAdapter.ts:**
```typescript
import type { IDataverseConnection } from '@/core/interfaces/IDataverseConnection';

export class DataverseAdapter implements IDataverseConnection {
  private get api() {
    if (!window.dataverseAPI) {
      throw new Error('window.dataverseAPI is not available');
    }
    return window.dataverseAPI;
  }

  async executeFetchXml(fetchXml: string): Promise<any> {
    const result = await this.api.executeFetchXml(fetchXml);
    return result;
  }

  async getEntityMetadata(entityLogicalName: string): Promise<any> {
    // Use PPTB API to fetch entity metadata
    const result = await this.api.retrieveEntityMetadata(entityLogicalName);
    return result;
  }

  async retrieveAllEntities(): Promise<any[]> {
    // Fetch all entity metadata
    const result = await this.api.retrieveAllEntityMetadata();
    return result;
  }

  // ... implement other IDataverseConnection methods
}
```

### Step 3: Port FetchXmlToSqlConverter

**src/core/converters/FetchXmlToSqlConverter.ts:**
```typescript
// Port from DataverseToPowerBI.XrmToolBox/Services/FetchXmlToSqlConverter.cs

export class FetchXmlToSqlConverter {
  constructor(private mode: 'TDS' | 'FabricLink') {}

  convert(fetchXml: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(fetchXml, 'text/xml');
    
    // Port the conversion logic
    const filterNode = doc.querySelector('filter');
    if (!filterNode) return '';

    return this.convertFilter(filterNode);
  }

  private convertFilter(filterNode: Element): string {
    // Port C# logic to TypeScript
    // Use Element.children, getAttribute(), textContent, etc.
    // ...
  }

  // ... port all methods
}
```

### Step 4: Start Porting SemanticModelBuilder

**src/core/tmdl/SemanticModelBuilder.ts:**
```typescript
import type { SemanticModelConfig } from '@/types/DataModels';
import type { IDataverseConnection } from '../interfaces/IDataverseConnection';

export class SemanticModelBuilder {
  constructor(
    private connection: IDataverseConnection,
    private config: SemanticModelConfig
  ) {}

  async generateTmdl(): Promise<void> {
    // Port the TMDL generation logic
    // Start with generateModel() method
    const tmdlContent = this.buildModelDefinition();
    
    // ... continue porting
  }

  private buildModelDefinition(): string {
    // Port from C# StringBuilder to TypeScript template literals
    let tmdl = `model Model\n`;
    tmdl += `\tannotation PBI_ProTooling = ["DevMode"]\n`;
    // ...
    return tmdl;
  }

  // ... port methods one by one
}
```

## Validation Checklist

After completing each phase, verify:

- [ ] **Phase 0:** Tool loads in PPTB without errors
- [ ] **Phase 0:** Connection status displays correctly
- [ ] **Phase 0:** Can call basic `window.dataverseAPI` methods
- [ ] **Phase 1:** All TypeScript interfaces compile without errors
- [ ] **Phase 1:** FetchXmlToSqlConverter produces same output as C# version
- [ ] **Phase 1:** SemanticModelBuilder generates valid TMDL (test with minimal config)

## Useful Commands

```bash
# Development server with hot reload
npm run dev

# Build for PPTB
npm run build

# Run tests
npm run test

# Run tests in watch mode
npm run test -- --watch

# Type checking
npx tsc --noEmit

# Lint code
npm run lint

# Format code
npm run format
```

## Common Issues & Solutions

### Issue: "window.toolboxAPI is undefined"

**Solution:** The tool must be loaded within PPTB. During development with `npm run dev`, APIs won't be available. You can mock them:

```typescript
// src/utils/mockPptbApi.ts (dev only)
if (import.meta.env.DEV && !window.toolboxAPI) {
  window.toolboxAPI = {
    connection: {
      getCurrentConnection: async () => ({
        organizationFriendlyName: 'Dev Org',
        url: 'https://dev.crm.dynamics.com',
      }),
      onConnectionChange: (cb) => {},
    },
    // ... mock other APIs
  } as any;
}
```

### Issue: Build succeeds but tool doesn't load in PPTB

**Solution:** Check vite.config.ts has IIFE format and compatibility plugin. Verify dist/index.html doesn't have `type="module"`.

### Issue: TypeScript errors with PPTB types

**Solution:** Install `@pptb/types` and create type augmentation:

```typescript
// src/types/pptb.d.ts
import '@pptb/types';

declare global {
  interface Window {
    toolboxAPI: import('@pptb/types').ToolboxAPI;
    dataverseAPI: import('@pptb/types').DataverseAPI;
  }
}
```

## Resources

- **Full Plan:** `PPTB_PORTING_PLAN.md`
- **Summary:** `PPTB_PORTING_SUMMARY.md`
- **PPTB Samples:** https://github.com/PowerPlatformToolBox/sample-tools
- **PPTB Docs:** https://docs.powerplatformtoolbox.com/ (if accessible)
- **Current XTB Code:** `/DataverseToPowerBI.XrmToolBox/` for reference

## Next Steps

1. Complete Phase 0 setup (above)
2. Verify tool loads in PPTB
3. Begin Phase 1 (core logic port)
4. Set up unit testing for ported code
5. Establish side-by-side validation process

Good luck! 🚀
