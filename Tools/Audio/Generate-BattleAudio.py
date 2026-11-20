"""Original Paper Defense SFX. Python standard library only; no recorded samples.

Run from any directory. Writes deterministic 44.1 kHz mono PCM16 WAV files and
an auditable metrics manifest. Short damped noise/resonators suggest paper,
cardboard, springs and small wooden mechanisms. Source licensed with project.
"""
import json
import math
from pathlib import Path
import random
import struct
import wave

RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
DESTINATION = ROOT / "Assets/PaperDefense/Resources/PaperDefenseAudio"
REPORT = ROOT / "docs/validation/2026-11-13-battle-feedback/audio-assets.json"


def make_clip(name, duration, seed, events):
    rng = random.Random(seed)
    samples = [0.0] * round(duration * RATE)

    def add(start, length, gain, kind, tone=400):
        low = previous = 0.0
        phase = rng.random() * math.tau
        for i in range(round(length * RATE)):
            index = round(start * RATE) + i
            if index >= len(samples):
                break
            t = i / RATE
            p = t / length
            n = rng.uniform(-1, 1)
            low += .16 * (n - low)
            high = n - previous
            previous = n
            # Raised-sine edges prevent start/end clicks independent of oscillator.
            edge = min(1., t / .0015) * min(1., (length - t) / .012)
            env = math.exp(-5 * p) * edge
            if kind == "paper":
                grain = .48 + .52 * abs(math.sin(2 * math.pi * (73*t + 19*t*t)))
                value = (.65 * high + .35 * low) * grain * env
            elif kind == "tear":
                grain = .28 + .72 * abs(math.sin(2 * math.pi * 43 * t))
                value = (.50*n + .50*low) * grain * math.sin(math.pi*p)**.7 * edge
            elif kind == "wood":
                value = (.65 * math.sin(2*math.pi*tone*t) + .22 * math.sin(2*math.pi*tone*1.63*t) + .35*low) * env
            elif kind == "spring":
                value = (.45*math.sin(2*math.pi*(tone*t - tone*.85*t*t)) + .22*math.sin(2*math.pi*tone*2.73*t) + .32*high) * env
            elif kind == "pluck":
                value = (math.sin(2*math.pi*tone*t + phase) + .23*math.sin(2*math.pi*tone*2*t) + .09*math.sin(2*math.pi*tone*3*t)) * env
            else:
                raise ValueError(kind)
            samples[index] += gain * value

    for event in events:
        add(*event)
    # Remove DC, reserve headroom across the six-voice pool and fade final 12 ms.
    mean = sum(samples) / len(samples)
    samples = [s - mean for s in samples]
    peak = max(abs(s) for s in samples)
    scale = .48 / max(peak, .001)
    samples = [s * scale * min(1., i / 32, (len(samples)-1-i)/529) for i, s in enumerate(samples)]
    path = DESTINATION / (name + ".wav")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(b"".join(struct.pack("<h", round(s * 32767)) for s in samples))
    rms = math.sqrt(sum(s*s for s in samples) / len(samples))
    return {"cue": name, "seconds": round(len(samples)/RATE, 3), "channels": 1,
            "sampleRate": RATE, "pcmBits": 16, "bytes": path.stat().st_size,
            "peak": round(max(abs(s) for s in samples), 6), "rms": round(rms, 6),
            "dc": round(sum(samples)/len(samples), 7)}


# Events: onset seconds, duration, level, material/resonator, frequency Hz.
CLIPS = [
    ("ShotSingle", .14, [(0,.10,.6,"wood",510),(.012,.08,.28,"spring",1400),(.025,.09,.2,"paper",0)]),
    ("ShotLine", .18, [(0,.07,.5,"wood",720),(.005,.13,.55,"spring",2300),(.04,.11,.22,"paper",0)]),
    ("ShotMortar", .25, [(0,.23,.9,"wood",110),(.008,.12,.25,"paper",0),(.015,.18,.2,"spring",265)]),
    ("ShotCone", .18, [(0,.12,.36,"wood",260),(0,.16,.6,"paper",0),(.03,.12,.3,"paper",0)]),
    ("ShotRicochet", .24, [(0,.08,.45,"wood",820),(.005,.21,.5,"spring",1800),(.04,.12,.12,"paper",0)]),
    ("ShotRadial", .21, [(0,.17,.7,"wood",185),(.014,.15,.5,"paper",0),(.048,.11,.27,"wood",330)]),
    ("ShotStapler", .19, [(0,.075,.65,"spring",740),(0,.06,.4,"wood",960),(.048,.075,.65,"wood",1450),(.059,.11,.18,"paper",0)]),
    ("MortarImpact", .40, [(0,.29,.8,"wood",81),(.006,.30,.55,"tear",0),(.07,.23,.2,"paper",0)]),
    ("BaseHit", .30, [(0,.26,.85,"wood",138),(.012,.20,.35,"paper",0),(.038,.15,.23,"wood",220)]),
    ("StructureDestroyed", .58, [(0,.27,.6,"wood",120),(.01,.45,.75,"tear",0),(.14,.26,.4,"paper",0),(.34,.19,.25,"wood",280)]),
    ("WaveStarted", .65, [(0,.38,.35,"tear",0),(.24,.19,.3,"wood",640),(.39,.22,.36,"wood",860)]),
    ("Victory", 1.12, [(0,.31,.13,"paper",0),(0,.45,.65,"pluck",523.25),(.19,.5,.6,"pluck",659.25),(.4,.65,.65,"pluck",783.99),(.41,.18,.15,"wood",392)]),
    ("Defeat", .95, [(0,.28,.2,"paper",0),(0,.49,.65,"pluck",293.66),(.29,.60,.62,"pluck",220),(.34,.25,.15,"wood",110)]),
    ("StructureCompleted", .32, [(0,.16,.3,"paper",0),(.09,.16,.7,"wood",410),(.105,.17,.26,"spring",1200)]),
    ("ModernizationCompleted", .48, [(0,.09,.5,"wood",850),(.055,.13,.22,"paper",0),(.10,.34,.45,"pluck",1046.5)]),
    ("CastleAlarm", .72, [(0,.27,.6,"pluck",880),(.035,.19,.2,"wood",440),(.30,.32,.6,"pluck",880),(.34,.19,.2,"wood",440)]),
]

if __name__ == "__main__":
    DESTINATION.mkdir(parents=True, exist_ok=True)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    manifest = [make_clip(name, duration, 76101+i, events) for i, (name, duration, events) in enumerate(CLIPS)]
    REPORT.write_text(json.dumps({"origin": "Original procedural synthesis; no external samples",
                                  "generator": "tools/Audio/Generate-BattleAudio.py",
                                  "totalBytes": sum(c["bytes"] for c in manifest), "clips": manifest}, indent=2)+"\n", encoding="utf-8")
    print(f"Generated {len(manifest)} original clips, {sum(c['bytes'] for c in manifest):,} bytes; peak <= 0.48")
