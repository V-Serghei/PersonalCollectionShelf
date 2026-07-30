#!/usr/bin/env python3
"""Create device-transfer JPEG covers while preserving PC originals."""

from __future__ import annotations

import argparse
import concurrent.futures
from pathlib import Path

from PIL import Image, ImageOps


def convert(source: Path, destination: Path, max_dimension: int, quality: int) -> None:
    if destination.exists():
        return
    try:
        with Image.open(source) as opened:
            image = ImageOps.exif_transpose(opened).convert("RGB")
            image.thumbnail((max_dimension, max_dimension), Image.Resampling.LANCZOS)
            temporary = destination.with_suffix(".tmp")
            image.save(temporary, format="JPEG", quality=quality, optimize=True, progressive=True)
            temporary.replace(destination)
    except (OSError, ValueError):
        return


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--max-dimension", type=int, default=1200)
    parser.add_argument("--quality", type=int, default=82)
    args = parser.parse_args()
    args.destination.mkdir(parents=True, exist_ok=True)
    files = list(args.source.glob("*.jpg"))
    completed = 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        futures = [
            executor.submit(convert, source, args.destination / source.name, args.max_dimension, args.quality)
            for source in files
        ]
        for future in concurrent.futures.as_completed(futures):
            future.result()
            completed += 1
            if completed % 200 == 0 or completed == len(files):
                print(f"converted {completed}/{len(files)}", flush=True)


if __name__ == "__main__":
    main()
