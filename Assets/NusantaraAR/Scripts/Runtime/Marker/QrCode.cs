using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NusantaraAR.Marker
{
    /// <summary>Tingkat koreksi galat QR (L ~7%, M ~15%, Q ~25%, H ~30% codeword boleh rusak).</summary>
    public enum QrEcc { L, M, Q, H }

    /// <summary>
    /// Kode QR (ISO/IEC 18004) versi 1-10 tanpa library luar: encoder (mode byte, untuk "Tampilkan QR")
    /// dan decoder matriks modul (mode numerik, alfanumerik, byte; koreksi Reed-Solomon).
    /// Matriks: modules[baris, kolom], true = hitam. Deteksi QR di gambar kamera ada di <see cref="QrDetector"/>.
    /// </summary>
    public static class QrCode
    {
        public const int MinVersion = 1;
        public const int MaxVersion = 10;

        // [tingkat ECC, versi]; kolom 0 tidak dipakai.
        static readonly int[,] EccPerBlock =
        {
            { -1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18 },
            { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 },
            { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24 },
            { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28 },
        };

        static readonly int[,] NumBlocks =
        {
            { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4 },
            { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 },
            { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8 },
            { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8 },
        };

        const string Alphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        public static int Size(int version) => 17 + 4 * version;

        /// <summary>Jumlah codeword data (tanpa ECC) untuk versi dan tingkat ECC tertentu.</summary>
        public static int DataCodewords(int version, QrEcc ecc) =>
            RawDataModules(version) / 8 - EccPerBlock[(int)ecc, version] * NumBlocks[(int)ecc, version];

        static int RawDataModules(int v)
        {
            int result = (16 * v + 128) * v + 64;
            if (v >= 2)
            {
                int numAlign = v / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (v >= 7) result -= 36;
            }
            return result;
        }

        /// <summary>Posisi baris/kolom pusat pola alignment (kosong untuk versi 1).</summary>
        public static int[] AlignmentPositions(int v)
        {
            if (v == 1) return new int[0];
            int numAlign = v / 7 + 2;
            int step = (v * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
            var result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = Size(v) - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        static int EccFormatBits(QrEcc e) => e == QrEcc.L ? 1 : e == QrEcc.M ? 0 : e == QrEcc.Q ? 3 : 2;

        static QrEcc EccFromFormatBits(int bits) => bits == 1 ? QrEcc.L : bits == 0 ? QrEcc.M : bits == 3 ? QrEcc.Q : QrEcc.H;

        /// <summary>15 bit informasi format (ECC + mask) setelah BCH dan XOR 0x5412.</summary>
        static int FormatWord(int data)
        {
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            return ((data << 10) | rem) ^ 0x5412;
        }

        /// <summary>18 bit informasi versi (versi >= 7).</summary>
        static int VersionWord(int v)
        {
            int rem = v;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            return (v << 12) | rem;
        }

        static bool MaskBit(int mask, int x, int y)
        {
            switch (mask)
            {
                case 0: return (x + y) % 2 == 0;
                case 1: return y % 2 == 0;
                case 2: return x % 3 == 0;
                case 3: return (x + y) % 3 == 0;
                case 4: return (x / 3 + y / 2) % 2 == 0;
                case 5: return x * y % 2 + x * y % 3 == 0;
                case 6: return (x * y % 2 + x * y % 3) % 2 == 0;
                default: return ((x + y) % 2 + x * y % 3) % 2 == 0;
            }
        }

        static int PopCount(int x)
        {
            int n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }

        // ------------------------------------------------------------------ pola fungsi

        /// <summary>Menggambar pola fungsi (finder, separator, timing, alignment, modul gelap) dan menandai area format/versi.</summary>
        static void DrawFunctionPatterns(int v, bool[,] modules, bool[,] isFunction)
        {
            int size = Size(v);
            void Set(int x, int y, bool dark) { modules[y, x] = dark; isFunction[y, x] = true; }

            for (int i = 0; i < size; i++)
            {
                Set(6, i, i % 2 == 0);
                Set(i, 6, i % 2 == 0);
            }
            foreach (var (cx, cy) in new[] { (3, 3), (size - 4, 3), (3, size - 4) })
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    Set(x, y, dist != 2 && dist != 4);
                }
            var align = AlignmentPositions(v);
            int n = align.Length;
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue;
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    Set(align[i] + dx, align[j] + dy, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != 1);
            }
            DrawFormat(modules, isFunction, size, 0);
            if (v >= 7) DrawVersion(modules, isFunction, v);
        }

        static IEnumerable<(int x, int y)> FormatPositions(int size, bool second)
        {
            if (!second)
            {
                for (int i = 0; i <= 5; i++) yield return (8, i);
                yield return (8, 7);
                yield return (8, 8);
                yield return (7, 8);
                for (int i = 9; i < 15; i++) yield return (14 - i, 8);
            }
            else
            {
                for (int i = 0; i < 8; i++) yield return (size - 1 - i, 8);
                for (int i = 8; i < 15; i++) yield return (8, size - 15 + i);
            }
        }

        static void DrawFormat(bool[,] modules, bool[,] isFunction, int size, int word)
        {
            for (int copy = 0; copy < 2; copy++)
            {
                int i = 0;
                foreach (var (x, y) in FormatPositions(size, copy == 1))
                {
                    modules[y, x] = ((word >> i) & 1) != 0;
                    isFunction[y, x] = true;
                    i++;
                }
            }
            modules[size - 8, 8] = true; // modul gelap tetap
            isFunction[size - 8, 8] = true;
        }

        static void DrawVersion(bool[,] modules, bool[,] isFunction, int v)
        {
            int size = Size(v), word = VersionWord(v);
            for (int i = 0; i < 18; i++)
            {
                bool bit = ((word >> i) & 1) != 0;
                int a = size - 11 + i % 3, b = i / 3;
                modules[b, a] = bit; isFunction[b, a] = true;
                modules[a, b] = bit; isFunction[a, b] = true;
            }
        }

        /// <summary>Urutan zig-zag modul data (kolom berpasangan dari kanan, naik-turun bergantian).</summary>
        static IEnumerable<(int x, int y)> DataPositions(bool[,] isFunction)
        {
            int size = isFunction.GetLength(0);
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                bool upward = ((right + 1) & 2) == 0;
                for (int vert = 0; vert < size; vert++)
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j, y = upward ? size - 1 - vert : vert;
                    if (!isFunction[y, x]) yield return (x, y);
                }
            }
        }

        // ------------------------------------------------------------------ encoder

        /// <summary>
        /// Membuat QR mode byte (UTF-8) dengan versi terkecil >= <paramref name="minVersion"/> yang cukup.
        /// mask -1 = pilih mask dengan penalti terkecil. Null bila teks terlalu panjang untuk versi 10.
        /// </summary>
        public static bool[,] Encode(string text, QrEcc ecc = QrEcc.M, int minVersion = 2, int mask = -1)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            int v = Mathf.Clamp(minVersion, MinVersion, MaxVersion);
            for (; v <= MaxVersion; v++)
                if (4 + (v < 10 ? 8 : 16) + bytes.Length * 8 <= DataCodewords(v, ecc) * 8) break;
            if (v > MaxVersion) return null;

            int capacity = DataCodewords(v, ecc);
            var bits = new BitWriter();
            bits.Write(0b0100, 4);
            bits.Write(bytes.Length, v < 10 ? 8 : 16);
            foreach (var b in bytes) bits.Write(b, 8);
            bits.Write(0, Mathf.Min(4, capacity * 8 - bits.Length));
            bits.Write(0, (8 - bits.Length % 8) % 8);
            var data = bits.ToBytes();
            var padded = new byte[capacity];
            System.Array.Copy(data, padded, data.Length);
            for (int i = data.Length; i < capacity; i++) padded[i] = (i - data.Length) % 2 == 0 ? (byte)0xEC : (byte)0x11;

            var codewords = AddEccAndInterleave(padded, v, ecc);
            int size = Size(v);
            var modules = new bool[size, size];
            var isFunction = new bool[size, size];
            DrawFunctionPatterns(v, modules, isFunction);
            int bit = 0;
            foreach (var (x, y) in DataPositions(isFunction))
            {
                if (bit < codewords.Length * 8) modules[y, x] = ((codewords[bit >> 3] >> (7 - (bit & 7))) & 1) != 0;
                bit++;
            }

            if (mask < 0)
            {
                int best = int.MaxValue;
                for (int m = 0; m < 8; m++)
                {
                    ApplyMask(modules, isFunction, m);
                    DrawFormat(modules, isFunction, size, FormatWord(EccFormatBits(ecc) << 3 | m));
                    int penalty = Penalty(modules);
                    if (penalty < best) { best = penalty; mask = m; }
                    ApplyMask(modules, isFunction, m); // XOR lagi = kembali
                }
            }
            ApplyMask(modules, isFunction, mask);
            DrawFormat(modules, isFunction, size, FormatWord(EccFormatBits(ecc) << 3 | mask));
            return modules;
        }

        static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (!isFunction[y, x] && MaskBit(mask, x, y)) modules[y, x] = !modules[y, x];
        }

        static byte[] AddEccAndInterleave(byte[] data, int v, QrEcc ecc)
        {
            int numBlocks = NumBlocks[(int)ecc, v], eccLen = EccPerBlock[(int)ecc, v];
            int raw = RawDataModules(v) / 8;
            int numShort = numBlocks - raw % numBlocks, shortLen = raw / numBlocks;
            var divisor = RsDivisor(eccLen);
            var blocks = new List<byte[]>();
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int dataLen = shortLen - eccLen + (i < numShort ? 0 : 1);
                var dat = new byte[dataLen];
                System.Array.Copy(data, k, dat, 0, dataLen);
                k += dataLen;
                var block = new byte[shortLen + 1];
                System.Array.Copy(dat, block, dataLen);
                var rem = RsRemainder(dat, divisor);
                System.Array.Copy(rem, 0, block, block.Length - eccLen, eccLen); // blok pendek: 1 byte kosong sebelum ECC
                blocks.Add(block);
            }
            var result = new List<byte>(raw);
            for (int i = 0; i < shortLen + 1; i++)
            for (int j = 0; j < numBlocks; j++)
                if (i != shortLen - eccLen || j >= numShort) result.Add(blocks[j][i]);
            return result.ToArray();
        }

        /// <summary>Skor penalti mask (aturan N1-N4 standar QR, versi ringkas).</summary>
        static int Penalty(bool[,] m)
        {
            int size = m.GetLength(0), score = 0, dark = 0;
            for (int pass = 0; pass < 2; pass++)
            for (int a = 0; a < size; a++)
            {
                int run = 0;
                bool prev = false;
                for (int b = 0; b < size; b++)
                {
                    bool c = pass == 0 ? m[a, b] : m[b, a];
                    if (b > 0 && c == prev) run++;
                    else
                    {
                        if (run >= 5) score += 3 + run - 5;
                        run = 1;
                    }
                    prev = c;
                    if (b + 10 < size)
                    {
                        int w = 0;
                        for (int k = 0; k < 11; k++) w = (w << 1) | ((pass == 0 ? m[a, b + k] : m[b + k, a]) ? 1 : 0);
                        if (w == 0b10111010000 || w == 0b00001011101) score += 40;
                    }
                }
                if (run >= 5) score += 3 + run - 5;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (m[y, x]) dark++;
                if (x + 1 < size && y + 1 < size && m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) score += 3;
            }
            int total = size * size;
            score += ((System.Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1) * 10;
            return score;
        }

        // ------------------------------------------------------------------ decoder

        /// <summary>
        /// Membaca matriks modul QR (tanpa zona sunyi). Mengoreksi galat dengan Reed-Solomon.
        /// False bila ukuran/format tidak valid atau kerusakan melebihi kapasitas koreksi.
        /// </summary>
        public static bool TryDecode(bool[,] modules, out string text)
        {
            text = null;
            int size = modules.GetLength(0);
            if (size != modules.GetLength(1) || (size - 17) % 4 != 0) return false;
            int v = (size - 17) / 4;
            if (v < MinVersion || v > MaxVersion) return false;

            // Informasi format: cari kode BCH valid terdekat dari kedua salinan.
            int bestData = -1, bestDist = int.MaxValue;
            for (int copy = 0; copy < 2; copy++)
            {
                int word = 0, i = 0;
                foreach (var (x, y) in FormatPositions(size, copy == 1)) word |= (modules[y, x] ? 1 : 0) << i++;
                for (int d = 0; d < 32; d++)
                {
                    int dist = PopCount(word ^ FormatWord(d));
                    if (dist < bestDist) { bestDist = dist; bestData = d; }
                }
            }
            if (bestDist > 3) return false;
            var ecc = EccFromFormatBits(bestData >> 3);
            int mask = bestData & 7;

            if (v >= 7 && !VersionMatches(modules, v)) return false;

            var isFunction = new bool[size, size];
            DrawFunctionPatterns(v, new bool[size, size], isFunction);
            int raw = RawDataModules(v) / 8;
            var codewords = new byte[raw];
            int bit = 0;
            foreach (var (x, y) in DataPositions(isFunction))
            {
                if (bit >= raw * 8) break;
                if (modules[y, x] ^ MaskBit(mask, x, y)) codewords[bit >> 3] |= (byte)(0x80 >> (bit & 7));
                bit++;
            }

            // Bongkar interleave -> perbaiki tiap blok -> gabungkan data.
            int numBlocks = NumBlocks[(int)ecc, v], eccLen = EccPerBlock[(int)ecc, v];
            int numShort = numBlocks - raw % numBlocks, shortLen = raw / numBlocks;
            var blocks = new byte[numBlocks][];
            for (int j = 0; j < numBlocks; j++) blocks[j] = new byte[shortLen + (j < numShort ? 0 : 1)];
            int k = 0;
            for (int i = 0; i < shortLen - eccLen + 1; i++)
            for (int j = 0; j < numBlocks; j++)
                if (i < blocks[j].Length - eccLen) blocks[j][i] = codewords[k++];
            for (int i = 0; i < eccLen; i++)
            for (int j = 0; j < numBlocks; j++)
                blocks[j][blocks[j].Length - eccLen + i] = codewords[k++];

            var data = new List<byte>();
            foreach (var block in blocks)
            {
                if (!RsCorrect(block, eccLen)) return false;
                for (int i = 0; i < block.Length - eccLen; i++) data.Add(block[i]);
            }
            return TryParseSegments(data.ToArray(), v, out text);
        }

        static bool VersionMatches(bool[,] modules, int v)
        {
            int size = modules.GetLength(0), best = -1, bestDist = int.MaxValue;
            for (int copy = 0; copy < 2; copy++)
            {
                int word = 0;
                for (int i = 0; i < 18; i++)
                {
                    int a = size - 11 + i % 3, b = i / 3;
                    bool bit = copy == 0 ? modules[b, a] : modules[a, b];
                    word |= (bit ? 1 : 0) << i;
                }
                for (int cand = 7; cand <= 40; cand++)
                {
                    int dist = PopCount(word ^ VersionWord(cand));
                    if (dist < bestDist) { bestDist = dist; best = cand; }
                }
            }
            return bestDist <= 3 && best == v;
        }

        static bool TryParseSegments(byte[] data, int v, out string text)
        {
            text = null;
            var reader = new BitReader(data);
            var bytes = new List<byte>();
            while (reader.Remaining >= 4)
            {
                int mode = reader.Read(4);
                if (mode == 0) break; // terminator
                if (mode == 0b0111) // ECI: abaikan penanda set karakter (1-3 byte)
                {
                    if (reader.Remaining < 8) return false;
                    int first = reader.Read(8);
                    if ((first & 0x80) != 0) reader.Read((first & 0x40) == 0 ? 8 : 16);
                    continue;
                }
                int countBits = mode == 0b0001 ? (v < 10 ? 10 : 12)
                    : mode == 0b0010 ? (v < 10 ? 9 : 11)
                    : mode == 0b0100 ? (v < 10 ? 8 : 16) : -1;
                if (countBits < 0 || reader.Remaining < countBits) return false;
                int count = reader.Read(countBits);
                switch (mode)
                {
                    case 0b0001:
                        for (; count >= 3; count -= 3)
                        {
                            if (reader.Remaining < 10) return false;
                            AppendDigits(bytes, reader.Read(10), 3);
                        }
                        if (count > 0)
                        {
                            int n = count == 2 ? 7 : 4;
                            if (reader.Remaining < n) return false;
                            AppendDigits(bytes, reader.Read(n), count);
                        }
                        break;
                    case 0b0010:
                        for (; count >= 2; count -= 2)
                        {
                            if (reader.Remaining < 11) return false;
                            int pair = reader.Read(11);
                            if (pair / 45 >= 45) return false;
                            bytes.Add((byte)Alphanumeric[pair / 45]);
                            bytes.Add((byte)Alphanumeric[pair % 45]);
                        }
                        if (count == 1)
                        {
                            if (reader.Remaining < 6) return false;
                            int c = reader.Read(6);
                            if (c >= 45) return false;
                            bytes.Add((byte)Alphanumeric[c]);
                        }
                        break;
                    default:
                        if (reader.Remaining < count * 8) return false;
                        for (int i = 0; i < count; i++) bytes.Add((byte)reader.Read(8));
                        break;
                }
            }
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes.ToArray());
            }
            catch (System.ArgumentException)
            {
                // Bukan UTF-8: anggap ISO-8859-1 (byte = kode karakter; GetEncoding(28591) belum tentu ada di IL2CPP).
                var chars = new char[bytes.Count];
                for (int i = 0; i < chars.Length; i++) chars[i] = (char)bytes[i];
                text = new string(chars);
            }
            return true;
        }

        static void AppendDigits(List<byte> bytes, int value, int digits)
        {
            if (value >= (digits == 3 ? 1000 : digits == 2 ? 100 : 10)) return;
            var s = value.ToString().PadLeft(digits, '0');
            foreach (var c in s) bytes.Add((byte)c);
        }

        // ------------------------------------------------------------------ Reed-Solomon GF(256), polinom 0x11D

        static readonly byte[] Exp = new byte[512];
        static readonly byte[] Log = new byte[256];

        static QrCode()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                Exp[i] = (byte)x;
                Log[x] = (byte)i;
                x <<= 1;
                if (x >= 256) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
        }

        static byte Mul(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : Exp[Log[a] + Log[b]];

        static byte Div(byte a, byte b) => a == 0 ? (byte)0 : Exp[Log[a] + 255 - Log[b]];

        /// <summary>Koefisien generator (x - a^0)...(x - a^(n-1)), tanpa koefisien utama 1, dari derajat tertinggi.</summary>
        static byte[] RsDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = Mul(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = Mul(root, 2);
            }
            return result;
        }

        static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (var b in data)
            {
                byte factor = (byte)(b ^ result[0]);
                System.Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= Mul(divisor[i], factor);
            }
            return result;
        }

        /// <summary>
        /// Koreksi galat satu blok (byte 0 = koefisien derajat tertinggi): sindrom -> Berlekamp-Massey -> Chien -> Forney.
        /// Publik untuk pengujian.
        /// </summary>
        public static bool RsCorrect(byte[] block, int eccLen)
        {
            int n = block.Length;
            var syn = new byte[eccLen];
            bool clean = true;
            for (int i = 0; i < eccLen; i++)
            {
                syn[i] = Evaluate(block, Exp[i]);
                if (syn[i] != 0) clean = false;
            }
            if (clean) return true;

            // Berlekamp-Massey: polinom lokator galat lambda (koefisien derajat rendah dulu).
            var lambda = new byte[eccLen + 1];
            var prev = new byte[eccLen + 1];
            lambda[0] = prev[0] = 1;
            int errors = 0, shift = 1;
            byte prevDisc = 1;
            for (int r = 0; r < eccLen; r++)
            {
                byte d = syn[r];
                for (int i = 1; i <= errors; i++) d ^= Mul(lambda[i], syn[r - i]);
                if (d == 0) { shift++; continue; }
                byte coef = Div(d, prevDisc);
                if (2 * errors <= r)
                {
                    var temp = (byte[])lambda.Clone();
                    for (int i = 0; i + shift <= eccLen; i++) lambda[i + shift] ^= Mul(coef, prev[i]);
                    errors = r + 1 - errors;
                    prev = temp;
                    prevDisc = d;
                    shift = 1;
                }
                else
                {
                    for (int i = 0; i + shift <= eccLen; i++) lambda[i + shift] ^= Mul(coef, prev[i]);
                    shift++;
                }
            }
            if (errors == 0 || errors * 2 > eccLen) return false;

            // Omega = S(x) * lambda(x) mod x^eccLen
            var omega = new byte[eccLen];
            for (int i = 0; i < eccLen; i++)
            for (int j = 0; j <= i && j <= errors; j++)
                omega[i] ^= Mul(syn[i - j], lambda[j]);

            int found = 0;
            for (int p = 0; p < n; p++)
            {
                int degree = n - 1 - p;
                byte xInv = Exp[(255 - degree % 255) % 255];
                if (EvaluateLow(lambda, errors, xInv) != 0) continue;
                // Turunan formal lambda: hanya suku berpangkat ganjil.
                byte deriv = 0, pow = 1, x2 = Mul(xInv, xInv);
                for (int i = 1; i <= errors; i += 2)
                {
                    deriv ^= Mul(lambda[i], pow);
                    pow = Mul(pow, x2);
                }
                if (deriv == 0) return false;
                byte magnitude = Mul(Exp[degree % 255], Div(EvaluateLow(omega, eccLen - 1, xInv), deriv));
                block[p] ^= magnitude;
                found++;
            }
            if (found != errors) return false;
            for (int i = 0; i < eccLen; i++)
                if (Evaluate(block, Exp[i]) != 0) return false;
            return true;
        }

        /// <summary>Evaluasi polinom (koefisien derajat tertinggi dulu) di x.</summary>
        static byte Evaluate(byte[] poly, byte x)
        {
            byte y = 0;
            foreach (var c in poly) y = (byte)(Mul(y, x) ^ c);
            return y;
        }

        /// <summary>Evaluasi polinom (koefisien derajat rendah dulu, sampai derajat maxDegree) di x.</summary>
        static byte EvaluateLow(byte[] poly, int maxDegree, byte x)
        {
            byte y = 0;
            for (int i = Mathf.Min(maxDegree, poly.Length - 1); i >= 0; i--) y = (byte)(Mul(y, x) ^ poly[i]);
            return y;
        }

        // ------------------------------------------------------------------ gambar

        /// <summary>Tekstur QR (hitam di atas putih) dengan zona sunyi <paramref name="quietModules"/> modul.</summary>
        public static Texture2D CreateTexture(bool[,] modules, int modulePixels = 24, int quietModules = 4)
        {
            int n = modules.GetLength(0), size = (n + quietModules * 2) * modulePixels;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, name = "QR" };
            var px = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int u = x / modulePixels - quietModules;
                int v = (size - 1 - y) / modulePixels - quietModules; // baris 0 = atas
                px[y * size + x] = u >= 0 && v >= 0 && u < n && v < n && modules[v, u] ? black : white;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        // ------------------------------------------------------------------ bit

        class BitWriter
        {
            readonly List<bool> bits = new List<bool>();
            public int Length => bits.Count;

            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
            }

            public byte[] ToBytes()
            {
                var result = new byte[(bits.Count + 7) / 8];
                for (int i = 0; i < bits.Count; i++)
                    if (bits[i]) result[i >> 3] |= (byte)(0x80 >> (i & 7));
                return result;
            }
        }

        class BitReader
        {
            readonly byte[] data;
            int position;

            public BitReader(byte[] data) => this.data = data;

            public int Remaining => data.Length * 8 - position;

            public int Read(int count)
            {
                int value = 0;
                for (int i = 0; i < count; i++, position++)
                    value = (value << 1) | ((data[position >> 3] >> (7 - (position & 7))) & 1);
                return value;
            }
        }
    }
}
