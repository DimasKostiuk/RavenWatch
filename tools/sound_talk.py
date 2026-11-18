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
# Tidy, Sounds_, At, Need), line by line in Python; its numbers it reads from the game's own source each run (NpcBabble's fields, the
# length a person's line is shown from NpcSpeech.Make, the hero's pause from NpcTalk), so a number changed there is heard here at once.
# Outside the guarded block and copied by hand: how a voice's hums are loaded (NpcBabble.Read, VoiceOf) — change those, change Rules.voice.
# What it is not: the game's sound core (no place, no distance, no Vol between people: every line as said to you), a person's chattiness
# (an average one), its pitch within its timbre (1.0). The rules changed in the game (not a comment, not a space) stop the build
# (tools/arch_check.js «babble-copy») till this file plays the same and «--mark» writes the new mark into RULES_OF below.
import os, re, sys, math, hashlib, numpy as np

RULES_OF = '9c44910ddc9b9605'  # the mark of the game's rules this copy plays (rules_mark): written by «talk --mark»

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
    """the game's numbers of the talk, read from its source: {'NpcBabble.Calm': 0.5, …, 'NpcBabble.Speeds': [...], …}"""
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
        m = re.search(r'const string Vowels = "([^"]+)"', t)
        if m:
            out[cls + '.Vowels'] = m.group(1)
    return out


def share(x):
    """(uint)(x * 100f) as the game reckons it: its field is a float32 (0.57 is 0.5699999928), multiplied — so Mono does — in double: 56.
    NOT measured in the game; Python's own 0.57 * 100 would be 57. The numbers in use now (0.5, 0.3) come out the same either way"""
    return int(float(np.float32(x)) * 100.0)


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
                    hz.append(s['Hz'])
                    rate = r
        if len(hums) < 4:
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
        parts, used, last_kind = [], [], ['']
        step_s = self.step * v['Rate']
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
                on, ff, ee = [False] * frames, [0.0] * frames, [0.0] * frames
                for i in range(frames):
                    src = i * (L - 1) / (frames - 1)
                    a = int(src); b = min(L - 1, a + 1); t = src - a
                    on[i] = p['V'][min(L - 1, round(src))]
                    ff[i] = lerp(p['F'][a], p['F'][b], t)
                    ee[i] = lerp(p['E'][a], p['E'][b], t)
                if want_rise and not p['Rising']:
                    k = int(frames * 0.75)
                    for i in range(k, frames):
                        ff[i] *= lerp(1, 1.12, (i - k) / max(1, frames - 1 - k))
                if mark == '!' and last:
                    for i in range(frames):
                        ff[i] *= 1.03; ee[i] *= 1.15
                self.tidy(on, ee, 11 if slow else 3, 10 if slow else 4)
                parts.append(self.sounds_(on, ff, ee, frames, step_s, v, pitch, seed, key, ci, slow, used, last_kind))
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
    def tidy(on, ee, join, least):
        n, i, last_end = len(on), 0, -1
        while i < n:
            if not on[i]:
                i += 1; continue
            if last_end >= 0 and i - last_end < join:
                for k in range(last_end, i):
                    on[k] = True
            while i < n and on[i]:
                i += 1
            last_end = i
        i = 0
        while i < n:
            if not on[i]:
                i += 1; continue
            a, top = i, 0.0
            while i < n and on[i]:
                top = max(top, ee[i]); i += 1
            if i - a < least:
                for k in range(a, i):
                    on[k] = False
                continue
            for k in range(a, i):
                ee[k] = max(ee[k], 0.45 * top)

    @staticmethod
    def at(a, fr):
        i = int(fr)
        if i >= len(a) - 1:
            return a[-1]
        return lerp(a[i], a[i + 1], fr - i)

    def need(self, h, v, ff, i0, i1, step_s, speed, lo, hi):
        k = speed * (v['Hz'] / h['Hz']) ** self.n('Together')
        f0, f1 = int(i0 / step_s), min(len(ff) - 1, int(i1 / step_s))
        s = sum(clamp(k * ff[f], lo, hi) for f in range(f0, f1 + 1))
        return s * step_s * (i1 - i0) / max(1.0, (f1 - f0 + 1) * step_s)

    def sounds_(self, on, ff, ee, frames, step_s, v, pitch, seed, key, ci, slow, used, last_kind):
        H, at, N = self.H, self.at, self.n
        speeds, rests, rare = N('Speeds'), N('Rests'), N('Rare')
        rate = v['Rate']
        n = int(frames * step_s)
        y = np.zeros(n)
        fr_, to_ = [], []
        f, region, last_speed = 0, 0, -1
        lo, hi = N('SpeedLow') * pitch, N('SpeedHigh') * pitch
        hums = v['Hums']
        while f < frames:
            if not on[f]:
                f += 1; continue
            g = f
            while g < frames and on[g]:
                g += 1
            sp = H(seed, key, ci, region, 3) % len(speeds)
            if sp == last_speed:
                sp = (sp + 2) % len(speeds)
            last_speed = sp
            if (g - f) * self.step > N('ShortOver') and H(seed, key, ci, region, 4) % 100 < share(N('ShortChance')):
                cut = f + int((g - f) * N('ShortTo'))
                for q in range(cut, g):
                    on[q] = False
                g = cut
            if (g - f) * self.step >= N('LeastSound'):
                i0, i1 = int(f * step_s), min(n, int(g * step_s))
                best, best_score, longest = -1, float('inf'), 0
                for r, hm in enumerate(hums):
                    if len(hm['X']) > len(hums[longest]['X']):
                        longest = r
                    if len(hm['X']) < self.need(hm, v, ff, i0, i1, step_s, speeds[sp] * pitch, lo, hi) + 8:
                        continue
                    score = (8 if r in used else 0) + (2 if hm['Kind'] == last_kind[0] else 0) + (H(seed, key, ci * 64 + region, r, 5) % 1000) / 1000
                    is_rare = hm['Kind'] in rare
                    for u in used:
                        if hums[u]['Kind'] == hm['Kind']:
                            score += 12 if is_rare else 1
                    if score < best_score:
                        best_score, best = score, r
                if best < 0:
                    best = longest
                    room0 = len(hums[best]['X']) - 8; pos0 = 0.0; s0 = i0
                    k0 = speeds[sp] * pitch * (v['Hz'] / hums[best]['Hz']) ** N('Together')
                    while s0 < i1 and pos0 < room0:
                        pos0 += clamp(k0 * at(ff, s0 / step_s), lo, hi); s0 += 1
                    i1 = s0
                    g = max(f + 1, math.ceil(i1 / step_s))
                h = hums[best]
                X = h['X']
                k = speeds[sp] * pitch * (v['Hz'] / h['Hz']) ** N('Together')
                room = int(len(X) - self.need(h, v, ff, i0, i1, step_s, speeds[sp] * pitch, lo, hi) - 8)
                pos = float(H(seed, key, ci * 64 + region, 0, 6) % (room + 1)) if room > 0 else 0.0
                ln = i1 - i0
                fin, fout = min(ln // 3, int(N('FadeIn') * rate)), min(ln // 2, int(N('FadeOut') * rate))
                for s in range(ln):
                    fr = (i0 + s) / step_s
                    pos += clamp(k * at(ff, fr), lo, hi)
                    a = int(pos)
                    if a >= len(X) - 1:
                        if s < ln - fout and s > 0 and abs(y[i0 + s - 1]) > 0.02:
                            self.cuts += 1  # (the measure: the hum ran out before its fade — the sound stops dead)
                        break
                    x = (X[a] + (X[a + 1] - X[a]) * (pos - a)) * at(ee, fr)
                    if s < fin:
                        x *= s / fin
                    elif s >= ln - fout:
                        d = (ln - s) / fout
                        x *= d * math.sqrt(d)
                    y[i0 + s] = x
                used.append(best); last_kind[0] = h['Kind']
                fr_.append(i0); to_.append(i1)
                rest = int(rests[H(seed, key, ci * 64 + region, 0, 7) % len(rests)] * (1.4 if slow else 1) / self.step)
                for q in range(g, min(g + rest, frames)):
                    on[q] = False
            region += 1
            f = max(g, f + 1)
        if not slow or len(fr_) < 2:
            return y
        g1 = g2 = -1
        for i in range(1, len(fr_)):
            w = fr_[i] - to_[i - 1]
            if g1 < 0 or w > fr_[g1] - to_[g1 - 1]:
                g2, g1 = g1, i
            elif g2 < 0 or w > fr_[g2] - to_[g2 - 1]:
                g2 = i
        later = max(g1, g2); earlier = -1 if g2 < 0 else min(g1, g2)
        add_l = int(0.32 * rate); add_e = 0 if earlier < 0 else int(0.2 * rate)
        mid_l = (to_[later - 1] + fr_[later]) // 2
        mid_e = 0 if earlier < 0 else (to_[earlier - 1] + fr_[earlier]) // 2
        return np.concatenate([y[:mid_e], np.zeros(add_e), y[mid_e:mid_l], np.zeros(add_l), y[mid_l:]])


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
