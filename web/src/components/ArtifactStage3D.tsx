import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { Layers, RotateCcw, RefreshCw, ZoomIn, ZoomOut, Move3d } from 'lucide-react';
import type { ArtifactSpec, Lang } from '../data/artifacts';
import { t } from '../data/i18n';

interface ArtifactStage3DProps {
  artifact: ArtifactSpec;
  lang: Lang;
  variant?: 'light' | 'dark';
  heightClass?: string;
}

interface ExplodeTarget {
  obj: THREE.Object3D;
  base: THREE.Vector3;
  offset: THREE.Vector3;
}

function disposeHierarchy(root: THREE.Object3D) {
  root.traverse((child) => {
    const mesh = child as THREE.Mesh;
    if (!mesh.isMesh) return;
    mesh.geometry?.dispose();
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    for (const mat of mats) {
      if (!mat) continue;
      for (const value of Object.values(mat)) {
        if (value && typeof value === 'object' && 'isTexture' in value && typeof (value as THREE.Texture).dispose === 'function') {
          (value as THREE.Texture).dispose();
        }
      }
      mat.dispose();
    }
  });
}

function createContactShadow(darkMode: boolean): THREE.Mesh {
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d')!;
  const grad = ctx.createRadialGradient(size / 2, size / 2, 4, size / 2, size / 2, size / 2);
  if (darkMode) {
    grad.addColorStop(0, 'rgba(212, 175, 55, 0.22)');
    grad.addColorStop(0.45, 'rgba(184, 90, 60, 0.14)');
    grad.addColorStop(1, 'rgba(0, 0, 0, 0)');
  } else {
    grad.addColorStop(0, 'rgba(42, 36, 32, 0.22)');
    grad.addColorStop(0.5, 'rgba(184, 90, 60, 0.10)');
    grad.addColorStop(1, 'rgba(42, 36, 32, 0)');
  }
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, size, size);
  const tex = new THREE.CanvasTexture(canvas);
  const mat = new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false });
  const geo = new THREE.PlaneGeometry(1.5, 1.5);
  const mesh = new THREE.Mesh(geo, mat);
  mesh.rotation.x = -Math.PI / 2;
  mesh.position.y = -0.62;
  return mesh;
}

export function ArtifactStage3D({
  artifact,
  lang,
  variant = 'light',
  heightClass = 'h-[440px] sm:h-[520px] lg:h-[580px]',
}: ArtifactStage3DProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [loadingPct, setLoadingPct] = useState(0);
  const [isLoaded, setIsLoaded] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const [exploded, setExploded] = useState(false);
  const [autoSpin, setAutoSpin] = useState(true);

  const explodedRef = useRef(false);
  const autoSpinRef = useRef(true);
  const resetFnRef = useRef<(() => void) | null>(null);
  const zoomFnRef = useRef<((factor: number) => void) | null>(null);

  useEffect(() => {
    explodedRef.current = exploded;
  }, [exploded]);

  useEffect(() => {
    autoSpinRef.current = autoSpin;
  }, [autoSpin]);

  // Reset mode bongkar ketika artefak diganti
  useEffect(() => {
    setExploded(false);
    explodedRef.current = false;
  }, [artifact.id]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        powerPreference: 'high-performance',
      });
    } catch {
      setLoadError(true);
      return;
    }

    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = variant === 'dark' ? 1.15 : 1.08;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.domElement.style.display = 'block';
    renderer.domElement.style.width = '100%';
    renderer.domElement.style.height = '100%';
    container.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const pmrem = new THREE.PMREMGenerator(renderer);
    const envTex = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    scene.environment = envTex;

    const camera = new THREE.PerspectiveCamera(
      34,
      Math.max(1, container.clientWidth) / Math.max(1, container.clientHeight),
      0.05,
      40
    );

    const keyLight = new THREE.DirectionalLight(0xfff5e8, 1.85);
    keyLight.position.set(2.8, 3.6, 3.2);
    scene.add(keyLight);

    const rimLight = new THREE.DirectionalLight(variant === 'dark' ? 0xf2a57f : 0xcbd9ee, 1.05);
    rimLight.position.set(-3.2, 2.0, -2.6);
    scene.add(rimLight);

    const hemi = new THREE.HemisphereLight(0xfff8f0, 0x3b302a, 0.55);
    scene.add(hemi);

    const shadowDisc = createContactShadow(variant === 'dark');
    scene.add(shadowDisc);

    const pivotGroup = new THREE.Group();
    scene.add(pivotGroup);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.065;
    controls.enablePan = false;
    // Wheel scroll halaman tidak pernah terjebak kanvas 3D; zoom dilakukan via tombol +/- atau pinch
    controls.enableZoom = false;
    controls.minDistance = 1.25;
    controls.maxDistance = 4.2;
    controls.maxPolarAngle = Math.PI * 0.86;
    controls.minPolarAngle = Math.PI * 0.12;

    const initDist = artifact.cameraDistance ?? 2.3;
    const resetCamera = () => {
      camera.position.set(0, 0.14, initDist);
      controls.target.set(0, 0.02, 0);
      controls.update();
    };
    resetCamera();

    resetFnRef.current = () => {
      setExploded(false);
      explodedRef.current = false;
      pivotGroup.rotation.set(0, artifact.initialYaw ?? 0, 0);
      resetCamera();
    };

    zoomFnRef.current = (factor: number) => {
      const dir = camera.position.clone().sub(controls.target);
      const nextLen = THREE.MathUtils.clamp(dir.length() * factor, controls.minDistance, controls.maxDistance);
      dir.setLength(nextLen);
      camera.position.copy(controls.target).add(dir);
      controls.update();
    };

    let currentModel: THREE.Object3D | null = null;
    let explodeTargets: ExplodeTarget[] = [];
    let explodeProgress = 0;
    let disposed = false;
    let inView = true;

    setIsLoaded(false);
    setLoadError(false);
    setLoadingPct(5);

    const loader = new GLTFLoader();
    loader.setMeshoptDecoder(MeshoptDecoder);

    loader.load(
      artifact.modelUrl,
      (gltf) => {
        if (disposed) {
          disposeHierarchy(gltf.scene);
          return;
        }
        const root = gltf.scene;

        // Normalisasi skala agar tinggi/bentang model pas rapi di panggung 3D (~1.22 unit)
        const rawBox = new THREE.Box3().setFromObject(root);
        const size = rawBox.getSize(new THREE.Vector3());
        const center = rawBox.getCenter(new THREE.Vector3());
        const maxDim = Math.max(size.x, size.y, size.z, 0.001);
        const normScale = 1.22 / maxDim;

        root.scale.setScalar(normScale);
        root.position.set(-center.x * normScale, -center.y * normScale, -center.z * normScale);

        // Siapkan target bongkar per node modular berdasarkan nama objek Blender
        const targets: ExplodeTarget[] = [];
        const matched = new Set<THREE.Object3D>();
        root.traverse((node) => {
          if (node === root || matched.has(node)) return;
          for (const rule of artifact.explodeRules) {
            if (rule.match.some((kw) => node.name.toLowerCase().includes(kw.toLowerCase()))) {
              // Hindari mendobel induk & anak sekaligus
              let parentMatched = false;
              let p = node.parent;
              while (p) {
                if (matched.has(p)) {
                  parentMatched = true;
                  break;
                }
                p = p.parent;
              }
              if (parentMatched) break;
              matched.add(node);
              targets.push({
                obj: node,
                base: node.position.clone(),
                offset: new THREE.Vector3(
                  rule.offset[0] / normScale,
                  rule.offset[1] / normScale,
                  rule.offset[2] / normScale
                ),
              });
              break;
            }
          }
        });

        explodeTargets = targets;
        currentModel = root;
        pivotGroup.rotation.set(0, artifact.initialYaw ?? 0, 0);
        pivotGroup.add(root);

        // Tempatkan bayangan lembut tepat di bawah alas model
        const fitBox = new THREE.Box3().setFromObject(pivotGroup);
        shadowDisc.position.y = fitBox.min.y - 0.04;

        setLoadingPct(100);
        setIsLoaded(true);
      },
      (ev) => {
        if (disposed) return;
        if (ev.total > 0) {
          setLoadingPct(Math.min(96, Math.round((ev.loaded / ev.total) * 100)));
        }
      },
      () => {
        if (!disposed) setLoadError(true);
      }
    );

    const ro = new ResizeObserver(() => {
      if (disposed || !container) return;
      const w = Math.max(1, container.clientWidth);
      const h = Math.max(1, container.clientHeight);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
    });
    ro.observe(container);

    const io = new IntersectionObserver(
      (entries) => {
        inView = entries[0]?.isIntersecting ?? true;
      },
      { threshold: 0.05 }
    );
    io.observe(container);

    const clock = new THREE.Clock();
    let rafId = 0;

    const tick = () => {
      if (disposed) return;
      rafId = requestAnimationFrame(tick);
      if (!inView || document.hidden) {
        clock.getDelta();
        return;
      }
      const dt = Math.min(clock.getDelta(), 0.05);

      if (currentModel) {
        if (autoSpinRef.current) {
          pivotGroup.rotation.y += dt * 0.28;
        }
        // Lerp halus untuk animasi Bongkar 3D / Gabung
        const targetE = explodedRef.current ? 1 : 0;
        if (Math.abs(explodeProgress - targetE) > 0.0005) {
          explodeProgress = THREE.MathUtils.lerp(explodeProgress, targetE, 1 - Math.exp(-5.5 * dt));
          const ease = explodeProgress * explodeProgress * (3 - 2 * explodeProgress);
          for (const item of explodeTargets) {
            item.obj.position.copy(item.base).addScaledVector(item.offset, ease);
          }
        }
      }

      controls.update();
      renderer.render(scene, camera);
    };
    tick();

    return () => {
      disposed = true;
      cancelAnimationFrame(rafId);
      ro.disconnect();
      io.disconnect();
      controls.dispose();
      if (currentModel) {
        pivotGroup.remove(currentModel);
        disposeHierarchy(currentModel);
      }
      shadowDisc.geometry.dispose();
      (shadowDisc.material as THREE.MeshBasicMaterial).map?.dispose();
      (shadowDisc.material as THREE.MeshBasicMaterial).dispose();
      envTex.dispose();
      pmrem.dispose();
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, [artifact.id, artifact.modelUrl, artifact.initialYaw, artifact.cameraDistance, variant]);

  const copy = t(lang).hero;
  const isDark = variant === 'dark';

  return (
    <div
      className={`relative w-full ${heightClass} overflow-hidden rounded-[28px] select-none ${
        isDark
          ? 'bg-gradient-to-b from-[#233148]/80 via-[#1A2536]/90 to-[#141D2B] border border-[#EDE6DC]/12 shadow-2xl'
          : 'bg-gradient-to-b from-[#EFE8DD]/75 via-[#F7F3EC]/85 to-[#EAE1D4]/90 border border-[#E3D9CC] shadow-[0_24px_64px_-28px_rgba(42,36,32,0.22)]'
      }`}
    >
      {/* Ring dekoratif halus belakang objek */}
      <div
        className={`pointer-events-none absolute inset-0 flex items-center justify-center ${
          isDark ? 'opacity-20' : 'opacity-30'
        }`}
      >
        <div className="h-[74%] aspect-square rounded-full border border-[#D4AF37]/40" />
      </div>

      {/* Kanvas WebGL Three.js */}
      <div
        ref={containerRef}
        className="absolute inset-0 cursor-grab active:cursor-grabbing"
        aria-label={`Interactive 3D view of ${artifact.name[lang]}`}
      />

      {/* Poster & Progress saat GLB sedang diunduh */}
      {!isLoaded && !loadError && (
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-4">
          <img
            src={artifact.thumbUrl}
            alt={artifact.name[lang]}
            className="h-56 w-auto object-contain opacity-80 transition-opacity duration-300"
          />
          <div
            className={`flex items-center gap-3 rounded-full px-4 py-2 text-xs font-medium ${
              isDark ? 'bg-[#141D2B]/85 text-[#EDE6DC] border border-white/10' : 'glass-light text-[#2A2420]'
            }`}
          >
            <span className="h-2 w-2 rounded-full bg-[#D4AF37] animate-pulse" />
            <span>
              {copy.loading3d} · <span className="font-mono">{loadingPct}%</span>
            </span>
          </div>
        </div>
      )}

      {/* Fallback bila WebGL tidak tersedia */}
      {loadError && (
        <div className="absolute inset-0 flex flex-col items-center justify-center p-6 text-center">
          <img src={artifact.thumbUrl} alt={artifact.name[lang]} className="max-h-72 object-contain" />
        </div>
      )}

      {/* Badge atas kiri: petunjuk interaksi */}
      <div className="pointer-events-none absolute top-4 left-4 flex items-center gap-2">
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium ${
            isDark
              ? 'bg-[#141D2B]/75 text-[#EDE6DC]/90 border border-white/10 backdrop-blur-md'
              : 'bg-[#FFFCF8]/80 text-[#5E534C] border border-[#E3D9CC] backdrop-blur-md'
          }`}
        >
          <Move3d className="h-3.5 w-3.5 text-[#B85A3C]" />
          {copy.dragHint}
        </span>
      </div>

      {/* Badge atas kanan: nama artefak aktif */}
      <div className="pointer-events-none absolute top-4 right-4 hidden sm:block">
        <span
          className={`inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-xs font-medium ${
            isDark
              ? 'bg-[#141D2B]/75 text-[#F4EFE8] border border-white/10 backdrop-blur-md'
              : 'bg-[#FFFCF8]/85 text-[#2A2420] border border-[#E3D9CC] backdrop-blur-md'
          }`}
        >
          <span className="h-2 w-2 rounded-full bg-[#D4AF37]" />
          {artifact.name[lang]}
        </span>
      </div>

      {/* Panel kontrol 3D bawah: Bongkar 3D, Putar, Zoom, Reset */}
      <div className="absolute bottom-4 inset-x-4 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setExploded((v) => !v)}
            className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-xs font-semibold transition-all cursor-pointer ${
              exploded
                ? 'bg-[#B85A3C] text-white shadow-lg shadow-[#B85A3C]/30'
                : isDark
                  ? 'bg-[#FFFCF8]/12 text-[#F4EFE8] hover:bg-[#FFFCF8]/20 border border-white/15 backdrop-blur-md'
                  : 'bg-[#FFFCF8]/90 text-[#2A2420] hover:bg-white border border-[#E3D9CC] backdrop-blur-md'
            }`}
          >
            <Layers className="h-3.5 w-3.5" />
            {exploded ? copy.assemble : copy.explode}
          </button>

          <button
            type="button"
            onClick={() => setAutoSpin((v) => !v)}
            className={`inline-flex items-center gap-1.5 rounded-full px-3.5 py-2 text-xs font-medium transition-all cursor-pointer ${
              autoSpin
                ? isDark
                  ? 'bg-[#D4AF37]/20 text-[#F2D57E] border border-[#D4AF37]/40 backdrop-blur-md'
                  : 'bg-[#B85A3C]/12 text-[#8F4128] border border-[#B85A3C]/30 backdrop-blur-md'
                : isDark
                  ? 'bg-[#141D2B]/70 text-[#EDE6DC]/75 border border-white/10 backdrop-blur-md'
                  : 'bg-[#FFFCF8]/80 text-[#5E534C] border border-[#E3D9CC] backdrop-blur-md'
            }`}
          >
            <RefreshCw className={`h-3.5 w-3.5 ${autoSpin ? 'animate-[spin_7s_linear_infinite]' : ''}`} />
            {copy.autoSpin}
          </button>
        </div>

        <div className="flex items-center gap-1.5">
          <button
            type="button"
            onClick={() => zoomFnRef.current?.(0.84)}
            title="Zoom in"
            aria-label="Zoom in"
            className={`inline-flex h-8 w-8 items-center justify-center rounded-full transition cursor-pointer ${
              isDark
                ? 'bg-[#141D2B]/75 text-[#EDE6DC] hover:bg-[#141D2B] border border-white/10'
                : 'bg-[#FFFCF8]/85 text-[#2A2420] hover:bg-white border border-[#E3D9CC]'
            }`}
          >
            <ZoomIn className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            onClick={() => zoomFnRef.current?.(1.18)}
            title="Zoom out"
            aria-label="Zoom out"
            className={`inline-flex h-8 w-8 items-center justify-center rounded-full transition cursor-pointer ${
              isDark
                ? 'bg-[#141D2B]/75 text-[#EDE6DC] hover:bg-[#141D2B] border border-white/10'
                : 'bg-[#FFFCF8]/85 text-[#2A2420] hover:bg-white border border-[#E3D9CC]'
            }`}
          >
            <ZoomOut className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            onClick={() => resetFnRef.current?.()}
            title={copy.reset}
            aria-label={copy.reset}
            className={`inline-flex h-8 w-8 items-center justify-center rounded-full transition cursor-pointer ${
              isDark
                ? 'bg-[#141D2B]/75 text-[#EDE6DC] hover:bg-[#141D2B] border border-white/10'
                : 'bg-[#FFFCF8]/85 text-[#2A2420] hover:bg-white border border-[#E3D9CC]'
            }`}
          >
            <RotateCcw className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
}
