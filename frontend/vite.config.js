import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development the browser talks to Vite (http://localhost:5173) and Vite forwards /api/* to the .NET API.
// Because the browser only ever sees one origin, there is no CORS problem and no untrusted-HTTPS-certificate
// problem. To point at a different API, set VITE_API_TARGET (see .env.example).
const apiTarget = process.env.VITE_API_TARGET || 'https://localhost:7001';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true, // the API's CORS policy only allows http://localhost:5173
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true, secure: false }
    }
  }
});
