"""Siapkan musik latar artefak dari unduhan Pixabay (daftar trek: Tools/musik/musik.json).

Pixabay memblokir unduhan otomatis, jadi unduh tiap trek dari `url` (tombol Download) dan simpan sebagai `raw`
(Tools/musik/asli/, tidak di-commit). Skrip ini menyamakan kenyaringan (-20 LUFS, agar kedua keris sama keras),
memotong trek yang lebih panjang dari `maxSeconds` (akhirnya di-fade-out 3 detik) dan menulis MP3 ke `file`
di Assets. Fade di batas loop dikerjakan AudioManager saat diputar.
Setelah itu jalankan menu Unity "Nusantara AR/Pasang Musik Latar" (atau Setup Everything).

Perlu:    ffmpeg & ffprobe di PATH (winget install Gyan.FFmpeg)
Jalankan: python Tools/musik/siapkan_musik.py
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
MANIFEST = os.path.join(HERE, "musik.json")
FADE_OUT = 3.0


def abs_path(rel):
    return os.path.join(PROJECT, rel.replace("/", os.sep))


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                         check=True, capture_output=True, text=True).stdout
    return float(out.strip())


def main():
    with open(MANIFEST, encoding="utf-8") as f:
        tracks = json.load(f)["tracks"]
    missing = 0
    for t in tracks:
        raw, out = abs_path(t["raw"]), abs_path(t["file"])
        if not os.path.exists(raw):
            print(f"  BELUM ADA: {t['raw']}\n    unduh \"{t['title']}\" - {t['author']}: {t['url']}")
            missing += 1
            continue
        length = duration(raw)
        filters = ["loudnorm=I=-20:TP=-2:LRA=11"]
        args = []
        if length > t["maxSeconds"]:
            cut = float(t["maxSeconds"])
            args = ["-t", f"{cut:.2f}"]
            filters.insert(0, f"afade=t=out:st={cut - FADE_OUT:.2f}:d={FADE_OUT:.2f}")
        os.makedirs(os.path.dirname(out), exist_ok=True)
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", raw, *args, "-af", ",".join(filters),
                        "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "3", out], check=True)
        print(f"  {t['file']}: {length:.0f} dtk -> {duration(out):.0f} dtk, {os.path.getsize(out) / 1e6:.1f} MB")
    if missing:
        sys.exit(f"{missing} trek belum diunduh.")
    print("Selesai. Lanjutkan dengan menu Unity \"Nusantara AR/Pasang Musik Latar\".")


if __name__ == "__main__":
    main()
