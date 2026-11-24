# The people's wordless talk heard outside the game (run it through tools/sound.js, which finds the Python):
#
#   node tools/sound.js talk <voice> <voice> [<voice> …] [--lines <file>] [--set <Class.Name>=<n> …] [--name <row>] [--note "<words>"]
#       a talk of these voices, one line each in turn (the first voice says the first line), written as one row of the listening page
#       (build/sound_try/<row>/, then «page» is made anew): what a new voice or a number sounds like in a talk, with no game run.
#       --lines   a text file, a line of the talk a line (none: the talk below); --set   a number of the game's other than it is (a trial:
#       «NpcBabble.Calm=0.7»); --name   the page row (none: talk_<voices>); several voices more than two: each says in turn
#   node tools/sound.js talk --measure [<voice> …]
#       what can be told by numbers, no ear: every voice the game deals (or these) says the lines below, and per voice is counted —
#       a sound cut off before its fade (its hum ran out: a click), samples the line's level would clip (squared off), the hums it has
#   node tools/sound.js talk --mark     the copy's mark written anew (see below) — only once this file plays what the game does
#
# A COPY of the game's rules (src/NpcBabble.cs, from «the line's rules» to their end: Line, Steady, PhraseOf, H, ClausesOf, Render, Pick,
# Said, Sounds_, At, Need), line by line in Python; its numbers it reads from the game's own source each run (NpcBabble's fields, the
# length a person's line is shown from NpcSpeech.Make, the hero's pause from NpcTalk), so a number changed there is heard here at once.
# Outside the guarded block and copied by hand: how a voice's hums are loaded (NpcBabble.Read, VoiceOf) — change those, change Rules.voice.
# What it is not: the game's sound core (no place, no distance, no Vol between people: every line as said to you), a person's chattiness
# (an average one), its pitch within its timbre (1.0). The rules changed in the game (not a comment, not a space) stop the build
# (tools/arch_check.js «babble-copy») till this file plays the same and «--mark» writes the new mark into RULES_OF below.
import os, re, sys, math, hashlib, numpy as np

RULES_OF = '12992276a01bbd4a'  # the mark of the game's rules this copy plays (rules_mark): written by «talk --mark»

M32 = 0xFFFFFFFF
TALK = [  # the talk said when no --lines: questions, a «…», a «!», short and long, commas (each rule of the line is heard)
    'Слухай, ти бачила, що коїться біля старого броду?',
    'Бачила. Вода піднялась, і міст знову знесло.',
    'Третій раз за місяць! Хто його тепер лагодитиме?',
    'Мабуть, ми з тобою... Більше нікому.',
    'Ну що ж, завтра зранку візьму сокиру й подивлюсь, що там лишилось від опор.',
    'Тільки обережно, гаразд? Там вовки ходять ночами.',
]


def rules_text(root):
    with open(os.path.join(root, 'src', 'NpcBabble.cs'), encoding='utf-8') as h:
        t = h.read()
    a = t.find("// ---------------------------------------------------------------- the line's rules")
    b = t.find("// ---------------------------------------------------------------- end of the line's rules")
    if a < 0 or b < a:
        raise SystemExit('sound talk: src/NpcBabble.cs has no «the line\'s rules» … «end of the line\'s rules»')
    return t[a:b]


def rules_mark(root):
    """the game's rules as they act: comments and spaces out (tools/arch_check.js counts it the same way)"""
    body = re.sub(r'//[^\n]*', '', rules_text(root))
    return hashlib.sha1(re.sub(r'\s+', '', body).encode('utf-8')).hexdigest()[:16]


def numbers(root):
    """the game's numbers of the talk, read from its source: {'NpcBabble.Calm': 0.5, …, 'NpcBabble.Sounds': [...], …}"""
    out = {}
    for cls, f in (('NpcBabble', 'NpcBabble.cs'), ('NpcSpeech', 'NpcSpeech.cs'), ('NpcTalk', 'NpcTalk.cs'), ('NpcTimbre', 'NpcTimbre.cs')):
        with open(os.path.join(root, 'src', f), encoding='utf-8') as h:
            t = re.sub(r'//[^\n]*', '', h.read())  # (a field in a comment is none)
        for m in re.finditer(r'internal static (?:float|int) ([^;]+);', t):
            for part in m.group(1).split(','):
                k, _, v = part.partition('=')
                v = v.strip().rstrip('fF')
                try:
                    out[cls + '.' + k.strip()] = float(v)  # (0.5, .5, 1e-5 alike)
                except ValueError:
                    pass
        for m in re.finditer(r'static readonly (float|string)\[\] (\w+) = \{([^}]*)\};', t):
            items = [x.strip() for x in m.group(3).split(',') if x.strip()]
            out[cls + '.' + m.group(2)] = [float(x.rstrip('f')) for x in items] if m.group(1) == 'float' else [x.strip('"') for x in items]
        for m in re.finditer(r'const string (Vowels|Open) = "([^"]+)"', t):
            out[cls + '.' + m.group(1)] = m.group(2)
    return out


def share(x):
    """(uint)(x * 100f) as the game reckons it: its field is a float32 (0.57 is 0.5699999928), multiplied — so Mono does — in double: 56.
    NOT measured in the game; Python's own 0.57 * 100 would be 57. Used for Calm alone now (0.5: the same either way); the chances of the
    words (Third, Skip) are rounded to whole percents on both sides — pct"""
    return int(float(np.float32(x)) * 100.0)


def pct(x):
    """a chance as whole percents, as the game rounds it (Mathf.RoundToInt(x * 100f)): the same however the product is reckoned"""
    return int(round(float(np.float32(x)) * 100.0))


def lerp(a, b, t):
    t = 0.0 if t < 0 else 1.0 if t > 1 else t
    return a + (b - a) * t


def clamp(x, a, b):
    return a if x < a else b if x > b else x


class Rules:
    """NpcBabble's rules (the C#, member for member); N: the numbers. Counts what the measure wants as it goes (cuts, clipped)"""

    def __init__(self, N, root):
        self.N, self.root = N, root
        self.cuts = self.clipped = self.samples = 0
        self.manner, self.spots, self.step = [], {}, 0.01
        sounds = os.path.join(root, 'assets', 'story', 'sounds')
        self.files = {}  # catalogue name → its files
        for f in sorted(os.listdir(sounds)):
            if not f.endswith('.txt'):
                continue
            with open(os.path.join(sounds, f), encoding='utf-8-sig') as h:
                for raw in h:
                    l = raw.strip()
                    w = raw.partition('#')[0].split()
                    if len(w) >= 2 and w[0].startswith('speech_'):
                        self.files[w[0]] = [x[5:] for x in w[1:] if x.startswith('file:')]
                    if len(l) < 5 or l[0] != '@':
                        continue
                    w = l.split()
                    if w[0] == '@mannerstep' and len(w) >= 2:
                        self.step = clamp(float(w[1]), 0.004, 0.05)
                    elif w[0] == '@manner' and len(w) >= 4:
                        p = self.phrase_of(w)
                        if p:
                            self.manner.append(p)
                    elif w[0] == '@hum' and len(w) >= 6:
                        s = dict(Kind=w[2], First=int(w[3]), Count=int(w[4]), Hz=float(w[5]))
                        if s['First'] >= 0 and s['Count'] >= 64 and s['Hz'] >= 40:
                            self.spots.setdefault(w[1].lower(), []).append(s)
        self.sounds = sounds
        self.most_hills = max([p['Hills'] for p in self.manner] or [1])

    def n(self, k):
        return self.N['NpcBabble.' + k]

    # ------------------------------------------------ the files (Read, VoiceOf: outside the copied rules, the same steps)
    def voice(self, timbre, plain):
        import soundfile as sf
        hums, hz, rate = [], [], 0
        for snd in self.n('Sounds'):
            for f in self.files.get(snd if plain else snd + '__' + timbre, []):
                x, r = sf.read(os.path.join(self.sounds, f), dtype='float32', always_2d=True)
                buf = x.reshape(-1)  # (interleaved, as GetData gives it)
                ch = x.shape[1]
                for s in self.spots.get(f.lower(), []):
                    n = min(s['Count'], x.shape[0] - s['First'])
                    if n < r // 8:
                        continue
                    hums.append(dict(X=self.steady(buf, s['First'], n, ch), Hz=s['Hz'], Kind=s['Kind']))
                    if s['Kind'] in self.N['NpcBabble.Level']:
                        hz.append(s['Hz'])  # (its middle pitch is where it talks: a «хм?» is asked higher, an «о» called out higher)
                    rate = r
        if len(hums) < 4 or not hz:
            return dict(Hums=[], Hz=0.0, Rate=rate, Few=len(hums))
        hz.sort()
        return dict(Hums=hums, Hz=hz[len(hz) // 2], Rate=rate, Few=0)

    # ------------------------------------------------ the copied rules
    def line(self, text, v, pitch, seed, seconds):
        pitch = clamp(pitch, 0.8, 1.25)
        y = self.render(text, v, pitch, seed, 1.0)
        if y is None:
            return None
        want = max(0.8, seconds)
        if len(y) / v['Rate'] > want + self.n('OverBy'):
            quick = self.render(text, v, pitch, seed, min(self.n('MostTempo'), len(y) / v['Rate'] / want))
            if quick is not None:
                y = quick
        most = int((want + self.n('OverBy') + 0.4) * v['Rate'])
        if len(y) > most:
            f = min(most, int(0.35 * v['Rate']))
            y = y[:most].copy()
            y[most - f:] *= 1 - np.arange(f) / f
        return y

    @staticmethod
    def steady(buf, first, n, ch):
        x = buf[first * ch:(first + n) * ch:ch].astype(np.float64)
        return x * (0.1 / max(1e-5, math.sqrt(float(np.mean(x * x)))))

    @staticmethod
    def phrase_of(w):
        hx = w[3]
        n = len(hx) // 4
        if n < 4:
            return None
        V, F, E = [False] * n, [0.0] * n, [0.0] * n
        last = -1
        for i in range(n):
            try:
                a, b = int(hx[i * 4:i * 4 + 2], 16), int(hx[i * 4 + 2:i * 4 + 4], 16)
            except ValueError:
                return None
            V[i] = a > 0
            E[i] = b / 255 * 1.6
            if a == 0:
                continue
            F[i] = 0.80 + (a - 1) / 254 * 0.45
            for k in range(last + 1, i):
                F[k] = F[i] if last < 0 else lerp(F[last], F[i], (k - last) / (i - last))
            last = i
        if last < 0:
            return None
        for k in range(last + 1, n):
            F[k] = F[last]
        try:
            hills = max(1, int(w[1]))
        except ValueError:
            hills = 1
        return dict(Hills=hills, Rising=w[2] == '1', V=V, F=F, E=E)

    @staticmethod
    def H(seed, key, a, b, c):
        h = (2166136261 ^ seed) & M32
        for ch in key:
            h ^= ord(ch)
            h = (h * 16777619) & M32
        h ^= (a * 0x9E3779B1) & M32; h = (h * 16777619) & M32
        h ^= (b * 0x85EBCA6B) & M32; h = (h * 16777619) & M32
        h ^= (c * 0xC2B2AE35) & M32; h = (h * 16777619) & M32
        h ^= h >> 15; h = (h * 2246822519) & M32; h ^= h >> 13
        return h

    def clauses_of(self, text):
        edge = '.,!?…:;«»"()—-\''
        vowels = self.n('Vowels')
        out, syl, key = [], 0, ''
        for w in [x for x in re.split(r'[ \t\n]', text.replace('—', ' — ').replace('...', '…')) if x]:  # (the game parts words at these three only)
            core = w.strip(edge)
            mark = '?' if '?' in w else '!' if '!' in w else '~' if '…' in w else '.' if w.endswith('.') else ',' if (w.endswith(',') or w.endswith(';') or w.endswith(':') or w == '—') else ' '
            if core:
                syl += max(1, sum(1 for c in core if c in vowels))
                key += core.lower() + ' '
            if mark != ' ' and syl > 0:
                out.append((syl, mark, key))
                syl, key = 0, ''
        if syl > 0:
            out.append((syl, '.', key))
        return out

    def render(self, text, v, pitch, seed, tempo):
        clauses = self.clauses_of(text)
        if not clauses:
            return None
        parts, said, part = [], dict(At={}, Times={}, Kinds=[], Prev=-1, LastKind=''), 0
        self.said = said  # (the measure looks at it)
        for syl, mark, key in clauses:
            chunks = [syl] if syl <= self.most_hills * 1.25 else [syl - syl // 2, syl // 2]
            for ci, m in enumerate(chunks):
                m = max(1, m)
                last = ci == len(chunks) - 1
                want_rise = last and mark == '?'
                p = self.pick(m, want_rise, seed, key, ci)
                slow = self.H(seed, key, ci, 0, 2) % 100 < share(self.n('Calm'))
                scale = clamp(m / p['Hills'], 0.75, 1.3) / max(0.5, tempo) * (self.n('SlowBy') if slow else 1)
                L = len(p['F'])
                frames = max(4, int(L * scale))
                ff, ee = [0.0] * frames, [0.0] * frames
                for i in range(frames):
                    src = i * (L - 1) / (frames - 1)
                    a = int(src); b = min(L - 1, a + 1); t = src - a
                    ff[i] = lerp(p['F'][a], p['F'][b], t)
                    ee[i] = lerp(p['E'][a], p['E'][b], t)
                if want_rise and not p['Rising']:
                    k = int(frames * 0.75)
                    for i in range(k, frames):
                        ff[i] *= lerp(1, 1.12, (i - k) / max(1, frames - 1 - k))
                if mark == '!' and last:
                    for i in range(frames):
                        ff[i] *= 1.03; ee[i] *= 1.15
                silent = part > 0 and self.H(seed, key, ci, 0, 8) % 100 < pct(self.n('Skip'))
                parts.append(np.zeros(int(frames * self.step * v['Rate'])) if silent else self.sounds_(ff, ee, frames, v, pitch, seed, key, ci, m, mark if last else ',', slow, said))
                part += 1
                pause = (0.14 if not last else 0.75 if mark == '~' else 0.24 if mark == ',' else 0.5) * (1.6 if slow else 1)
                parts.append(np.zeros(int(pause * v['Rate'])))
        line = np.concatenate(parts)
        s = line[np.abs(line) > 1e-4]
        if len(s) < v['Rate'] // 20:
            return None
        y = line * (self.n('Loud') / max(1e-5, math.sqrt(float(np.mean(s * s)))))
        self.clipped += int(np.sum(np.abs(y) > 1)); self.samples += len(y)
        return np.clip(y, -1, 1)

    def pick(self, m, rise, seed, key, ci):
        best = sorted((abs(p['Hills'] - m) + (1.5 if p['Rising'] != rise else 0) + (self.H(seed, key, ci, r, 0) % 1000) / 100000, r) for r, p in enumerate(self.manner))
        n = min(3, len(best))
        return self.manner[best[self.H(seed, key, ci, 0, 1) % n][1]]

    @staticmethod
    def at(a, fr):
        i = int(fr)
        if i >= len(a) - 1:
            return a[-1]
        return lerp(a[i], a[i + 1], fr - i)

    def sounds_(self, ff, ee, frames, v, pitch, seed, key, ci, m, mark, slow, said):
        H, N = self.H, self.n
        rate, hums = v['Rate'], v['Hums']
        words = max(1, int(round(m / N('WordEvery'))))
        groups = [min(max(1, m), 2 + (1 if H(seed, key, ci, b, 36) % 100 < pct(N('Third')) else 0)) for b in range(words)]
        D = frames * self.step
        gaps = [(N('GapLeast') + (H(seed, key, ci, g, 32) % 1000) / 1000 * N('GapSpan')) * (1.5 if slow else 1) for g in range(words - 1)]
        syl = min(N('HumMost') * (1.25 if slow else 1), max(N('HumLeast'), (D - sum(gaps)) / sum(groups)))
        lens = []
        for gi, g in enumerate(groups):
            for s in range(g):
                d = syl * (0.85 + (H(seed, key, ci * 64 + gi, s, 33) % 30) / 100)
                if gi == words - 1 and s == g - 1:
                    d *= N('LastLonger')
                lens.append(d)
        whole = sum(gaps) + 0.05 + sum(lens)
        if D > whole:
            gaps = [g + (D - whole) / len(gaps) for g in gaps]
            whole = D
        n = int(whole * rate)
        y = np.zeros(n)
        lo, hi = N('SpeedLow') * pitch, N('SpeedHigh') * pitch
        top = max(ee) if ee else 0.0
        fx = np.arange(frames)
        t, idx = 0.0, 0
        for gi, g in enumerate(groups):
            if gi > 0:
                t += gaps[gi - 1]
            fin = gi == words - 1
            a_, b_ = ((1.05, 0.88) if mark == '.' else (0.96, 1.20) if mark == '?' else (1.14, 0.94) if mark == '!' else (1.0, 0.93) if mark == '~' else (0.98, 1.06)) if fin else (1.03, 0.97)
            own = 1 + ((H(seed, key, ci, gi, 37) % 60) - 30) / 1000
            for s in range(g):
                d, t0 = lens[idx], t
                cnt, i0 = int((d + (N('Run') if s < g - 1 else 0)) * rate), int(t0 * rate)
                t += d
                at_idx = idx
                idx += 1
                if cnt < 8:
                    continue
                ar = np.arange(cnt)
                mel = np.sqrt(np.interp((ar / rate + t0) / whole * (frames - 1), fx, ff)) * (a_ + (b_ - a_) * ((s + ar / cnt) / g)) * own
                e = max(self.at(ee, (t0 + d / 2) / whole * (frames - 1)), 0.55 * top) * ((1.18 if s == 0 else 0.9) if g > 1 else 1)
                best, best_score = -1, float('inf')
                for r, hm in enumerate(hums):
                    need = self.need(mel, pitch * v['Hz'] / hm['Hz'], lo, hi)
                    if len(hm['X']) < need:
                        continue
                    score = 3 * said['Times'].get(r, 0) + (6 if r == said['Prev'] else 0) + (2 if hm['Kind'] == said['LastKind'] else 0) + (H(seed, key, ci * 64 + at_idx, r, 34) % 1000) / 1000
                    if hm['Kind'] in N('Rare') and hm['Kind'] in said['Kinds']:
                        score += 4
                    if said['At'].get(r, 0.0) + need > len(hm['X']):
                        score += 5
                    if hm['Kind'] == N('Open') and (s != 0 or N('Open') in said['Kinds']):
                        score += 100
                    if score < best_score:
                        best_score, best = score, r
                if best < 0:  # (no hum so long: its longest one, and the sound as long as that allows)
                    best = max(range(len(hums)), key=lambda r: (len(hums[r]['X']), -r))
                    cnt = int(cnt * (len(hums[best]['X']) - 16) / self.need(mel, pitch * v['Hz'] / hums[best]['Hz'], lo, hi))
                    if cnt < 8:
                        continue
                    mel = mel[:cnt]
                    said['At'].pop(best, None)
                h = hums[best]; X = h['X']
                k = pitch * v['Hz'] / h['Hz']
                room = self.need(mel, k, lo, hi)
                pos = said['At'].get(best, 0.0)
                if pos + room > len(X):
                    free = int(len(X) - room)
                    pos = float(H(seed, key, ci * 64 + at_idx, 0, 35) % (free + 1)) if free > 0 else 0.0
                fade_in, fade_out = min(cnt // 3, int(N('FadeIn') * rate)), min(cnt // 2, int((N('FadeOut') if s < g - 1 else N('FadeLast')) * rate))
                where = pos + np.cumsum(np.clip(k * mel, lo, hi))
                ran = np.flatnonzero(where.astype(int) >= len(X) - 1)
                ok = max(0, min(cnt, n - i0, int(ran[0]) if len(ran) else cnt))
                if len(ran) and ok < cnt - fade_out:
                    self.cuts += 1  # (the measure: the hum ran out before its fade — the sound stops dead)
                a = where[:ok].astype(int)
                x = (X[a] + (X[np.minimum(a + 1, len(X) - 1)] - X[a]) * (where[:ok] - a)) * e
                env = np.ones(cnt)
                if fade_in > 0:
                    env[:fade_in] = 0.5 - 0.5 * np.cos(np.pi * np.arange(fade_in) / fade_in)
                if fade_out > 0:
                    env[cnt - fade_out:] = 0.5 + 0.5 * np.cos(np.pi * np.arange(fade_out) / fade_out)
                y[i0:i0 + ok] += x * env[:ok]
                said['At'][best] = float(where[ok - 1]) if ok > 0 else pos
                said['Times'][best] = said['Times'].get(best, 0) + 1
                said['Prev'], said['LastKind'] = best, h['Kind']
                said['Kinds'].append(h['Kind'])
        return y

    @staticmethod
    def need(mel, k, lo, hi):
        return float(np.sum(np.clip(k * mel, lo, hi))) + 8


def shown_for(text, N):
    """how long a line is shown (the seconds the game gives the hum): NpcSpeech.Make's length for an average speaker"""
    vowels = N['NpcSpeech.Vowels'] if 'NpcSpeech.Vowels' in N else N['NpcBabble.Vowels']
    per, now = 1.0 / N['NpcSpeech.Syllables'], 0.25
    for word in text.split():
        v = sum(1 for c in word if c in vowels)
        now += v * per
        bare = word.rstrip('»"\')”“')
        end = bare[-1] if bare else ' '
        if '…' in word or '...' in word:
            now += N['NpcSpeech.TrailPause']
        elif '?' in word or end in '!.':
            now += N['NpcSpeech.StopPause']
        elif end in ',;:' or word in ('—', '-'):
            now += N['NpcSpeech.CommaPause']
        else:
            now += per * 0.25
    return now + 0.2


def voice_seed(name):
    return Rules.H(0x51A7, name, 1, 2, 3)


def cmd_talk(sound, root, a):
    if a.mark:
        path = os.path.abspath(__file__)
        with open(path, encoding='utf-8') as h:
            t = h.read()
        mark = rules_mark(root)
        t2 = re.sub(r"^RULES_OF = '[0-9a-f]*'", "RULES_OF = '%s'" % mark, t, count=1, flags=re.M)
        with open(path, 'w', encoding='utf-8', newline='\n') as h:
            h.write(t2)
        print('the copy\'s mark: %s (was %s)' % (mark, RULES_OF))
        return
    if rules_mark(root) != RULES_OF:
        print('!! the game\'s rules of a hummed line changed (src/NpcBabble.cs) and this copy does not know it: what you hear may not be the game')
    N = numbers(root)
    for s in a.set or []:
        k, _, v = s.partition('=')
        if k not in N:
            sound.fail('--set %s: no such number of the game\'s talk (%s)' % (k, ', '.join(sorted(x for x in N if not isinstance(N[x], (list, str))))))
        N[k] = [float(x) for x in v.split(',')] if isinstance(N[k], list) else float(v)
    R = Rules(N, root)
    if not R.manner:
        sound.fail('no «@manner» line in assets/story/sounds: node tools/sound.js babble')
    defs = sound.timbre_lines()
    shaping = set(N['NpcTimbre.ToolWords']) | {'rec'}  # (a timbre with none of these is the first recording, its sounds under their plain names: NpcTimbre)

    def voice_of(name):
        if name not in defs:
            sound.fail('no «@timbre %s» line (assets/story/sounds/timbres.txt)' % name)
        v = R.voice(name, not any(k in defs[name] for k in shaping))
        if not v['Hums']:
            sound.fail('the voice «%s» has %d stretches of hum: too few to talk with (the game keeps its short sounds); node tools/sound.js babble' % (name, v['Few']))
        return v

    lines = TALK
    if a.lines:
        with open(a.lines, encoding='utf-8-sig') as h:
            lines = [l.strip() for l in h if l.strip()]
    if a.measure:
        names = a.voices or [n for n, d in defs.items() if 'only' not in d]
        print('%-9s %5s  %10s  %8s  %s' % ('voice', 'hums', 'cut dead', 'clipped', 'lines'))
        for name in names:
            v = voice_of(name)
            R.cuts = R.clipped = R.samples = 0
            said = 0
            for i, text in enumerate(lines * 3):
                y = R.line(text, v, 1.0, voice_seed(name) + i // len(lines), shown_for(text, N))
                said += y is not None
            print('%-9s %5d  %10s  %7.3f%%  %d' % (name, len(v['Hums']), '%.2f a line' % (R.cuts / max(1, said)), 100.0 * R.clipped / max(1, R.samples), said))
        print('cut dead: sounds that stop before their fade (the hum ran out: heard as a click). clipped: of the line\'s samples, squared off at full scale')
        return
    if len(a.voices) < 2:
        sound.fail('talk: two voices at least (or --measure)')
    vs = [voice_of(n) for n in a.voices]
    rate = vs[0]['Rate']
    if any(v['Rate'] != rate for v in vs):
        sound.fail('the voices\' files are at different rates: %s' % ', '.join('%s %d' % (n, v['Rate']) for n, v in zip(a.voices, vs)))
    gap = N['NpcTalk.HeardOut'] + N['NpcTalk.WordsAfter']  # (one heard out before the other speaks, as the hero is: NpcTalk)
    out = []
    for i, text in enumerate(lines):
        k = i % len(vs)
        y = R.line(text, vs[k], 1.0, voice_seed(a.voices[k]), shown_for(text, N))
        if y is not None:
            out += [y, np.zeros(int(gap * rate))]
    y = np.concatenate(out)
    row = a.name or 'talk_' + '_'.join(a.voices)
    folder = os.path.join(root, 'build', 'sound_try', row)
    os.makedirs(folder, exist_ok=True)
    import soundfile as sf
    path = os.path.join(folder, '+'.join(a.voices) + '.wav')
    sf.write(path, y.astype(np.float32), rate, subtype='PCM_16')
    changed = ', '.join(a.set or []) or 'числа гри як є'
    note = a.note or 'Розмова голосами %s (%s), %d реплік: правила гри (NpcBabble), зіграні поза грою (tools/sound_talk.py).' % (', '.join(a.voices), changed, len(lines))
    with open(os.path.join(folder, 'note.txt'), 'w', encoding='utf-8') as h:
        h.write(note + '\n')
    print('%s: %.1f s, %s' % (path, len(y) / rate, changed))
    sound.cmd_page(None)
