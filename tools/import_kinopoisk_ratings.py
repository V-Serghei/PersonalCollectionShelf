#!/usr/bin/env python3
"""Build an enriched, resumable seed from saved Kinopoisk rating pages."""

from __future__ import annotations

import argparse
import concurrent.futures
import gzip
import html
import json
import os
import re
import threading
import time
import unicodedata
import uuid
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import requests


ITEM_RE = re.compile(
    r'<div class="item(?: even)?">(?P<body>.*?)'
    r'ur_data\.push\(\{film:\s*(?P<rating_id>\d+),\s*rating:\s*\'(?P<rating>\d+)\'',
    re.S,
)
TITLE_RE = re.compile(
    r'<div class="nameRus"><a href="https://www\.kinopoisk\.ru/(?P<kind>film|series)/(?P<id>\d+)/"[^>]*>'
    r'(?P<title>.*?)</a></div>',
    re.S,
)
NAMESPACE = uuid.UUID("ba951bd4-f609-52e7-afbb-c6c900ba7453")
TMDB_BASE = "https://api.themoviedb.org/3"
TMDB_IMAGE_BASE = "https://image.tmdb.org/t/p/original"
IMDB_RATINGS = "https://datasets.imdbws.com/title.ratings.tsv.gz"
KINOPOISK_BASE = "https://kinopoiskapiunofficial.tech/api"
ROLE_MAP = {
    "Director": "Director",
    "Screenplay": "Screenwriter",
    "Writer": "Screenwriter",
    "Producer": "Producer",
    "Executive Producer": "Producer",
    "Director of Photography": "Cinematographer",
    "Original Music Composer": "Composer",
    "Casting": "CastingDirector",
    "Production Design": "ProductionDesigner",
}
KINOPOISK_ROLE_MAP = {
    "DIRECTOR": "Director",
    "WRITER": "Screenwriter",
    "PRODUCER": "Producer",
    "OPERATOR": "Cinematographer",
    "COMPOSER": "Composer",
    "ACTOR": "Actor",
}


def text_content(value: str | None) -> str:
    return html.unescape(re.sub(r"<[^>]+>", "", value or "")).strip()


def parse_int(value: str | None) -> int | None:
    if not value:
        return None
    digits = re.sub(r"\D", "", value)
    return int(digits) if digits else None


def parse_pages(downloads: Path) -> list[dict[str, Any]]:
    result: dict[int, dict[str, Any]] = {}
    files = sorted(downloads.glob("Профиль_ 142002se - Оценки*.html"))
    for path in files:
        source = path.read_text(encoding="utf-8")
        for match in ITEM_RE.finditer(source):
            body = match.group("body")
            title_match = TITLE_RE.search(body)
            if not title_match:
                continue
            kinopoisk_id = int(title_match.group("id"))
            if kinopoisk_id != int(match.group("rating_id")):
                continue
            raw_title = text_content(title_match.group("title"))
            years = [int(value) for value in re.findall(r"(?<!\d)(?:18|19|20)\d{2}(?!\d)", raw_title)]
            if title_match.group("kind") == "series":
                clean_title = re.sub(r"\s*\(сериал,.*?\)\s*$", "", raw_title, flags=re.I)
            else:
                clean_title = re.sub(r"\s*\((?:18|19|20)\d{2}\)\s*$", "", raw_title)
            english = re.search(r'<div class="nameEng">(.*?)</div>', body, re.S)
            rating = re.search(
                r'<div class="rating">\s*<b>([\d.]+)</b>\s*'
                r'<span class="text-grey">\((.*?)\)</span>\s*'
                r'<span class="text-grey">(\d+)\s*мин\.</span>',
                body,
                re.S,
            )
            rated_at = re.search(r'<div class="date">(\d{2}\.\d{2}\.\d{4}),\s*(\d{2}:\d{2})</div>', body)
            result[kinopoisk_id] = {
                "id": str(uuid.uuid5(NAMESPACE, f"kinopoisk:{kinopoisk_id}")),
                "kinopoiskId": kinopoisk_id,
                "sourceKind": title_match.group("kind"),
                "title": clean_title.strip(),
                "originalTitle": text_content(english.group(1)) if english else None,
                "releaseYear": years[0] if years else None,
                "rating": int(match.group("rating")),
                "kinopoiskRating": float(rating.group(1)) if rating else None,
                "kinopoiskVoteCount": parse_int(rating.group(2)) if rating else None,
                "runtimeMinutesFromKinopoisk": int(rating.group(3)) if rating else None,
                "finishDate": (
                    datetime.strptime(f"{rated_at.group(1)} {rated_at.group(2)}", "%d.%m.%Y %H:%M").isoformat()
                    if rated_at
                    else None
                ),
            }
    return sorted(result.values(), key=lambda value: (value["finishDate"] or "", value["kinopoiskId"]))


def normalize_title(value: str | None) -> str:
    value = unicodedata.normalize("NFKD", value or "").casefold()
    return "".join(character for character in value if character.isalnum())


def candidate_score(source: dict[str, Any], candidate: dict[str, Any], is_tv: bool) -> int:
    names = {
        normalize_title(candidate.get("name" if is_tv else "title")),
        normalize_title(candidate.get("original_name" if is_tv else "original_title")),
    }
    wanted = {normalize_title(source.get("title")), normalize_title(source.get("originalTitle"))}
    wanted.discard("")
    names.discard("")
    score = 0
    if names & wanted:
        score += 100
    elif any(left in right or right in left for left in wanted for right in names if min(len(left), len(right)) >= 5):
        score += 45
    date = candidate.get("first_air_date" if is_tv else "release_date") or ""
    year = int(date[:4]) if len(date) >= 4 and date[:4].isdigit() else None
    wanted_year = source.get("releaseYear")
    if year and wanted_year:
        difference = abs(year - wanted_year)
        score += 35 if difference == 0 else 12 if difference == 1 else -min(difference * 5, 40)
    score += min(int(candidate.get("vote_count") or 0) // 1000, 15)
    return score


class TmdbClient:
    def __init__(self, token: str):
        self.token = token
        self.local = threading.local()

    def session(self) -> requests.Session:
        if not hasattr(self.local, "session"):
            session = requests.Session()
            session.headers.update({"Authorization": f"Bearer {self.token}", "Accept": "application/json"})
            self.local.session = session
        return self.local.session

    def get(self, path: str, params: dict[str, Any]) -> dict[str, Any]:
        for attempt in range(7):
            response = self.session().get(f"{TMDB_BASE}/{path}", params=params, timeout=45)
            if response.status_code == 429:
                time.sleep(float(response.headers.get("Retry-After", "2")) + attempt)
                continue
            if response.status_code >= 500:
                time.sleep(1.5 ** attempt)
                continue
            response.raise_for_status()
            return response.json()
        raise RuntimeError(f"TMDB request failed after retries: {path}")


class KinopoiskClient:
    def __init__(self, api_key: str):
        self.api_key = api_key
        self.local = threading.local()

    def session(self) -> requests.Session:
        if not hasattr(self.local, "session"):
            session = requests.Session()
            session.headers.update({"X-API-KEY": self.api_key, "Accept": "application/json"})
            self.local.session = session
        return self.local.session

    def get(self, path: str, params: dict[str, Any] | None = None) -> Any:
        for attempt in range(6):
            response = self.session().get(f"{KINOPOISK_BASE}/{path}", params=params, timeout=45)
            if response.status_code == 429:
                time.sleep(float(response.headers.get("Retry-After", "3")) + attempt)
                continue
            if response.status_code >= 500:
                time.sleep(1.5 ** attempt)
                continue
            response.raise_for_status()
            return response.json()
        raise RuntimeError(f"Kinopoisk request failed after retries: {path}")


def read_age_rating(details: dict[str, Any], is_tv: bool) -> str | None:
    container = details.get("content_ratings" if is_tv else "release_dates", {}).get("results", [])
    for country_code in ("RU", "US"):
        country = next((value for value in container if value.get("iso_3166_1") == country_code), None)
        if not country:
            continue
        if is_tv and country.get("rating"):
            return country["rating"]
        if not is_tv:
            value = next((entry.get("certification") for entry in country.get("release_dates", []) if entry.get("certification")), None)
            if value:
                return value
    return None


def enrich_one(source: dict[str, Any], client: TmdbClient, cache_dir: Path) -> dict[str, Any]:
    cache_path = cache_dir / f'{source["kinopoiskId"]}.json'
    if cache_path.exists():
        return json.loads(cache_path.read_text(encoding="utf-8"))
    is_tv = source["sourceKind"] == "series"
    kind = "tv" if is_tv else "movie"
    queries = [source.get("originalTitle"), source.get("title")]
    candidates: dict[int, dict[str, Any]] = {}
    for query in dict.fromkeys(value for value in queries if value):
        params: dict[str, Any] = {"query": query, "language": "ru-RU", "include_adult": "false", "page": 1}
        if source.get("releaseYear"):
            params["first_air_date_year" if is_tv else "primary_release_year"] = source["releaseYear"]
        search = client.get(f"search/{kind}", params)
        for candidate in search.get("results", [])[:10]:
            candidates[candidate["id"]] = candidate
        if any(candidate_score(source, value, is_tv) >= 100 for value in candidates.values()):
            break
    if not candidates:
        enriched = {**source, "matchStatus": "not-found"}
    else:
        chosen = max(candidates.values(), key=lambda value: candidate_score(source, value, is_tv))
        score = candidate_score(source, chosen, is_tv)
        if score < 55:
            enriched = {**source, "matchStatus": "low-confidence", "matchScore": score}
        else:
            append = "credits,external_ids,content_ratings" if is_tv else "credits,external_ids,release_dates"
            details = client.get(f'{kind}/{chosen["id"]}', {"language": "ru-RU", "append_to_response": append})
            genres = [value["name"] for value in details.get("genres", [])]
            genre_ids = {value.get("id") for value in details.get("genres", [])}
            language = details.get("original_language")
            if is_tv:
                media_type = "Anime" if 16 in genre_ids and language == "ja" else "AnimatedSeries" if 16 in genre_ids else "Series"
            else:
                media_type = "Cartoon" if 16 in genre_ids else "Movie"
            people: list[dict[str, Any]] = []
            seen: set[tuple[str, str]] = set()
            for member in details.get("credits", {}).get("crew", []):
                role = ROLE_MAP.get(member.get("job"))
                name = member.get("name")
                if role and name and (role, name.casefold()) not in seen:
                    people.append({"name": name, "role": role, "details": None})
                    seen.add((role, name.casefold()))
            for member in details.get("credits", {}).get("cast", [])[:12]:
                name = member.get("name")
                if name and ("Actor", name.casefold()) not in seen:
                    people.append({"name": name, "role": "Actor", "details": member.get("character")})
                    seen.add(("Actor", name.casefold()))
            countries = details.get("origin_country", []) if is_tv else [value.get("name") for value in details.get("production_countries", [])]
            runtime = next(iter(details.get("episode_run_time") or []), None) if is_tv else details.get("runtime")
            poster_path = details.get("poster_path")
            enriched = {
                **source,
                "matchStatus": "matched",
                "matchScore": score,
                "tmdbId": details["id"],
                "imdbId": details.get("external_ids", {}).get("imdb_id"),
                "title": details.get("name" if is_tv else "title") or source["title"],
                "originalTitle": details.get("original_name" if is_tv else "original_title") or source.get("originalTitle"),
                "description": details.get("overview") or None,
                "releaseYear": source.get("releaseYear"),
                "mediaType": media_type,
                "tmdbRating": details.get("vote_average"),
                "tmdbVoteCount": details.get("vote_count"),
                "posterUrl": f"{TMDB_IMAGE_BASE}{poster_path}" if poster_path else None,
                "genres": genres,
                "people": people,
                "studios": [value["name"] for value in details.get("production_companies", [])],
                "runtimeMinutes": runtime or source.get("runtimeMinutesFromKinopoisk"),
                "originalLanguage": language,
                "country": next((value for value in countries if value), None),
                "ageRating": read_age_rating(details, is_tv),
                "seasonCount": details.get("number_of_seasons"),
                "episodeCount": details.get("number_of_episodes"),
                "network": next((value.get("name") for value in details.get("networks", []) if value.get("name")), None),
                "airingStatus": details.get("status"),
                "collectionName": (details.get("belongs_to_collection") or {}).get("name"),
            }
    temporary = cache_path.with_suffix(".tmp")
    temporary.write_text(json.dumps(enriched, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(cache_path)
    return enriched


def repair_with_kinopoisk(item: dict[str, Any], client: KinopoiskClient) -> dict[str, Any]:
    if item.get("matchStatus") == "matched":
        return item
    try:
        details = client.get(f'v2.2/films/{item["kinopoiskId"]}')
        staff = client.get("v1/staff", {"filmId": item["kinopoiskId"]})
    except requests.HTTPError as exception:
        return {
            **item,
            "matchStatus": "html-only",
            "mediaType": "Series" if item.get("sourceKind") == "series" else "Movie",
            "repairError": str(exception.response.status_code) if exception.response is not None else "http",
        }
    genres = [value.get("genre") for value in details.get("genres", []) if value.get("genre")]
    countries = [value.get("country") for value in details.get("countries", []) if value.get("country")]
    genre_keys = {value.casefold() for value in genres}
    country_keys = {value.casefold() for value in countries}
    is_tv = bool(details.get("serial")) or item.get("sourceKind") == "series"
    is_anime = "аниме" in genre_keys or ("мультфильм" in genre_keys and "япония" in country_keys)
    is_animation = "мультфильм" in genre_keys or is_anime
    if is_tv:
        media_type = "Anime" if is_anime else "AnimatedSeries" if is_animation else "Series"
    else:
        media_type = "Cartoon" if is_animation else "Movie"
    people: list[dict[str, Any]] = []
    seen: set[tuple[str, str]] = set()
    for member in staff:
        role = KINOPOISK_ROLE_MAP.get(member.get("professionKey"))
        name = member.get("nameRu") or member.get("nameEn")
        if not role or not name or (role, name.casefold()) in seen:
            continue
        if role == "Actor" and sum(1 for value in people if value["role"] == "Actor") >= 12:
            continue
        people.append({"name": name, "role": role, "details": member.get("description")})
        seen.add((role, name.casefold()))
    return {
        **item,
        "matchStatus": "kinopoisk-only",
        "title": details.get("nameRu") or item.get("title"),
        "originalTitle": details.get("nameOriginal") or details.get("nameEn") or item.get("originalTitle"),
        "description": details.get("description") or details.get("shortDescription"),
        "releaseYear": details.get("year") or item.get("releaseYear"),
        "mediaType": media_type,
        "imdbId": details.get("imdbId"),
        "imdbRating": details.get("ratingImdb"),
        "imdbVoteCount": details.get("ratingImdbVoteCount"),
        "kinopoiskRating": details.get("ratingKinopoisk") or item.get("kinopoiskRating"),
        "kinopoiskVoteCount": details.get("ratingKinopoiskVoteCount") or item.get("kinopoiskVoteCount"),
        "posterUrl": details.get("posterUrl"),
        "genres": genres,
        "people": people,
        "studios": [],
        "runtimeMinutes": details.get("filmLength") or item.get("runtimeMinutesFromKinopoisk"),
        "originalLanguage": None,
        "country": next(iter(countries), None),
        "ageRating": details.get("ratingAgeLimits") or details.get("ratingMpaa"),
        "seasonCount": None,
        "episodeCount": None,
        "network": None,
        "airingStatus": details.get("productionStatus"),
        "collectionName": None,
    }


def load_imdb_ratings(cache_root: Path, needed_ids: set[str]) -> dict[str, tuple[float, int]]:
    if not needed_ids:
        return {}
    path = cache_root / "title.ratings.tsv.gz"
    if not path.exists() or time.time() - path.stat().st_mtime > 24 * 60 * 60:
        response = requests.get(IMDB_RATINGS, timeout=120, stream=True)
        response.raise_for_status()
        temporary = path.with_suffix(".tmp")
        with temporary.open("wb") as target:
            for chunk in response.iter_content(1024 * 1024):
                target.write(chunk)
        temporary.replace(path)
    result: dict[str, tuple[float, int]] = {}
    with gzip.open(path, "rt", encoding="utf-8") as source:
        next(source, None)
        for line in source:
            imdb_id, rating, votes = line.rstrip("\n").split("\t")
            if imdb_id in needed_ids:
                result[imdb_id] = (float(rating), int(votes))
                if len(result) == len(needed_ids):
                    break
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--downloads", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--workers", type=int, default=6)
    parser.add_argument("--limit", type=int)
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    item_cache = args.cache / "items"
    item_cache.mkdir(exist_ok=True)
    options = json.loads(args.metadata.read_text(encoding="utf-8"))
    token = options.get("tmdbReadAccessToken", "").strip()
    if not token:
        raise SystemExit("TMDB token is not configured")
    sources = parse_pages(args.downloads)
    if args.limit:
        sources = sources[: args.limit]
    client = TmdbClient(token)
    kinopoisk_key = options.get("kinopoiskApiKey", "").strip()
    completed = 0
    lock = threading.Lock()

    def work(source: dict[str, Any]) -> dict[str, Any]:
        nonlocal completed
        value = enrich_one(source, client, item_cache)
        with lock:
            completed += 1
            if completed % 25 == 0 or completed == len(sources):
                print(f"enriched {completed}/{len(sources)}", flush=True)
        return value

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as executor:
        items = list(executor.map(work, sources))
    if kinopoisk_key:
        repair_client = KinopoiskClient(kinopoisk_key)
        repair_targets = sum(1 for value in items if value.get("matchStatus") != "matched")
        repaired = 0

        def repair(value: dict[str, Any]) -> dict[str, Any]:
            nonlocal repaired
            result = repair_with_kinopoisk(value, repair_client)
            if value.get("matchStatus") != "matched":
                with lock:
                    repaired += 1
                    if repaired % 20 == 0 or repaired == repair_targets:
                        print(f"repaired {repaired}/{repair_targets}", flush=True)
            return result

        with concurrent.futures.ThreadPoolExecutor(max_workers=min(args.workers, 4)) as executor:
            items = list(executor.map(repair, items))
    imdb = load_imdb_ratings(args.cache, {value["imdbId"] for value in items if value.get("imdbId")})
    for item in items:
        if item.get("imdbId") in imdb:
            item["imdbRating"], item["imdbVoteCount"] = imdb[item["imdbId"]]
    document = {
        "version": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "items": items,
        "summary": {
            "total": len(items),
            "matchStatus": dict(Counter(value.get("matchStatus", "unknown") for value in items)),
            "mediaTypes": dict(Counter(value.get("mediaType", "Unknown") for value in items)),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(document["summary"], ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
