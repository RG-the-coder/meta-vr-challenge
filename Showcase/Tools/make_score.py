"""Generate an original instrumental underscore for the WAR showcase.

Requires numpy (bundled with the local Blender Python). No samples or downloads.
Usage: python make_score.py --duration 160 --output Showcase/Audio/WAR_Score.wav
"""
from __future__ import annotations

import argparse
import math
from pathlib import Path
import wave

import numpy as np


RATE = 48000


def envelope(length, attack=0.02, release=0.25):
    n = int(length * RATE)
    env = np.ones(n, dtype=np.float32)
    a, r = min(n, int(attack * RATE)), min(n, int(release * RATE))
    if a:
        env[:a] *= np.sin(np.linspace(0, math.pi / 2, a, dtype=np.float32)) ** 2
    if r:
        env[-r:] *= np.sin(np.linspace(math.pi / 2, 0, r, dtype=np.float32)) ** 2
    return env


def midi(note):
    return 440 * 2 ** ((note - 69) / 12)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--duration", type=float, default=160)
    parser.add_argument("--output", type=Path, default=Path("Showcase/Audio/WAR_Score.wav"))
    args = parser.parse_args()
    duration = args.duration
    rng = np.random.default_rng(1974)
    mix = np.zeros((int((duration + 3) * RATE), 2), dtype=np.float32)

    def add(signal, when, gain=1.0, pan=0.0):
        start = max(0, int(when * RATE))
        count = min(len(signal), len(mix) - start)
        if count <= 0:
            return
        s = signal[:count] * gain
        mix[start:start + count, 0] += s * math.sqrt((1 - pan) / 2)
        mix[start:start + count, 1] += s * math.sqrt((1 + pan) / 2)

    def swell(note, length, phase=0):
        t = np.arange(int(length * RATE), dtype=np.float32) / RATE
        f = midi(note)
        vibrato = 0.016 * np.sin(2 * math.pi * 4.4 * t + phase)
        sound = np.zeros(len(t), dtype=np.float32)
        for detune in (-0.0015, 0.0011):
            for h, level in ((1, .55), (2, .14), (3, .07), (4, .028), (5, .013)):
                sound += level * np.sin(2 * math.pi * f * h * (1 + detune) * t + vibrato * h + phase)
        return sound * envelope(length, min(1.6, length * .22), min(2.2, length * .33))

    def pluck(note, length=1.8):
        t = np.arange(int(length * RATE), dtype=np.float32) / RATE
        f = midi(note)
        sound = (np.sin(2 * math.pi * f * t) * np.exp(-t * 2.1)
                 + .22 * np.sin(2 * math.pi * f * 2 * t) * np.exp(-t * 4.5)
                 + .10 * np.sin(2 * math.pi * f * 3 * t) * np.exp(-t * 7))
        return sound * envelope(length, .012, .4)

    def drum(strength=1, length=1.5):
        t = np.arange(int(length * RATE), dtype=np.float32) / RATE
        phase = 2 * math.pi * (42 * t + 58 * .045 * (1 - np.exp(-t / .045)))
        attack = rng.normal(0, 1, len(t)).astype(np.float32) * np.exp(-t * 70) * .13
        tone = np.sin(phase) * np.exp(-t * 4.7)
        over = .19 * np.sin(phase * 1.63) * np.exp(-t * 8)
        return (tone + over + attack) * envelope(length, .002, .3) * strength

    def shimmer(length):
        noise = rng.normal(0, .17, int(length * RATE)).astype(np.float32)
        # A soft high-pass wash with a long, smooth attack; no sharp white noise.
        smooth = np.convolve(noise, np.ones(11, dtype=np.float32) / 11, mode="same")
        signal = noise - smooth
        return signal * envelope(length, length * .85, length * .14)

    # D minor / Bb / F / C, 96 BPM. Four-bar harmonic phrases.
    beat = .625
    bar = 4 * beat
    chords = [(50, 57, 62, 65, 69), (46, 53, 58, 62, 65),
              (41, 53, 57, 60, 65), (48, 55, 60, 64, 67)]
    blocks = math.ceil(duration / (bar * 4))
    for block in range(blocks):
        when = block * bar * 4
        progress = when / duration
        chord = chords[block % len(chords)]
        level = .06 if progress < .08 else .085 if progress < .4 else .10
        if progress > .91:
            chord = chords[0]
            level = .078
        for j, note in enumerate(chord):
            add(swell(note, bar * 4 + 1.8, j * .73), when, level, -.7 + j * .35)
        # Bass remains restrained and is slightly articulated under the chord.
        add(swell(chord[0] - 12, bar * 4 + .6), when, .11, 0)

    # Motif is sparse early and broadens as the battle develops.
    for i in range(math.ceil(duration / beat)):
        when = i * beat
        progress = when / duration
        chord = chords[int(when // (bar * 4)) % 4]
        if progress > .9:
            continue
        if .075 < progress < .91 and i % (2 if progress < .34 else 1) == 0:
            pattern = (2, 3, 4, 3, 2, 4, 3, 1)
            note = chord[pattern[i % 8]] + (12 if i % 16 in (7, 15) else 0)
            add(pluck(note), when, .07 if progress < .34 else .09, -.30 if i % 2 else .30)
        if .16 < progress < .89:
            if i % 4 == 0:
                add(drum(), when, .33 if progress < .42 else .42)
            elif .42 < progress < .84 and i % 4 == 2:
                add(drum(.64, 1), when, .32, -.10)
            if .55 < progress < .82 and i % 8 == 7:
                add(drum(.4, .7), when + beat / 2, .23, .18)

    # A slow, legato top line rather than a distracting lead melody.
    melody = [74, 77, 76, 72, 70, 74, 72, 69, 69, 72, 74, 77, 76, 72, 71, 67]
    start = duration * .34
    for idx, note in enumerate(melody):
        when = start + idx * bar
        if when > duration * .89:
            break
        add(swell(note, bar + .45, idx), when, .045, .12)

    for fraction in (.16, .34, .5, .68, .85):
        when = duration * fraction
        add(shimmer(2.2), max(0, when - 2.2), .11, -.15)
        add(drum(), when, .32)

    # Final resolved tonic and a natural reverb tail.
    for j, note in enumerate((38, 50, 57, 62, 65, 74)):
        add(swell(note, 7.8, j), max(0, duration - 9.2), .07, -.5 + j * .2)
    add(drum(), max(0, duration - 9.2), .38)

    # Stereo room reflections, then soft saturation and a true end fade.
    dry = mix.copy()
    for delay, gain in ((.137, .16), (.239, .13), (.373, .10), (.529, .075), (.743, .05), (1.091, .035)):
        shift = int(delay * RATE)
        mix[shift:, 0] += dry[:-shift, 1] * gain
        mix[shift:, 1] += dry[:-shift, 0] * gain
    mix = mix[:int(duration * RATE)]
    mix = np.tanh(mix * 1.15)
    mix *= envelope(duration, 2.2, 4.5)[:, None]
    peak = float(np.max(np.abs(mix)))
    mix *= .83 / max(.01, peak)
    pcm = (mix * 32767).astype("<i2")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(args.output), "wb") as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    print(f"Original score: {args.output.resolve()} | {duration:.2f}s | 48 kHz stereo | peak {20 * math.log10(.83):.2f} dBFS")


if __name__ == "__main__":
    main()
