import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
    'process.env': {},
  },
  build: {
    lib: {
      entry: 'src/index.tsx',
      name: 'SupportLayerWidget',
      fileName: 'widget',
      formats: ['umd'],
    },
    rollupOptions: {
      // Bundle React into the UMD output so it's self-contained
      external: [],
    },
  },
})
