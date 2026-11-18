# The wordless talk of the people («бельмекання», the user 2026-11-15): what the game needs to hum a line with no voice package — written
# by «node tools/sound.js babble» into assets/story/sounds/babble.txt, read by src/NpcBabble.cs (its head says how a line is made of it).
# Two things, both worked out here once so the game does no analysis of sound at all:
#   the manner   how a person really speaks, with none of its sound: every reading there is (voice-proto/rec/voices/*/голос*.m4a — the minute
#                each recorded person reads) is cut into its phrases at its own pauses, and of a phrase are kept, every 10 ms: is there voice,
#                how its pitch stands to the phrase's middle (smoothed, its wrong jumps out, only a share of its movement kept: a hum played far
#                from its own speed is no longer that voice — the user: «викривлено»), how loud it is. And how many syllables it has, and
#                whether it ends rising. A manner is nobody's voice: all the readings serve every voice.
#                  @mannerstep <seconds a frame>
#                  @manner <syllables> <0|1 ends rising> <frames: two hex bytes each — pitch (00: no voice; else 0.80 + (n-1)/254*0.45), loudness (n/255*1.6)>
#   the hums     where in each file of a voice's wordless sounds (hm, hm_ask, uhu, em, chuckle, sigh — every timbre's own files, as they are
#                shipped) there is a stretch of steady voice long enough to hum with, and its pitch:
#                  @hum <file> <kind> <first sample> <samples> <Hz>
# A new voice or new timbres (sound.js voice, timbres): run this again. Needs librosa (the pitch of the readings).
import os
import re
import sys

import numpy as np
import soundfile as sf
from scipy import signal

RATE = 22050          # (the analysis; a file's own rate is kept for its samples)
HOP = 220             # ~10 ms
BEND = 0.4            # the share of the pitch's movement kept
LEAST = 0.25          # s: a stretch of hum shorter than this is not written
KINDS = [('speech_hm_ask_', 'ask'), ('speech_hm_', 'hm'), ('speech_uhu_', 'uhu'), ('speech_em_', 'em'), ('speech_chuckle_', 'chu'), ('speech_sigh_', 'sig')]


def manner_of(x, say):
    """the phrases of a reading: (syllables, rising, pitch bytes, loudness bytes)"""
    import librosa
    f0, vflag, _ = librosa.pyin(x, fmin=70, fmax=400, sr=RATE, frame_length=1024, hop_length=HOP)
    f0 = np.where(vflag, f0, np.nan)
    rms = librosa.feature.rms(y=x, frame_length=1024, hop_length=HOP)[0][:len(f0)]
    rms = signal.filtfilt(*signal.butter(2, 0.25), rms)
    top = np.percentile(rms, 95)
    speaker = float(np.nanmedian(f0))
    # the syllables: the hills of the voice band's loudness
    band = signal.sosfilt(signal.butter(4, [250, 3200], btype='band', fs=RATE, output='sos'), x)
    hop5 = int(RATE * 0.005)
    env = np.sqrt(signal.convolve(band ** 2, np.ones(int(RATE * 0.02)) / (RATE * 0.02), mode='same'))[::hop5]
    env = signal.filtfilt(*signal.butter(2, 12, fs=200), env)
    edb = 20 * np.log10(np.maximum(env, 1e-5))
    lo, hi = np.percentile(edb, 15), np.percentile(edb, 90)
    peaks, _ = signal.find_peaks(edb, height=lo + 0.45 * (hi - lo), distance=22, prominence=3.0)
    step = HOP / RATE
    quiet = rms < 0.07 * top
    out, a, gap = [], None, 0
    for i in range(len(rms) + 1):
        q = quiet[i] if i < len(rms) else True
        if not q:
            if a is None:
                a = i
            gap = 0
            continue
        if a is None:
            continue
        gap += 1
        if gap * step < 0.22 and i < len(rms):
            continue
        b = i - gap + 1
        seg = f0[a:b]
        voiced = ~np.isnan(seg)
        if 0.35 <= (b - a) * step <= 5.0 and voiced.sum() * step >= 0.2:
            hills = max(1, int(np.sum((peaks * 0.005 >= a * step) & (peaks * 0.005 < b * step))))
            v = seg[voiced]
            tail, before = v[int(len(v) * 0.85):], v[int(len(v) * 0.55):int(len(v) * 0.85)]
            rising = len(tail) > 2 and len(before) > 2 and np.median(tail) > 1.06 * np.median(before)
            ff = np.interp(np.arange(len(seg)), np.arange(len(seg))[voiced], v / speaker)
            ff = signal.medfilt(ff, 7)
            if len(ff) > 12:
                ff = signal.filtfilt(*signal.butter(2, 0.2), ff)
            ff = np.clip(np.clip(ff / np.median(ff), 0.6, 1.7) ** BEND, 0.88, 1.16)
            pitch = np.where(voiced, np.clip(np.round((ff - 0.80) / 0.45 * 254), 0, 254) + 1, 0).astype(int)
            loud = np.clip(np.round(rms[a:b] / top / 1.6 * 255), 0, 255).astype(int)
            out.append((hills, 1 if rising else 0, pitch, loud))
        a, gap = None, 0
    say('%d phrases, %d of them end rising; the reader speaks at about %.0f Hz' % (len(out), sum(p[1] for p in out), speaker))
    return out


def hums_of(path):
    """the steady voiced stretches of a file: (first sample, samples, Hz) at the file's own rate"""
    data, rate = sf.read(path, dtype='float32', always_2d=True)
    x = data.mean(axis=1)
    y = signal.resample_poly(x, RATE, rate).astype(np.float32) if rate != RATE else x
    peak = float(np.max(np.abs(y))) if len(y) else 0.0
    if peak < 1e-4:
        return [], rate
    y = y / peak
    win, hop = int(RATE * 0.045), int(RATE * 0.01)   # (45 ms: four periods of the lowest voice of the pool, 99 Hz — at 30 ms it had two stretches in all)
    if len(y) <= win:
        return [], rate
    frames = range(0, len(y) - win, hop)
    top = max(float(np.sqrt(np.mean(y[a:a + win] ** 2))) for a in frames)
    lag0, lag1 = int(RATE / 420), int(RATE / 70)
    flags, hzs = [], []
    for a in frames:
        seg = y[a:a + win]
        ok, hz = False, 0.0
        if np.sqrt(np.mean(seg ** 2)) > 0.12 * top:
            ac = signal.correlate(seg, seg, mode='full')[len(seg) - 1:]
            k = lag0 + int(np.argmax(ac[lag0:lag1]))
            ok, hz = ac[0] > 0 and ac[k] / ac[0] > 0.45, RATE / k   # (0.45: an old, airy voice is voice too; at 0.6 it had 1.7 s in all)
        flags.append(ok)
        hzs.append(hz)
    found, a = [], None
    for i, ok in enumerate(flags + [False]):
        if ok and a is None:
            a = i
        elif not ok and a is not None:
            if (i - a) * 0.01 >= LEAST:
                first = int(a * hop * rate / RATE)
                n = min(len(x) - first, int(((i - a) * hop + win) * rate / RATE))
                found.append((first, n, float(np.median([h for h in hzs[a:i] if h > 0]))))
            a = None
    return found, rate


def cmd_babble(sound, root):
    """sound: tools/sound.py as a module (its reader of recordings); root: the mod's folder"""
    sounds = os.path.join(root, 'assets', 'story', 'sounds')
    voices = os.path.join(root, 'voice-proto', 'rec', 'voices')
    lines = ['# babble — made by «node tools/sound.js babble» (tools/sound_babble.py: its head says each line): do not edit, make it again.',
             '# The manner of real reading (no sound of it) and where each voice\'s files have a steady hum: src/NpcBabble.cs makes the people\'s wordless talk of them.',
             '@mannerstep %.6f' % (HOP / RATE)]
    count = 0
    for who in sorted(os.listdir(voices)) if os.path.isdir(voices) else []:
        for name in sorted(os.listdir(os.path.join(voices, who))):
            if not name.lower().startswith('голос'):
                continue
            x, rate = sound.read(os.path.join(voices, who, name))
            x = signal.resample_poly(x.mean(axis=1), RATE, rate).astype(np.float32)
            x /= max(1e-6, float(np.max(np.abs(x))))
            got = manner_of(x, lambda t: print('  the reading of «%s» (%.0f s): %s' % (who, len(x) / RATE, t)))
            for hills, rising, pitch, loud in got:
                lines.append('@manner %d %d %s' % (hills, rising, ''.join('%02x%02x' % (p, l) for p, l in zip(pitch, loud))))
            count += len(got)
    if count == 0:
        sound.fail('no reading found (voice-proto/rec/voices/<who>/голос*.m4a): the manner of the talk is taken from one')
    files = stretches = 0
    by_voice = {}
    for name in sorted(os.listdir(sounds)):
        if not name.endswith('.ogg'):
            continue
        kind = next((k for pre, k in KINDS if name.startswith(pre)), None)
        if kind is None:
            continue
        got, rate = hums_of(os.path.join(sounds, name))
        m = re.search(r'__(\w+)\.ogg$', name)
        voice = m.group(1) if m else 'own'
        v = by_voice.setdefault(voice, [0, 0.0])
        for first, n, hz in got:
            lines.append('@hum %s %s %d %d %.1f' % (name, kind, first, n, hz))
            v[0] += 1; v[1] += n / rate
        files += 1; stretches += len(got)
    out = os.path.join(sounds, 'babble.txt')
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('babble: the manner — %d phrases; the hums — %d stretches in %d files' % (count, stretches, files))
    for voice in sorted(by_voice):
        print('  %-8s %3d stretches, %.1f s' % (voice, by_voice[voice][0], by_voice[voice][1]))
    print('  → %s (%.0f KB)' % (out, os.path.getsize(out) / 1024))
