"""Download a slice of English Wikipedia as one .txt file per article."""
import argparse
import json
import tempfile
from pathlib import Path

import pyarrow.parquet as pq
import requests

SHARD_URL_TEMPLATE = (
    "https://huggingface.co/datasets/wikimedia/wikipedia/resolve/main/"
    "20231101.en/train-{index:05d}-of-{count:05d}.parquet"
)
SHARD_COUNT = 41
MIN_ARTICLE_CHARS = 500
FILES_PER_FOLDER = 1000
DEFAULT_SIZE_GB = 2.0
DEFAULT_OUT_DIR = "data/wiki"
PROGRESS_FILE_NAME = ".progress.json"
BYTES_PER_GB = 1024 ** 3
DOWNLOAD_CHUNK_BYTES = 1024 * 1024
REQUEST_TIMEOUT_SECONDS = 60
PROGRESS_EVERY_FILES = 1000
COLUMNS = ["id", "title", "text"]


def download_shard(index, target):
    url = SHARD_URL_TEMPLATE.format(index=index, count=SHARD_COUNT)
    with requests.get(url, stream=True, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        response.raise_for_status()
        for chunk in response.iter_content(DOWNLOAD_CHUNK_BYTES):
            target.write(chunk)
    target.flush()


def write_article(out_dir, article_id, title, text):
    """Writes the article unless it exists; returns the file size in bytes."""
    folder = out_dir / f"{int(article_id) // FILES_PER_FOLDER:04d}"
    path = folder / f"{article_id}.txt"
    if path.exists():
        return path.stat().st_size
    folder.mkdir(parents=True, exist_ok=True)
    path.write_text(f"{title}\n\n{text}", encoding="utf-8")
    return path.stat().st_size


def process_shard(index, out_dir, bytes_so_far, target_bytes):
    """Streams one shard into files; returns (bytes_written, reached_target)."""
    files = 0
    with tempfile.NamedTemporaryFile(suffix=".parquet") as shard:
        download_shard(index, shard)
        for batch in pq.ParquetFile(shard.name).iter_batches(columns=COLUMNS):
            for row in batch.to_pylist():
                if len(row["text"]) < MIN_ARTICLE_CHARS:
                    continue
                bytes_so_far += write_article(out_dir, row["id"], row["title"], row["text"])
                files += 1
                if files % PROGRESS_EVERY_FILES == 0:
                    print(f"shard {index}: {files} files, {bytes_so_far / BYTES_PER_GB:.3f} GB", flush=True)
                if bytes_so_far >= target_bytes:
                    return bytes_so_far, True
    return bytes_so_far, False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size-gb", type=float, default=DEFAULT_SIZE_GB)
    parser.add_argument("--out", default=DEFAULT_OUT_DIR)
    args = parser.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    progress_path = out_dir / PROGRESS_FILE_NAME
    progress = {"shard": 0, "bytes": 0}
    if progress_path.exists():
        progress = json.loads(progress_path.read_text())

    target_bytes = args.size_gb * BYTES_PER_GB
    bytes_written = progress["bytes"]
    for index in range(progress["shard"], SHARD_COUNT):
        shard_start_bytes = bytes_written
        bytes_written, reached = process_shard(index, out_dir, shard_start_bytes, target_bytes)
        if reached:
            break
        progress = {"shard": index + 1, "bytes": bytes_written}
        progress_path.write_text(json.dumps(progress))
        print(f"shard {index} done, {bytes_written / BYTES_PER_GB:.3f} GB", flush=True)
    print(f"finished: {bytes_written / BYTES_PER_GB:.3f} GB in {out_dir}", flush=True)


if __name__ == "__main__":
    main()
