import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

/**
 * Custom plugin to fix HTML for PPTB compatibility
 * Removes type="module" and crossorigin attributes
 * Moves scripts to end of body with defer
 */
function fixHtmlForPPTB() {
  return {
    name: 'fix-html-for-pptb',
    transformIndexHtml(html: string) {
      return html
        .replace(/\s*type="module"/g, '')
        .replace(/\s*crossorigin/g, '')
        .replace(/<script/g, '<script defer');
    },
  };
}

export default defineConfig({
  plugins: [react(), fixHtmlForPPTB()],
  test: {
    environment: 'jsdom',
  },
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
