import os
import numpy as np
from scipy.io import wavfile

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "wav")
os.makedirs(OUT, exist_ok=True)

NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def midi(name):
    base = NOTE[name[0]]
    rest = name[1:]
    if rest.startswith("#"):
        base += 1
        rest = rest[1:]
    elif rest.startswith("b"):
        base -= 1
        rest = rest[1:]
    return 12 * (int(rest) + 1) + base


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def parse(bars, steps_per_bar=16):
    events = []
    step = 0
    for index, bar in enumerate(bars):
        total = 0
        for token in bar.split():
            name, dur = token.split(":")
            dur = int(dur)
            if name != "-":
                events.append((step + total, dur, midi(name)))
            total += dur
        if total != steps_per_bar:
            raise ValueError(f"mesure {index + 1} : {total} pas au lieu de {steps_per_bar} ({bar})")
        step += steps_per_bar
    return events


CHORDS = {
    "C": ["C", "E", "G"], "Cm": ["C", "Eb", "G"], "D": ["D", "F#", "A"], "Dm": ["D", "F", "A"],
    "E": ["E", "G#", "B"], "Em": ["E", "G", "B"], "F": ["F", "A", "C"], "G": ["G", "B", "D"],
    "Gm": ["G", "Bb", "D"], "A": ["A", "C#", "E"], "Am": ["A", "C", "E"], "Bb": ["Bb", "D", "F"],
    "B": ["B", "D#", "F#"], "Asus": ["A", "D", "E"],
}


def chord_midis(chord, octave):
    names = CHORDS[chord]
    root = midi(names[0] + str(octave))
    out = []
    for name in names:
        m = midi(name + str(octave))
        while m < root:
            m += 12
        out.append(m)
    return out


def arp_events(chords, octave, pattern, step_len, bar_steps=16):
    events = []
    for bar, chord in enumerate(chords):
        tones = chord_midis(chord, octave)
        tones = tones + [tones[0] + 12]
        for k in range(bar_steps // step_len):
            events.append((bar * bar_steps + k * step_len, step_len, tones[pattern[k % len(pattern)]]))
    return events


def bass_events(chords, octave, pattern):
    events = []
    for bar, chord in enumerate(chords):
        tones = chord_midis(chord, octave)
        root, fifth = tones[0], tones[2]
        pos = 0
        for kind, dur in pattern:
            m = {"R": root, "O": root + 12, "5": fifth, "L": fifth - 12}[kind]
            events.append((bar * 16 + pos, dur, m))
            pos += dur
    return events


def frames_quantize(env, levels=15):
    frame = SR // 60
    n = len(env)
    held = env[(np.arange(n) // frame) * frame]
    return np.floor(held * levels + 0.5) / levels


def place(buf, sig, pos, wrap):
    n = len(buf)
    if wrap:
        idx = (np.arange(len(sig)) + pos) % n
        np.add.at(buf, idx, sig)
    else:
        end = min(n, pos + len(sig))
        if end > pos:
            buf[pos:end] += sig[: end - pos]


def pulse_voice(buf, events, step_sec, duty, attack_level=1.0, sustain=0.6, decay=0.15, release=0.04,
                vibrato=0.0, gap=0.92, wrap=False, transpose=0):
    for start, dur, m in events:
        length = dur * step_sec * gap
        n = int(SR * length)
        tail = int(SR * release)
        t = np.arange(n + tail) / SR
        env = sustain + (attack_level - sustain) * np.exp(-t / max(decay, 1e-3))
        env[n:] *= np.linspace(1, 0, tail)
        env = frames_quantize(env)
        f = hz(m + transpose)
        if vibrato > 0:
            ramp = np.clip((t - 0.18) / 0.15, 0, 1)
            f = f * 2 ** (vibrato * ramp * np.sin(2 * np.pi * 5.5 * t) / 12)
            phase = np.cumsum(np.full_like(t, 1.0) * f) / SR
        else:
            phase = f * t
        wave = np.where(np.mod(phase, 1.0) < duty, 1.0, -1.0)
        place(buf, wave * env, int(round(start * step_sec * SR)), wrap)


def triangle_voice(buf, events, step_sec, gap=0.95, wrap=False):
    for start, dur, m in events:
        n = int(SR * dur * step_sec * gap)
        t = np.arange(n) / SR
        phase = np.mod(hz(m) * t, 1.0)
        tri = np.abs(phase * 2 - 1) * 2 - 1
        tri = np.floor((tri + 1) * 7.5 + 0.5) / 7.5 - 1
        edge = min(n, int(SR * 0.004))
        if edge:
            tri[-edge:] *= np.linspace(1, 0, edge)
        place(buf, tri, int(round(start * step_sec * SR)), wrap)


def lfsr(n, period, short, seed=1):
    reg = seed or 1
    out = np.empty(n)
    acc = 0.0
    v = 1.0
    step = SR / period
    for i in range(n):
        acc += 1
        while acc >= step:
            acc -= step
            bit = (reg ^ (reg >> (6 if short else 1))) & 1
            reg = (reg >> 1) | (bit << 14)
            v = 1.0 if reg & 1 else -1.0
        out[i] = v
    return out


def drum_kit():
    k = int(SR * 0.12)
    t = np.arange(k) / SR
    f = 50 + 130 * np.exp(-t * 35)
    kick = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 22) + lfsr(k, 9000, False, 3) * np.exp(-t * 160) * 0.3
    s = int(SR * 0.16)
    t = np.arange(s) / SR
    snare = lfsr(s, 7000, False, 7) * np.exp(-t * 24) * 0.8 + np.sin(2 * np.pi * 190 * t) * np.exp(-t * 40) * 0.4
    h = int(SR * 0.04)
    t = np.arange(h) / SR
    hat = lfsr(h, 22000, True, 11) * np.exp(-t * 110)
    c = int(SR * 0.9)
    t = np.arange(c) / SR
    crash = lfsr(c, 16000, False, 13) * np.exp(-t * 4.5)
    return {"k": kick, "s": snare, "h": hat, "c": crash}


KIT = drum_kit()


def drums(buf, pattern_bars, step_sec, wrap=False, volumes=None):
    volumes = volumes or {"k": 1.0, "s": 0.7, "h": 0.35, "c": 0.6}
    for bar, pattern in enumerate(pattern_bars):
        for lane in pattern.split("|"):
            kind, hits = lane.split("=")
            hits = hits.ljust(16, ".")
            if len(hits) > 16 and set(hits[16:]) == {"."}:
                hits = hits[:16]
            if len(hits) != 16:
                raise ValueError(f"batterie mesure {bar + 1} : « {lane} » fait {len(hits)} pas")
            for step, ch in enumerate(hits):
                if ch == "x":
                    place(buf, KIT[kind] * volumes[kind], int(round((bar * 16 + step) * step_sec * SR)), wrap)
                elif ch == "o":
                    place(buf, KIT[kind] * volumes[kind] * 0.45, int(round((bar * 16 + step) * step_sec * SR)), wrap)


def lowpass(x, cutoff=9000):
    spec = np.fft.rfft(x)
    fr = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= 1 / np.sqrt(1 + (fr / cutoff) ** 4)
    spec *= 1 / np.sqrt(1 + (25 / np.maximum(fr, 1e-6)) ** 4)
    return np.fft.irfft(spec, len(x))


def master(x, rms_db=-17.0, peak_db=-1.0):
    x = lowpass(x)
    x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 10 ** (rms_db / 20)
    ceiling = 10 ** (peak_db / 20)
    return np.tanh(x / ceiling) * ceiling


def fade(x, start=0.005, end=0.3):
    a = int(SR * start)
    b = int(SR * end)
    x = x.copy()
    x[:a] *= np.linspace(0, 1, a)
    x[-b:] *= np.linspace(1, 0, b)
    return x


def save(name, x):
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
    print(name, f"{len(x) / SR:.1f} s")


def song(bpm, bars, loop, tail_sec=2.0):
    step_sec = 60 / bpm / 4
    length = bars * 16 * step_sec
    n = int(round(length * SR)) + (0 if loop else int(tail_sec * SR))
    return step_sec, n


def title_hall():
    bpm = 96
    chords = ["Dm", "Dm", "Bb", "C", "Dm", "Dm", "Gm", "A", "Bb", "C", "Dm", "Am", "Bb", "C", "A", "A"]
    lead = [
        "D5:6 E5:2 F5:4 E5:2 D5:2", "A4:12 -:4", "Bb4:4 D5:4 F5:6 E5:2", "E5:4 D5:2 C5:2 E5:8",
        "D5:6 E5:2 F5:4 G5:2 A5:2", "A5:8 G5:4 F5:4", "G5:4 F5:2 E5:2 D5:4 Bb4:4", "C#5:8 E5:4 A4:4",
        "F5:6 G5:2 F5:4 D5:4", "E5:6 F5:2 E5:4 C5:4", "D5:4 F5:4 A5:4 D6:4", "C6:6 A5:2 E5:8",
        "F5:6 D5:2 Bb4:4 D5:4", "E5:6 C5:2 G5:4 E5:4", "C#5:4 E5:4 A5:8", "G5:4 F5:4 E5:4 C#5:4",
    ]
    step, n = song(bpm, 16, True)
    p1, p2, tr, nz = (np.zeros(n) for _ in range(4))
    pulse_voice(p1, parse(lead), step, 0.25, vibrato=0.25, wrap=True)
    pulse_voice(p2, arp_events(chords, 4, [0, 1, 2, 3, 2, 1], 1), step, 0.125, sustain=0.3, decay=0.06, gap=0.8, wrap=True)
    triangle_voice(tr, bass_events(chords, 2, [("R", 3), ("R", 1), ("O", 2), ("R", 2), ("R", 3), ("R", 1), ("5", 4)]), step, wrap=True)
    drums(nz, ["k=x.......x.......|h=....o.......o..."] * 16, step, wrap=True)
    return master(p1 * 0.24 + p2 * 0.11 + tr * 0.32 + nz * 0.14)


def title_dungeon():
    bpm = 76
    chords = ["Am", "Am", "F", "E", "Am", "Dm", "E", "E", "Am", "G", "F", "E", "Dm", "Am", "E", "E"]
    lead = [
        "E5:12 -:4", "C6:4 B5:4 A5:8", "F5:8 A5:4 C6:4", "B5:12 G#5:4",
        "A5:8 E5:8", "F5:4 E5:4 D5:8", "E5:4 F5:4 G#5:4 B5:4", "G#5:8 E5:8",
        "A4:4 C5:4 E5:8", "D5:4 B4:4 G4:8", "A4:4 C5:4 F5:8", "E5:6 D5:2 B4:8",
        "D5:4 F5:4 A5:8", "C6:4 B5:2 A5:2 E5:8", "G#5:8 B5:8", "E6:8 -:8",
    ]
    step, n = song(bpm, 16, True)
    p1, echo, p2, tr, nz = (np.zeros(n) for _ in range(5))
    events = parse(lead)
    pulse_voice(p1, events, step, 0.5, sustain=0.55, decay=0.3, vibrato=0.3, wrap=True)
    pulse_voice(echo, [(s + 3, d, m) for s, d, m in events], step, 0.5, sustain=0.55, decay=0.3, wrap=True)
    pulse_voice(p2, arp_events(chords, 4, [0, 2, 1, 2], 2), step, 0.25, sustain=0.25, decay=0.1, gap=0.7, wrap=True)
    triangle_voice(tr, bass_events(chords, 2, [("R", 8), ("L", 8)]), step, wrap=True)
    drums(nz, ["k=x..o..........|h=........o......."] * 16, step, wrap=True)
    return master(p1 * 0.22 + echo * 0.08 + p2 * 0.10 + tr * 0.34 + nz * 0.12)


def title_adventure():
    bpm = 132
    chords = ["Em", "C", "D", "B", "Em", "C", "Am", "B", "C", "D", "Em", "Em", "C", "D", "B", "B"]
    lead = [
        "E5:3 E5:1 G5:2 B5:2 A5:4 G5:4", "E5:3 E5:1 G5:2 C6:2 B5:4 G5:4", "F#5:3 F#5:1 A5:2 D6:2 C6:4 A5:4", "B5:6 A5:2 F#5:4 D#5:4",
        "E5:3 E5:1 G5:2 B5:2 E6:4 D6:4", "C6:4 B5:2 A5:2 G5:4 E5:4", "A5:4 C6:4 B5:2 A5:2 G5:2 F#5:2", "F#5:8 D#5:4 B4:4",
        "G5:6 A5:2 G5:4 E5:4", "F#5:6 G5:2 A5:4 D5:4", "B5:8 G5:4 E5:4", "E5:2 F#5:2 G5:2 A5:2 B5:4 D6:4",
        "E6:6 D6:2 C6:4 G5:4", "D6:6 C6:2 A5:4 F#5:4", "B5:4 A5:4 G5:4 F#5:4", "D#5:4 F#5:4 B5:8",
    ]
    step, n = song(bpm, 16, True)
    p1, p2, tr, nz = (np.zeros(n) for _ in range(4))
    pulse_voice(p1, parse(lead), step, 0.25, vibrato=0.2, wrap=True)
    pulse_voice(p2, arp_events(chords, 4, [0, 1, 2, 1], 1), step, 0.125, sustain=0.3, decay=0.05, gap=0.75, wrap=True)
    triangle_voice(tr, bass_events(chords, 2, [("R", 2), ("O", 2)] * 4), step, wrap=True)
    beat = "k=x.....x.x.....|s=....x.......x...|h=x.o.x.o.x.o.x.o."
    fill = "k=x.....x.x.....|s=....x.......xxxx|h=x.o.x.o.x.o....."
    drums(nz, [beat] * 7 + [fill] + [beat] * 7 + [fill], step, wrap=True)
    return master(p1 * 0.22 + p2 * 0.10 + tr * 0.30 + nz * 0.16)


def finale_fanfare():
    bpm = 120
    chords = ["C", "F", "G", "C", "Am", "G", "C", "C"]
    lead = [
        "G4:2 C5:2 E5:2 G5:6 E5:2 G5:2", "A5:4 F5:4 C6:8", "B5:2 A5:2 G5:4 D5:2 E5:2 F5:4", "E5:2 G5:2 C6:12",
        "A5:4 G5:4 F5:4 E5:4", "D5:2 E5:2 F5:2 G5:2 B5:4 D6:4", "C6:16", "-:16",
    ]
    harmony = [
        "E4:2 G4:2 C5:2 E5:6 C5:2 E5:2", "F5:4 C5:4 A5:8", "G5:2 F5:2 D5:4 B4:2 C5:2 D5:4", "C5:2 E5:2 G5:12",
        "E5:4 E5:4 C5:4 C5:4", "B4:2 C5:2 D5:2 E5:2 G5:4 B5:4", "G5:16", "-:16",
    ]
    step, n = song(bpm, 8, False, 0.5)
    p1, p2, tr, nz = (np.zeros(n) for _ in range(4))
    pulse_voice(p1, parse(lead), step, 0.25, sustain=0.7, vibrato=0.25)
    pulse_voice(p2, parse(harmony), step, 0.5, sustain=0.5)
    triangle_voice(tr, bass_events(chords[:7], 2, [("R", 4), ("5", 4), ("R", 4), ("O", 4)]), step)
    triangle_voice(tr, [(7 * 16, 12, midi("C2"))], step)
    beat = "k=x.......x.....|s=....x.......x..|h=x.x.x.x.x.x.x.x."
    drums(nz, [beat] * 5 + ["k=x.......x.....|s=....x...xxxxxxxx|h=x.x.x.x........."] + ["c=x...............|k=x..............."] + ["k=................"], step)
    out = p1 * 0.24 + p2 * 0.13 + tr * 0.30 + nz * 0.15
    return master(out)


def finale_triumph():
    bpm = 112
    chords = ["G", "D", "Em", "C", "G", "D", "C", "D", "G", "B", "Em", "C", "Am", "D", "G", "G"]
    lead = [
        "D5:4 G5:4 B5:6 A5:2", "A5:4 F#5:4 D5:8", "E5:4 G5:4 B5:4 E6:4", "D6:6 C6:2 B5:4 G5:4",
        "B5:4 D6:4 G6:6 F#6:2", "E6:4 D6:4 A5:8", "C6:4 B5:2 A5:2 G5:4 E5:4", "F#5:6 G5:2 A5:8",
        "G5:4 B5:4 D6:8", "D#6:4 B5:4 F#5:8", "G5:4 B5:4 E6:8", "E6:6 D6:2 C6:4 G5:4",
        "A5:4 C6:4 E6:4 C6:4", "D6:4 C6:2 B5:2 A5:4 F#5:4", "G5:16", "G5:4 -:12",
    ]
    step, n = song(bpm, 16, False, 1.5)
    p1, p2, tr, nz = (np.zeros(n) for _ in range(4))
    pulse_voice(p1, parse(lead), step, 0.25, sustain=0.65, vibrato=0.25)
    pulse_voice(p2, arp_events(chords[:15], 4, [0, 1, 2, 3, 2, 1, 0, 1], 2), step, 0.125, sustain=0.3, decay=0.08, gap=0.8)
    triangle_voice(tr, bass_events(chords[:15], 2, [("R", 4), ("5", 4), ("O", 4), ("5", 4)]), step)
    triangle_voice(tr, [(15 * 16, 4, midi("G2"))], step)
    beat = "k=x.......x.x...|s=....x.......x..|h=x.o.x.o.x.o.x.o."
    roll = "k=x.......x.....|s=....x...x.xxxxxx|h=x.o.x.o........."
    drums(nz, [beat] * 7 + [roll] + [beat] * 5 + [roll] + ["c=x...............|k=x..............."] + ["c=x...............|k=x..............."], step)
    return master(p1 * 0.24 + p2 * 0.10 + tr * 0.30 + nz * 0.15)


def finale_daylight():
    bpm = 88
    chords = ["Dm", "Bb", "Gm", "A", "D", "G", "A", "D", "G", "A", "D"]
    lead = [
        "D5:6 E5:2 F5:4 E5:2 D5:2", "A4:8 Bb4:4 D5:4", "G4:4 Bb4:4 D5:6 C5:2", "C#5:8 E5:8",
        "D5:6 E5:2 F#5:4 E5:2 D5:2", "B4:4 D5:4 G5:8", "A5:4 G5:2 F#5:2 E5:4 C#5:4", "D5:4 F#5:4 A5:8",
        "B5:6 A5:2 G5:4 D5:4", "C#6:6 B5:2 A5:8", "D6:16",
    ]
    step, n = song(bpm, 11, False, 2.5)
    p1, p2, tr, nz = (np.zeros(n) for _ in range(4))
    pulse_voice(p1, parse(lead), step, 0.25, sustain=0.6, vibrato=0.3)
    arp = arp_events(chords[:4], 4, [0, 1, 2, 1], 2) + [(s + 4 * 16, d, m) for s, d, m in arp_events(chords[4:], 4, [0, 1, 2, 3, 2, 1, 0, 1], 1)]
    pulse_voice(p2, arp, step, 0.125, sustain=0.3, decay=0.07, gap=0.8)
    triangle_voice(tr, bass_events(chords[:10], 2, [("R", 8), ("5", 8)]), step)
    triangle_voice(tr, [(10 * 16, 16, midi("D2"))], step)
    light = "k=x.......x.....|h=....o.......o..."
    full = "k=x.......x.x...|s=....x.......x..|h=x.o.x.o.x.o.x.o."
    drums(nz, ["k=................"] * 4 + [light] * 2 + [full] * 3 + ["k=x.......x.....|s=....x...x.x.xxxx|h=x.o.x.o........."] + ["c=x...............|k=x..............."], step)
    return master(p1 * 0.24 + p2 * 0.10 + tr * 0.30 + nz * 0.14)


if __name__ == "__main__":
    save("titre 1 - hall de zorgus (boucle)", title_hall())
    save("titre 2 - les cachots (boucle)", title_dungeon())
    save("titre 3 - mob en route (boucle)", title_adventure())
    save("victoire 1 - fanfare", fade(finale_fanfare()))
    save("victoire 2 - triomphe du magus", fade(finale_triumph()))
    save("victoire 3 - retour au jour", fade(finale_daylight()))
