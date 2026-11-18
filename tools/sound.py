# A raw sound file made ready for the mod (run it through tools/sound.js, which finds the Python).
#
#   node tools/sound.js info <file> …                 what a raw file is: length, channels, loudness, peak, noise floor, silence at its ends
#   node tools/sound.js add <file> <name> [kind] …    cut, levelled, written as assets/story/sounds/<name>.ogg, its line added to the catalogue
#   node tools/sound.js list                          every file of assets/story/sounds: length, loudness, peak, size, whether the catalogue names it
#   node tools/sound.js voice <name> <folder> --for man|woman [--age young,grown,old]
#                                                     ANOTHER PERSON'S recording of the wordless voice sounds becomes a timbre of its own:
#                                                     see «A recorded voice» below
#   node tools/sound.js timbres                       every person's timbre made anew from the wordless voice sounds (the names on «speech_»):
#                                                     see «Timbres» below
#   node tools/sound.js talk <voice> <voice> … | --measure   the people's wordless talk heard outside the game: a talk of these voices
#                                                     as a row of the page, or what its numbers tell (cut-off sounds, clipping) —
#                                                     tools/sound_talk.py, a copy of the game's rules held to them by arch_check
#   node tools/sound.js page                          the one listening page, build/sounds.html, made anew: every name of the catalogue with its
#                                                     note; our own files play in the browser — «як у грі» (one of its takes by chance, never the
#                                                     one before, the line's vol, pitch and spread) and each take by itself. A game's sound is
#                                                     only named there: it is heard in the game («Звуки» tab). Trials not in the mod are on it
#                                                     too: each folder build/sound_try/<name>/ of sound files is a row (its note: note.txt)
#
# kind: what the sound is for — it sets how the file is made:
#   shot    a one-shot at a place (a door, a step, a thing falling). Mono, silence cut off both ends, -18 LUFS      (the default)
#   voice   a wordless sound of a person or a bird (hm, a sigh, a chuckle). Mono, cut, -19 LUFS: the ravens' spoken lines are at -19 too
#   bed     a long loop under a scene (wind, fire, a hall's hum). Its end is crossfaded into its start, so it loops with no seam; -26 LUFS
#   music   a track. Stereo kept, nothing cut; -20 LUFS
# Words of «add»:
#   --cut <from> <to>     only these seconds of the file
#   --join "<a>-<b>,<c>-[<d>]"   these pieces of the file one after another (no end: to the file's end), each seam a 30 ms equal-power
#                         crossfade: a track shortened to its scene by whole phrases (a piece ends 5 ms before a downbeat, the next starts
#                         35 ms before its own — the beat goes on in time across the seam). Instead of --cut
#   --split               one recording with pauses → <name>_1.ogg, <name>_2.ogg … (a piece = sound between pauses of --gap seconds, 0.35 by default);
#                         each piece is cut and levelled by itself, and all of them are the variants of the one name: the game plays one of
#                         them each time, never the one before, its pitch a little off (the line's spread=)
#   --mono | --stereo     against the kind's own choice (a bed heard at a place wants --mono: only a mono sound has a place in the world)
#   --lufs <n>            another loudness than the kind's
#   --src "<where from, whose, licence>" | --own   REQUIRED: where the file came from (own = the user's own recording). It goes into the
#                         catalogue line's note: the package is public, every file in it must say what allows it to be there
#   --note "<what it is>" the catalogue line's note
#   --to <file>           the catalogue file of assets/story/sounds the line goes to (story by default)
#   --force               write over a file of that name
# The loudness is one standard for a kind, so «vol» in a scene means the same for every file; a file whose peaks would clip at its standard
# is left quieter and the report says so. The numbers are a starting point chosen here, NOT measured against the game's own sounds: the
# ear decides in the «Звуки» tab (vol=, pitch=, range= of the line).
# Loudness: ITU-R BS.1770 (K-weighting, 400 ms blocks, the two gates); a sound shorter than a block is one block.
# Reads .wav .ogg .mp3 .flac (libsndfile), anything else (.m4a .mp4 .webm …) through PyAV. Writes Ogg Vorbis, 44.1 kHz.
# A packet PyAV cannot decode is left out and its moment printed (a phone's recorder has left one such in each of its ALAC files).
# Timbres. The voice sounds are recorded by one voice. A timbre is that recording turned into another voice: a line «@timbre <name> …» in a
# file of assets/story/sounds (the game reads the same lines to know who may have it: NpcTimbre), its words for this tool:
#   tone=1.6      the pitch of the voice ×          throat=1.14   the size of the throat and mouth × (the formants; over 1: a smaller body)
#   air=0.3       breath in the voice, 0…1 (hoarse, old)          tilt=-1.5   dB an octave: under 0 darker, over 0 brighter
#   tremor=0.03   the pitch trembles (an old voice): its depth
#   like=Speaker_53   not a shift of the recorded voice but ANOTHER voice: the take's playing in the timbre of one of the speech model's own
#                 voices (tools/sound_voice.py says how and why: a shifted voice stays the recorded one to the ear). With it tone and
#                 throat mean nothing (the other voice has its own height and throat); air, tilt, tremor still do
#   of=rec1       made of the takes of the recorded voice «rec1» (a «@timbre rec1 … rec» line, see «A recorded voice») and not of the
#                 first recording's: the same words, the same rules; a sound that voice did not record its timbres do not say («@none»)
# A timbre with none of these is the recording itself (its sounds keep their plain names). For every other, takes of every «speech_»
# name (TAKES of a sound at most, evenly through the recording: a person plays three of its own) become <take>__<timbre>.ogg, and the catalogue file speech__<timbre>.txt is written whole: «<name>__<timbre> file:… <the plain
# line's own options>». Made from the uncompressed take kept by «add» (sound_masters/, this machine only, not in git): a file compressed
# twice is heard — 2026-11-13 the user named exactly the noisy takes (a breath, a throat cleared, a sigh) «harsh, distorted», and the Ogg
# setting of then left a noisy take 13–18 dB clean against 28 for a voiced one. Pitch and throat are moved apart by the WORLD vocoder
# (pyworld), and only what is voice goes through it: a vocoder sings whatever it is given, noise too. So by how much of a take is voice:
#   nearly all (85% and over)   made: the voice sung in the timbre; its airy end and the creak it sinks into stay the take's own sound
#   nearly none (under 25%)     a breath: the take itself, only its colour tilted — for a timbre of the recording's own sex. For the other
#                               sex it is not made at all, and what is air or creak inside a voiced take is left out of it (the voice
#                               simply ends): a man's breath and the creak his sigh sinks into are heard as a man's — the user, in the
#                               game, 2026-11-13: «для жіночих нпс чомусь деколи явно чоловічі голоси». Air does have a sex
#   between                     NOT made in any timbre: voice and air woven together cannot be told apart cleanly, and what is left of the
#                               speaker's own voice sounds after the sung one as its echo (the user heard exactly these, 2026-11-13: sighs
#                               1 and 7; 125 ms of the old pitch measured after the new one). Such a take is heard in the recording's own
#                               timbre only; a timbre's line names the takes that are made.
# The first recording's takes are its masters, not what its plain lines name: the mod keeps of that recording only what is played as it
# is (a take a sound, every take of a sound no timbre can be made of), since nobody is dealt it (timbres_of says why).
# Nothing here cuts the air into frames (a spectrum changed frame by frame smears a sound in time). Files of a timbre no longer written
# are removed.
# A recorded voice. The surest other voice is another person: the same sounds recorded by them, a file a sound, each sound said a few times
# with a pause between (as the first recording was made). «voice <name> <folder>» takes the files by their names — «Хммм», «хмм_» (the
# asking one), «Емм», «Угу», «О», «Зітхання», «Смішок», «Кхм», «Вдих…» — cuts each into its takes as «add --split» does, writes them as
# <sound>_<i>__<name>.ogg with the catalogue file speech__<name>.txt, and the line «@timbre <name> for=… age=… rec» into timbres.txt
# (after the last voice the game hands out, not under the story people's «only» ones; whose voice it is — for=, age= — the author says by
# ear: the tool does not hear it, and a voice between a man's and a woman's by its pitch is not told by numbers):
# «rec» says the timbre is its own recording — nothing is made of it, «timbres» leaves its files alone, the game deals it as any other.
# creak=<Hz> on that line: under it this voice creaks (CREAK, 78, was measured on the first recording). A deep voice hums cleanly lower —
# rec4 (2026-11-16) hummed «хм», «емм» at 70–75 Hz with no noise in it, and at 78 three of its six sounds read as creak and were made in no timbre.
# Every take is kept, the mixed ones too: it is that person's own voice all through. A sound they did not record they do not say.
# Needs numpy, scipy, soundfile, av (and pyworld for «timbres»): the Python of voice-proto/pytools has them (when that folder is cleaned away this tool needs them elsewhere).
import argparse
import math
import os
import re
import sys

import numpy as np
import soundfile as sf
from scipy import signal

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOUNDS = os.path.join(ROOT, 'assets', 'story', 'sounds')
RATE = 44100
PEAK_MAX = 10 ** (-1.0 / 20)  # no sample over -1 dBFS
MASTERS = os.path.join(ROOT, 'sound_masters')  # each made take uncompressed: what a timbre is made from (this machine only)
MARK = '__'                                    # <name>__<timbre>
AIR_UNDER, VOICE_FROM = 0.25, 0.85             # the share of a take that is voice: under the first it is air, from the second a voice; between — not made in a timbre
CREAK = 78.0                                   # Hz: under this the recorded voice creaks, it does not sing (voice_parts). Measured on the first recording;
                                               # a recorded voice says its own on its «rec» line: creak=<Hz> (a deep voice hums cleanly under 78)
OGG_MUSIC = 0.65  # a track (kind music): ~100 kbps stereo — a background track's quality, half the size of OGG_LONG (the user 2026-11-15: «зжимав, щоб важила мало?»; measured: The Adventure 99 s 2.0 MB → 1.2 MB, the crypt loop 218 → 117 KB)
OGG_NOISY, OGG_CLEAN, OGG_LONG, LONG_FROM = 0.1, 0.4, 0.35, 10.0  # libsndfile's compression level (0 best … 1 smallest): a short noisy sound · a short clean voice · one over LONG_FROM s
NOISY_HZ, NOISY_HIGH = 420.0, 0.01                              # a sound is noisy when its middle lies over this, or this share of it lies over 4 kHz (noisy)
KINDS = {
    'shot': dict(lufs=-18.0, mono=True, trim=True, fade=0.005, loop=False),
    'voice': dict(lufs=-19.0, mono=True, trim=True, fade=0.008, loop=False),
    'bed': dict(lufs=-26.0, mono=False, trim=False, fade=0.0, loop=True),
    'music': dict(lufs=-20.0, mono=False, trim=False, fade=0.0, loop=False),
}


def fail(text):
    print('sound: ' + text)
    sys.exit(1)


def db(x):
    return 20 * math.log10(x) if x > 0 else -120.0


# ---------------------------------------------------------------- reading

def read(path):
    """the file as float32 [frames, channels] and its rate"""
    if not os.path.isfile(path):
        fail('no file ' + path)
    try:
        data, rate = sf.read(path, dtype='float32', always_2d=True)
        return data, rate
    except Exception:
        pass
    try:
        import av
    except ImportError:
        fail(os.path.basename(path) + ': libsndfile cannot read it and PyAV is not there')
    try:
        with av.open(path) as c:
            if not c.streams.audio:
                fail(os.path.basename(path) + ': no sound in it')
            s = c.streams.audio[0]
            rs = av.AudioResampler(format='fltp', layout=s.layout, rate=s.rate)
            parts, lost = [], []
            for packet in c.demux(s):  # a packet at a time: one that cannot be decoded is left out and said, the file is not refused for it
                try:
                    frames = packet.decode()
                except av.error.FFmpegError:
                    lost.append(float(packet.pts * packet.time_base) if packet.pts is not None else -1.0)
                    continue
                for frame in frames:
                    for f in rs.resample(frame):
                        parts.append(f.to_ndarray())
            for f in rs.resample(None):
                parts.append(f.to_ndarray())
            if not parts:
                fail(os.path.basename(path) + ': no sound in it' + (' (none of its %d packets can be decoded)' % len(lost) if lost else ''))
            data = np.concatenate(parts, axis=1).T.astype(np.float32)
            if lost:
                print('  %s: %d packet(s) of it cannot be decoded and are left out, at %s s of %.1f s' % (
                    os.path.basename(path), len(lost), ', '.join('%.2f' % t if t >= 0 else '?' for t in lost[:6]) + (' …' if len(lost) > 6 else ''), len(data) / s.rate))
            return data, s.rate
    except SystemExit:
        raise
    except Exception as e:
        fail(os.path.basename(path) + ': cannot be read (' + str(e) + ')')


def to_rate(x, rate):
    if rate == RATE:
        return x
    g = math.gcd(RATE, rate)
    return signal.resample_poly(x, RATE // g, rate // g, axis=0).astype(np.float32)


# ---------------------------------------------------------------- measuring

def k_weight(x, rate):
    """BS.1770 K-weighting: a high shelf, then a high pass (the coefficients worked out for this rate)"""
    def shelf(f0, gain, q):
        a = 10 ** (gain / 40)
        w = 2 * math.pi * f0 / rate
        al = math.sin(w) / (2 * q)
        c = math.cos(w)
        b = [a * ((a + 1) + (a - 1) * c + 2 * math.sqrt(a) * al), -2 * a * ((a - 1) + (a + 1) * c), a * ((a + 1) + (a - 1) * c - 2 * math.sqrt(a) * al)]
        d = [(a + 1) - (a - 1) * c + 2 * math.sqrt(a) * al, 2 * ((a - 1) - (a + 1) * c), (a + 1) - (a - 1) * c - 2 * math.sqrt(a) * al]
        return np.array(b) / d[0], np.array(d) / d[0]

    def high(f0, q):
        w = 2 * math.pi * f0 / rate
        al = math.sin(w) / (2 * q)
        c = math.cos(w)
        b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
        d = [1 + al, -2 * c, 1 - al]
        return np.array(b) / d[0], np.array(d) / d[0]

    b, a = shelf(1681.974450955533, 3.999843853973347, 0.7071752369554196)
    y = signal.lfilter(b, a, x, axis=0)
    b, a = high(38.13547087602444, 0.5003270373238773)
    return signal.lfilter(b, a, y, axis=0)


def lufs(x, rate):
    """integrated loudness; a sound shorter than one block is one block"""
    if len(x) == 0:
        return -120.0
    y = k_weight(x.astype(np.float64), rate)
    block = int(0.4 * rate)
    if len(y) <= block:
        p = float(np.sum(np.mean(y ** 2, axis=0)))
        return -0.691 + 10 * math.log10(p) if p > 0 else -120.0
    step = block // 4
    sq = np.concatenate([np.zeros((1, y.shape[1])), np.cumsum(y ** 2, axis=0)])
    starts = np.arange(0, len(y) - block + 1, step)
    power = np.sum((sq[starts + block] - sq[starts]) / block, axis=1)  # channels summed (weights 1: mono, stereo)
    loud = -0.691 + 10 * np.log10(np.maximum(power, 1e-12))
    keep = power[loud > -70]
    if len(keep) == 0:
        return -120.0
    rel = -0.691 + 10 * math.log10(float(np.mean(keep))) - 10
    keep = power[(loud > -70) & (loud > rel)]
    if len(keep) == 0:
        return -120.0
    return -0.691 + 10 * math.log10(float(np.mean(keep)))


def frames_db(x, rate, ms):
    """the level of each frame of ms milliseconds, dBFS (the loudest channel)"""
    n = max(1, int(rate * ms / 1000))
    count = len(x) // n
    if count == 0:
        return np.array([db(float(np.sqrt(np.mean(x ** 2))) if len(x) else 0.0)]), n
    cut = x[:count * n].reshape(count, n, x.shape[1])
    rms = np.sqrt(np.max(np.mean(cut.astype(np.float64) ** 2, axis=1), axis=1))
    return 20 * np.log10(np.maximum(rms, 1e-6)), n


def facts(x, rate):
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    lv, n = frames_db(x, rate, 10)
    top = float(np.max(lv))
    on = np.nonzero(lv > top - 45)[0]
    head = on[0] * n / rate if len(on) else 0.0
    tail = (len(lv) - 1 - on[-1]) * n / rate if len(on) else 0.0
    return dict(sec=len(x) / rate, ch=x.shape[1], lufs=lufs(x, rate), peak=db(peak), floor=float(np.percentile(lv, 10)), head=head, tail=tail,
                clipped=int(np.sum(np.abs(x) >= 0.999)))


def say_facts(name, f, rate):
    line = '%-28s %6.2f s  %s  %5d Hz  %6.1f LUFS  peak %5.1f dB  floor %5.1f dB' % (name, f['sec'], 'mono  ' if f['ch'] == 1 else 'stereo' if f['ch'] == 2 else '%d ch ' % f['ch'], rate, f['lufs'], f['peak'], f['floor'])
    notes = []
    if f['head'] > 0.05 or f['tail'] > 0.15:
        notes.append('silence %.2f s before, %.2f s after' % (f['head'], f['tail']))
    if f['clipped'] > 8:
        notes.append('CLIPPED: %d samples at full scale' % f['clipped'])
    if f['floor'] > -45 and f['sec'] > 1.5:
        notes.append('never quiet (floor %.0f dB): a steady sound, or noise under it' % f['floor'])
    print(line + ('   ' + '; '.join(notes) if notes else ''))


# ---------------------------------------------------------------- shaping

def trim(x, rate):
    """silence off both ends: the start sharp (45 dB under the loudest frame), the tail let ring (55 dB under)"""
    lv, n = frames_db(x, rate, 5)
    top = float(np.max(lv))
    head = np.nonzero(lv > max(top - 45, -70))[0]
    tail = np.nonzero(lv > max(top - 55, -75))[0]
    if len(head) == 0:
        return x
    a = max(0, head[0] * n - int(0.010 * rate))
    b = min(len(x), (tail[-1] + 1) * n + int(0.030 * rate))
    return x[a:b]


def fade(x, rate, sec):
    n = min(int(sec * rate), len(x) // 2)
    if n <= 0:
        return x
    x = x.copy()
    ramp = np.linspace(0.0, 1.0, n, dtype=np.float32)[:, None]
    x[:n] *= ramp
    x[-n:] *= ramp[::-1]
    return x


def loop(x, rate):
    """the end crossfaded into the start (equal power): played round, it has no seam; it gets shorter by the crossfade"""
    n = int(min(1.0, len(x) / rate / 4) * rate)
    if n < int(0.05 * rate):
        return x, 0.0
    t = np.linspace(0.0, math.pi / 2, n, dtype=np.float32)[:, None]
    out = x[n:].copy()
    out[-n:] = x[-n:] * np.cos(t) + x[:n] * np.sin(t)
    return out, n / rate


def level(x, rate, target):
    """to the loudness asked, unless its peaks would clip: then as loud as they let it"""
    now = lufs(x, rate)
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    if now <= -100 or peak <= 0:
        return x, now, False
    gain = 10 ** ((target - now) / 20)
    capped = peak * gain > PEAK_MAX
    if capped:
        gain = PEAK_MAX / peak
    return (x * gain).astype(np.float32), now + db(gain), capped


def pieces(x, rate, gap):
    """the sounds of one recording, apart by pauses of at least gap seconds.
    Two levels, both over the recording's own noise (the level its quietest tenth stays under): a sound is found where it is well over the
    noise (12 dB), then its ends are let out to where it sinks into the noise (5 dB over it) — a soft start and a long tail stay whole.
    A level counted from the loudest frame alone (40 dB under it) sat inside the noise of a quiet recording: the pauses read as sound and
    five takes came out as one (2026-11-13, the user's first recordings)."""
    hp = signal.sosfilt(signal.butter(2, 70, 'highpass', fs=rate, output='sos'), x, axis=0)  # (a room's rumble is not a sound)
    lv, n = frames_db(hp.astype(np.float32), rate, 10)
    lv = np.convolve(lv, np.ones(3) / 3, mode='same')  # (30 ms: one loud frame of noise is not a sound)
    noise = float(np.percentile(lv, 10))
    top = float(np.max(lv))
    hi, lo = max(noise + 12, top - 40), noise + 5
    on = lv > hi
    quiet = max(1, int(gap * rate / n))
    out, start, last = [], None, None
    for i, v in enumerate(on):
        if v:
            if start is None:
                start = i
            last = i
        elif start is not None and i - last >= quiet:
            out.append([start, last])
            start = None
    if start is not None:
        out.append([start, last])
    for k, p in enumerate(out):  # its ends, out to the noise — never into its neighbour
        left = out[k - 1][1] + 1 if k > 0 else 0
        right = out[k + 1][0] - 1 if k + 1 < len(out) else len(lv) - 1
        while p[0] > left and lv[p[0] - 1] > lo:
            p[0] -= 1
        while p[1] < right and lv[p[1] + 1] > lo:
            p[1] += 1
    loudest = max((float(np.max(lv[a:b + 1])) for a, b in out), default=top)
    cut = []
    for a, b in out:
        sec, peak = (b - a + 1) * n / rate, float(np.max(lv[a:b + 1]))
        if sec < 0.06 or peak < loudest - 22:
            print('   left out: %.2f s at %.1f s, %.0f dB under the loudest take (a click, a rustle)' % (sec, a * n / rate, loudest - peak))
            continue
        cut.append(x[max(0, a * n - int(0.03 * rate)):min(len(x), (b + 1) * n + int(0.06 * rate))])
    return cut


# ---------------------------------------------------------------- writing

def noisy(x):
    """is the sound noise rather than a clean voice (a breath, a throat cleared, a thing falling): where its strength lies. Measured on the
    takes of 2026-11-13: a hum, «о!», a chuckle have their middle at 190–370 Hz and nothing over 4 kHz; a breath, a sigh of air, «кхм» — 470–2500 Hz, 1–17% up there"""
    f, pw = signal.welch(x[:, 0].astype(np.float64), RATE, nperseg=min(2048, len(x)))
    total = float(np.sum(pw)) + 1e-20
    return float(np.sum(f * pw)) / total > NOISY_HZ or float(np.sum(pw[f > 4000])) / total > NOISY_HIGH


def write(path, x, kind=None):
    # Noise is what compression spoils (measured 2026-11-13 on a breath: 13 dB clean at the library's own setting, 25 dB at 0.1, for a file
    # 1.7 times as big — the user heard exactly the noisy takes as «harsh, distorted»); a clean voice was 28 dB at the library's own and is
    # written a little better than that, not at the noise's price (the same day: «а чому так багато?» — 10.7 MB, every take at 0.1).
    # A long one (a bed, a track) has no such fine noise to lose and would only grow
    level = OGG_MUSIC if kind == 'music' else OGG_LONG if len(x) / RATE > LONG_FROM else OGG_NOISY if noisy(x) else OGG_CLEAN
    with sf.SoundFile(path, 'w', RATE, x.shape[1], format='OGG', subtype='VORBIS', compression_level=level) as f:
        for i in range(0, len(x), RATE):  # (a second at a time: libsndfile has crashed on one long write)
            f.write(x[i:i + RATE])
    back, rate = sf.read(path, dtype='float32', always_2d=True)
    return facts(back, rate)


def catalogue():
    """name → the catalogue file it stands in, and every file the catalogue names"""
    names, files = {}, set()
    if not os.path.isdir(SOUNDS):
        return names, files
    for f in sorted(os.listdir(SOUNDS)):
        if not f.endswith('.txt'):
            continue
        with open(os.path.join(SOUNDS, f), encoding='utf-8-sig') as h:
            for line in h:
                w = line.split('#')[0].split()
                if len(w) < 2 or w[0].startswith('@'):
                    continue
                names[w[0]] = f
                for t in w[1:]:
                    if t.startswith('file:'):
                        files.add(t[5:].lower())
    return names, files


def joined(x, rate, spec, seam=0.030):
    """--join: the pieces «a-b,c-d» of x one after another, each seam an equal-power crossfade of `seam` s"""
    parts = []
    for p in spec.split(','):
        lo, _, hi = p.strip().partition('-')
        try:
            a = float(lo)
            b = float(hi) if hi.strip() else len(x) / rate
        except ValueError:
            fail('--join: «%s» is not <from>-<to> in seconds' % p)
        piece = x[int(a * rate):int(b * rate)]
        if len(piece) == 0:
            fail('--join: nothing between %s' % p)
        parts.append(piece)
    n = int(seam * rate)
    y = parts[0]
    for q in parts[1:]:
        if len(y) <= n or len(q) <= n:
            fail('--join: a piece shorter than its seam')
        t = np.linspace(0.0, np.pi / 2, n, dtype=np.float32)[:, None]
        y = np.concatenate([y[:-n], y[-n:] * np.cos(t) + q[:n] * np.sin(t), q[n:]])
    print('joined: %d pieces, %.2f s' % (len(parts), len(y) / rate))
    return y


def make(x, rate, kind, a, under=0.0):
    """under: this many dB under the kind's loudness (a take much quieter than the others of its recording stays quieter)"""
    k = KINDS[kind]
    mono = a.mono or (k['mono'] and not a.stereo)
    if mono and x.shape[1] > 1:
        x = np.mean(x, axis=1, keepdims=True)
    elif x.shape[1] > 2:
        x = x[:, :2]
    x = to_rate(x, rate)
    if k['trim']:
        x = trim(x, RATE)
    seam = 0.0
    if k['loop']:
        x, seam = loop(x, RATE)
    x, loud, capped = level(x, RATE, (a.lufs if a.lufs is not None else k['lufs']) - under)
    if k['fade'] > 0:
        x = fade(x, RATE, k['fade'])
    return x, capped, seam


def cmd_info(a):
    for p in a.files:
        x, rate = read(p)
        say_facts(os.path.basename(p), facts(x, rate), rate)


PAGE = r'''<!doctype html>
<html lang="uk"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>RavenWatch: звуки</title>
<style>
:root { --bg:#f4f1ea; --card:#fffdf8; --ink:#26221c; --dim:#7a7264; --line:#ddd5c5; --acc:#8a4b1f; --btn:#efe8da; --on:#c9762f; }
@media (prefers-color-scheme: dark) { :root { --bg:#1b1916; --card:#24211d; --ink:#ece6da; --dim:#9a917f; --line:#3a352d; --acc:#e0a36a; --btn:#2f2b25; --on:#c9762f; } }
* { box-sizing: border-box; }
body { margin:0; padding:20px 16px 60px; background:var(--bg); color:var(--ink); font:15px/1.45 "Segoe UI", system-ui, sans-serif; }
main { max-width:860px; margin:0 auto; }
h1 { font-size:22px; margin:0 0 4px; } h2 { font-size:15px; margin:28px 0 8px; color:var(--dim); font-weight:600; }
.sub { color:var(--dim); margin:0 0 14px; }
.bar { position:sticky; top:0; z-index:2; background:var(--bg); padding:10px 0; border-bottom:1px solid var(--line); display:flex; flex-wrap:wrap; gap:8px 18px; align-items:center; }
.bar label { display:flex; gap:8px; align-items:center; color:var(--dim); } .bar output { color:var(--ink); min-width:3.2em; font-variant-numeric:tabular-nums; }
.row { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:12px 14px; margin:8px 0; }
.head { display:flex; flex-wrap:wrap; gap:6px 12px; align-items:baseline; }
.name { font:600 15px/1.3 Consolas, monospace; } .note { color:var(--ink); } .opts { color:var(--dim); font-size:13px; }
.takes { display:flex; flex-wrap:wrap; gap:6px; margin-top:10px; align-items:center; }
button { font:inherit; color:var(--ink); background:var(--btn); border:1px solid var(--line); border-radius:8px; padding:6px 11px; cursor:pointer; }
button:hover { border-color:var(--acc); } button.game { background:var(--acc); color:#fff; border-color:var(--acc); font-weight:600; }
button.on { background:var(--on); color:#fff; border-color:var(--on); }
button small { color:inherit; opacity:.7; margin-left:4px; }
.only { color:var(--dim); font-size:13px; margin-top:8px; }
.v { display:flex; flex-wrap:wrap; gap:6px 10px; align-items:center; background:var(--card); border:1px solid var(--line); border-radius:10px; padding:9px 12px; margin:6px 0; }
.v .name { min-width:74px; } .v .who { color:var(--dim); font-size:13px; min-width:210px; flex:1 1 210px; }
details { margin-top:26px; } summary { cursor:pointer; color:var(--dim); font-weight:600; padding:6px 0; }
</style></head><body><main>
<h1>RavenWatch: звуки</h1>
<p class="sub">Каталог <code>assets/story/sounds</code>, зроблено __WHEN__. «Як у грі»: випадковий дубль (не той самий двічі поспіль), гучність, тон і розкид тону з рядка каталогу. Номер — саме цей дубль, без зсуву тону, з гучністю рядка.</p>
<div class="bar">
  <label>Тон людини <input id="tone" type="range" min="0.9" max="1.1" step="0.01" value="1"><output id="toneOut">1.00</output></label>
  <label>Гучність сторінки <input id="vol" type="range" min="0" max="1" step="0.05" value="1"><output id="volOut">1.00</output></label>
  <button id="stop">■ Стоп</button>
</div>
<h2>Голоси людей: по одному дублю п'яти вигуків</h2>
<div id="voices"></div>
<details id="all"><summary>Усі звуки каталогу: кожна назва, кожен дубль</summary>
<div id="list"></div>
</details>
</main><script>
const DATA = __DATA__;
const tone = document.getElementById('tone'), vol = document.getElementById('vol');
tone.oninput = () => document.getElementById('toneOut').textContent = (+tone.value).toFixed(2);
vol.oninput = () => document.getElementById('volOut').textContent = (+vol.value).toFixed(2);
let playing = [];
function stopAll() { for (const a of playing) { a.pause(); } playing = []; document.querySelectorAll('button.on').forEach(b => b.classList.remove('on')); }
document.getElementById('stop').onclick = stopAll;
function play(file, rate, gain, btn) {
  const a = new Audio(file);
  a.preservesPitch = false; a.mozPreservesPitch = false; a.webkitPreservesPitch = false;   // (faster is higher, as the game does it)
  a.playbackRate = Math.min(4, Math.max(0.25, rate));
  a.volume = Math.min(1, Math.max(0, gain * +vol.value));
  playing.push(a);
  if (btn) { btn.classList.add('on'); a.onended = a.onpause = () => btn.classList.remove('on'); }
  a.play().catch(e => { if (btn) { btn.classList.remove('on'); btn.title = 'не грає: ' + e.message; } });
}
const voices = document.getElementById('voices');
for (const v of DATA.voices) {
  const row = document.createElement('div'); row.className = 'v';
  row.innerHTML = '<span class="name"></span><span class="who"></span>';
  row.children[0].textContent = v.name; row.children[1].textContent = v.who;
  for (const smp of v.samples) {
    const b = document.createElement('button'); b.textContent = smp.label;
    b.onclick = () => play(smp.src, +tone.value, smp.vol, b);
    row.appendChild(b);
  }
  voices.appendChild(row);
}
if (DATA.sounds.length && DATA.sounds[0].cat.indexOf('Проба') === 0) document.getElementById('all').open = true;
const list = document.getElementById('list');
let group = null;
for (const s of DATA.sounds) {
  if (s.cat !== group) { group = s.cat; const h = document.createElement('h2'); h.textContent = s.cat; list.appendChild(h); }
  const row = document.createElement('div'); row.className = 'row';
  const head = document.createElement('div'); head.className = 'head';
  head.innerHTML = '<span class="name"></span><span class="note"></span><span class="opts"></span>';
  head.children[0].textContent = s.name; head.children[1].textContent = s.note; head.children[2].textContent = s.opts;
  row.appendChild(head);
  if (s.files.length) {
    const takes = document.createElement('div'); takes.className = 'takes';
    const g = document.createElement('button'); g.className = 'game'; g.textContent = '▶ як у грі';
    let last = -1;
    g.onclick = () => {
      let i = Math.floor(Math.random() * s.files.length);
      if (s.files.length > 1 && last >= 0) { i = Math.floor(Math.random() * (s.files.length - 1)); if (i >= last) i++; }
      last = i;
      const spread = s.spread ? 1 + (Math.random() * 2 - 1) * s.spread : 1;
      play(s.files[i].src, (s.pitch || 1) * +tone.value * spread, s.vol, takes.children[i + 1]);
    };
    takes.appendChild(g);
    s.files.forEach((f, i) => {
      const b = document.createElement('button');
      b.innerHTML = (s.files.length > 1 ? (i + 1) : '▶') + '<small></small>';
      b.lastChild.textContent = f.sec.toFixed(2) + ' с';
      b.title = f.file + ' · ' + f.lufs.toFixed(1) + ' LUFS · пік ' + f.peak.toFixed(1) + ' дБ';
      b.onclick = () => play(f.src, 1, s.vol, b);
      takes.appendChild(b);
    });
    row.appendChild(takes);
  }
  if (s.game.length) { const o = document.createElement('div'); o.className = 'only'; o.textContent = (s.files.length ? 'ще звук гри: ' : 'звук гри, чути лише в грі (вкладка «Звуки»): ') + s.game.join(', '); row.appendChild(o); }
  list.appendChild(row);
}
</script></body></html>
'''


def cmd_page(a):
    """build/sounds.html: the catalogue, to listen to in a browser"""
    import json
    import time
    from urllib.parse import quote
    out = os.path.join(ROOT, 'build', 'sounds.html')
    sounds, seen = [], {}
    for f in sorted(os.listdir(SOUNDS)):
        if not f.endswith('.txt'):
            continue
        with open(os.path.join(SOUNDS, f), encoding='utf-8-sig') as h:
            head = None  # (the file's first line, when it is a note: what the file is, under its name on the page)
            for raw in h:
                if head is None:
                    head = raw.strip().lstrip('#').strip()[:110] if raw.startswith('#') else ''
                line, _, note = raw.rstrip('\n').partition('#')
                w = line.split()
                if len(w) < 2 or w[0].startswith('@'):
                    continue
                s = dict(cat=f + (' — ' + head if head else ''), name=w[0], note=note.strip(), files=[], game=[], vol=1.0, pitch=0.0, spread=0.0, opts='')
                opts = []
                for t in w[1:]:
                    if t.startswith('file:'):
                        p = os.path.join(SOUNDS, t[5:])
                        if not os.path.isfile(p):
                            fail('%s: «%s» names %s, which is not there' % (f, w[0], t[5:]))
                        if t[5:] not in seen:
                            x, rate = read(p)
                            v = facts(x, rate)
                            # («?v=<when the file was written>»: a file made anew under its old name is not played from the browser's memory)
                            seen[t[5:]] = dict(file=t[5:], src='../assets/story/sounds/' + quote(t[5:]) + '?v=%d' % int(os.path.getmtime(p)), sec=round(v['sec'], 2), lufs=round(v['lufs'], 1), peak=round(v['peak'], 1))
                        s['files'].append(seen[t[5:]])
                    elif '=' in t:
                        k, _, val = t.partition('=')
                        opts.append(t)
                        if k in ('vol', 'pitch', 'spread'):
                            s[k] = float(val)
                    elif t in ('2d', 'wet'):
                        opts.append(t)
                    else:
                        s['game'].append(t)
                s['opts'] = ' '.join(opts)
                sounds.append(s)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    # trials, not in the mod: build/sound_try/<name>/*.ogg|wav — a row each, to be heard before anything of them goes into the catalogue
    # (its note: the first line of <name>/note.txt)
    tried = 0
    trials = os.path.join(ROOT, 'build', 'sound_try')
    if os.path.isdir(trials):
        for d in sorted(os.listdir(trials)):
            folder = os.path.join(trials, d)
            takes = sorted(f for f in os.listdir(folder) if re.search(r'\.(ogg|wav|mp3)$', f, re.I)) if os.path.isdir(folder) else []
            if not takes:
                continue
            note = ''
            if os.path.isfile(os.path.join(folder, 'note.txt')):
                with open(os.path.join(folder, 'note.txt'), encoding='utf-8-sig') as h:
                    note = h.readline().strip()
            s = dict(cat='Проба — не в моді (build/sound_try)', name=d, note=note, files=[], game=[], vol=1.0, pitch=0.0, spread=0.0, opts='')
            for f in takes:
                x, rate = read(os.path.join(folder, f))
                v = facts(x, rate)
                s['files'].append(dict(file=f, src='sound_try/' + quote(d) + '/' + quote(f), sec=round(v['sec'], 2), lufs=round(v['lufs'], 1), peak=round(v['peak'], 1)))
            sounds.append(s)
            tried += 1
    if tried:
        sounds = sounds[-tried:] + sounds[:-tried]  # (what waits for the ear's word stands first on the page)
    # the voices, short: one take of five sounds in each timbre — to tell the voices apart without the whole catalogue (the user, 2026-11-13:
    # «голоси окремо, суто по фактах: голос і 3–5 вигуків»). The pool first, then the voices given by name only
    by_name = {x['name']: x for x in sounds}
    show = [('speech_hm', 'хм'), ('speech_uhu', 'угу'), ('speech_oh', 'о!'), ('speech_sigh', 'зітхання'), ('speech_chuckle', 'смішок')]
    age = {'young': 'молодий', 'grown': 'дорослий', 'old': 'старий'}
    voices = []
    for n, d in sorted(timbre_lines().items(), key=lambda e: ('only' in e[1], e[1].get('for', '') != 'man')):
        plain = not any(k in d for k in ('like', 'tone', 'throat', 'air', 'tilt', 'tremor', 'rec'))
        who = ['чоловічий' if d.get('for') == 'man' else 'жіночий' if d.get('for') == 'woman' else '']
        who.append('лише названому' if 'only' in d else 'голос героя (гравця)' if 'hero' in d else 'вік: ' + ', '.join(age.get(a, a) for a in d.get('age', 'young,grown,old').split(',')))
        who.append(d['note'])
        row = dict(name=n, who=' · '.join(w for w in who if w), samples=[])
        for kind, label in show:
            x = by_name.get(kind if plain else kind + MARK + n)
            if x and x['files']:
                row['samples'].append(dict(label=label, src=x['files'][0]['src'], vol=x['vol']))
        voices.append(row)
    data = json.dumps(dict(sounds=sounds, voices=voices), ensure_ascii=False)
    with open(out, 'w', encoding='utf-8', newline='\n') as h:
        h.write(PAGE.replace('__DATA__', data.replace('</', '<\\/')).replace('__WHEN__', time.strftime('%Y-%m-%d %H:%M')))
    print('%s: %d names, %d files of our own, %d trial rows' % (out, len(sounds) - tried, len(seen), tried))


def timbre_lines():
    """the «@timbre <name> …» lines of the sound files: name → its words and its note"""
    out = {}
    for f in sorted(os.listdir(SOUNDS)):
        if not f.endswith('.txt'):
            continue
        with open(os.path.join(SOUNDS, f), encoding='utf-8-sig') as h:
            for raw in h:
                line, _, note = raw.rstrip('\n').partition('#')
                w = line.split()
                if len(w) < 2 or w[0] != '@timbre':
                    continue
                if not re.fullmatch(r'[a-z][a-z0-9_]*', w[1]) or MARK in w[1]:
                    fail('%s: a timbre\'s name is small Latin letters, digits and single «_»: %s' % (f, w[1]))
                d = dict(note=note.strip())
                for t in w[2:]:
                    k, _, v = t.partition('=')
                    d[k] = v
                out[w[1]] = d
    return out


def voice_parts(x, creak=CREAK):
    """a take taken apart by WORLD once (its pitch, its spectrum, its breath), for every timbre made of it; voiced: the share of it that is voice"""
    import pyworld as pw
    x = np.ascontiguousarray(x[:, 0].astype(np.float64))
    f0, t = pw.harvest(x, RATE, f0_floor=55.0, f0_ceil=600.0)
    # A voice that sinks into a creak (a sigh's end: 60–78 Hz measured on the takes of 2026-11-13, where the «hm» itself hums at 82–120) is
    # not sung by the vocoder: it takes a creak's slow, uneven beats for a low even tone, and raised to a woman's pitch that tone rang «as
    # from a bucket» (the user). A creak beats as slowly in any throat, so it stays the take's own, with the air.
    f0[(f0 > 0) & (f0 < creak)] = 0
    run = 0
    for i in range(len(f0) + 1):  # a few frames of «voice» in a noise are the tracker's guess: sung as a tone they buzz
        if i < len(f0) and f0[i] > 0:
            run += 1
            continue
        if 0 < run < 10:
            f0[i - run:i] = 0
        run = 0
    ap = pw.d4c(x, f0, t, RATE)
    n = ap.shape[1]
    low = ap[:, int(300 / (RATE / 2) * (n - 1)):int(2500 / (RATE / 2) * (n - 1))].mean(axis=1)
    f0[low >= 0.15] = 0  # (the tracker heard a pitch, but where a voice's strength lies the frame is half noise: a breath, a creak. A voiced frame measures 0.05)
    at = np.minimum((t * RATE).astype(int), max(0, len(x) - 220))  # (the last frames stand at the take's very end: measured on its last samples)
    lv = np.array([np.sqrt(np.mean(x[a:a + 220] ** 2) + 1e-12) for a in at])
    f0[lv < lv.max() * 0.03] = 0  # (30 dB under its loudest: a tail dying away is air, whatever pitch is found in it)
    loud = lv > lv.max() * 0.1
    voiced = float(np.mean(f0[loud] > 0)) if loud.any() else 0.0
    return dict(x=x, f0=f0, t=t, sp=pw.cheaptrick(x, f0, t, RATE), ap=ap, voiced=voiced)


def air_as(x, tilt):
    """the air of a take in that timbre: the take itself, untouched in time — only its colour tilted"""
    if tilt == 0.0:
        return x.copy()
    spec = np.fft.rfft(x)  # (the whole take at once: nothing is cut into frames, so nothing of it is moved in time)
    hz = np.clip(np.fft.rfftfreq(len(x), 1.0 / RATE), 100.0, 12000.0)
    return np.fft.irfft(spec * 10 ** (tilt * np.log2(hz / 1000.0) / 20), len(x))


def rms_where(x, on):
    return float(np.sqrt(np.mean(x[on] ** 2)) + 1e-9) if on.any() else 0.0


def as_timbre(p, d, seed, sung_as=None, cross=False):
    """the take in that timbre: its voice through the vocoder (pitch and throat moved apart — or, sung_as given, another voice's pitch,
    spectrum and breath: sound_voice.sing), its air kept as its own noise (air_as). The vocoder sings whatever it is given: the breath at
    the end of a sigh came out «as from a bucket» (the user, 2026-11-13) while the whole take went through it."""
    import pyworld as pw
    tone, throat = float(d.get('tone', 1)), float(d.get('throat', 1))
    air, tilt, tremor = float(d.get('air', 0)), float(d.get('tilt', 0)), float(d.get('tremor', 0))
    noise = np.zeros(len(p['x'])) if cross else air_as(p['x'], tilt)  # (cross: a timbre of the other sex than the recording — none of its air, none of its creak)
    if p['voiced'] < AIR_UNDER:  # a breath: no voice to move
        return noise
    sp, ap, f0 = p['sp'], p['ap'], p['f0'].copy()
    n = sp.shape[1]
    k = np.arange(n)
    if sung_as is not None:
        f0, sp2, ap2 = sung_as[0].copy(), sung_as[1].copy(), sung_as[2].copy()
        tone = 1.0
    else:
        src = np.clip(k / throat, 0, n - 1)
        lo = np.floor(src).astype(int)
        hi = np.minimum(lo + 1, n - 1)
        w = src - lo
        sp2 = sp[:, lo] * (1 - w) + sp[:, hi] * w
        ap2 = ap[:, lo] * (1 - w) + ap[:, hi] * w
    if tilt != 0.0:
        hz = np.maximum(k * RATE / 2 / (n - 1), 100.0)
        sp2 = sp2 * (10 ** (tilt * np.log2(hz / 1000.0) / 10))[None, :]
    if air > 0:
        ap2 = np.clip(ap2 + air * (1 - ap2), 0, 1)
    f0 *= tone
    if tremor > 0:
        rng = np.random.default_rng(seed)
        slow = np.interp(p['t'], np.linspace(0, p['t'][-1] + 1e-6, max(2, int(p['t'][-1] * 3) + 2)), rng.normal(0, 1, max(2, int(p['t'][-1] * 3) + 2)))
        f0 *= 1 + tremor * np.sin(2 * math.pi * 5.3 * p['t'] + rng.uniform(0, 6.28)) + tremor * 0.6 * slow
    sung = pw.synthesize(np.ascontiguousarray(f0), np.ascontiguousarray(sp2), np.ascontiguousarray(ap2), RATE)
    x = p['x']
    sung = np.pad(sung, (0, max(0, len(x) - len(sung))))[:len(x)]
    # where it is voice (a little wider, so a voice's first and last wave are whole), eased over 15 ms; each part as loud as it was in the take
    v = np.convolve((p['f0'] > 0).astype(float), np.ones(5), 'same') > 0
    m = np.interp(np.arange(len(x)) / RATE, p['t'], v.astype(float))
    win = np.hanning(int(0.015 * RATE))
    m = np.clip(np.convolve(m, win / win.sum(), 'same'), 0, 1)
    on, off = m > 0.5, m <= 0.5
    if rms_where(sung, on) > 0:
        sung = sung * (rms_where(x, on) / rms_where(sung, on))
    if rms_where(noise, off) > 0:
        noise = noise * (rms_where(x, off) / rms_where(noise, off))
    return m * sung + (1 - m) * noise


SHAPING = ('tone', 'throat', 'air', 'tilt', 'tremor', 'like')  # the words that make a timbre of a recording
TAKES = 4  # takes of a sound a made timbre keeps: a person plays three of its own (AudioBank), a fourth sets people of one timbre apart;
#            every take of every recording in every timbre was 10.7 MB for one recording (the user, 2026-11-13: «а чому так багато?»)


def spread_of(items, n):
    """n of them, evenly through the list: a recording's takes change from the first to the last (measured on the second voice: «хм?»
    climbs from 123 to 354 Hz over its seven), so the first n would be one manner of it"""
    if len(items) <= n:
        return list(items)
    return [items[int(i * (len(items) - 1) / (n - 1) + 0.5)] for i in range(n)]


def timbres_of(base, group, defs, speech, order, whose, made, lines, twice):
    """the timbres of one recording made: base None is the first recording (the plain names), else a recorded voice's name (of=)"""
    kinds = []  # this recording's sounds: (plain name, its files, its options, its note)
    for name in order:
        key = name + MARK + base if base else name
        if key in speech:
            kinds.append((name,) + speech[key])
    in_mod = {k[0]: k[1] for k in kinds}  # the files the mod has under the recording's own names
    if not base and os.path.isdir(MASTERS):
        # The first recording is the stuff timbres are made of, not a voice the game deals (its line is «only»; the user heard himself in
        # every man). So the mod keeps of it only what is played as it is — a take a sound for its plain name, and every take of a sound no
        # timbre can be made of (the men's timbres say that one by the recording) — and every take lives on as its master: the takes to
        # make timbres of are the masters, whatever the plain line still names (the user, 2026-11-13: «видали той запас, якщо він не треба»)
        for i, (name, files, opts, note) in enumerate(kinds):
            kept = [f[:-4] + '.ogg' for f in os.listdir(MASTERS) if re.fullmatch(re.escape(name) + r'(_\d+)?\.wav', f)]
            kinds[i] = (name, sorted(set(kept) | set(files), key=lambda f: (int((re.search(r'_(\d+)\.ogg$', f) or [0, 0])[1]), f)), opts, note)
    print('\n%s → %s' % ('the voice «%s» (%s)' % (base, whose or '?') if base else 'the first recording (%s)' % (whose or '?'), ', '.join(group)), flush=True)
    # every take taken apart once; and, for the timbres that are another voice (like=), heard once by the recogniser, all in one go
    taken_apart, levels = {}, {}
    creak = float(defs[base].get('creak', CREAK)) if base else CREAK  # (where this voice's creak begins: its «rec» line says it, else the first recording's)
    for name, files, opts, note in kinds:
        for f in files:
            take = os.path.splitext(f)[0]
            if base and not take.endswith(MARK + base):
                fail('%s: a take of the voice «%s» is named <sound>_<i>%s%s.ogg' % (f, base, MARK, base))
            master = os.path.join(MASTERS, take + '.wav')
            if not os.path.isfile(master):
                master = os.path.join(SOUNDS, f)
                twice.append(f)
            x, rate = read(master)
            x = to_rate(x[:, :1], rate)
            taken_apart[take] = voice_parts(x, creak)
            levels[take] = lufs(x, RATE)
    others = sorted(set(d['like'] for d in group.values() if 'like' in d))
    said, banks, own_hz = {}, {}, 0.0
    if others:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # (the Python of pytools does not look beside the script by itself)
        import sound_voice
        clean = [(t, p['x']) for t, p in taken_apart.items() if p['voiced'] >= VOICE_FROM]
        own_hz = float(np.median(np.concatenate([p['f0'][p['f0'] > 0] for t, p in taken_apart.items() if p['voiced'] >= VOICE_FROM])))
        sound_voice.reference(ROOT, MASTERS, others)
        banks = {v: sound_voice.bank(ROOT, MASTERS, v, RATE) for v in others}
        said = sound_voice.hear_takes(ROOT, clean, RATE)
        print('another voice for %d timbres: %s (the recorded voice: %d Hz)' % (sum(1 for d in group.values() if 'like' in d), ', '.join('%s %d Hz' % (v, banks[v]['hz']) for v in others), own_hz), flush=True)
    cross = {n: bool(whose) and d.get('for') != whose for n, d in group.items()}  # (a timbre of the other sex than the recording)
    for name, files, opts, note in kinds:
        share = {f: taken_apart[os.path.splitext(f)[0]]['voiced'] for f in files}
        can = [f for f in files if not AIR_UNDER <= share[f] < VOICE_FROM]  # (voice and air woven together: the header says why it is left to the recording's own timbre)
        told = ' '.join('%d%%%s' % (round(share[f] * 100), ' (air)' if share[f] < AIR_UNDER else '' if share[f] >= VOICE_FROM else ' (mixed: left out)') for f in files)
        if not can:
            # none of its takes can be made (a throat cleared is voice and noise at once): a timbre of the recording's own sex says it by the
            # recording itself (the same sex's rough sound, the person's own pitch on it); the other sex has none of it and is silent there
            for n in group:
                if not cross[n]:
                    lines[n].append('%s%s%s %s%s   # %s — запис як є: у тембр не переробляється (голос і шум разом)' % (name, MARK, n, ' '.join('file:' + f for f in in_mod[name]), (' ' + ' '.join(opts)) if opts else '', note))
                else:
                    lines[n].append('@none %s%s%s   # %s — нема: голос і шум разом у тембр іншої статі не переробляються; людина тут мовчить, доки не буде свого запису' % (name, MARK, n, note))
            print('%-16s 0 of %d takes made (voice in each: %s): the recording itself for timbres of its own sex, none for the other' % (name, len(files), told))
            continue
        counts = []
        for n, d in group.items():
            mine = spread_of([f for f in can if not (cross[n] and share[f] < AIR_UNDER)], TAKES)  # (the recording's breath is not the other sex's breath)
            counts.append(len(mine))
            names = []
            for f in mine:
                take = os.path.splitext(f)[0]
                parts = taken_apart[take]
                other = sound_voice.sing(parts, said[take], banks[d['like']], own_hz, RATE) if 'like' in d and take in said else None
                y = as_timbre(parts, d, files.index(f) * 7919 + len(n), other, cross[n]).astype(np.float32)[:, None]
                was, now = levels[take], lufs(y, RATE)
                peak = float(np.max(np.abs(y)))
                gain = 10 ** ((was - now) / 20) if now > -100 else 1.0
                if peak * gain > PEAK_MAX:
                    gain = PEAK_MAX / peak
                y = fade(y * gain, RATE, 0.006)
                out = (take[:-len(MARK + base)] if base else take) + MARK + n + '.ogg'
                write(os.path.join(SOUNDS, out), y)
                made.add(out.lower())
                names.append(out)
            if names:
                lines[n].append('%s%s%s %s%s   # %s' % (name, MARK, n, ' '.join('file:' + o for o in names), (' ' + ' '.join(opts)) if opts else '', note))
            else:
                lines[n].append('@none %s%s%s   # %s — нема: у записі це лише повітря, а повітря іншої статі чути як чуже; людина тут мовчить, доки не буде свого запису' % (name, MARK, n, note))
        print('%-16s %s of %d takes made (a timbre keeps %d at most) · voice in each: %s' % (name, ' · '.join(str(c) for c in sorted(set(counts), reverse=True)), len(files), TAKES, told), flush=True)
    have = set(k[0] for k in kinds)
    for name in order:  # a sound this voice did not record: its timbres do not say it
        if name not in have:
            for n in group:
                lines[n].append('@none %s%s%s   # нема: у записі голосу «%s» цього вигуку нема' % (name, MARK, n, base))
            print('%-16s not recorded by «%s»: its timbres are silent there' % (name, base))


def cmd_timbres(a):
    defs = timbre_lines()
    if not defs:
        fail('no «@timbre <name> …» line in assets/story/sounds')
    recorded = set(n for n, d in defs.items() if 'rec' in d)
    shaped = {n: d for n, d in defs.items() if any(k in d for k in SHAPING)}
    for n, d in defs.items():
        if 'rec' in d and (n in shaped or 'of' in d):
            fail('timbre «%s»: «rec» is a recording as it is — a voice made of it is a line of its own with of=%s' % (n, n))
        if 'of' in d and d['of'] not in recorded:
            fail('timbre «%s»: of=%s — no recorded voice («rec») is named so' % (n, d['of']))
        if 'of' in d and n not in shaped:
            fail('timbre «%s»: of=%s and no word that makes it another voice (%s)' % (n, d['of'], ' '.join(k + '=' for k in SHAPING)))
    speech, order = {}, []  # every «speech_…» line of the catalogue: name → (its files, its options, its note); the plain names in the files' order
    for f in sorted(os.listdir(SOUNDS)):
        if not f.endswith('.txt'):
            continue
        with open(os.path.join(SOUNDS, f), encoding='utf-8-sig') as h:
            for raw in h:
                line, _, note = raw.rstrip('\n').partition('#')
                w = line.split()
                if len(w) < 2 or not w[0].startswith('speech_'):
                    continue
                speech[w[0]] = ([t[5:] for t in w[1:] if t.startswith('file:')], [t for t in w[1:] if not t.startswith('file:')], note.strip())
                if MARK not in w[0]:
                    order.append(w[0])
    if not order:
        fail('no «speech_…» name in the catalogue to make timbres of')
    made, lines, twice = set(), {n: [] for n in shaped}, []
    own_for = next((d.get('for') for n, d in defs.items() if n not in shaped and 'rec' not in d and d.get('for')), None)  # (whose voice the first recording itself is)
    for base in [None] + sorted(set(d['of'] for d in shaped.values() if 'of' in d)):
        group = {n: d for n, d in shaped.items() if d.get('of') == base}
        if group:
            timbres_of(base, group, defs, speech, order, defs[base].get('for') if base else own_for, made, lines, twice)
    for n, d in shaped.items():
        with open(os.path.join(SOUNDS, 'speech%s%s.txt' % (MARK, n)), 'w', encoding='utf-8', newline='\n') as h:
            h.write('# Тембр «%s»: %s\n# Зроблено інструментом (node tools/sound.js timbres) із вигуків %s — руками не правити: слова тембру в рядку «@timbre %s», гучність і розкид — у рядку самого вигуку.\n' % (
                n, d['note'], 'записаного голосу «%s» (speech_…%s%s)' % (d['of'], MARK, d['of']) if 'of' in d else 'speech_…', n))
            h.write('\n'.join(lines[n]) + '\n')
    gone = 0
    for f in os.listdir(SOUNDS):
        m = re.fullmatch(r'(speech_.+?)__(.+)\.(ogg|txt)', f) or re.fullmatch(r'(speech)__(.+)\.(txt)', f)
        if m and m.group(2) in recorded:
            continue  # (a recorded voice: its own files, none of them made here)
        if m and ((f.endswith('.ogg') and f.lower() not in made) or (f.endswith('.txt') and m.group(2) not in shaped)):
            os.remove(os.path.join(SOUNDS, f))
            gone += 1
    size = sum(os.path.getsize(os.path.join(SOUNDS, f)) for f in os.listdir(SOUNDS) if f.lower() in made)
    print('\n%d timbres (%s), %d files, %.1f MB; %d old files removed' % (len(shaped), ', '.join(shaped), len(made), size / 1048576, gone))
    plain = [n for n in defs if n not in shaped and n not in recorded]
    if recorded:
        print('recorded voices, left as they are: ' + ', '.join(sorted(recorded)))
    if plain:
        print('the recording itself, under the plain names: ' + ', '.join(plain))
    if twice:
        print('!! %d takes had no uncompressed master (sound_masters/): made from the .ogg, compressed twice — make them anew with «add --force»: %s …' % (len(twice), ', '.join(twice[:4])))


def cmd_list(a):
    names, files = catalogue()
    total = 0
    rows = [f for f in sorted(os.listdir(SOUNDS)) if re.search(r'\.(ogg|wav|mp3)$', f, re.I)] if os.path.isdir(SOUNDS) else []
    for f in rows:
        p = os.path.join(SOUNDS, f)
        x, rate = read(p)
        total += os.path.getsize(p)
        v = facts(x, rate)
        print('%-28s %6.2f s  %s  %6.1f LUFS  peak %5.1f dB  %6.1f KB  %s' % (f, v['sec'], 'mono  ' if v['ch'] == 1 else 'stereo', v['lufs'], v['peak'], os.path.getsize(p) / 1024, '' if f.lower() in files else 'NOT in the catalogue'))
    print('%d files, %.1f KB in all' % (len(rows), total / 1024))


def cmd_add(a):
    if not re.fullmatch(r'[a-z][a-z0-9_]*', a.name):
        fail('a name is small Latin letters, digits and «_»: ' + a.name)
    if a.name.startswith('sfx_'):
        fail('a name on «sfx_» hides the game\'s own sound')
    if not a.own and not a.src:
        fail('where is the file from? --src "<where, whose, licence>" or --own (the user\'s own recording)')
    kind = a.kind
    voice = getattr(a, 'voice', None)
    if voice and (not re.fullmatch(r'[a-z][a-z0-9]*(_[a-z0-9]+)*', voice)):
        fail('--voice: a voice\'s name is small Latin letters, digits and single «_»: ' + voice)
    mark = (MARK + voice) if voice else ''
    x, rate = read(a.file)
    print('from:')
    say_facts(os.path.basename(a.file), facts(x, rate), rate)
    if a.cut and a.join:
        fail('--cut or --join, not both')
    if a.cut:
        x = x[int(a.cut[0] * rate):int(a.cut[1] * rate)]
        if len(x) == 0:
            fail('--cut: nothing between these seconds')
    if a.join:
        x = joined(x, rate, a.join)
    names, files = catalogue()
    os.makedirs(SOUNDS, exist_ok=True)
    parts = pieces(x, rate, a.gap) if a.split else [x]
    if a.split and len(parts) < 2 and not a.voice:  # (a recorded voice may have said a sound once: that one take is its sound)
        fail('--split: %d piece found — no pauses of %.2f s in it (try --gap)' % (len(parts), a.gap))
    made = []
    print('made (%s):' % kind)
    # the takes of one recording are levelled each by itself, but one said much more quietly than the rest (over 6 dB under their middle)
    # keeps what is over those 6 dB: a soft take stays a soft take, and its noise is not raised with it
    was = [lufs(np.mean(part, axis=1, keepdims=True), rate) for part in parts]
    middle = float(np.median(was))
    for i, part in enumerate(parts):
        base = a.name + ('_%d' % (i + 1) if a.split else '') + mark
        path = os.path.join(SOUNDS, base + '.ogg')
        if os.path.exists(path) and not a.force:
            fail(base + '.ogg is there already (--force writes over it)')
        under = max(0.0, middle - was[i] - 6.0) if a.split else 0.0
        y, capped, seam = make(part, rate, kind, a, under)
        if len(y) < int(0.02 * RATE):
            fail(base + ': nothing left of it after the cut')
        v = write(path, y, kind)
        os.makedirs(MASTERS, exist_ok=True)
        sf.write(os.path.join(MASTERS, base + '.wav'), y, RATE, subtype='PCM_24')
        say_facts(base + '.ogg', v, RATE)
        if capped:
            print('   quieter than its standard: louder, its peaks would clip — raise vol= in the catalogue if it is too quiet in game')
        if seam > 0:
            print('   loops: %.2f s of its end crossfaded into its start' % seam)
        made.append(base)
    if a.name + mark in names:
        print('the catalogue has «%s» already (%s): its line is left as it is' % (a.name + mark, names[a.name + mark]))
        return
    cat = os.path.join(SOUNDS, ('speech' + mark if voice else a.to) + '.txt')
    note = '; '.join(t for t in [a.note, ('джерело: ' + a.src) if a.src else 'власний запис'] if t)
    more = getattr(a, 'more', None) or []
    line = '%s%s %s%s%s   # %s\n' % (a.name, mark, ' '.join('file:%s.ogg' % m for m in made), ' spread=0.04' if a.split else '', ''.join(' ' + t for t in more), note)
    text = ''
    if os.path.exists(cat):
        with open(cat, encoding='utf-8') as h:
            text = h.read()
    elif voice:
        text = '# Голос «%s»: власний запис цієї людини (node tools/sound.js voice), кожен рядок — її дублі одного вигуку.\n' % voice
    with open(cat, 'w', encoding='utf-8', newline='') as h:
        h.write(text + ('' if text == '' or text.endswith('\n') else '\n') + line)
    print('catalogue: %s ← %s' % (os.path.basename(cat), line.strip()))
    print('Next: the «Звуки» tab — listen at a distance, set vol / pitch / range, «Зберегти».')


VOICE_FILES = [  # how a recorded file's name begins → the sound it is (the first that fits; «хм» last: the asking one is told by its mark)
    ('вдих', 'speech_breath'), ('зітх', 'speech_sigh'), ('сміш', 'speech_chuckle'), ('хе', 'speech_chuckle'), ('ха', 'speech_chuckle'), ('кхм', 'speech_khm'), ('кхе', 'speech_khm'), ('угу', 'speech_uhu'), ('ага', 'speech_uhu'),
    ('ем', 'speech_em'), ('е-е', 'speech_em'), ('ого', 'speech_oh'), ('ох', 'speech_oh'), ('о', 'speech_oh'), ('хм', 'speech_hm'), ('гм', 'speech_hm')]


def cmd_voice(a):
    """another person's recording of the voice sounds → a timbre of its own («A recorded voice» in the header)"""
    if not re.fullmatch(r'[a-z][a-z0-9]*(_[a-z0-9]+)*', a.name):
        fail('a voice\'s name is small Latin letters, digits and single «_»: ' + a.name)
    if not os.path.isdir(a.folder):
        fail('no folder ' + a.folder)
    defs = timbre_lines()
    if a.name in defs and 'rec' not in defs[a.name]:
        fail('«%s» is a timbre already, and not a recorded one: give the voice another name' % a.name)
    plain = {}  # the first recording's lines: a sound's options (vol=) and note go over to the new voice's line
    for f in sorted(os.listdir(SOUNDS)):
        if f.endswith('.txt'):
            with open(os.path.join(SOUNDS, f), encoding='utf-8-sig') as h:
                for raw in h:
                    line, _, note = raw.rstrip('\n').partition('#')
                    w = line.split()
                    if len(w) >= 2 and w[0].startswith('speech_') and MARK not in w[0]:
                        plain[w[0]] = ([t for t in w[1:] if not t.startswith('file:') and not t.startswith('spread=')], note.split(';')[0].strip())
    found, strange = {}, []
    for f in sorted(os.listdir(a.folder)):
        if not re.search(r'\.(m4a|mp3|wav|ogg|flac|aac|mp4|webm|opus)$', f, re.I):
            continue
        low = f.lower()
        kind = next((k for start, k in VOICE_FILES if low.startswith(start)), None)
        if kind == 'speech_hm' and re.search(r'[_?]|пит', os.path.splitext(low)[0]):
            kind = 'speech_hm_ask'
        if kind is None or kind in found:
            strange.append(f)
        else:
            found[kind] = f
    if not found:
        fail('no recording in %s is named as a sound (Хммм, хмм_, Емм, Угу, О, Зітхання, Смішок, Кхм, Вдих…)' % a.folder)
    for kind, f in found.items():
        print('\n%s ← %s' % (kind, f))
        opts, note = plain.get(kind, ([], ''))
        cmd_add(argparse.Namespace(file=os.path.join(a.folder, f), name=kind, kind='voice', cut=None, join=None, split=True, gap=a.gap, mono=False, stereo=False, lufs=None,
                                   src=None, own=True, note=note, to='story', force=a.force, voice=a.name, more=opts))
    if a.name not in defs:
        tfile = os.path.join(SOUNDS, 'timbres.txt')
        with open(tfile, encoding='utf-8') as h:
            text = h.read()
        rows = text.split('\n')
        pool = [i for i, r in enumerate(rows) if r.startswith('@timbre') and 'only' not in r.partition('#')[0].split()]
        at = pool[-1] + 1 if pool else len(rows) - (1 if rows and rows[-1] == '' else 0)  # among the voices the game hands out, not under the story people's «only» ones
        rows.insert(at, '@timbre %s  for=%s  age=%s  pitch=0.96..1.04  rec    # власний запис людини: %s' % (a.name, a.who, a.age, a.name))
        with open(tfile, 'w', encoding='utf-8', newline='') as h:
            h.write('\n'.join(rows) + ('' if rows[-1] == '' else '\n'))
        print('\ntimbres.txt ← @timbre %s for=%s age=%s rec' % (a.name, a.who, a.age))
    missing = [k for k in plain if k not in found]
    if missing:
        print('not recorded (this voice will not say them): ' + ', '.join(missing))
    if strange:
        print('files not taken (no sound begins so, or a second file of one sound): ' + ', '.join(strange))
    print('Next: node tools/sound.js page — the voice is a row of «Голоси»; a build puts it into the game.')


def cmd_babble(a):
    """the people's wordless talk: the manner of real reading and each voice's hums → assets/story/sounds/babble.txt (tools/sound_babble.py)"""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # (the portable Python does not look beside the script)
    import sound_babble
    sound_babble.cmd_babble(sys.modules[__name__], os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))


def cmd_talk(a):
    """the people's wordless talk heard outside the game: a talk of these voices as a row of the page, or its numbers (tools/sound_talk.py)"""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import sound_talk
    sound_talk.cmd_talk(sys.modules[__name__], os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')), a)


def main():
    p = argparse.ArgumentParser(prog='node tools/sound.js', description='A raw sound file made ready for the mod (the header of tools/sound.py says each word).')
    sub = p.add_subparsers(dest='cmd', required=True)
    i = sub.add_parser('info')
    i.add_argument('files', nargs='+')
    sub.add_parser('list')
    sub.add_parser('page')
    sub.add_parser('timbres')
    sub.add_parser('babble')
    k = sub.add_parser('talk')
    k.add_argument('voices', nargs='*')
    k.add_argument('--lines')
    k.add_argument('--set', action='append')
    k.add_argument('--name')
    k.add_argument('--note')
    k.add_argument('--measure', action='store_true')
    k.add_argument('--mark', action='store_true')
    v = sub.add_parser('voice')
    v.add_argument('name')
    v.add_argument('folder')
    v.add_argument('--for', dest='who', required=True, choices=['man', 'woman'])
    v.add_argument('--age', default='young,grown,old')
    v.add_argument('--gap', type=float, default=0.35)
    v.add_argument('--force', action='store_true')
    a = sub.add_parser('add')
    a.add_argument('file')
    a.add_argument('name')
    a.add_argument('kind', nargs='?', default='shot', choices=sorted(KINDS))
    a.add_argument('--cut', nargs=2, type=float)
    a.add_argument('--join')
    a.add_argument('--split', action='store_true')
    a.add_argument('--gap', type=float, default=0.35)
    a.add_argument('--mono', action='store_true')
    a.add_argument('--stereo', action='store_true')
    a.add_argument('--lufs', type=float)
    a.add_argument('--src')
    a.add_argument('--own', action='store_true')
    a.add_argument('--note')
    a.add_argument('--to', default='story')
    a.add_argument('--voice')
    a.add_argument('--force', action='store_true')
    args = p.parse_args()
    {'info': cmd_info, 'list': cmd_list, 'add': cmd_add, 'page': cmd_page, 'timbres': cmd_timbres, 'voice': cmd_voice, 'babble': cmd_babble, 'talk': cmd_talk}[args.cmd](args)


if __name__ == '__main__':
    main()
