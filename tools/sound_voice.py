# A recorded take in the timbre of another voice: the conversion behind the timbre word «like=<voice>» of tools/sound.py («timbres»).
# Why it is here: the wordless sounds of people are recorded by one voice, and a shift of that voice (pitch, throat) stays that voice — the
# user, 2026-11-13: «з чоловічими я просто себе чую всюди». The speech model's own voices saying the sounds themselves were no way either
# («вимовляють щось незрозуміле»: a speech model reads letters, it does not hum). So: the take's playing stays (its pitch line, loudness,
# timing), the voice becomes another's.
# How, with nothing but what is on this machine (no outside code is fetched or run):
#   the other voice   one of the speech model's own nameless voices («Speaker_53»: voice-proto/holos/voices.pt), made to speak a minute of
#                     reference text by the voice host (reference): sound_masters/voices/<voice>.wav
#   what is said      at each 20 ms, of the take and of the reference alike, as the speech recogniser hears it (the encoder of Whisper
#                     large-v3, faster-whisper: here since the voice research): hear
#   the conversion    each moment of the take takes the spectrum of the K moments of the reference that sound most like it (WORLD's
#                     envelope; the voiced moments only), its own pitch moved to the other voice's height, its own loudness: sing
#   a closed mouth    stays closed. A «hm» is hummed with the mouth shut: nearly none of its sound lies where an open mouth sounds
#                     (600–3500 Hz: 0–1% measured on the takes). A reference of speech is mostly open vowels, and a hum given the spectrum
#                     of the moment «most like it» was often given a vowel's: the made «hm» of several voices had 10–40% of its sound
#                     there, jumping by 30–85 points within one take — syllables. The user: «чому вони говорять знову, це ж звуки, а не
#                     слова» (2026-11-13; the first voice of the trial happened to be clean, the whole set showed it). So of the moments
#                     that sound alike, those whose mouth is as open as the take's are taken (open_share, OPEN_WEIGHT), eased over
#                     neighbouring moments. And a moment hummed with the mouth shut takes no matched moment at all but the other voice's
#                     own closed sound: the mean spectrum of its «м», «н» (hum) — one still shape for the whole hum, that voice's nose,
#                     not this one's. (Holding what was taken to the take's own low and open parts instead was tried first: no syllables,
#                     but the voices went back to the recorded one — 0.73–0.80 to the user where it had been 0.53–0.75.)
# Then tools/sound.py joins it as it joins a shifted timbre: the voice from the vocoder, the air the take's own.
# Measured on the first three voices (the voice encoder's timbre half, 1 = the same voice): the converted takes 0.61–0.75 to their target
# (the raw takes: 0.12–0.37) and 0.55–0.75 to the user; a shifted timbre moved toward no target at all. The ear's word on it: his «ок гуд».
# A voice's bank (what is said + the spectra of its reference) is kept beside its reference (<voice>.npz, ~10 MB): made once.
# Needs numpy, scipy, soundfile, pyworld, faster-whisper (and torch + the voice host for a new reference): the Python of voice-proto/pytools.
import os
import site
import struct
import subprocess
import sys

import numpy as np
import soundfile as sf
from scipy import signal

K = 2  # how many moments of the reference make one of the take (6 was tried: a little nearer the user, farther from the target — more is blurrier)
NEAR = 60          # of the moments that sound most like the take's, so many are weighed by how open the mouth is there
OPEN_WEIGHT = 3.0  # how much a mouth open otherwise than the take's counts against a moment (likeness is 0…1, the open share 0…1)
CLOSED = 0.05      # a moment with less of its sound than this in the open-mouth band is hummed with the mouth shut (the takes' «hm»: 0–1%)
HUM_UNDER = 0.10   # the other voice's own closed moments (its «м», «н»): those under this; too few of them (HUM_LEAST) — its closest ones
HUM_LEAST = 40
REFERENCE = [
    'Добрий день. Сьогодні гарна погода, але ввечері буде дощ.',
    'Хм. Ммм. Угу. Ага. Ем. Ну. Мм-гм.',
    'Мама мила раму, а ми несемо молоко на млин.',
    'О! Ох. Ех. Ай. Ой, як же так? Оце так новина!',
    'Ха-ха-ха. Хе-хе. Ну ти й вигадав, друже.',
    'Хто там ходить уночі біля нашої хати? Хіба я знаю.',
    'Не знаю, не знаю. Може, завтра. А може, й ніколи.',
    'Візьми сокиру, наноси дров і розпали вогонь, поки не стемніло.',
    'Колись давно тут стояла велика зала, і в ній горів вогонь.',
    'Угу, зрозумів. Гаразд, зроблю. Тільки дай мені трохи часу.',
    'Шість шапок шила швачка, чотири чорні чаплі чекали на човен.',
    'Енн, ум, ом, ан, он, мене, мною, імена, знамено.',
    'Ммм, смачно. Ааа, он воно що. Ооо, оце так. Еее, ні. Ууу, холодно.',
    'Ми мали мало меду, а мама нам намолола муки на млині.',
]
_whisper = None


def _fail(text):
    print('sound: ' + text)
    sys.exit(1)


def _ears(root):
    global _whisper
    if _whisper is None:
        os.environ.setdefault('HF_HOME', os.path.join(root, 'voice-proto', 'pytools', 'hf'))
        for sp in site.getsitepackages():
            for sub in ('nvidia/cublas/bin', 'nvidia/cudnn/bin'):
                p = os.path.join(sp, sub)
                if os.path.isdir(p):
                    os.add_dll_directory(p)
                    os.environ['PATH'] = p + os.pathsep + os.environ['PATH']
        from faster_whisper import WhisperModel
        _whisper = WhisperModel('large-v3', device='cpu', compute_type='int8')
    return _whisper


def to16(x, rate):
    return (signal.resample_poly(x, 160, rate // 100) if rate != 16000 else x).astype(np.float32)


def hear(root, x16):
    """what is said, a row each 20 ms (each of length 1): the recogniser's encoder over the sound, 30 s at a time"""
    from faster_whisper.audio import pad_or_trim
    w = _ears(root)
    rows = []
    for a in range(0, len(x16), 480000):
        chunk = x16[a:a + 480000]
        enc = np.array(w.encode(pad_or_trim(w.feature_extractor(chunk)))).astype(np.float32)[0]
        rows.append(enc[:int(np.ceil(len(chunk) / 320))])
    f = np.concatenate(rows)
    return f / (np.linalg.norm(f, axis=1, keepdims=True) + 1e-9)


def hear_takes(root, takes, rate):
    """the takes heard in one go (joined with pauses: a short take alone would be 30 s of silence to the recogniser): name → its rows"""
    gap = np.zeros(int(0.4 * 16000), np.float32)
    joined, at, pos = [], [], 0
    for name, x in takes:
        x16 = to16(x, rate)
        at.append((name, pos, len(x16)))
        joined += [x16, gap]
        pos += len(x16) + len(gap)
    if not joined:
        return {}
    heard = hear(root, np.concatenate(joined))
    return {name: heard[a // 320:(a + n) // 320 + 1] for name, a, n in at}


def reference(root, masters, wanted):
    """the reference speech of each voice (made by the voice host where there is none yet): <masters>/voices/<voice>.wav"""
    folder = os.path.join(masters, 'voices')
    os.makedirs(folder, exist_ok=True)
    missing = [v for v in wanted if not os.path.isfile(os.path.join(folder, v + '.wav'))]
    if not missing:
        return folder
    import torch
    store = os.path.join(root, 'voice-proto', 'holos', 'voices.pt')
    host = os.path.join(root, 'voicehost', 'out', 'RavenVoice.exe')
    if not os.path.isfile(store) or not os.path.isfile(host):
        _fail('like=: no reference speech of %s, and none can be made here (the speech model\'s voices: %s; the voice host: %s)' % (', '.join(missing), store, host))
    voices = torch.load(store, map_location='cpu', weights_only=True)
    for v in missing:
        if v not in voices:
            _fail('like=%s: the speech model has no such voice (it has: %s)' % (v, ', '.join(sorted(n for n in voices if n.startswith('Speaker_')))))
    data, tmp = os.path.join(root, 'voicehost', 'data'), os.path.join(folder, '_data')
    os.makedirs(tmp, exist_ok=True)
    for f in ['model.onnx', 'vocab.txt', 'stress_dict.tsv', 'contours.tsv', 'language.txt']:
        if not os.path.exists(os.path.join(tmp, f)):
            os.link(os.path.join(data, f), os.path.join(tmp, f))  # (a hard link: the model is not copied)
    with open(os.path.join(tmp, 'voices.bin'), 'wb') as h:
        h.write(struct.pack('<i', -(100000 + len(missing))))
        for v in missing:
            h.write(voices[v].reshape(-1).numpy().astype('<f4').tobytes())
            h.write(struct.pack('<7f', 1, 0, 1, 1, 1, 1, 1))  # pitch, fx, delivery: the voice as the model has it
    jobs = [(i, k, os.path.join(folder, '%s_%02d.wav' % (v, k)), text) for i, v in enumerate(missing) for k, text in enumerate(REFERENCE)]
    proc = subprocess.Popen([host, '--data', tmp, '--threads', '4'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    line = proc.stdout.readline().decode('utf-8', 'replace').strip()
    if not line.startswith('READY'):
        _fail('the voice host did not start: ' + line)
    for n, (i, k, wav, text) in enumerate(jobs):
        proc.stdin.write(('SAY\t%d\tn\t%d\t%d\t%s\t%s\n' % (n + 1, i, 8 + n, wav, text)).encode('utf-8'))
    proc.stdin.flush()
    done = 0
    while done < len(jobs):
        line = proc.stdout.readline().decode('utf-8', 'replace').strip()
        if not line:
            break
        if line.startswith('OK') or line.startswith('ERR'):
            done += 1
    proc.stdin.close()
    proc.wait()
    for v in missing:
        parts, rate = [], 24000
        for k in range(len(REFERENCE)):
            p = os.path.join(folder, '%s_%02d.wav' % (v, k))
            if not os.path.isfile(p):
                _fail('like=%s: the voice host made no sound of «%s»' % (v, REFERENCE[k]))
            x, rate = sf.read(p, dtype='float32')
            parts += [x if x.ndim == 1 else x[:, 0], np.zeros(int(0.2 * rate), 'float32')]
            os.remove(p)
        sf.write(os.path.join(folder, v + '.wav'), np.concatenate(parts), rate)
        print('   reference speech of %s made: %.0f s' % (v, sum(len(p) for p in parts) / rate), flush=True)
    return folder


def bank(root, masters, voice, rate):
    """a voice's bank: for each voiced 20 ms of its reference — what is said there, its spectrum and breath (in log / as is), and the voice's height"""
    import pyworld as pw
    folder = reference(root, masters, [voice])
    wav, kept = os.path.join(folder, voice + '.wav'), os.path.join(folder, voice + '.npz')
    if os.path.isfile(kept) and os.path.getmtime(kept) >= os.path.getmtime(wav):
        z = np.load(kept)
        if int(z['rate']) == rate:
            sp = z['sp'].astype(np.float32)
            return dict(f=z['f'].astype(np.float32), sp=sp, ap=z['ap'].astype(np.float32), hz=float(z['hz']), open=open_share(np.exp(sp.astype(np.float64)), rate))
    x, r = sf.read(wav, dtype='float64')
    if x.ndim > 1:
        x = x[:, 0]
    said = hear(root, to16(x, r))
    y = np.ascontiguousarray(signal.resample_poly(x, rate // 100, r // 100))
    f0, t = pw.harvest(y, rate, f0_floor=55.0, f0_ceil=450.0)
    sp = pw.cheaptrick(y, f0, t, rate)
    ap = pw.d4c(y, f0, t, rate)
    n = min(len(said), len(f0) // 4)
    power = np.array([np.sum(sp[4 * j:4 * j + 4]) for j in range(n)])
    ok = np.array([(f0[4 * j:4 * j + 4] > 0).sum() >= 3 for j in range(n)]) & (power > np.percentile(power, 40) * 0.05)
    b = dict(f=said[:n][ok], sp=np.array([np.mean(np.log(sp[4 * j:4 * j + 4] + 1e-16), axis=0) for j in range(n)])[ok].astype(np.float32),
             ap=np.array([np.mean(ap[4 * j:4 * j + 4], axis=0) for j in range(n)])[ok].astype(np.float32), hz=float(np.median(f0[f0 > 0])))
    np.savez(kept, f=b['f'].astype(np.float16), sp=b['sp'].astype(np.float16), ap=b['ap'].astype(np.float16), hz=b['hz'], rate=rate)
    b['open'] = open_share(np.exp(b['sp'].astype(np.float64)), rate)
    print('   the voice %s heard: %d voiced moments of %.0f s, %d Hz' % (voice, len(b['f']), len(x) / r, b['hz']), flush=True)
    return b


def bands(rate, bins):
    hz = np.linspace(0, rate / 2, bins)
    return (hz >= 80) & (hz < 600), hz >= 600, (hz >= 600) & (hz < 3500)


def open_share(sp, rate):
    """how open the mouth is at each moment: the share of the sound (power spectra, a row a moment) that lies where an open mouth sounds"""
    low, high, mid = bands(rate, sp.shape[1])
    m = sp[:, mid].sum(axis=1)
    return m / (m + sp[:, low].sum(axis=1) + 1e-20)


def sing(parts, said, b, own_hz, rate):
    """the take's voice as that voice would sound it: (pitch, spectrum, breath) for the vocoder, frame for frame of the take (5 ms)"""
    own = parts['sp']
    rows = min(len(said), (len(own) + 3) // 4)
    said = said[:rows]
    mine = open_share(np.array([own[4 * j:4 * j + 4].mean(axis=0) for j in range(rows)]), rate)  # (the take's own mouth, at the recogniser's 20 ms)
    sim = said @ b['f'].T
    near = np.argsort(-sim, axis=1)[:, :NEAR]
    score = np.take_along_axis(sim, near, axis=1) - OPEN_WEIGHT * np.abs(mine[:, None] - b['open'][near])
    best = np.argsort(-score, axis=1)[:, :K]
    nn = np.take_along_axis(near, best, axis=1)
    w = np.take_along_axis(score, best, axis=1)
    w = np.exp((w - w.max(axis=1, keepdims=True)) * 20)
    w /= w.sum(axis=1, keepdims=True)
    e = np.einsum('fk,fkb->fb', w, b['sp'][nn])
    a = np.einsum('fk,fkb->fb', w, b['ap'][nn])
    # One colour for one sound. A stretch of voice (a hum, a sigh, one «ха» of a chuckle) takes ONE spectrum of the other voice — the mean
    # of what its moments matched; with the mouth shut, that voice's own closed sound (its «м», «н») — and all that changes within the
    # stretch is what changed in the take itself (its own spectrum against its own mean over the stretch, smoothed over ~200 Hz: how the
    # mouth moved, not whose it is). Spectra matched moment by moment made a sigh pass through several vowels: «звучить як вимова слова»
    # (the user, 2026-11-13) — the sound can now say nothing the take did not.
    hum = np.argsort(b['open'])[:max(HUM_LEAST, int(np.sum(b['open'] < HUM_UNDER)))]
    f0 = parts['f0']
    voiced = np.array([(f0[4 * j:4 * j + 4] > 0).sum() >= 2 for j in range(rows)])
    mine_log = np.log(np.array([own[4 * j:4 * j + 4].mean(axis=0) for j in range(rows)]) + 1e-16)
    box = np.ones(9) / 9
    mine_log = np.array([np.convolve(np.pad(r, 4, mode='edge'), box, mode='valid') for r in mine_log])
    j = 0
    while j < rows:
        if not voiced[j]:
            j += 1
            continue
        k = j
        while k < rows and voiced[k]:
            k += 1
        shut = np.median(mine[j:k]) < CLOSED
        colour = b['sp'][hum].mean(axis=0) if shut else e[j:k].mean(axis=0)
        breath = b['ap'][hum].mean(axis=0) if shut else a[j:k].mean(axis=0)
        moved = np.clip(mine_log[j:k] - mine_log[j:k].mean(axis=0), -2.8, 2.8)  # (±12 dB: its own movement, not a hole or a spike of one frame)
        e[j:k] = colour + moved
        a[j:k] = breath
        j = k
    t = parts['t']
    at = np.clip(np.interp(t, np.arange(len(said)) * 0.02 + 0.01, np.arange(len(said))), 0, len(said) - 1)  # (20 ms rows to the take's 5 ms frames)
    lo = np.floor(at).astype(int)
    hi = np.minimum(lo + 1, len(said) - 1)
    ww = (at - lo)[:, None]
    sp2 = np.exp(e[lo] * (1 - ww) + e[hi] * ww).astype(np.float64)
    ap2 = np.clip(a[lo] * (1 - ww) + a[hi] * ww, 0, 1).astype(np.float64)
    sp2 *= (np.sum(own, axis=1) / (np.sum(sp2, axis=1) + 1e-16))[:, None]  # (as loud, frame by frame, as the take was)
    return parts['f0'] * (b['hz'] / own_hz), sp2, ap2
