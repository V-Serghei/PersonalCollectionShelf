#!/usr/bin/env python3
"""Build a resumable Google Books/Open Library seed from a saved LiveLib read list."""

from __future__ import annotations

import argparse
import calendar
import html
import json
import re
import time
import unicodedata
import uuid
from datetime import datetime, timezone
from difflib import SequenceMatcher
from pathlib import Path
from typing import Any

import requests


NAMESPACE = uuid.UUID("6d1399b1-a1e1-5b32-b8a8-f80c145ce218")
MONTHS = {
    "январь": 1, "февраль": 2, "март": 3, "апрель": 4,
    "май": 5, "июнь": 6, "июль": 7, "август": 8,
    "сентябрь": 9, "октябрь": 10, "ноябрь": 11, "декабрь": 12,
}


def clean_text(value: str | None) -> str:
    value = re.sub(r"<br\s*/?>", "\n", value or "", flags=re.I)
    value = re.sub(r"<[^>]+>", " ", value)
    return re.sub(r"\s+", " ", html.unescape(value)).strip()


def normalize(value: str | None) -> str:
    value = unicodedata.normalize("NFKD", value or "").casefold()
    return " ".join(re.findall(r"[\w]+", value, flags=re.UNICODE))


def similarity(left: str | None, right: str | None) -> float:
    return SequenceMatcher(None, normalize(left), normalize(right)).ratio()


def first_match(pattern: str, value: str, flags: int = re.S) -> str | None:
    match = re.search(pattern, value, flags)
    return clean_text(match.group(1)) if match else None


def parse_month(value: str) -> tuple[str, str]:
    match = re.search(r"([\wЀ-ӿ]+)\s+(\d{4})", clean_text(value), re.I)
    if not match:
        return "", ""
    month = MONTHS.get(match.group(1).casefold())
    year = int(match.group(2))
    if not month:
        return "", ""
    # The source contains only month/year. The first day is a stable storage convention.
    return datetime(year, month, 1).isoformat(), f"{month:02d}.{year}"


def parse_livelib(path: Path) -> list[dict[str, Any]]:
    source = path.read_text(encoding="utf-8", errors="replace")
    item_starts = [match.start() for match in re.finditer(
        r'<div class="book-item-manage(?: [^"]*)?" id="div-userbook-', source)]
    date_headers = [(match.start(), match.group(1)) for match in re.finditer(
        r'<div class="brow-h2"><h2[^>]*>(.*?)</h2>', source, re.S)]
    result: dict[int, dict[str, Any]] = {}
    for index, start in enumerate(item_starts):
        end = item_starts[index + 1] if index + 1 < len(item_starts) else len(source)
        body = source[start:end]
        title_match = re.search(
            r'<a class="brow-book-name[^"]*" href="https://www\.livelib\.ru/(book|work)/(\d+)-[^"]+"[^>]*>(.*?)</a>',
            body, re.S)
        if not title_match:
            continue
        source_kind = title_match.group(1)
        livelib_id = int(title_match.group(2))
        header = next((text for position, text in reversed(date_headers) if position < start), "")
        finish_date, finish_month = parse_month(header)
        author = first_match(r'<a class="brow-book-author"[^>]*>(.*?)</a>', body)
        personal_rating = first_match(r'class="rating-value stars-color-green">\s*([\d.]+)', body)
        livelib_rating = first_match(r'class="rating-value stars-color-orange">\s*([\d.]+)', body)
        description = first_match(r'<p[^>]+id="book-\d+-\d+-full"[^>]*>(.*?)</p>', body)
        cover_tag = re.search(r'<img[^>]+class="cover-rounded"[^>]*>', body, re.S)
        cover = re.search(r'\bsrc="([^"]+)"', cover_tag.group(0)) if cover_tag else None
        cover_path = None
        if cover:
            relative = html.unescape(cover.group(1)).replace("./", "")
            candidate = path.parent / Path(relative.replace("/", "\\"))
            if candidate.exists():
                cover_path = str(candidate.resolve())
        result[livelib_id] = {
            "id": str(uuid.uuid5(NAMESPACE, f"livelib:{source_kind}:{livelib_id}")),
            "livelibId": livelib_id,
            "livelibKind": source_kind,
            "title": clean_text(title_match.group(3)),
            "authors": [author] if author else [],
            "rating": float(personal_rating) if personal_rating else None,
            "finishDate": finish_date or None,
            "finishMonth": finish_month or None,
            "livelibRating": float(livelib_rating) if livelib_rating else None,
            "description": description,
            "localCoverPath": cover_path,
            "sourceUrl": re.search(r'href="(https://www\.livelib\.ru/(?:book|work)/[^\"]+)"', body).group(1),
        }
    return list(result.values())


class ApiClient:
    def __init__(self, google_key: str, contact: str):
        self.google_key = google_key
        self.session = requests.Session()
        self.session.headers.update({
            "User-Agent": f"PersonalCollectionShelf/1.0 ({contact or 'personal-use'})",
            "Accept": "application/json",
        })
        self.google_available = True

    def get(self, url: str, params: dict[str, Any]) -> dict[str, Any]:
        for attempt in range(6):
            response = self.session.get(url, params=params, timeout=45)
            if response.status_code == 429:
                time.sleep(2 + attempt * 2)
                continue
            if response.status_code >= 500:
                time.sleep(1.5 ** attempt)
                continue
            response.raise_for_status()
            return response.json()
        raise RuntimeError(f"Request failed after retries: {url}")

    def google_search(self, book: dict[str, Any]) -> list[dict[str, Any]]:
        if not self.google_available:
            return []
        author = book["authors"][0] if book["authors"] else ""
        query = f'intitle:"{book["title"]}"' + (f' inauthor:"{author}"' if author else "")
        data = self.get("https://www.googleapis.com/books/v1/volumes", {
            "q": query, "printType": "books", "maxResults": 10, "key": self.google_key,
        })
        return data.get("items", [])

    def openlibrary_search(self, book: dict[str, Any]) -> list[dict[str, Any]]:
        params: dict[str, Any] = {
            "title": book["title"], "limit": 10,
            "fields": "key,title,subtitle,author_name,first_publish_year,isbn,cover_i,ratings_average,ratings_count,language,publisher,number_of_pages_median,subject",
        }
        if book["authors"]:
            params["author"] = book["authors"][0]
        return self.get("https://openlibrary.org/search.json", params).get("docs", [])


def author_score(wanted: list[str], candidates: list[str]) -> float:
    if not wanted or not candidates:
        return 0.0
    return max(similarity(left, right) for left in wanted for right in candidates)


def choose_google(book: dict[str, Any], items: list[dict[str, Any]]) -> tuple[dict[str, Any] | None, float]:
    ranked: list[tuple[float, dict[str, Any]]] = []
    for item in items:
        info = item.get("volumeInfo", {})
        title_score = max(similarity(book["title"], info.get("title")), similarity(book["title"], info.get("subtitle")))
        authors = info.get("authors") or []
        a_score = author_score(book["authors"], authors)
        score = title_score * 0.72 + a_score * 0.28
        if title_score >= 0.97 and not authors:
            score = max(score, 0.76)
        ranked.append((score, item))
    return (ranked[0][1], ranked[0][0]) if ranked and (ranked := sorted(ranked, reverse=True, key=lambda x: x[0])) else (None, 0)


def choose_openlibrary(book: dict[str, Any], items: list[dict[str, Any]]) -> tuple[dict[str, Any] | None, float]:
    ranked: list[tuple[float, dict[str, Any]]] = []
    for item in items:
        title_score = similarity(book["title"], item.get("title"))
        a_score = author_score(book["authors"], item.get("author_name") or [])
        ranked.append((title_score * 0.72 + a_score * 0.28, item))
    return (ranked[0][1], ranked[0][0]) if ranked and (ranked := sorted(ranked, reverse=True, key=lambda x: x[0])) else (None, 0)


def identifiers(info: dict[str, Any]) -> tuple[str | None, str | None]:
    values = {value.get("type"): value.get("identifier") for value in info.get("industryIdentifiers", [])}
    return values.get("ISBN_10"), values.get("ISBN_13")


def high_resolution_google_cover(info: dict[str, Any]) -> str | None:
    links = info.get("imageLinks") or {}
    value = links.get("extraLarge") or links.get("large") or links.get("medium") or links.get("thumbnail")
    if not value:
        return None
    value = value.replace("http://", "https://")
    value = re.sub(r"[?&]zoom=\d+", "", value)
    return value.replace("&edge=curl", "")


def enrich(book: dict[str, Any], api: ApiClient, cache_dir: Path) -> dict[str, Any]:
    cache_path = cache_dir / f'{book["livelibKind"]}-{book["livelibId"]}.json'
    if cache_path.exists():
        cached = json.loads(cache_path.read_text(encoding="utf-8"))
        cached["mediaType"] = "Book"
        return cached
    try:
        google, google_score = choose_google(book, api.google_search(book))
    except (requests.RequestException, RuntimeError):
        api.google_available = False
        google, google_score = None, 0
    try:
        openlibrary, openlibrary_score = choose_openlibrary(book, api.openlibrary_search(book))
    except (requests.RequestException, RuntimeError):
        openlibrary, openlibrary_score = None, 0
    # Conservative threshold prevents a similarly named book from silently replacing the saved item.
    if google_score < 0.62:
        google = None
    if openlibrary_score < 0.62:
        openlibrary = None
    info = (google or {}).get("volumeInfo", {})
    isbn10, isbn13 = identifiers(info)
    ol_isbns = (openlibrary or {}).get("isbn") or []
    if not isbn13:
        isbn13 = next((value for value in ol_isbns if len(value.replace("-", "")) == 13), None)
    if not isbn10:
        isbn10 = next((value for value in ol_isbns if len(value.replace("-", "")) == 10), None)
    authors = info.get("authors") or (openlibrary or {}).get("author_name") or book["authors"]
    published = str(info.get("publishedDate") or (openlibrary or {}).get("first_publish_year") or "")
    year_match = re.match(r"(\d{4})", published)
    cover = high_resolution_google_cover(info)
    if not cover and (openlibrary or {}).get("cover_i"):
        cover = f'https://covers.openlibrary.org/b/id/{openlibrary["cover_i"]}-L.jpg'
    enriched = {
        **book,
        "mediaType": "Book",
        "matchStatus": "google-books" if google else "open-library" if openlibrary else "livelib-only",
        "matchScore": round(max(google_score if google else 0, openlibrary_score if openlibrary else 0), 4),
        "title": info.get("title") or (openlibrary or {}).get("title") or book["title"],
        "originalTitle": book["title"],
        "subtitle": info.get("subtitle") or (openlibrary or {}).get("subtitle"),
        "authors": authors,
        "description": info.get("description") or book.get("description"),
        "releaseYear": int(year_match.group(1)) if year_match else None,
        "publisher": info.get("publisher") or next(iter((openlibrary or {}).get("publisher") or []), None),
        "pageCount": info.get("pageCount") or (openlibrary or {}).get("number_of_pages_median"),
        "language": info.get("language") or next(iter((openlibrary or {}).get("language") or []), None),
        "isbn10": isbn10,
        "isbn13": isbn13,
        "genres": (info.get("categories") or (openlibrary or {}).get("subject") or [])[:12],
        "posterUrl": cover,
        "catalogProvider": "GoogleBooks" if google else "OpenLibrary" if openlibrary else "LiveLib",
        "catalogItemId": (google or {}).get("id") or (openlibrary or {}).get("key") or str(book["livelibId"]),
        "catalogSourceUrl": info.get("canonicalVolumeLink") or (f'https://openlibrary.org{openlibrary["key"]}' if openlibrary else book["sourceUrl"]),
        "catalogRatingPrimarySource": "LiveLib",
        "catalogRatingPrimary": book.get("livelibRating"),
        "catalogRatingPrimaryCount": None,
        "catalogRatingSecondarySource": "Google Books" if info.get("averageRating") is not None else "Open Library" if (openlibrary or {}).get("ratings_average") is not None else None,
        "catalogRatingSecondary": info.get("averageRating") if info.get("averageRating") is not None else (openlibrary or {}).get("ratings_average"),
        "catalogRatingSecondaryCount": info.get("ratingsCount") if info.get("ratingsCount") is not None else (openlibrary or {}).get("ratings_count"),
    }
    cache_path.write_text(json.dumps(enriched, ensure_ascii=False, indent=2), encoding="utf-8")
    return enriched


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--html", required=True, type=Path)
    parser.add_argument("--metadata", required=True, type=Path)
    parser.add_argument("--cache", type=Path, default=Path(".cache/livelib-import/items"))
    parser.add_argument("--output", type=Path, default=Path(".cache/livelib-import/livelib-books-seed.json"))
    args = parser.parse_args()
    settings = json.loads(args.metadata.read_text(encoding="utf-8-sig"))
    books = parse_livelib(args.html)
    args.cache.mkdir(parents=True, exist_ok=True)
    api = ApiClient(settings.get("googleBooksApiKey", ""), settings.get("openLibraryContactEmail", ""))
    output = []
    for index, book in enumerate(books, 1):
        item = enrich(book, api, args.cache)
        # Saved LiveLib identity wins over catalog display variants. Low-confidence
        # catalog matches are discarded so homonymous books cannot contaminate data.
        if item.get("matchStatus") != "livelib-only" and float(item.get("matchScore") or 0) < 0.74:
            item = {
                **book,
                "mediaType": "Book",
                "matchStatus": "livelib-only",
                "matchScore": item.get("matchScore"),
                "originalTitle": None,
                "releaseYear": None,
                "publisher": None,
                "pageCount": None,
                "language": None,
                "isbn10": None,
                "isbn13": None,
                "genres": [],
                "posterUrl": None,
                "catalogProvider": "LiveLib",
                "catalogItemId": str(book["livelibId"]),
                "catalogSourceUrl": book["sourceUrl"],
                "catalogRatingPrimarySource": "LiveLib",
                "catalogRatingPrimary": book.get("livelibRating"),
                "catalogRatingPrimaryCount": None,
                "catalogRatingSecondarySource": None,
                "catalogRatingSecondary": None,
                "catalogRatingSecondaryCount": None,
            }
        else:
            catalog_title = item.get("title")
            item["title"] = book["title"]
            item["originalTitle"] = catalog_title if normalize(catalog_title) != normalize(book["title"]) else None
            item["authors"] = book["authors"] or item.get("authors") or []
        output.append(item)
        if index % 20 == 0 or index == len(books):
            print(f"enriched {index}/{len(books)}")
    document = {"generatedAtUtc": datetime.now(timezone.utc).isoformat(), "items": output}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8")
    counts: dict[str, int] = {}
    for item in output:
        counts[item["matchStatus"]] = counts.get(item["matchStatus"], 0) + 1
    print(json.dumps({"total": len(output), "matches": counts}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
