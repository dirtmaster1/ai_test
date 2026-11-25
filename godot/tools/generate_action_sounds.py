"""Render the original one-shot action cues (Python standard library only)."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
DEST = Path(__file__).resolve().parents[1] / "assets" / "audio" / "sfx"


def note(start, duration, hz, end=None, gain=0.65, noise=0.0):
    return start, duration, hz, hz if end is None else end, gain, noise


def bells(frequencies, spacing, decay):
    return [note(i * spacing, decay, f) for i, f in enumerate(frequencies)]


swish = note(0, .14, 1100, 260, .5, .94)
thud = note(.1, .13, 145, 49, .9, .32)
bow = note(0, .14, 440, 135, .7, .16)
tick = note(0, .045, 2400, 600, .45, .65)
cues = {
    "level-up": [note(0, .78, 1046.5), note(0, .45, 2093, gain=.30),
                 note(0, .20, 2820, gain=.14)],
    "door-open": [note(0, .08, 430, 105, .72, .59),
                  note(.055, .47, 166, 78, .67, .28),
                  note(.085, .36, 480, 245, .30, .18)],
    "chest-open": [tick, note(.055, .09, 820, 320, .64, .36),
                   note(.10, .21, 245, 382, .45, .32),
                   note(.28, .13, 160, 62, .78, .27)],
    "melee": [swish, thud],
    "ranged": [bow],
    "arrow-impact": [note(0, .10, 240, 70, .8, .52)],
    "pin": [bow, note(.035, .11, 1550, 900, .22)],
    "rain-of-arrows": [bow] + [note(.13+i*.045, .13, 320+i*38, 90, .55, .73) for i in range(6)],
    "weapon-sweep": [note(0, .30, 430, 1400, .7, .92), note(.17, .20, 360, 110, .5, .8)],
    "charge": [note(0, .22, 110, 60, .8, .55), note(.09, .18, 190, 45), thud],
    "shadow-strike": [note(0, .16, 510, 95, .45, .5), note(.12, .12, 1600, 330, .7, .8)],
    "poison-strike": [swish, thud, note(.12, .24, 2200, 630, .45, .92)],
    "poison": [note(0, .13, 300, 86, .34), note(.06, .12, 235, 65, .28, .17)],
    "defend": bells([248, 689, 1020], .009, .19),
    "taunt": [note(0, .16, 180, 255, .5, .25), note(.10, .20, 245, 118, .7, .2)],
    "pick-lock": [tick, note(.07, .05, 1700, 660), note(.19, .1, 540, 150, .6, .48)],
    "disarm-trap": [note(0, .09, 770, 1400, .5, .4), note(.12, .14, 880, 92, .7, .55)],
    "magic-missile": [note(0, .20, 430, 1600), note(.025, .17, 650, 2200, .26)],
    "arcane-impact": bells([1270, 840], .022, .10),
    "fireball": [note(0, .23, 130, 610, .8, .83)],
    "fire-impact": [note(0, .36, 138, 43, .9, .64), note(.03, .40, 84, 40, .5)],
    "lightning-bolt": [note(i*.035, .07, 2700-i*430, 140, .7, .89) for i in range(4)],
    "web": [note(i*.042, .11, 290+i*85, 87, .6, .28) for i in range(3)],
    "teleport": [note(0, .17, 880, 120), note(.20, .24, 220, 1050)],
    "sleep": bells([660, 550, 440], .12, .35),
    "identify": bells([990, 1485], .07, .24),
    "lesser-heal": bells([392, 494], .08, .30),
    "greater-heal": bells([392, 494, 587], .08, .37),
    "mass-heal": bells([392, 494, 587, 784], .09, .45),
    "protection": bells([294, 441, 588], .025, .40),
    "blessing": bells([440, 554, 660, 880], .065, .41),
    "remove-malady": [note(0, .2, 410, 990, .4, .57)] + bells([880, 1320], .12, .26),
    "resurrect": bells([262, 392, 523, 659, 784], .12, .55),
}


def render(name, layers):
    rng = random.Random(name)
    samples = [0.] * (1 + math.ceil(max(t+d for t, d, *_ in layers) * RATE))
    for start, duration, hz, end, gain, noise in layers:
        phase = filtered = 0.
        for i in range(int(duration * RATE)):
            t = i / RATE
            u = t / duration
            phase += math.tau * (hz + (end-hz)*u) / RATE
            filtered += .36 * (rng.uniform(-1, 1) - filtered)
            env = min(1., t/.006) * (1-u)**2
            samples[int(start*RATE)+i] += gain*env*((1-noise)*math.sin(phase) + noise*filtered)
    scale = 22000 / max(1., max(map(abs, samples)))
    with wave.open(str(DEST / (name + ".wav")), "wb") as out:
        out.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        out.writeframes(struct.pack(f"<{len(samples)}h", *(round(s*scale) for s in samples)))


if __name__ == "__main__":
    DEST.mkdir(parents=True, exist_ok=True)
    for cue, layers in cues.items():
        render(cue, layers)
