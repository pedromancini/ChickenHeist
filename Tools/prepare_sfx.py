# Prepares the recorded sound effects (E:/Jogo3D/audios, files named "<key>.wav_—_<prompt>...wav") for the game:
# Assets/Resources/Sfx/<key>.wav, mono 48 kHz 16 bit (stereo for the night ambience), leading silence cut, short fades,
# peak at -3 dB. Single hits are cut from takes that hold several (the knock, a snore, a step); loops are cross-faded
# so their end runs into their start; the truck's starter, start and stall come from the one starter take plus the
# idling engine. SoundLibrary picks the files up by key.
# Run:  python Tools/prepare_sfx.py [source folder]
import glob, os, sys, wave
import numpy as np

SRC = sys.argv[1] if len(sys.argv) > 1 else 'E:/Jogo3D/audios'
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Sfx')
os.makedirs(OUT, exist_ok=True)
SR = 48000

def load(key):
    path = sorted(glob.glob(os.path.join(SRC, key + '.wav*')))[0]
    w = wave.open(path); n = w.getnframes(); sr = w.getframerate(); ch = w.getnchannels(); sw = w.getsampwidth()
    a = np.frombuffer(w.readframes(n), dtype={2: np.int16, 4: np.int32}[sw]).astype(np.float64) / (2 ** (8 * sw - 1)); w.close()
    a = a.reshape(-1, ch)
    if sr != SR:
        t = np.arange(0, len(a) / sr, 1 / SR); a = np.stack([np.interp(t, np.arange(len(a)) / sr, a[:, c]) for c in range(ch)], 1)
    return a

def mono(a): return a.mean(1)
def frames_db(x, hop=SR // 200):
    x = x if x.ndim == 1 else x.mean(1)
    r = np.array([np.sqrt(np.mean(x[i:i + hop] ** 2)) for i in range(0, max(1, len(x) - hop), hop)]); return 20 * np.log10(r + 1e-9), hop
def active(x, below=35):
    db, hop = frames_db(x); idx = np.where(db > db.max() - below)[0]
    return idx[0] * hop, min(len(x), (idx[-1] + 1) * hop)
def onsets(x):
    db, hop = frames_db(x); top = db.max(); out = []; low = True
    for i, v in enumerate(db):
        if low and v > top - 12: out.append(i * hop); low = False
        elif v < top - 25: low = True
    return out
def fade(x, fin=.003, fout=.03):
    x = x.copy(); a = int(fin * SR); b = int(fout * SR)
    if a: x[:a] *= np.linspace(0, 1, a)[:, None] if x.ndim > 1 else np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)[:, None] if x.ndim > 1 else np.linspace(1, 0, b)
    return x
def normalize(x, peak_db=-3): p = np.abs(x).max(); return x if p == 0 else x * (10 ** (peak_db / 20) / p)
def loop(x, cross=.15):
    # the tail fades into the head: the result repeats without a click
    c = int(cross * SR); body = x[:-c].copy(); tail = x[-c:]
    r = np.linspace(0, 1, c); r = r[:, None] if x.ndim > 1 else r
    body[:c] = body[:c] * r + tail * (1 - r); return body
def save(key, x):
    x = np.clip(x, -1, 1); data = (x * 32767).astype(np.int16)
    w = wave.open(os.path.join(OUT, key + '.wav'), 'wb'); w.setnchannels(1 if x.ndim == 1 else x.shape[1]); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(data.tobytes()); w.close(); print('%-16s %5.2f s' % (key, len(x) / SR))
def one_shot(key, longest=None, below=35, src=None):
    x = mono(load(src or key)); s, e = active(x, below); x = x[s:e]
    if longest: x = x[:int(longest * SR)]
    save(key, normalize(fade(x, fout=min(.08, len(x) / SR / 4))))
def first_hit(key, src=None, longest=.35):
    x = mono(load(src or key)); on = onsets(x); s = max(0, on[0] - int(.004 * SR))
    e = on[1] - int(.01 * SR) if len(on) > 1 else len(x); e = min(e, s + int(longest * SR))
    save(key, normalize(fade(x[s:e], fout=.04)))

for key in ['bell', 'cage_lid', 'chicken', 'chicken_scared', 'chicken_pickup', 'cow', 'creak', 'gate_metal', 'latch_click',
            'latch_slip', 'porch_step', 'rooster_morning', 'shotgun_blast', 'shotgun_reload', 'spray', 'step', 'truck_door',
            'wake', 'wire_rattle', 'box_open']:
    one_shot(key)
one_shot('door', longest=1.4); one_shot('door_creak', longest=2.6); one_shot('camera_alert', longest=1.0)
first_hit('knock', longest=.3); first_hit('sleep', longest=2.2); first_hit('wood', longest=.45)
for key in ['engine', 'latch_scrape', 'chicken_walk']:
    x = mono(load(key)); s, e = active(x, 45); save(key, normalize(loop(x[s:e])))
# the night: grains of the one-second take laid over each other into a 20 s stereo loop
x = load('night_ambience'); rng = np.random.default_rng(3); n = 20 * SR; g = SR; out = np.zeros((n, 2)); win = np.hanning(g)
for start in range(0, n, g // 2):
    for c in range(2):
        off = rng.integers(0, len(x)); grain = np.roll(x[:, c % x.shape[1]], -off)[:g]
        if len(grain) < g: grain = np.pad(grain, (0, g - len(grain)))
        seg = grain * win * rng.uniform(.8, 1.0); end = min(n, start + g); out[start:end, c] += seg[:end - start]
save('night_ambience', normalize(loop(out, .5), -6))
# the truck: the starter take is the cranking; the start is cranking that catches into the idle; the stall is the idle
# slowing down and dying
crank = mono(load('starter')); s, e = active(crank); crank = crank[s:e]
idle = mono(load('engine')); s, e = active(idle, 45); idle = idle[s:e]
save('starter', normalize(fade(crank)))
c = int(.12 * SR); catch = np.concatenate([crank[:int(.75 * SR)], np.tile(idle, 2)[:int(1.1 * SR)]])
j = int(.75 * SR); r = np.linspace(0, 1, c); catch[j - c:j] = crank[j - c:j] * (1 - r) + idle[:c] * r
save('start', normalize(fade(catch, fout=.25)))
long_idle = np.tile(idle, 3); speed = np.linspace(1.0, .45, int(1.2 * SR)); pos = np.cumsum(speed); pos = pos[pos < len(long_idle) - 1]
dying = np.interp(pos, np.arange(len(long_idle)), long_idle) * np.linspace(1, 0, len(pos)) ** 1.5
save('stall', normalize(fade(dying, fout=.1)))
