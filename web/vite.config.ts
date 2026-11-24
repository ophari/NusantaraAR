import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import fs from 'node:fs';
import path from 'node:path';

/**
 * Menyajikan Builds/Android/NusantaraAR.apk langsung pada /downloads/NusantaraAR.apk
 * saat mode dev & preview tanpa perlu menggandakan biner APK 94 MB ke dalam Git.
 */
function serveLocalApkPlugin(): Plugin {
  const apkPath = path.resolve(__dirname, '../Builds/Android/NusantaraAR.apk');
  const mount = (server: { middlewares: { use: Function } }) => {
    server.middlewares.use('/downloads/NusantaraAR.apk', (_req: unknown, res: any, next: () => void) => {
      if (!fs.existsSync(apkPath)) return next();
      const stat = fs.statSync(apkPath);
      res.setHeader('Content-Type', 'application/vnd.android.package-archive');
      res.setHeader('Content-Disposition', 'attachment; filename="NusantaraAR.apk"');
      res.setHeader('Content-Length', stat.size);
      fs.createReadStream(apkPath).pipe(res);
    });
  };
  return {
    name: 'serve-local-apk',
    configureServer: mount,
    configurePreviewServer: mount,
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), serveLocalApkPlugin()],
  build: {
    target: 'es2022',
    rollupOptions: {
      output: {
        manualChunks: {
          three: ['three'],
          motion: ['gsap', 'lenis'],
        },
      },
    },
  },
});
