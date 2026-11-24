import { useEffect, useMemo, useRef, useState } from 'react';
import Lenis from 'lenis';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import {
  ArrowDownRight,
  Compass,
  Download,
  FileDown,
  Globe,
  Layers,
  QrCode,
  ShieldCheck,
  Smartphone,
  Sparkles,
} from 'lucide-react';
import { APK_META, ARTIFACTS, type ArtifactCategoryKey, type Lang } from './data/artifacts';
import { t } from './data/i18n';
import { ArtifactStage3D } from './components/ArtifactStage3D';
import { PhoneShowcase } from './components/PhoneShowcase';

gsap.registerPlugin(ScrollTrigger);

export function App() {
  const [lang, setLang] = useState<Lang>('id');
  const [heroArtifactId, setHeroArtifactId] = useState<string>('golok_betawi');
  const [galleryCategory, setGalleryCategory] = useState<ArtifactCategoryKey>('all');
  const [galleryArtifactId, setGalleryArtifactId] = useState<string>('keris_bali');

  const pageRef = useRef<HTMLDivElement>(null);
  const copy = t(lang);

  const heroArtifact = useMemo(
    () => ARTIFACTS.find((a) => a.id === heroArtifactId) ?? ARTIFACTS[0],
    [heroArtifactId]
  );

  const filteredArtifacts = useMemo(
    () => (galleryCategory === 'all' ? ARTIFACTS : ARTIFACTS.filter((a) => a.category === galleryCategory)),
    [galleryCategory]
  );

  const galleryArtifact = useMemo(
    () => ARTIFACTS.find((a) => a.id === galleryArtifactId) ?? filteredArtifacts[0] ?? ARTIFACTS[0],
    [galleryArtifactId, filteredArtifacts]
  );

  // Sinkronisasi Lenis smooth scroll + GSAP ScrollTrigger untuk guliran super mulus 60-120 FPS
  useEffect(() => {
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let lenis: Lenis | null = null;
    let rafCallback: ((time: number) => void) | null = null;

    if (!reduced) {
      lenis = new Lenis({
        duration: 1.1,
        easing: (x) => Math.min(1, 1.001 - Math.pow(2, -10 * x)),
        smoothWheel: true,
      });
      lenis.on('scroll', ScrollTrigger.update);
      rafCallback = (time: number) => lenis?.raf(time * 1000);
      gsap.ticker.add(rafCallback);
      gsap.ticker.lagSmoothing(0);
    }

    const ctx = gsap.context(() => {
      if (reduced) return;

      gsap.from('.hero-stagger', {
        y: 26,
        opacity: 0,
        duration: 0.85,
        stagger: 0.1,
        ease: 'power3.out',
        clearProps: 'transform,opacity',
      });

      gsap.utils.toArray<HTMLElement>('.reveal-section').forEach((sec) => {
        gsap.from(sec, {
          y: 34,
          opacity: 0,
          duration: 0.8,
          ease: 'power3.out',
          clearProps: 'transform,opacity',
          scrollTrigger: {
            trigger: sec,
            start: 'top 84%',
            once: true,
          },
        });
      });
    }, pageRef);

    return () => {
      ctx.revert();
      if (rafCallback) gsap.ticker.remove(rafCallback);
      lenis?.destroy();
    };
  }, []);

  const scrollToSection = (id: string) => {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <div ref={pageRef} className="min-h-screen bg-kawung text-[#2A2420]">
      {/* ================= FLOATING GLASS NAVBAR ================= */}
      <header className="fixed top-4 inset-x-0 z-50 px-4 sm:px-6">
        <nav className="mx-auto flex max-w-6xl items-center justify-between rounded-full px-4 sm:px-6 py-2.5 glass-light">
          <a
            href="#top"
            className="flex items-center gap-2.5 font-display text-xl sm:text-2xl font-bold tracking-tight text-[#2A2420]"
          >
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-[#1E2B3F] text-[#D4AF37] text-sm font-mono">
              N
            </span>
            <span>
              Nusantara <span className="text-[#B85A3C]">AR</span>
            </span>
          </a>

          <div className="hidden md:flex items-center gap-7 text-sm font-medium text-[#5E534C]">
            <button type="button" onClick={() => scrollToSection('koleksi')} className="hover:text-[#B85A3C] transition cursor-pointer">
              {copy.nav.collection}
            </button>
            <button type="button" onClick={() => scrollToSection('fitur')} className="hover:text-[#B85A3C] transition cursor-pointer">
              {copy.nav.features}
            </button>
            <button type="button" onClick={() => scrollToSection('mode-ar')} className="hover:text-[#B85A3C] transition cursor-pointer">
              {copy.nav.arMode}
            </button>
            <button type="button" onClick={() => scrollToSection('unduh')} className="hover:text-[#B85A3C] transition cursor-pointer">
              {copy.nav.download}
            </button>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setLang((l) => (l === 'id' ? 'en' : 'id'))}
              className="inline-flex items-center gap-1.5 rounded-full border border-[#E3D9CC] bg-[#FFFCF8]/90 px-3 py-1.5 text-xs font-mono font-medium text-[#5E534C] hover:text-[#2A2420] transition cursor-pointer"
              aria-label="Switch language"
            >
              <Globe className="h-3.5 w-3.5 text-[#B85A3C]" />
              {lang.toUpperCase()}
            </button>

            <a
              href={APK_META.downloadUrl}
              download={APK_META.fileName}
              className="inline-flex items-center gap-2 rounded-full bg-[#B85A3C] px-4 py-2 text-xs sm:text-sm font-semibold text-white shadow-md shadow-[#B85A3C]/25 hover:bg-[#8F4128] transition"
            >
              <Download className="h-3.5 w-3.5" />
              <span>{copy.nav.cta}</span>
            </a>
          </div>
        </nav>
      </header>

      {/* ================= HERO + INTERACTIVE 3D STAGE ================= */}
      <section id="top" className="relative pt-28 pb-16 sm:pt-32 sm:pb-24 px-4 sm:px-6">
        <div className="mx-auto max-w-6xl">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-8 items-center">
            {/* Kiri: Editorial Copy & CTA */}
            <div className="lg:col-span-6 space-y-6">
              <div className="hero-stagger inline-flex items-center gap-2 rounded-full border border-[#E3D9CC] bg-[#FFFCF8]/80 px-3.5 py-1.5 text-xs font-medium text-[#8F4128] backdrop-blur-md">
                <span className="h-2 w-2 rounded-full bg-[#D4AF37]" />
                <span>{copy.hero.eyebrow}</span>
              </div>

              <h1 className="hero-stagger font-display text-5xl sm:text-6xl lg:text-[66px] font-semibold leading-[1.03] tracking-tight text-[#2A2420]">
                {copy.hero.titleLine1}
                <br />
                <span className="italic text-[#B85A3C]">{copy.hero.titleLine2}</span>
              </h1>

              <p className="hero-stagger max-w-xl text-base sm:text-[17px] leading-relaxed text-[#5E534C]">
                {copy.hero.subtitle}
              </p>

              <div className="hero-stagger flex flex-wrap items-center gap-3.5 pt-1">
                <a
                  href={APK_META.downloadUrl}
                  download={APK_META.fileName}
                  className="inline-flex items-center gap-2.5 rounded-full bg-[#B85A3C] px-6 py-3.5 text-sm sm:text-base font-semibold text-white shadow-lg shadow-[#B85A3C]/30 hover:bg-[#8F4128] hover:-translate-y-0.5 transition-all"
                >
                  <Download className="h-4 w-4" />
                  <span>{copy.hero.ctaDownload}</span>
                  <span className="rounded-full bg-white/20 px-2 py-0.5 font-mono text-[11px]">
                    {APK_META.sizeLabel}
                  </span>
                </a>

                <button
                  type="button"
                  onClick={() => scrollToSection('koleksi')}
                  className="inline-flex items-center gap-2 rounded-full border border-[#D8CBB8] bg-[#FFFCF8]/85 px-5 py-3.5 text-sm sm:text-base font-semibold text-[#2A2420] hover:bg-white transition cursor-pointer"
                >
                  <span>{copy.hero.ctaExplore}</span>
                  <ArrowDownRight className="h-4 w-4 text-[#B85A3C]" />
                </button>
              </div>

              {/* Strip Metrik Ringkas */}
              <div className="hero-stagger grid grid-cols-2 sm:grid-cols-4 gap-3 pt-4">
                {copy.hero.stats.map((s) => (
                  <div key={s.label} className="rounded-2xl border border-[#E3D9CC]/85 bg-[#FFFCF8]/65 px-3.5 py-3 backdrop-blur-md">
                    <div className="font-display text-2xl sm:text-3xl font-bold text-[#2A2420]">{s.value}</div>
                    <div className="text-[11px] font-medium text-[#5E534C] leading-snug">{s.label}</div>
                  </div>
                ))}
              </div>
            </div>

            {/* Kanan: Panggung WebGL 3D Interaktif + Pilihan Cepat Artefak */}
            <div className="hero-stagger lg:col-span-6 space-y-3">
              <ArtifactStage3D artifact={heroArtifact} lang={lang} variant="light" heightClass="h-[430px] sm:h-[520px]" />

              {/* Selector cepat untuk mengganti model 3D langsung di Hero */}
              <div className="flex items-center gap-2 overflow-x-auto pb-1">
                {ARTIFACTS.map((item) => {
                  const active = item.id === heroArtifact.id;
                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => setHeroArtifactId(item.id)}
                      className={`shrink-0 inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-medium transition cursor-pointer ${
                        active
                          ? 'bg-[#1E2B3F] text-[#F4EFE8] shadow-sm'
                          : 'bg-[#FFFCF8]/80 text-[#5E534C] border border-[#E3D9CC] hover:border-[#B85A3C]/50 hover:text-[#2A2420]'
                      }`}
                    >
                      <img src={item.thumbUrl} alt="" className="h-5 w-5 rounded-full object-contain bg-[#EAE2D6]" />
                      <span>{item.name[lang]}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ================= DARK MUSEUM 3D COLLECTION STUDIO ================= */}
      <section id="koleksi" className="reveal-section bg-indigo-museum py-20 sm:py-28 px-4 sm:px-6 text-[#F4EFE8]">
        <div className="mx-auto max-w-6xl space-y-10">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6">
            <div className="space-y-3 max-w-2xl">
              <span className="inline-flex items-center gap-2 font-mono text-xs uppercase tracking-[0.18em] text-[#E28D66]">
                <Sparkles className="h-3.5 w-3.5 text-[#D4AF37]" />
                {copy.gallery.eyebrow}
              </span>
              <h2 className="font-display text-4xl sm:text-5xl font-semibold leading-tight text-[#FFFCF8]">
                {copy.gallery.title}
              </h2>
              <p className="text-sm sm:text-base text-[#EDE6DC]/75 leading-relaxed">{copy.gallery.subtitle}</p>
            </div>

            {/* Filter kategori */}
            <div className="flex flex-wrap gap-2">
              {(['all', 'weapons', 'temple', 'wildlife'] as ArtifactCategoryKey[]).map((cat) => {
                const active = galleryCategory === cat;
                return (
                  <button
                    key={cat}
                    type="button"
                    onClick={() => {
                      setGalleryCategory(cat);
                      const first = cat === 'all' ? ARTIFACTS[0] : ARTIFACTS.find((a) => a.category === cat);
                      if (first) setGalleryArtifactId(first.id);
                    }}
                    className={`rounded-full px-4 py-2 text-xs font-semibold transition cursor-pointer ${
                      active
                        ? 'bg-[#B85A3C] text-white shadow-md shadow-[#B85A3C]/30'
                        : 'bg-white/8 text-[#EDE6DC]/80 hover:bg-white/14 border border-white/10'
                    }`}
                  >
                    {copy.gallery.filters[cat]}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Grid Studio 3D + Detail Kuratorial */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            {/* Daftar kartu artefak kiri */}
            <div className="lg:col-span-4 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-1 gap-3">
              {filteredArtifacts.map((item) => {
                const active = item.id === galleryArtifact.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setGalleryArtifactId(item.id)}
                    className={`flex items-center gap-3.5 rounded-2xl p-3 text-left transition-all cursor-pointer ${
                      active
                        ? 'bg-[#FFFCF8]/14 border border-[#D4AF37]/55 shadow-lg'
                        : 'bg-white/[0.04] hover:bg-white/[0.09] border border-white/8'
                    }`}
                  >
                    <div className="h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-[#EAE2D6]/95 p-1 flex items-center justify-center">
                      <img src={item.thumbUrl} alt={item.name[lang]} loading="lazy" className="h-full w-full object-contain" />
                    </div>
                    <div className="min-w-0">
                      <div className="truncate font-semibold text-sm text-[#FFFCF8]">{item.name[lang]}</div>
                      <div className="truncate text-xs text-[#E28D66]">{item.localName}</div>
                      <div className="truncate text-[11px] text-[#EDE6DC]/60">{item.region[lang]}</div>
                    </div>
                  </button>
                );
              })}
            </div>

            {/* Panggung 3D Gelap + Info Kuratorial kanan */}
            <div className="lg:col-span-8 space-y-5">
              <ArtifactStage3D
                artifact={galleryArtifact}
                lang={lang}
                variant="dark"
                heightClass="h-[380px] sm:h-[460px]"
              />

              <div className="glass-dark rounded-3xl p-6 sm:p-7 space-y-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="font-mono text-xs text-[#D4AF37] uppercase tracking-wider">
                      {galleryArtifact.code} · {galleryArtifact.localName}
                    </div>
                    <h3 className="font-display text-3xl sm:text-4xl font-semibold text-[#FFFCF8] mt-1">
                      {galleryArtifact.name[lang]}
                    </h3>
                  </div>

                  <a
                    href={galleryArtifact.qrPdfUrl}
                    download
                    className="inline-flex items-center gap-2 rounded-full bg-[#FFFCF8]/12 hover:bg-[#B85A3C] px-4 py-2 text-xs font-semibold text-[#FFFCF8] border border-white/15 transition"
                  >
                    <FileDown className="h-3.5 w-3.5" />
                    <span>{copy.gallery.downloadQr}</span>
                  </a>
                </div>

                <p className="text-sm sm:text-[15px] leading-relaxed text-[#EDE6DC]/80">
                  {galleryArtifact.description[lang]}
                </p>

                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-1">
                  <div className="rounded-xl bg-white/[0.05] px-3.5 py-2.5 border border-white/8">
                    <div className="text-[11px] text-[#EDE6DC]/55">{copy.gallery.originLabel}</div>
                    <div className="text-xs font-semibold text-[#FFFCF8] mt-0.5">{galleryArtifact.region[lang]}</div>
                  </div>
                  <div className="rounded-xl bg-white/[0.05] px-3.5 py-2.5 border border-white/8">
                    <div className="text-[11px] text-[#EDE6DC]/55">{copy.gallery.eraLabel}</div>
                    <div className="text-xs font-semibold text-[#FFFCF8] mt-0.5">{galleryArtifact.era[lang]}</div>
                  </div>
                  <div className="rounded-xl bg-white/[0.05] px-3.5 py-2.5 border border-white/8">
                    <div className="text-[11px] text-[#EDE6DC]/55">{copy.gallery.hotspotsUnit}</div>
                    <div className="font-mono text-sm font-semibold text-[#D4AF37] mt-0.5">{galleryArtifact.hotspots}</div>
                  </div>
                  <div className="rounded-xl bg-white/[0.05] px-3.5 py-2.5 border border-white/8">
                    <div className="text-[11px] text-[#EDE6DC]/55">{copy.gallery.stagesUnit}</div>
                    <div className="font-mono text-sm font-semibold text-[#E28D66] mt-0.5">
                      {galleryArtifact.stages} {galleryArtifact.hasDraw ? '· +Hunus' : ''}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ================= FITUR APLIKASI + MOCKUP LAYAR ASLI ================= */}
      <section id="fitur" className="reveal-section py-20 sm:py-28 px-4 sm:px-6">
        <div className="mx-auto max-w-6xl space-y-12">
          <div className="max-w-2xl space-y-3">
            <span className="font-mono text-xs uppercase tracking-[0.18em] text-[#8F4128]">{copy.features.eyebrow}</span>
            <h2 className="font-display text-4xl sm:text-5xl font-semibold leading-tight text-[#2A2420]">
              {copy.features.title}
            </h2>
            <p className="text-sm sm:text-base text-[#5E534C] leading-relaxed">{copy.features.subtitle}</p>
          </div>

          <PhoneShowcase lang={lang} />
        </div>
      </section>

      {/* ================= DUAL AR MODE & KARTU QR A5 ================= */}
      <section id="mode-ar" className="reveal-section py-16 sm:py-24 px-4 sm:px-6">
        <div className="mx-auto max-w-6xl space-y-10">
          <div className="max-w-2xl space-y-3">
            <span className="font-mono text-xs uppercase tracking-[0.18em] text-[#8F4128]">{copy.ar.eyebrow}</span>
            <h2 className="font-display text-4xl sm:text-5xl font-semibold leading-tight text-[#2A2420]">{copy.ar.title}</h2>
            <p className="text-sm sm:text-base text-[#5E534C] leading-relaxed">{copy.ar.subtitle}</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="glass-light rounded-3xl p-7 space-y-4">
              <div className="flex items-center justify-between">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[#B85A3C] text-white shadow-md shadow-[#B85A3C]/25">
                  <QrCode className="h-6 w-6" />
                </div>
                <span className="rounded-full bg-[#B85A3C]/12 px-3 py-1 font-mono text-[11px] font-medium text-[#8F4128]">
                  {copy.ar.mode1Tag}
                </span>
              </div>
              <h3 className="font-display text-2xl sm:text-3xl font-semibold text-[#2A2420]">{copy.ar.mode1Title}</h3>
              <p className="text-sm sm:text-[15px] leading-relaxed text-[#5E534C]">{copy.ar.mode1Desc}</p>
            </div>

            <div className="glass-light rounded-3xl p-7 space-y-4">
              <div className="flex items-center justify-between">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[#1E2B3F] text-[#D4AF37]">
                  <Compass className="h-6 w-6" />
                </div>
                <span className="rounded-full bg-[#1E2B3F]/10 px-3 py-1 font-mono text-[11px] font-medium text-[#1E2B3F]">
                  {copy.ar.mode2Tag}
                </span>
              </div>
              <h3 className="font-display text-2xl sm:text-3xl font-semibold text-[#2A2420]">{copy.ar.mode2Title}</h3>
              <p className="text-sm sm:text-[15px] leading-relaxed text-[#5E534C]">{copy.ar.mode2Desc}</p>
            </div>
          </div>

          {/* Kartu QR A5 siap cetak */}
          <div className="glass-light rounded-3xl p-6 sm:p-8 space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-3">
              <div>
                <h3 className="font-display text-2xl sm:text-3xl font-semibold text-[#2A2420]">{copy.ar.cardsTitle}</h3>
                <p className="text-sm text-[#5E534C] mt-1">{copy.ar.cardsSub}</p>
              </div>
              <span className="font-mono text-xs text-[#8F4128]">A5 · 300 DPI · PDF</span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
              {ARTIFACTS.map((item) => (
                <a
                  key={item.id}
                  href={item.qrPdfUrl}
                  download
                  className="group flex flex-col items-center gap-2.5 rounded-2xl border border-[#E3D9CC] bg-[#FFFCF8]/90 p-3.5 text-center hover:border-[#B85A3C]/60 hover:-translate-y-0.5 transition-all"
                >
                  <img
                    src={item.qrPngUrl}
                    alt={`QR ${item.name[lang]}`}
                    loading="lazy"
                    className="aspect-[148/210] w-full rounded-lg object-cover border border-[#E8E0D5]"
                  />
                  <div className="text-xs font-semibold text-[#2A2420] group-hover:text-[#B85A3C] transition">
                    {item.name[lang]}
                  </div>
                  <span className="inline-flex items-center gap-1 font-mono text-[10px] text-[#5E534C]">
                    <FileDown className="h-3 w-3" /> PDF
                  </span>
                </a>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ================= UNDUH APK ANDROID ================= */}
      <section id="unduh" className="reveal-section py-16 sm:py-24 px-4 sm:px-6">
        <div className="mx-auto max-w-6xl">
          <div className="relative overflow-hidden rounded-[36px] bg-indigo-museum p-8 sm:p-12 lg:p-14 text-[#F4EFE8] shadow-2xl">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
              <div className="lg:col-span-7 space-y-6">
                <span className="inline-flex items-center gap-2 font-mono text-xs uppercase tracking-[0.18em] text-[#D4AF37]">
                  <ShieldCheck className="h-4 w-4" />
                  {copy.download.eyebrow}
                </span>

                <h2 className="font-display text-4xl sm:text-5xl font-semibold leading-tight text-[#FFFCF8]">
                  {copy.download.title}
                </h2>

                <p className="text-sm sm:text-base leading-relaxed text-[#EDE6DC]/80 max-w-xl">
                  {copy.download.subtitle}
                </p>

                <div className="flex flex-wrap items-center gap-4 pt-1">
                  <a
                    href={APK_META.downloadUrl}
                    download={APK_META.fileName}
                    className="inline-flex items-center gap-3 rounded-full bg-[#B85A3C] hover:bg-[#cc6645] px-7 py-4 text-sm sm:text-base font-semibold text-white shadow-xl shadow-[#B85A3C]/35 hover:-translate-y-0.5 transition-all"
                  >
                    <Download className="h-5 w-5" />
                    <span>{copy.download.btn}</span>
                  </a>
                  <span className="inline-flex items-center gap-2 font-mono text-xs text-[#EDE6DC]/70">
                    <Smartphone className="h-4 w-4 text-[#D4AF37]" />
                    {APK_META.minAndroid}
                  </span>
                </div>

                {/* Grid spesifikasi APK */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-2">
                  {[
                    [copy.download.specs.version, APK_META.version],
                    [copy.download.specs.size, APK_META.sizeLabel],
                    [copy.download.specs.os, 'API 26 – 36'],
                    [copy.download.specs.arch, 'ARM64 IL2CPP'],
                  ].map(([k, v]) => (
                    <div key={k} className="rounded-2xl bg-white/[0.06] border border-white/10 px-3.5 py-3">
                      <div className="text-[11px] text-[#EDE6DC]/55">{k}</div>
                      <div className="font-mono text-xs sm:text-sm font-semibold text-[#FFFCF8] mt-0.5">{v}</div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Panduan pemasangan 3 langkah */}
              <div className="lg:col-span-5 glass-dark rounded-3xl p-6 sm:p-7 space-y-5">
                <div className="flex items-center gap-2.5">
                  <Layers className="h-5 w-5 text-[#D4AF37]" />
                  <h3 className="font-display text-2xl font-semibold text-[#FFFCF8]">{copy.download.stepsTitle}</h3>
                </div>
                <ol className="space-y-4">
                  {copy.download.steps.map((step, i) => (
                    <li key={i} className="flex items-start gap-3.5">
                      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#B85A3C] font-mono text-xs font-bold text-white">
                        {i + 1}
                      </span>
                      <p className="text-sm leading-relaxed text-[#EDE6DC]/85">{step}</p>
                    </li>
                  ))}
                </ol>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ================= FOOTER ================= */}
      <footer className="border-t border-[#E3D9CC] py-10 px-4 sm:px-6">
        <div className="mx-auto max-w-6xl flex flex-col sm:flex-row items-center justify-between gap-4 text-center sm:text-left">
          <div>
            <div className="font-display text-2xl font-bold text-[#2A2420]">
              Nusantara <span className="text-[#B85A3C]">AR</span>
            </div>
            <p className="text-xs text-[#5E534C] mt-1">{copy.footer.tagline}</p>
          </div>
          <div className="text-xs font-mono text-[#5E534C]">{copy.footer.rights}</div>
        </div>
      </footer>
    </div>
  );
}
export default App;
