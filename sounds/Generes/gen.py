import numpy as np
from scipy.io import wavfile
import os

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "wav")
os.makedirs(OUT, exist_ok=True)


def circ_filter(x, lo=None, hi=None):
    f = np.fft.rfft(x)
    fr = np.fft.rfftfreq(len(x), 1 / SR)
    g = np.ones_like(fr)
    if hi:
        g *= 1 / np.sqrt(1 + (fr / hi) ** 4)
    if lo:
        g *= 1 / np.sqrt(1 + (lo / np.maximum(fr, 1e-6)) ** 4)
    return np.fft.irfft(f * g, len(x))


def lfsr_noise(n, period, short=False, seed=1):
    reg = seed & 0x7FFF or 1
    out = np.empty(n)
    v = 1.0
    acc = 0.0
    step = SR / period
    for i in range(n):
        acc += 1
        while acc >= step:
            acc -= step
            tap = 6 if short else 1
            bit = (reg ^ (reg >> tap)) & 1
            reg = (reg >> 1) | (bit << 14)
            v = 1.0 if reg & 1 else -1.0
        out[i] = v
    return out


def add_wrapped(buf, ev, pos):
    n = len(buf)
    idx = (np.arange(len(ev)) + pos) % n
    np.add.at(buf, idx, ev)


def crackle(rng, length_ms, bright):
    n = int(SR * length_ms / 1000)
    env = np.exp(-np.linspace(0, 6, n))
    e = rng.standard_normal(n) * env
    return circ_filter(np.pad(e, (0, 256)), lo=bright * 0.4, hi=bright)[: n + 256]


def norm(x, peak=0.7):
    return x / (np.max(np.abs(x)) + 1e-9) * peak


def loud(x, rms=0.13):
    x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * rms
    return np.tanh(x * 1.6) / 1.6


def save(name, x):
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))


def torch_soft(seed=11, dur=4.0):
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    bed = circ_filter(rng.standard_normal(n), lo=60, hi=500) * 0.35
    t = np.arange(n) / SR
    bed *= 0.8 + 0.2 * np.sin(2 * np.pi * t / dur * 3)
    cr = np.zeros(n)
    for _ in range(int(dur * 7)):
        add_wrapped(cr, crackle(rng, rng.uniform(4, 14), rng.uniform(2500, 6000)) * rng.uniform(0.2, 1), rng.integers(n))
    return loud(bed + cr * 0.9, 0.11)


def brazier(seed=23, dur=4.0):
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    t = np.arange(n) / SR
    roar = circ_filter(rng.standard_normal(n), lo=40, hi=900)
    roar *= 0.7 + 0.3 * np.sin(2 * np.pi * t / dur * 2) * np.sin(2 * np.pi * t / dur * 5)
    cr = np.zeros(n)
    for _ in range(int(dur * 18)):
        add_wrapped(cr, crackle(rng, rng.uniform(3, 20), rng.uniform(1800, 7000)) * rng.uniform(0.15, 1), rng.integers(n))
    for _ in range(int(dur * 2)):
        pos = rng.integers(n)
        burst = rng.integers(3, 7)
        for k in range(burst):
            add_wrapped(cr, crackle(rng, 5, 5000) * 0.8, pos + k * int(SR * rng.uniform(0.015, 0.04)))
    return loud(roar * 0.45 + cr, 0.14)


def torch_8bit(seed=37, dur=4.0):
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    frame = SR // 60
    frames = n // frame
    out = np.zeros(n)
    base = lfsr_noise(n, 2200, seed=seed)
    pop = lfsr_noise(n, 9000, short=True, seed=seed + 5)
    vol = np.zeros(frames)
    popvol = np.zeros(frames)
    walk = 6.0
    for f in range(frames):
        walk += rng.uniform(-1, 1)
        walk = min(8, max(4, walk))
        vol[f] = round(walk) / 15
    for f in range(frames - 30, frames):
        a = (f - (frames - 30)) / 30
        vol[f] = round(((1 - a) * vol[f] + a * vol[0]) * 15) / 15
    for _ in range(int(dur * 6)):
        f0 = rng.integers(frames)
        lvl = rng.integers(9, 15)
        for k in range(4):
            popvol[(f0 + k) % frames] = max(popvol[(f0 + k) % frames], max(0, lvl - k * 4) / 15)
    for f in range(frames):
        s = slice(f * frame, (f + 1) * frame)
        out[s] = base[s] * vol[f] * 0.35 + pop[s] * popvol[f] * 0.6
    out[frames * frame:] = 0
    out = out[: frames * frame]
    return norm(circ_filter(out, hi=7000), 0.6)


def bubbles(seed=5):
    rng = np.random.default_rng(seed)
    dur = 0.9
    n = int(SR * dur)
    out = np.zeros(n)
    t0 = 0.0
    for k in range(6):
        ln = rng.uniform(0.05, 0.09)
        m = int(SR * ln)
        tt = np.arange(m) / SR
        f = rng.uniform(300, 520) * (1 + 3.5 * tt / ln)
        ph = 2 * np.pi * np.cumsum(f) / SR
        sq = np.sign(np.sin(ph))
        env = np.sin(np.pi * tt / ln) ** 2
        p = int(SR * t0)
        if p + m < n:
            out[p:p + m] += sq * env * (0.8 - k * 0.08)
        t0 += ln + rng.uniform(0.04, 0.09)
    return norm(circ_filter(out, hi=4000), 0.6)


def blob_squish(seed=9):
    dur = 0.55
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = 180 * (1 - 0.55 * t / dur) * (1 + 0.25 * np.sin(2 * np.pi * 14 * t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    tri = 2 / np.pi * np.arcsin(np.sin(ph))
    rng = np.random.default_rng(seed)
    nz = circ_filter(rng.standard_normal(n), lo=200, hi=1200)
    env = np.minimum(1, t / 0.02) * np.exp(-t * 5)
    return norm((tri * 0.8 + nz * 0.25) * env, 0.65)


def metal_click(rng, pitch):
    n = int(SR * 0.035)
    t = np.arange(n) / SR
    tone = sum(np.sin(2 * np.pi * pitch * k * t + rng.uniform(0, 6)) / k for k in (1, 2.7, 4.1))
    noise = rng.standard_normal(n) * 0.6
    return (tone + noise) * np.exp(-t * 140)


def thud(rng, dur=0.35):
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = 95 * np.exp(-t * 6) + 40
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
    hit = circ_filter(np.pad(rng.standard_normal(int(SR * 0.04)) * np.exp(-np.arange(int(SR * 0.04)) / SR * 70), (0, n - int(SR * 0.04))), lo=300, hi=3500)
    ring = metal_click(rng, 420)
    ring = np.pad(ring, (0, n - len(ring))) * 0.5
    return body * 1.0 + hit * 0.7 + ring


def portcullis_up(seed=41, clicks=9, dur=0.75):
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    out = np.zeros(n)
    rumble = circ_filter(rng.standard_normal(n), lo=60, hi=400) * 0.25
    pos = 0.02
    for k in range(clicks):
        c = metal_click(rng, 900 + k * 25 + rng.uniform(-40, 40)) * rng.uniform(0.6, 1)
        p = int(SR * pos)
        if p + len(c) < n:
            out[p:p + len(c)] += c
        pos += 0.075 + rng.uniform(-0.01, 0.01)
    env = np.minimum(1, np.arange(n) / (SR * 0.05)) * np.minimum(1, (n - np.arange(n)) / (SR * 0.08))
    return loud((out + rumble) * env, 0.16)


def portcullis_down(seed=43, clicks=5):
    rng = np.random.default_rng(seed)
    dur = 0.25 + 0.6
    n = int(SR * dur)
    out = np.zeros(n)
    pos = 0.0
    for k in range(clicks):
        c = metal_click(rng, 1100 - k * 40) * (0.5 + k * 0.08)
        p = int(SR * pos)
        out[p:p + len(c)] += c
        pos += 0.045
    th = thud(rng)
    p = int(SR * (pos + 0.02))
    out[p:p + len(th)] += th[: n - p] * 1.6
    return loud(out, 0.17)


def portcullis_8bit(seed=47, up=True):
    rng = np.random.default_rng(seed)
    dur = 0.6
    n = int(SR * dur)
    out = np.zeros(n)
    frame = SR // 60
    steps = 8
    for k in range(steps):
        p = int(SR * (0.02 + k * 0.065))
        m = frame * 2
        t = np.arange(m) / SR
        f = (330 + (k * 30 if up else -k * 25))
        sq = np.sign(np.sin(2 * np.pi * f * t)) * np.exp(-t * 60)
        nz = lfsr_noise(m, 12000, short=True, seed=seed + k) * np.exp(-t * 90) * 0.7
        if p + m < n:
            out[p:p + m] += sq * 0.5 + nz
    if not up:
        th = lfsr_noise(int(SR * 0.2), 1500, seed=seed) * np.exp(-np.arange(int(SR * 0.2)) / SR * 18)
        p = n - len(th) - 1
        out[p:p + len(th)] += th
    return loud(circ_filter(out, hi=8000), 0.17)


save("herse monte", portcullis_up())
save("herse tombe", portcullis_down())
save("herse 8 bits monte", portcullis_8bit(up=True))
save("herse 8 bits tombe", portcullis_8bit(up=False))
save("herse cliquetis court", portcullis_up(seed=53, clicks=5, dur=0.45))
def creak(seed=61, dur=0.7, rising=True):
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    t = np.arange(n) / SR
    base = 140 + 90 * (t / dur if rising else 1 - t / dur)
    jitter = 1 + 0.08 * circ_filter(rng.standard_normal(n), hi=30) * 8
    f = base * jitter
    ph = 2 * np.pi * np.cumsum(f) / SR
    pulses = (np.mod(ph, 2 * np.pi) < 0.35).astype(float)
    tone = circ_filter(pulses - pulses.mean(), lo=300, hi=2600)
    env = np.minimum(1, t / 0.08) * np.minimum(1, (dur - t) / 0.12) * (0.7 + 0.3 * np.sin(2 * np.pi * 3 * t))
    return tone * env


def wood_knock(rng, dur=0.3, pitch=180):
    n = int(SR * dur)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * pitch * t) * np.exp(-t * 28) + 0.5 * np.sin(2 * np.pi * pitch * 2.3 * t) * np.exp(-t * 45)
    m = int(SR * 0.012)
    click = np.pad(circ_filter(rng.standard_normal(m), lo=800, hi=5000) * np.exp(-np.arange(m) / SR * 300), (0, n - m))
    return body + click * 0.6


def door_open(seed=63):
    return loud(creak(seed, 0.7, True), 0.14)


def door_close(seed=65):
    rng = np.random.default_rng(seed)
    c = creak(seed, 0.35, False) * 0.6
    k = wood_knock(rng, 0.35, 150)
    out = np.concatenate([c, np.zeros(int(SR * 0.4))])
    p = int(SR * 0.33)
    out[p:p + len(k)] += k * 1.4
    return loud(out, 0.16)


def door_8bit(seed=67, opening=True):
    dur = 0.45
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = np.where(opening, 180 + 220 * t / dur, 400 - 260 * t / dur)
    f = np.round(f / 20) * 20
    sq = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.4
    env = np.minimum(1, t / 0.01) * np.exp(-t * 3)
    out = sq * env
    if not opening:
        m = int(SR * 0.12)
        nz = lfsr_noise(m, 3000, seed=seed) * np.exp(-np.arange(m) / SR * 30)
        out[n - m:] += nz
    return loud(circ_filter(out, hi=7000), 0.16)


save("porte bois ouvre", door_open())
save("porte bois ferme", door_close())
save("porte 8 bits ouvre", door_8bit(opening=True))
save("porte 8 bits ferme", door_8bit(opening=False))
save("torche douce (boucle)", torch_soft())
save("brasero (boucle)", brazier())
save("torche 8 bits (boucle)", torch_8bit())
save("torche douce variante (boucle)", torch_soft(seed=77))
save("potion glouglou", bubbles())
save("blob mutation squish", blob_squish())
print(sorted(os.listdir(OUT)))
