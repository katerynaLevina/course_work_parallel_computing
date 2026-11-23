"""Download long English Wikipedia articles as one .txt file per article.

Only articles of at least MIN_ARTICLE_CHARS are kept, so ~20k files already hold 1-2 GB of text.
"""
import argparse
import json
import os
import tempfile
from pathlib import Path

import pyarrow.parquet as pq
import requests

SHARD_URL_TEMPLATE = (
    "https://huggingface.co/datasets/wikimedia/wikipedia/resolve/{revision}/"
    "{dataset_date}.en/train-{index:05d}-of-{count:05d}.parquet"
)
# Pinned dataset commit: together with the fixed shard/row order, length threshold and file cap
# it makes every run select exactly the same articles.
DATASET_REVISION = "b04c8d1ceb2f5cd4588862100d08de323dccfbaa"
DATASET_DATE = "20231101"
SHARD_COUNT = 41
ITER_BATCH_ROWS = 256
TEMP_SUFFIX = ".tmp"
MIN_ARTICLE_CHARS = 40_000
FILES_PER_FOLDER = 1000
DEFAULT_SIZE_GB = 2.0
DEFAULT_MAX_FILES = 20_000
DEFAULT_OUT_DIR = "data/wiki"
PROGRESS_FILE_NAME = ".progress.json"
BYTES_PER_GB = 1024 ** 3
DOWNLOAD_CHUNK_BYTES = 1024 * 1024
REQUEST_TIMEOUT_SECONDS = 60
PROGRESS_EVERY_FILES = 1000
COLUMNS = ["id", "title", "text"]


def download_shard(index, target):
    url = SHARD_URL_TEMPLATE.format(
        revision=DATASET_REVISION, dataset_date=DATASET_DATE, index=index, count=SHARD_COUNT)
    with requests.get(url, stream=True, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        response.raise_for_status()
        for chunk in response.iter_content(DOWNLOAD_CHUNK_BYTES):
            target.write(chunk)
    target.flush()


def write_atomically(path, content):
    temp_path = path.with_name(path.name + TEMP_SUFFIX)
    temp_path.write_text(content, encoding="utf-8")
    os.replace(temp_path, path)


def write_article(out_dir, number, article_id, title, text):
    """Writes the article unless it exists; returns the file size in bytes."""
    folder = out_dir / f"{number // FILES_PER_FOLDER:02d}"
    path = folder / f"{article_id}.txt"
    if path.exists():
        return path.stat().st_size
    folder.mkdir(parents=True, exist_ok=True)
    write_atomically(path, f"{title}\n\n{text}")
    return path.stat().st_size


def process_shard(index, out_dir, files, bytes_so_far, target_bytes, max_files):
    """Streams one shard into files; returns (files_written, bytes_written, reached_target)."""
    with tempfile.NamedTemporaryFile(suffix=".parquet") as shard:
        download_shard(index, shard)
        for batch in pq.ParquetFile(shard.name).iter_batches(batch_size=ITER_BATCH_ROWS, columns=COLUMNS):
            for row in batch.to_pylist():
                if len(row["text"]) < MIN_ARTICLE_CHARS:
                    continue
                bytes_so_far += write_article(out_dir, files, row["id"], row["title"], row["text"])
                files += 1
                if files % PROGRESS_EVERY_FILES == 0:
                    print(f"shard {index}: {files} files, {bytes_so_far / BYTES_PER_GB:.3f} GB", flush=True)
                if bytes_so_far >= target_bytes or files >= max_files:
                    return files, bytes_so_far, True
    return files, bytes_so_far, False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size-gb", type=float, default=DEFAULT_SIZE_GB)
    parser.add_argument("--max-files", type=int, default=DEFAULT_MAX_FILES)
    parser.add_argument("--out", default=DEFAULT_OUT_DIR)
    args = parser.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    progress_path = out_dir / PROGRESS_FILE_NAME
    progress = {"shard": 0, "files": 0, "bytes": 0}
    if progress_path.exists():
        progress = json.loads(progress_path.read_text())

    target_bytes = args.size_gb * BYTES_PER_GB
    files_written, bytes_written = progress["files"], progress["bytes"]
    for index in range(progress["shard"], SHARD_COUNT):
        if bytes_written >= target_bytes or files_written >= args.max_files:
            break
        files_written, bytes_written, reached = process_shard(
            index, out_dir, files_written, bytes_written, target_bytes, args.max_files)
        if reached:
            break
        progress = {"shard": index + 1, "files": files_written, "bytes": bytes_written}
        write_atomically(progress_path, json.dumps(progress))
        print(f"shard {index} done, {bytes_written / BYTES_PER_GB:.3f} GB", flush=True)
    print(f"finished: {files_written} files, {bytes_written / BYTES_PER_GB:.3f} GB in {out_dir}", flush=True)


if __name__ == "__main__":
    main()
