# Builds the night soundscape from scratch (the one-second recorded take, stretched into a loop, read as a constant
# cicada hiss with clicks): Assets/Resources/Sfx/
#   night_ambience.wav   60 s stereo loop: a breeze that rises and falls through the grass and the trees, and now and
#                        then a far cricket or two; long stretches with nothing but the air
#   night_cricket_1..3   a cricket's bout (a few seconds of chirps, each its own pitch and rhythm), placed around the
#                        player at random by GameAudioMix, with silences between
#   night_owl.wav        an owl hooting far off
#   night_frogs.wav      a few frogs croaking by the water, far off
# 48 kHz 16 bit. Deterministic (fixed seeds).  Run:  python Tools/make_night_ambience.py
import os, wave
import numpy as np
from scipy.signal import butter, sosfilt, sosfiltfilt

SR = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Sfx')


def save(name, x):
    x = np.clip(x, -1, 1); data = (x * 32767).astype(np.int16)
    w = wave.open(os.path.join(OUT, name + '.wav'), 'wb'); w.setnchannels(1 if x.ndim == 1 else x.shape[1]); w.setsampwidth(2)
    w.setframerate(SR); w.writeframes(data.tobytes()); w.close()
    rms = np.sqrt(np.mean(x ** 2)); print('%-20s %5.1f s  peak %.2f  rms %.3f' % (name, len(x) / SR, np.abs(x).max(), rms))


def band(x, lo, hi, order=2):
    if lo is None: sos = butter(order, hi, 'lowpass', fs=SR, output='sos')
    elif hi is None: sos = butter(order, lo, 'highpass', fs=SR, output='sos')
    else: sos = butter(order, [lo, hi], 'bandpass', fs=SR, output='sos')
    return sosfiltfilt(sos, x, axis=0)


def smooth_random(rng, n, seconds):
    # a slowly wandering value in 0..1 (control points every `seconds`, eased between them)
    k = int(np.ceil(n / (seconds * SR))) + 2; pts = rng.uniform(0, 1, k)
    t = np.arange(n) / (seconds * SR); i = t.astype(int); f = t - i; f = f * f * (3 - 2 * f)
    return pts[i] * (1 - f) + pts[i + 1] * f


def room(x, rng, wet=.25, length=.9):
    # a little open-air reflection: a short decaying noise tail, so far sounds sit in the night instead of on top
    n = int(length * SR); ir = rng.normal(0, 1, n) * np.exp(-np.arange(n) / (SR * length / 5)); ir = band(ir, 200, 5000)
    ir /= np.sqrt(np.sum(ir ** 2)); tail = np.convolve(x, ir)[:len(x)]
    return x * (1 - wet) + tail * wet


def fade(x, a=.01, b=.05):
    x = x.copy(); na = int(a * SR); nb = int(b * SR)
    if na: x[:na] *= np.linspace(0, 1, na)
    if nb: x[-nb:] *= np.linspace(1, 0, nb)
    return x


def normalize(x, peak_db): p = np.abs(x).max(); return x * (10 ** (peak_db / 20) / p) if p else x


# --- a cricket: a carrier near 4-5 kHz, chirps of 3-4 pulses, a chirp every half second or so ------------------------
def cricket(rng, seconds, carrier, pulses, rate):
    n = int(seconds * SR); out = np.zeros(n); t0 = rng.uniform(.05, .3)
    while t0 < seconds - .3:
        for p in range(pulses):
            start = int((t0 + p * .034) * SR); m = int(.021 * SR)
            if start + m >= n: break
            tt = np.arange(m) / SR; env = np.sin(np.pi * np.arange(m) / m) ** 2
            f = carrier * (1 - .015 * tt / .021)   # each pulse drops a little in pitch
            ph = 2 * np.pi * np.cumsum(f) / SR
            out[start:start + m] += env * (np.sin(ph) + .12 * np.sin(2 * ph)) * rng.uniform(.75, 1)
        t0 += rate * rng.uniform(.9, 1.12)
    # the bout swells in and fades out instead of starting and stopping dead
    env = np.minimum(1, np.minimum(np.arange(n) / (.8 * SR), (n - np.arange(n)) / (1.2 * SR)))
    return out * env


# --- an owl: soft hoots near 350 Hz, breathy -------------------------------------------------------------------------
def hoot(rng, length, f0):
    m = int(length * SR); tt = np.arange(m) / SR
    f = f0 * (1.02 - .06 * tt / length); ph = 2 * np.pi * np.cumsum(f) / SR
    env = np.minimum(1, tt / .07) * np.exp(-np.maximum(0, tt - length * .45) / (length * .25))
    breath = band(rng.normal(0, 1, m), 250, 700) * .25
    return (np.sin(ph) + .18 * np.sin(2 * ph) + breath) * env


def owl(rng):
    out = np.zeros(int(3.2 * SR))
    for at, length, f0 in [(.1, .42, 360), (1.05, .2, 352), (1.32, .2, 350), (1.6, .5, 344)]:
        s = int(at * SR); h = hoot(rng, length, f0); out[s:s + len(h)] += h
    return room(band(out, None, 1800), rng, .45, 1.4)


# --- frogs by the water: croaks made of quick glottal pulses through a throat resonance ------------------------------
def croak(rng, length, rate, formant):
    m = int(length * SR); src = np.zeros(m); period = int(SR / rate)
    for s in range(0, m, period): src[s:s + 40] += np.hanning(40) * rng.uniform(.7, 1)
    voiced = band(src, formant * .7, formant * 1.4) + .5 * band(src, formant * 2.1, formant * 2.9)
    env = np.sin(np.pi * np.arange(m) / m) ** .7
    return voiced * env


def frogs(rng):
    out = np.zeros(int(5 * SR))
    for k in range(9):
        at = rng.uniform(0, 4.4); length = rng.uniform(.16, .3); s = int(at * SR)
        c = croak(rng, length, rng.uniform(22, 34), rng.choice([420, 520, 650])) * rng.uniform(.4, 1)
        out[s:s + len(c)] += c[:len(out) - s]
    return room(band(out, None, 2500), rng, .5, 1.6)


# --- the breeze: low rumble of moving air, plus leaves stirring when it rises ---------------------------------------
def breeze(rng, n):
    out = np.zeros((n, 2)); gust = .25 + .75 * smooth_random(rng, n, 7) ** 2
    for c in range(2):
        brown = np.cumsum(rng.normal(0, 1, n)); brown -= band(brown, None, 8, 1)
        air = band(brown, 40, 420); air /= np.abs(air).max()
        leaves = band(rng.normal(0, 1, n), 1400, 5200); leaves /= np.abs(leaves).max()
        rustle = smooth_random(rng, n, 1.3)
        out[:, c] = air * (.35 + .65 * gust) + leaves * .22 * gust ** 2 * rustle
    return out


def seamless(x, cross):
    # the end runs into the start: no bump at the loop point
    c = int(cross * SR); body = x[:-c].copy(); r = np.linspace(0, 1, c)[:, None]
    body[:c] = body[:c] * r + x[-c:] * (1 - r); return body


rng = np.random.default_rng(11)
# the bed: a minute of breeze, three far crickets at a few moments, never more than one at a time
n = int(62 * SR); bed = breeze(rng, n) * .5
for at, seconds, carrier, pulses, rate, pan in [(9, 6, 4700, 3, .55, .25), (31, 5, 4380, 4, .7, -.4), (47, 4.5, 4950, 3, .48, .1)]:
    c = room(band(cricket(rng, seconds, carrier, pulses, rate), 2500, None), rng, .4) * .05
    s = int(at * SR); bed[s:s + len(c), 0] += c * (1 - pan) * .7; bed[s:s + len(c), 1] += c * (1 + pan) * .7
save('night_ambience', normalize(seamless(bed, 2), -12))

# the events the game scatters around the player, quiet and far
for k, (seconds, carrier, pulses, rate) in enumerate([(5, 4600, 3, .52), (7, 4300, 4, .68), (4, 5050, 3, .45)], 1):
    c = room(band(cricket(rng, seconds, carrier, pulses, rate), 2500, None), rng, .3)
    save('night_cricket_%d' % k, normalize(fade(c), -8))
save('night_owl', normalize(fade(owl(rng), .01, .4), -8))
save('night_frogs', normalize(fade(frogs(rng), .05, .5), -8))
