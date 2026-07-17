"""Synthesize a starter set of retro game SFX as 16-bit mono WAVs — Python stdlib only.

Usage: python gen-sfx.py <output-dir>   (e.g. a project's Assets/Audio/SFX)
Customize freely: every sound is a small function; tweak frequencies, durations, envelopes.
"""
import math
import os
import random
import struct
import sys
import wave

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "generated-sfx"
os.makedirs(OUT, exist_ok=True)


def write_wav(name, samples):
    path = os.path.join(OUT, name)
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        peak = max(1e-9, max(abs(s) for s in samples))
        norm = 0.85 / peak
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s * norm)) * 32767))
            for s in samples))
    return path


def square(phase):
    return 1.0 if (phase % 1.0) < 0.5 else -1.0


def coin():
    samples, phase = [], 0.0
    for i in range(int(SR * 0.35)):
        t = i / SR
        freq = 987.77 if t < 0.08 else 1318.51  # B5 -> E6
        phase += freq / SR
        env = 1.0 if t < 0.08 else math.exp(-(t - 0.08) * 14)
        samples.append(square(phase) * env)
    return samples


def laser():
    samples, phase = [], 0.0
    dur = 0.25
    for i in range(int(SR * dur)):
        t = i / SR
        freq = 1800 * (300 / 1800) ** (t / dur)  # exponential downward sweep
        phase += freq / SR
        samples.append(square(phase) * math.exp(-t * 10))
    return samples


def jump():
    samples, phase = [], 0.0
    dur = 0.3
    for i in range(int(SR * dur)):
        t = i / SR
        freq = 220 * (880 / 220) ** (t / dur)  # upward sweep
        phase += freq / SR
        samples.append(square(phase) * math.exp(-t * 6))
    return samples


def explosion():
    rng = random.Random(42)
    dur = 0.9
    raw = [rng.uniform(-1, 1) for _ in range(int(SR * dur))]
    samples, window = [], []
    for i, s in enumerate(raw):
        t = i / SR
        k = 1 + int(2 + t * 60)  # widening moving average darkens the tail
        window.append(s)
        if len(window) > k:
            window = window[-k:]
        samples.append(sum(window) / len(window) * math.exp(-t * 5))
    return samples


def ui_click():
    rng = random.Random(7)
    samples, phase = [], 0.0
    for i in range(int(SR * 0.06)):
        t = i / SR
        phase += 2200 / SR
        samples.append((math.sin(2 * math.pi * phase) * 0.7
                        + rng.uniform(-1, 1) * 0.3) * math.exp(-t * 90))
    return samples


def hurt():
    samples, phase = [], 0.0
    dur = 0.22
    for i in range(int(SR * dur)):
        t = i / SR
        freq = 400 * (90 / 400) ** (t / dur)
        phase += freq / SR
        samples.append(square(phase) * (1.0 - (t / dur) ** 2))
    return samples


for fn in (coin, laser, jump, explosion, ui_click, hurt):
    path = write_wav(fn.__name__ + ".wav", fn())
    with wave.open(path) as w:
        print(f"{os.path.basename(path)}: {w.getnframes() / SR:.2f}s")
