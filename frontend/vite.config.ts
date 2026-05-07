import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/IsDB.Hospitality.API/wwwroot',
    emptyOutDir: true,
  },
  server: {
    allowedHosts: true,
    proxy: {
      '/api': 'http://localhost:5050',
    },
  },
})
