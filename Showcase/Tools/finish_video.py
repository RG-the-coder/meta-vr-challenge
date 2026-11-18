"""Finish captured Unity footage as a shareable H.264 WAR showcase.

Accept a movie or an image sequence pattern, such as Frames/frame_%06d.jpg.
Titles and chapter timing come from the Unity showcase director's shots.json.
The finishing pass adds original music, a restrained grade, minimal film titles,
brief dips between shots and web-compatible H.264 encoding.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys


def stamp(seconds):
    value = round(max(0, seconds) * 100)
    return f"{value // 360000}:{value // 6000 % 60:02d}:{value // 100 % 60:02d}.{value % 100:02d}"


def text_escape(value):
    return str(value).replace("\\", "\\\\").replace("{", "(").replace("}", ")").replace("\n", "\\N")


def write_titles(path, shots, duration):
    header = """[Script Info]
Title: Wardens And Raiders — Cinematic Feature Showcase
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
ScaledBorderAndShadow: yes
WrapStyle: 2

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Title,Bahnschrift,44,&H00F1EEE7,&H00F1EEE7,&H800C1218,&H00000000,0,0,0,0,100,100,1,0,1,0,1,7,0,0,0,1
Style: Copy,Segoe UI,27,&H00D3D9DA,&H00D3D9DA,&H800C1218,&H00000000,0,0,0,0,100,100,0,0,1,0,1,7,0,0,0,1
Style: Small,Bahnschrift,17,&H00C5C9C8,&H00C5C9C8,&H00000000,&H00000000,0,0,0,0,100,100,3,0,1,0,0,7,0,0,0,1
Style: Mark,Georgia,138,&H00DBD0B6,&H00DBD0B6,&H60000000,&H00000000,-1,0,0,0,100,100,12,0,1,0,1,5,0,0,0,1
Style: Brand,Bahnschrift,46,&H00F1EEE7,&H00F1EEE7,&H60000000,&H00000000,0,0,0,0,100,100,5,0,1,0,1,5,0,0,0,1
Style: Shape,Arial,20,&H00000000,&H00000000,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    events = []

    def event(layer, begin, end, style, content):
        events.append(f"Dialogue: {layer},{stamp(begin)},{stamp(end)},{style},,0,0,0,,{content}")

    def rectangle(layer, begin, end, x, y, width, height, colour="000000", alpha="00", extra=""):
        tags = f"{{\\an7\\pos({x},{y})\\1c&H{colour}&\\alpha&H{alpha}&\\p1{extra}}}"
        event(layer, begin, end, "Shape", tags + f"m 0 0 l {width} 0 {width} {height} 0 {height}")

    rectangle(0, 0, duration, 0, 0, 1920, 60)
    rectangle(0, 0, duration, 0, 1020, 1920, 60)
    event(3, .5, duration - 1, "Small", "{\\pos(100,21)\\fad(600,600)}WAR  /  FEATURE SHOWCASE")
    intro_end = float(shots[0].get("duration", 8)) if shots and float(shots[0].get("start", 0)) == 0 else 8
    outro_start = duration - 8
    if shots:
        final = shots[-1]
        if str(final.get("id", "")).lower() in ("outro", "ending", "end", "finale"):
            outro_start = float(final["start"])

    for begin, end in ((.6, intro_end - .3), (outro_start + .3, duration - .7)):
        # Large mark + restrained wordmark maintain readable hierarchy.
        event(4, begin, end, "Mark", "{\\pos(960,420)\\fad(1100,650)}WAR")
        event(4, begin + .28, end, "Brand", "{\\pos(960,550)\\fad(1000,650)}WARDENS AND RAIDERS")
        subtitle = "HAND-CONTROLLED TOWER WARFARE" if begin < 2 else "SUMMON  /  BUILD  /  COMMAND  /  DEFEND"
        event(4, begin + .65, end, "Small", "{\\an5\\pos(960,624)\\fs20\\fsp4\\fad(850,650)}" + subtitle)
        rectangle(4, begin + .6, end, 800, 590, 320, 2, "9FBCDA", "10", "\\fad(1000,650)")

    for index, shot in enumerate(shots):
        start = float(shot["start"])
        length = float(shot["duration"])
        end = min(duration, start + length)
        if start < intro_end - .05 or start >= outro_start - .05:
            continue
        # A soft vertical shade keeps text legible without a heavy card.
        for j in range(24):
            opacity = .55 * ((j + 1) / 24) ** .75
            rectangle(1, start + .12, end - .08, 0, 780 + j * 10, 1920, 11,
                      "1C1407", f"{round(255 * (1 - opacity)):02X}", "\\fad(350,220)")
        rectangle(3, start + .22, end - .12, 100, 865, 4, 103, "9FBCDA", "00", "\\fad(350,250)")
        event(3, start + .25, end - .12, "Title", "{\\pos(126,860)\\fad(420,250)}" + text_escape(shot.get("title", "")))
        event(3, start + .40, end - .12, "Copy", "{\\pos(127,924)\\fad(420,250)}" + text_escape(shot.get("subtitle", "")))
        # Very brief symmetric dips conceal hard scenario cuts without slowing pacing.
        if start > .2:
            rectangle(10, start - .18, start + .18, 0, 0, 1920, 1080, extra="\\fad(180,180)")

    if outro_start > 0:
        rectangle(10, outro_start - .25, outro_start + .25, 0, 0, 1920, 1080, extra="\\fad(250,250)")
    path.write_text(header + "\n".join(events) + "\n", encoding="utf-8-sig")


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("input", help="Movie path or printf image sequence pattern")
    p.add_argument("--score", type=Path, default=Path("Showcase/Audio/WAR_Score.wav"))
    p.add_argument("--shots", type=Path, default=Path("Showcase/capture/shots.json"))
    p.add_argument("--output", type=Path, default=Path("Showcase/Wardens_And_Raiders_Feature_Showcase.mp4"))
    p.add_argument("--duration", type=float, default=160)
    p.add_argument("--fps", type=int, default=30)
    p.add_argument("--ffmpeg", default="C:/Program Files/ShareX/ffmpeg.exe")
    p.add_argument("--keep-source-audio", action="store_true")
    p.add_argument("--no-grade", action="store_true")
    p.add_argument("--no-fades", action="store_true")
    p.add_argument("--no-titles", action="store_true")
    p.add_argument("--titles-only", action="store_true", help="Generate ASS without encoding")
    a = p.parse_args()
    a.output.parent.mkdir(parents=True, exist_ok=True)
    title_path = a.output.parent / "WAR_Titles.ass"
    if not a.no_titles:
        data = json.loads(a.shots.read_text(encoding="utf-8-sig"))
        shots = data if isinstance(data, list) else data["shots"]
        write_titles(title_path, shots, a.duration)
    if a.titles_only:
        print(title_path.resolve())
        return
    if not a.score.exists():
        subprocess.run([sys.executable, str(Path(__file__).with_name("make_score.py")),
                        "--duration", str(a.duration), "--output", str(a.score)], check=True)
    command = [a.ffmpeg, "-y", "-hide_banner"]
    if "%" in a.input:
        command += ["-framerate", str(a.fps)]
    command += ["-i", a.input, "-i", str(a.score)]
    filters = ["scale=1920:1080:flags=lanczos", "setsar=1"]
    if not a.no_grade:
        filters += ["eq=contrast=1.025:saturation=1.045:brightness=0.001"]
    if not a.no_titles:
        ass_path = str(title_path.resolve()).replace("\\", "/").replace(":", "\\:").replace("'", "\\'")
        filters += [f"ass=filename='{ass_path}'"]
    if not a.no_fades:
        filters += ["fade=t=in:st=0:d=0.65", f"fade=t=out:st={max(0, a.duration - 1.2)}:d=1.2"]
    audio = f"loudnorm=I=-18:LRA=11:TP=-1.5,afade=t=in:st=0:d=1.5,afade=t=out:st={max(0, a.duration - 4)}:d=4"
    if a.keep_source_audio:
        complex_filter = f"[1:a]{audio},volume=0.75[score];[0:a][score]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.9[a]"
        command += ["-filter_complex", complex_filter, "-map", "0:v:0", "-map", "[a]"]
    else:
        command += ["-map", "0:v:0", "-map", "1:a:0", "-af", audio]
    command += ["-vf", ",".join(filters), "-t", str(a.duration),
                "-c:v", "libx264", "-preset", "slow", "-crf", "17",
                "-pix_fmt", "yuv420p", "-r", str(a.fps),
                "-c:a", "aac", "-b:a", "256k", "-ar", "48000",
                "-movflags", "+faststart", "-metadata", "title=Wardens And Raiders (WAR) — Feature Showcase",
                "-metadata", "comment=In-engine Unity feature showcase. Original synthesized instrumental score.",
                str(a.output)]
    subprocess.run(command, check=True)
    print(a.output.resolve())


if __name__ == "__main__":
    main()
