"""QuestCodex 브리핑 계약 스냅샷을 Briefing.Tests 용으로 자른다.

사용: python -I tools/trim-fixture.py <원본.json> <출력.json>
- 최상위 필드·traders 는 전부 유지
- maps 는 KEEP_MAPS 만(원래 순서). 그 맵의 quests 는 그대로, 나머지 배열은 앞 2개만
- anywhere.quests 는 그대로, 나머지 배열은 앞 2개만
"""
import json
import sys

KEEP_MAPS = ("bigmap", "factory4_day", "labyrinth")
SHORT = ("bring", "find", "gear", "special", "exits", "lockedDoors")


def trim(doc):
    doc["maps"] = [m for m in doc["maps"] if m["key"] in KEEP_MAPS]
    for m in doc["maps"]:
        for k in SHORT:
            if k in m:
                m[k] = m[k][:2]
    a = doc["anywhere"]
    for k in ("bring", "gear", "special"):
        if k in a:
            a[k] = a[k][:2]
    return doc


def main(src, dst):
    with open(src, encoding="utf-8") as f:
        doc = json.load(f)
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        json.dump(trim(doc), f, ensure_ascii=False, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
