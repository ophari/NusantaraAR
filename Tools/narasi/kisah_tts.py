"""Buat audio narasi mode Kisah dari Tools/narasi/kisah.json.

Suara: Microsoft Edge neural TTS (paket `edge-tts`, perlu internet) - default id-ID-GadisNeural (perempuan, Indonesia)
dan en-US-AvaNeural (perempuan, Inggris). Keluaran per bab per bahasa:
    Assets/NusantaraAR/Content/<artifactId>/Story/<artifactId>_<nn>_<key>_<id|en>.mp3
beserta waktu mulai tiap kalimat (untuk subtitle) di Tools/narasi/kisah_cues.json.
Bab yang teks/suaranya tidak berubah dilewati (cache hash). Paksa ulang semua: --force.
Setelah itu jalankan menu Unity "Nusantara AR/Bangun Kisah" (atau Setup Everything) untuk membuat aset ArtifactStory.

Pasang:   python -m pip install --user edge-tts
          (bila muncul "Could not contact DNS servers": python -m pip uninstall -y aiodns)
Jalankan: python Tools/narasi/kisah_tts.py [--force]
"""
import asyncio
import hashlib
import json
import os
import sys

import edge_tts

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
SCRIPT = os.path.join(HERE, "kisah.json")
CUES = os.path.join(HERE, "kisah_cues.json")
TICKS_PER_SECOND = 10_000_000  # offset edge-tts dalam satuan 100 ns


def clip_path(artifact_id, index, key, lang):
    rel = f"Assets/NusantaraAR/Content/{artifact_id}/Story/{artifact_id}_{index:02d}_{key}_{lang}.mp3"
    return rel, os.path.join(PROJECT, rel.replace("/", os.sep))


async def synthesize(text, voice, rate, out_path):
    """Tulis mp3 dan kembalikan [{time, text}] awal tiap kalimat (detik)."""
    tmp = out_path + ".tmp"
    cues = []
    communicate = edge_tts.Communicate(text, voice, rate=rate, boundary="SentenceBoundary")
    with open(tmp, "wb") as f:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                f.write(chunk["data"])
            elif chunk["type"] == "SentenceBoundary":
                cues.append({"time": round(chunk["offset"] / TICKS_PER_SECOND, 3), "text": chunk["text"]})
    os.replace(tmp, out_path)
    return cues


async def main(force):
    with open(SCRIPT, encoding="utf-8") as f:
        script = json.load(f)
    old = {}
    if os.path.exists(CUES) and not force:
        with open(CUES, encoding="utf-8") as f:
            old = {c["file"]: c for c in json.load(f).get("clips", [])}

    rate = script.get("rate", "+0%")
    voices = {"id": script["voiceID"], "en": script["voiceEN"]}
    clips, made, kept = [], 0, 0
    for art in script["artifacts"]:
        for i, ch in enumerate(art["chapters"], start=1):
            for lang, voice in voices.items():
                text = ch[lang].strip()
                rel, path = clip_path(art["artifactId"], i, ch["key"], lang)
                digest = hashlib.sha1(f"{voice}|{rate}|{text}".encode("utf-8")).hexdigest()
                prev = old.get(rel)
                if prev and prev.get("hash") == digest and os.path.exists(path):
                    clips.append(prev)
                    kept += 1
                    continue
                os.makedirs(os.path.dirname(path), exist_ok=True)
                print(f"  {rel}")
                cues = await synthesize(text, voice, rate, path)
                clips.append({"file": rel, "hash": digest, "cues": cues})
                made += 1

    with open(CUES, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"clips": clips}, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"Selesai: {made} dibuat, {kept} tidak berubah. Waktu kalimat: {os.path.relpath(CUES, PROJECT)}")


if __name__ == "__main__":
    asyncio.run(main("--force" in sys.argv))
