export type Lang = 'id' | 'en';
export type ArtifactCategoryKey = 'all' | 'weapons' | 'temple' | 'wildlife';

export interface LocalizedText {
  id: string;
  en: string;
}

export interface ArtifactSpec {
  id: string;
  code: string;
  category: Exclude<ArtifactCategoryKey, 'all'>;
  name: LocalizedText;
  localName: string;
  region: LocalizedText;
  era: LocalizedText;
  tagline: LocalizedText;
  description: LocalizedText;
  hotspots: number;
  stages: number;
  hasDraw: boolean;
  modelUrl: string;
  thumbUrl: string;
  qrPdfUrl: string;
  qrPngUrl: string;
  qrPayload: string;
  /** Offset pecahan (unit ter-normalisasi terhadap tinggi model) per pola nama node untuk mode Bongkar 3D interaktif */
  explodeRules: Array<{ match: string[]; offset: [number, number, number] }>;
  /** Sudut awal kamera / rotasi Y agar muka terbaik menghadap pengunjung */
  initialYaw?: number;
  cameraDistance?: number;
}

export const APK_META = {
  fileName: 'NusantaraAR.apk',
  version: 'v0.1.0',
  sizeLabel: '101,6 MB',
  minAndroid: 'Android 8.0+ (API 26)',
  targetAndroid: 'Android 16 (API 36)',
  arch: 'ARM64 · IL2CPP · OpenGL ES 3',
  downloadUrl:
    import.meta.env.VITE_APK_URL ||
    (import.meta.env.DEV
      ? '/downloads/NusantaraAR.apk'
      : 'https://github.com/ophari/NusantaraAR/releases/latest/download/NusantaraAR.apk'),
};

export const ARTIFACTS: ArtifactSpec[] = [
  {
    id: 'golok_betawi',
    code: 'GOLOK_BETAWI_01',
    category: 'weapons',
    name: { id: 'Golok Betawi', en: 'Betawi Golok' },
    localName: 'Golok Sorenan',
    region: { id: 'DKI Jakarta (Betawi)', en: 'Jakarta (Betawi)' },
    era: { id: 'Tradisi Betawi · WBTb 2021', en: 'Betawi Tradition · ICH 2021' },
    tagline: {
      id: 'Tanduk kerbau belang, 14 simpay kuningan & bilah sorenan 30 cm',
      en: 'Striated buffalo horn, 14 brass simpay rings & 30 cm sorenan blade',
    },
    description: {
      id: 'Golok sorenan pusaka masyarakat Betawi yang ditetapkan sebagai Warisan Budaya Takbenda Indonesia 2021. Memadukan bilah baja 30 cm bertanda cap pande, salut kuningan, hulu jengkolan tanduk kerbau hitam berpaku kuningan, dan sarung tanduk belang dengan cantelan pinggang (sangkep).',
      en: 'Traditional Betawi waist-worn sorenan golok recognized as Indonesian Intangible Cultural Heritage (2021). Features a 30 cm smith-stamped steel blade, brass guard, black buffalo-horn jengkolan hilt, and striated horn scabbard with 14 brass bands and belt hook.',
    },
    hotspots: 10,
    stages: 4,
    hasDraw: true,
    modelUrl: '/models/golok_betawi.glb',
    thumbUrl: '/thumbs/golok_betawi.png',
    qrPdfUrl: '/markers/KartuQR_GolokBetawi_A5.pdf',
    qrPngUrl: '/markers/KartuQR_GolokBetawi_A5.png',
    qrPayload: 'NUSANTARA:GOLOK_BETAWI_01',
    initialYaw: -0.35,
    cameraDistance: 2.35,
    explodeRules: [
      { match: ['Bilah'], offset: [0.34, 0.02, 0] },
      { match: ['Salut'], offset: [0.34, 0.16, 0] },
      { match: ['Hulu'], offset: [0.34, 0.30, 0] },
      { match: ['Paku'], offset: [0.34, 0.22, 0.16] },
      { match: ['Sarung_Kepala'], offset: [-0.04, 0.16, 0] },
      { match: ['Sarung_Sangkep'], offset: [-0.04, 0.04, 0.18] },
      { match: ['Sarung_Simpay'], offset: [-0.22, 0, 0] },
      { match: ['Sarung_Badan'], offset: [-0.04, 0, 0] },
    ],
  },
  {
    id: 'keris_bali',
    code: 'KERIS_BALI_01',
    category: 'weapons',
    name: { id: 'Keris Bali', en: 'Balinese Keris' },
    localName: 'Keris Luk 9 · Jagrak',
    region: { id: 'Bali', en: 'Bali' },
    era: { id: 'Tradisi Bali · UNESCO 2005', en: 'Balinese Tradition · UNESCO 2005' },
    tagline: {
      id: 'Bilah 40 cm luk 9 pamor banyu tetes di atas jagrak Karang Boma',
      en: '40 cm 9-wave banyu tetes blade resting on a Karang Boma stand',
    },
    description: {
      id: 'Keris gaya Bali dengan bilah luk 9 berpamor banyu tetes, ganja dan hulu figur dewa emas berpermata, warangka sesrengatan dari kayu timoho berpelet, serta dudukan ukir jagrak Karang Boma setinggi 45 cm.',
      en: 'Balinese keris showcasing a 9-wave banyu tetes patterned blade, gold deity hilt with jeweled selut, timoho wood sesrengatan scabbard, and a 45 cm carved Karang Boma display stand.',
    },
    hotspots: 11,
    stages: 5,
    hasDraw: true,
    modelUrl: '/models/keris_bali.glb',
    thumbUrl: '/thumbs/keris_bali.png',
    qrPdfUrl: '/markers/KartuQR_KerisBali_A5.pdf',
    qrPngUrl: '/markers/KartuQR_KerisBali_A5.png',
    qrPayload: 'NUSANTARA:KERIS_BALI_01',
    initialYaw: 0.22,
    cameraDistance: 2.35,
    explodeRules: [
      { match: ['Wilah', 'Bilah'], offset: [0, 0.34, 0] },
      { match: ['Ganja'], offset: [0.16, 0.44, 0] },
      { match: ['Selut', 'Mendak'], offset: [0.24, 0.52, 0] },
      { match: ['Hulu', 'Danganan'], offset: [0.34, 0.60, 0] },
      { match: ['Warangka', 'Sampir'], offset: [-0.20, 0.16, 0] },
      { match: ['Pendok'], offset: [-0.14, -0.14, 0.12] },
      { match: ['Jagrak', 'Dudukan'], offset: [0, -0.18, 0] },
    ],
  },
  {
    id: 'keris_sumatra',
    code: 'KERIS_SUMATRA_01',
    category: 'weapons',
    name: { id: 'Keris Sumatra', en: 'Sumatran Keris' },
    localName: 'Keris Melayu Luk 7',
    region: { id: 'Sumatra', en: 'Sumatra' },
    era: { id: 'Tradisi Melayu · UNESCO 2005', en: 'Malay Tradition · UNESCO 2005' },
    tagline: {
      id: 'Pamor wos wutah, hulu burl paruh burung & sampir bulan sabit',
      en: 'Wos wutah pamor, carved burl hilt & crescent sampir sheath',
    },
    description: {
      id: 'Keris gaya Melayu Sumatra berbilah 36 cm luk 7 dengan motif pamor wos wutah, pendongkok kuningan bertingkat, hulu kayu burl berukir sulur, sampir bulan sabit, dan pendok kuningan repoussé.',
      en: 'Malay-Sumatran keris with a 36 cm 7-wave wos wutah blade, stepped brass pendongkok collar, scroll-carved burl hilt, crescent sampir, and embossed brass sheath sleeves.',
    },
    hotspots: 9,
    stages: 4,
    hasDraw: true,
    modelUrl: '/models/keris_sumatra.glb',
    thumbUrl: '/thumbs/keris_sumatra.png',
    qrPdfUrl: '/markers/KartuQR_KerisSumatra_A5.pdf',
    qrPngUrl: '/markers/KartuQR_KerisSumatra_A5.png',
    qrPayload: 'NUSANTARA:KERIS_SUMATRA_01',
    initialYaw: -0.25,
    cameraDistance: 2.35,
    explodeRules: [
      { match: ['Wilah'], offset: [0.32, 0.04, 0] },
      { match: ['Ganja'], offset: [0.32, 0.16, 0] },
      { match: ['Mendak'], offset: [0.32, 0.26, 0] },
      { match: ['Hulu'], offset: [0.32, 0.38, 0] },
      { match: ['Warangka'], offset: [-0.04, 0.16, 0] },
      { match: ['Pendok'], offset: [-0.14, -0.06, 0.10] },
    ],
  },
  {
    id: 'candi_borobudur',
    code: 'BOROBUDUR_01',
    category: 'temple',
    name: { id: 'Candi Borobudur', en: 'Borobudur Temple' },
    localName: 'Borobudur · Skala 1:200',
    region: { id: 'Magelang, Jawa Tengah', en: 'Magelang, Central Java' },
    era: { id: 'Abad ke-9 · Wangsa Syailendra', en: '9th Century · Sailendra Dynasty' },
    tagline: {
      id: 'Tiga ranah kosmologi Kamadhatu, Rupadhatu & Arupadhatu dalam 3D',
      en: 'Three cosmological realms Kamadhatu, Rupadhatu & Arupadhatu in 3D',
    },
    description: {
      id: 'Monumen Buddha terbesar di dunia dalam miniatur presisi 1:200 lengkap dengan kaki candi, lima teras persegi berelief, 432 relung arca, tiga teras melingkar berisi 72 stupa terawang, dan stupa induk di puncak.',
      en: 'The world’s largest Buddhist monument modeled at 1:200 scale, featuring the hidden foot, five square relief galleries, 432 niche statues, three circular terraces with 72 perforated stupas, and the central main stupa.',
    },
    hotspots: 10,
    stages: 4,
    hasDraw: false,
    modelUrl: '/models/candi_borobudur.glb',
    thumbUrl: '/thumbs/candi_borobudur.png',
    qrPdfUrl: '/markers/KartuQR_CandiBorobudur_A5.pdf',
    qrPngUrl: '/markers/KartuQR_CandiBorobudur_A5.png',
    qrPayload: 'NUSANTARA:BOROBUDUR_01',
    initialYaw: 0.65,
    cameraDistance: 2.15,
    explodeRules: [
      { match: ['Induk', 'Puncak', 'Main'], offset: [0, 0.42, 0] },
      { match: ['Arupadhatu', 'Melingkar', 'Stupa'], offset: [0, 0.28, 0] },
      { match: ['Teras_3', 'Teras_4', 'Teras_5', 'Atas'], offset: [0, 0.16, 0] },
      { match: ['Teras_1', 'Teras_2', 'Bawah'], offset: [0, 0.06, 0] },
    ],
  },
  {
    id: 'karambit',
    code: 'KARAMBIT_01',
    category: 'weapons',
    name: { id: 'Karambit Minangkabau', en: 'Minangkabau Karambit' },
    localName: 'Kurambiak Jantan',
    region: { id: 'Minangkabau, Sumatra Barat', en: 'Minangkabau, West Sumatra' },
    era: { id: 'Tradisi Silek Minangkabau', en: 'Minangkabau Silek Tradition' },
    tagline: {
      id: 'Bilah cakar 7 gerigi bermotif kaluak paku & hulu kayu kemuning',
      en: '7-serration claw blade with kaluak paku motif & kemuning hilt',
    },
    description: {
      id: 'Kurambiak jenis jantan dengan bilah melengkung 120° menyerupai cakar harimau Sumatra, 7 gerigi di punggung, lubang jari telunjuk pada hulu kayu kemuning, cincin kuningan, dan sarung berukir tinta emas.',
      en: 'Male-type kurambiak featuring a 120° tiger-claw blade with 7 spine serrations, index-finger ring carved into kemuning wood, brass ferrule, and a gold-ink engraved wooden scabbard.',
    },
    hotspots: 9,
    stages: 4,
    hasDraw: false,
    modelUrl: '/models/karambit.glb',
    thumbUrl: '/thumbs/karambit.png',
    qrPdfUrl: '/markers/KartuQR_Karambit_A5.pdf',
    qrPngUrl: '/markers/KartuQR_Karambit_A5.png',
    qrPayload: 'NUSANTARA:KARAMBIT_01',
    initialYaw: -0.25,
    cameraDistance: 2.25,
    explodeRules: [
      { match: ['Sarung'], offset: [0.24, -0.22, 0] },
      { match: ['Hulu'], offset: [-0.08, 0.26, 0] },
      { match: ['Cincin'], offset: [-0.04, 0.14, 0] },
      { match: ['Mata'], offset: [0.02, 0.06, 0] },
      { match: ['Dudukan'], offset: [0, -0.14, 0] },
    ],
  },
  {
    id: 'komodo',
    code: 'KOMODO_01',
    category: 'wildlife',
    name: { id: 'Komodo', en: 'Komodo Dragon' },
    localName: 'Varanus komodoensis · 1:10',
    region: { id: 'Taman Nasional Komodo, NTT', en: 'Komodo National Park, NTT' },
    era: { id: 'Satwa Endemik · UNESCO 1991', en: 'Endemic Wildlife · UNESCO 1991' },
    tagline: {
      id: 'Diorama savana Pulau Rinca dengan sarang telur & anatomi rahang',
      en: 'Rinca Island savanna diorama with nest mound & jaw anatomy',
    },
    description: {
      id: 'Diorama 1:10 kadal terbesar di dunia melangkah di savana Pulau Rinca di depan gundukan sarang burung gosong berisi tujuh telur, lengkap dengan sisik prosedural, 48 gigi bergerigi berlapis besi, dan lidah bercabang.',
      en: 'A 1:10 diorama of the world’s largest lizard on Rinca Island beside a cutaway scrubfowl nest revealing 7 eggs, detailed with procedural scales, 48 iron-tipped serrated teeth, and a forked yellow tongue.',
    },
    hotspots: 10,
    stages: 4,
    hasDraw: false,
    modelUrl: '/models/komodo.glb',
    thumbUrl: '/thumbs/komodo.png',
    qrPdfUrl: '/markers/KartuQR_Komodo_A5.pdf',
    qrPngUrl: '/markers/KartuQR_Komodo_A5.png',
    qrPayload: 'NUSANTARA:KOMODO_01',
    initialYaw: -0.55,
    cameraDistance: 2.15,
    explodeRules: [
      { match: ['Rahang', 'Lidah'], offset: [0.06, 0.14, 0.10] },
      { match: ['Telur'], offset: [0, 0.22, 0.08] },
      { match: ['Komodo', 'Tubuh', 'Badan', 'Kepala'], offset: [0, 0.18, 0] },
    ],
  },
];
