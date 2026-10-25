"""MinIO TTS parça önbelleği (cache/tts/) LRU/boyut temizlik ve tmp/ yaşam döngüsü betiği (Faz 9)."""

from __future__ import annotations

import argparse
from datetime import UTC, datetime, timedelta
import os
from minio import Minio


def prune_cache(
    endpoint: str,
    access_key: str,
    secret_key: str,
    bucket: str,
    max_gb: float,
    tmp_retention_days: int = 1,
) -> tuple[int, int]:
    """`tmp/` altındaki eski nesneleri ve `cache/tts/` altında kotayı aşan en eski nesneleri siler."""
    client = Minio(endpoint, access_key=access_key, secret_key=secret_key, secure=False)
    if not client.bucket_exists(bucket):
        return 0, 0

    now = datetime.now(UTC)
    tmp_cutoff = now - timedelta(days=tmp_retention_days)
    tmp_deleted = 0

    for obj in client.list_objects(bucket, prefix="tmp/", recursive=True):
        if obj.last_modified and obj.last_modified < tmp_cutoff:
            client.remove_object(bucket, obj.object_name)
            tmp_deleted += 1

    cache_objs = list(client.list_objects(bucket, prefix="cache/tts/", recursive=True))
    total_bytes = sum(o.size or 0 for o in cache_objs)
    max_bytes = int(max_gb * (1024**3))

    cache_deleted = 0
    if total_bytes > max_bytes:
        cache_objs.sort(key=lambda o: o.last_modified or now)
        for obj in cache_objs:
            if total_bytes <= max_bytes:
                break
            client.remove_object(bucket, obj.object_name)
            total_bytes -= obj.size or 0
            cache_deleted += 1

    return tmp_deleted, cache_deleted


def main() -> None:
    parser = argparse.ArgumentParser(description="SesliKitap MinIO TTS önbellek temizleyici")
    parser.add_argument("--endpoint", default=os.getenv("MINIO_ENDPOINT", "localhost:9010"))
    parser.add_argument("--access-key", default=os.getenv("MINIO_ROOT_USER", "minioadmin"))
    parser.add_argument("--secret-key", default=os.getenv("MINIO_ROOT_PASSWORD", "minioadmin"))
    parser.add_argument("--bucket", default=os.getenv("MINIO_BUCKET", "seslikitap"))
    parser.add_argument("--max-gb", type=float, default=30.0, help="Azami TTS önbellek boyutu (GB)")
    args = parser.parse_args()

    tmp_del, cache_del = prune_cache(
        endpoint=args.endpoint,
        access_key=args.access_key,
        secret_key=args.secret_key,
        bucket=args.bucket,
        max_gb=args.max_gb,
    )
    print(f"Temizlenen tmp/ nesnesi: {tmp_del}, Temizlenen cache/tts/ nesnesi: {cache_del}")


if __name__ == "__main__":
    main()
