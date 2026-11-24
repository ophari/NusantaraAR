import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import type { Lang } from '../data/artifacts';
import { t } from '../data/i18n';

interface PhoneShowcaseProps {
  lang: Lang;
}

export function PhoneShowcase({ lang }: PhoneShowcaseProps) {
  const copy = t(lang).features;
  const [activeIdx, setActiveIdx] = useState(0);
  const current = copy.items[activeIdx] ?? copy.items[0];

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
      {/* Daftar 4 kartu fitur interaktif */}
      <div className="lg:col-span-7 flex flex-col gap-4">
        {copy.items.map((item, idx) => {
          const active = idx === activeIdx;
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => setActiveIdx(idx)}
              className={`group text-left rounded-3xl p-6 transition-all duration-300 cursor-pointer ${
                active
                  ? 'glass-light border-[#B85A3C]/45 shadow-[0_16px_40px_-20px_rgba(184,90,60,0.32)] translate-x-1'
                  : 'bg-[#FFFCF8]/45 hover:bg-[#FFFCF8]/75 border border-[#E3D9CC]/70'
              }`}
            >
              <div className="flex items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2.5 mb-2">
                    <span
                      className={`inline-flex h-6 w-6 items-center justify-center rounded-full font-mono text-xs font-semibold ${
                        active ? 'bg-[#B85A3C] text-white' : 'bg-[#E8E0D5] text-[#5E534C]'
                      }`}
                    >
                      0{idx + 1}
                    </span>
                    <h3 className="text-lg sm:text-xl font-semibold text-[#2A2420]">{item.title}</h3>
                  </div>
                  <p className="text-sm sm:text-[15px] leading-relaxed text-[#5E534C]">{item.desc}</p>
                </div>
                <span
                  className={`shrink-0 rounded-full px-3 py-1 text-[11px] font-mono font-medium ${
                    active ? 'bg-[#B85A3C]/12 text-[#8F4128]' : 'bg-[#E8E0D5]/80 text-[#5E534C]'
                  }`}
                >
                  {item.badge}
                </span>
              </div>
            </button>
          );
        })}
      </div>

      {/* Mockup ponsel realistis menampilkan tangkapan layar asli Nusantara AR */}
      <div className="lg:col-span-5 flex justify-center">
        <div className="relative">
          {/* Halo terakota-emas lembut */}
          <div className="pointer-events-none absolute -inset-6 rounded-[52px] bg-gradient-to-tr from-[#B85A3C]/22 via-[#D4AF37]/16 to-transparent blur-2xl" />

          <div className="relative w-[290px] sm:w-[316px] rounded-[46px] bg-[#1D222B] p-[11px] shadow-[0_32px_70px_-24px_rgba(30,43,63,0.45)] border border-white/15">
            {/* Kamera depan / notch tipis */}
            <div className="pointer-events-none absolute top-4 left-1/2 -translate-x-1/2 z-20 h-5 w-20 rounded-full bg-[#12161C]" />

            <div className="relative aspect-[540/1170] w-full overflow-hidden rounded-[36px] bg-[#F4EFE8]">
              {copy.items.map((item, idx) => (
                <img
                  key={item.id}
                  src={item.screen}
                  alt={item.title}
                  loading="lazy"
                  className={`absolute inset-0 h-full w-full object-cover transition-opacity duration-500 ${
                    idx === activeIdx ? 'opacity-100' : 'opacity-0 pointer-events-none'
                  }`}
                />
              ))}
            </div>
          </div>

          <div className="mt-4 flex items-center justify-center gap-2 text-xs text-[#5E534C]">
            <Sparkles className="h-3.5 w-3.5 text-[#D4AF37]" />
            <span>{current.title}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
