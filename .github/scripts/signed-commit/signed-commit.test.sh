#!/usr/bin/env bash
set -euo pipefail
#
# signed-commit.test.sh - signed-commit.sh 로컬 검증 (실제 GitHub 호출 없음)
#
# `gh` 를 가짜(python)로 대체해 bare 저장소 위에서 blobs/trees/commits/refs 를 흉내 낸다.
# 스테이징한 index tree 와 원격 브랜치 tree 가 같은지로 추가·수정·삭제·이름변경·실행 비트·
# 심볼릭 링크·바이너리·대용량·CRLF·빈 파일·유니코드 경로의 재현을 확인하고,
# 미서명(verified=false) 시 ref 가 만들어지지 않는지, force 없는 non-FF 갱신이 거부되는지도 본다.
#
# 실행: bash .github/scripts/signed-commit/signed-commit.test.sh

SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/signed-commit.sh"
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT

mkdir -p "$T/bin"
cat > "$T/bin/gh" <<'PYEOF'
#!/usr/bin/env python3
import base64, json, os, subprocess, sys

args = sys.argv[1:]
assert args[0] == "api", args
args = args[1:]
method, path, inp, jq = "GET", None, None, None
i = 0
while i < len(args):
    a = args[i]
    if a == "-X":
        method = args[i + 1]; i += 2
    elif a == "--input":
        inp = args[i + 1]; i += 2
    elif a == "--jq":
        jq = args[i + 1]; i += 2
    else:
        path = a; i += 1

env = dict(os.environ, GIT_DIR=os.environ["FAKE_REMOTE"],
           GIT_AUTHOR_NAME="x", GIT_AUTHOR_EMAIL="x@x", GIT_COMMITTER_NAME="x", GIT_COMMITTER_EMAIL="x@x")
with open(os.environ["FAKE_LOG"], "a") as f:
    f.write(f"{method} {path}\n")

def git(*a, inp=None, extra=None):
    e = dict(env, **(extra or {}))
    r = subprocess.run(["git", *a], input=inp, capture_output=True, env=e)
    if r.returncode != 0:
        sys.stderr.write(r.stderr.decode()); sys.exit(1)
    return r.stdout

def out(obj):
    if jq == ".sha":
        print(obj["sha"], end="")
    else:
        print(json.dumps(obj), end="")
    sys.exit(0)

def fail(code, msg):
    sys.stderr.write(f"gh: {msg} (HTTP {code})\n"); sys.exit(1)

body = json.load(open(inp)) if inp else None
parts = path.split("/")  # repos/o/r/git/...
sub = "/".join(parts[3:])

if method == "POST" and sub == "git/blobs":
    assert body["encoding"] == "base64"
    sha = git("hash-object", "-w", "--stdin", inp=base64.b64decode(body["content"])).decode().strip()
    out({"sha": sha})
if method == "POST" and sub == "git/trees":
    idx = os.path.join(os.environ["FAKE_TMP"], "idx")
    if os.path.exists(idx): os.remove(idx)
    os.makedirs(os.path.join(os.environ["FAKE_TMP"], "wt"), exist_ok=True)
    x = {"GIT_INDEX_FILE": idx, "GIT_WORK_TREE": os.path.join(os.environ["FAKE_TMP"], "wt")}
    git("read-tree", body["base_tree"], extra=x)
    for e in body["tree"]:
        if "sha" in e and e["sha"] is None:
            git("update-index", "--force-remove", "--", e["path"], extra=x); continue
        if "content" in e:
            sha = git("hash-object", "-w", "--stdin", inp=e["content"].encode()).decode().strip()
        else:
            sha = e["sha"]; git("cat-file", "-e", sha)
        git("update-index", "--add", "--cacheinfo", f'{e["mode"]},{sha},{e["path"]}', extra=x)
    out({"sha": git("write-tree", extra=x).decode().strip()})
if method == "POST" and sub == "git/commits":
    assert "author" not in body and "committer" not in body, "author/committer 지정 금지"
    sha = git("commit-tree", body["tree"], *sum([["-p", p] for p in body["parents"]], []), "-m", body["message"]).decode().strip()
    ver = os.environ.get("FAKE_UNSIGNED") != "1"
    out({"sha": sha, "verification": {"verified": ver, "reason": "valid" if ver else "unsigned"}})
if sub.startswith("git/ref/heads/") and method == "GET":
    r = subprocess.run(["git", "rev-parse", "--verify", "-q", "refs/heads/" + sub[len("git/ref/heads/"):]], capture_output=True, env=env)
    if r.returncode != 0: fail(404, "Not Found")
    out({"sha": r.stdout.decode().strip()})
if method == "POST" and sub == "git/refs":
    git("update-ref", body["ref"], body["sha"]); out({"sha": body["sha"]})
if method == "PATCH" and sub.startswith("git/refs/heads/"):
    ref = "refs/heads/" + sub[len("git/refs/heads/"):]
    cur = git("rev-parse", ref).decode().strip()
    r = subprocess.run(["git", "merge-base", "--is-ancestor", cur, body["sha"]], env=env)
    if r.returncode != 0 and not body["force"]: fail(422, "Update is not a fast forward")
    git("update-ref", ref, body["sha"]); out({"sha": body["sha"]})
fail(404, f"unhandled {method} {sub}")
PYEOF
chmod +x "$T/bin/gh"

export PATH="$T/bin:$PATH"
export FAKE_REMOTE="$T/remote.git" FAKE_LOG="$T/log" FAKE_TMP="$T"
: > "$T/log"
export GIT_AUTHOR_NAME=t GIT_AUTHOR_EMAIL=t@t GIT_COMMITTER_NAME=t GIT_COMMITTER_EMAIL=t@t
export GH_TOKEN=dummy REPO=o/r

git init -q --bare "$T/remote.git"
git init -q -b main "$T/work"
cd "$T/work"
git config commit.gpgsign false
git remote add origin "$T/remote.git"

printf 'keep\n' > keep.txt
printf 'old\n' > modify.txt
printf 'gone\n' > delete.txt
printf '#!/bin/sh\necho hi\n' > run.sh
printf 'rename me\n' > before-rename.txt
printf 'mode only\n' > modeonly.sh
ln -s keep.txt link
mkdir -p sub
git add -A && git commit -q -m base
git push -q origin main
PARENT=$(git rev-parse HEAD)

# 변경 구성
printf 'new\n' > modify.txt
git rm -q delete.txt
chmod +x run.sh
git mv before-rename.txt "sub/한글 이름 (1).txt"
chmod +x modeonly.sh
printf 'line1\r\nline2\r\n' > crlf.txt
printf 'no newline' > nonl.txt
: > empty.txt
head -c 3000 /dev/urandom > bin.dat
head -c 700000 /dev/urandom | base64 > big.txt
ln -sf modify.txt link
ln -s sub/x newlink
printf 'x\n' > '.hidden file'
git add -A

run_script() {
  MESSAGE="${TEST_MESSAGE-$'제목\n\n본문 줄'}" PARENT_SHA="${PARENT_SHA:-}" FORCE="${FORCE:-false}" BRANCH="$1" bash "$SCRIPT"
}

fail() { echo "FAIL: $*" >&2; exit 1; }

# 1) 기본: 새 브랜치 생성
run_script feature/test > "$T/out1"
EXPECT_TREE=$(git write-tree)
REMOTE_TREE=$(git --git-dir="$FAKE_REMOTE" rev-parse feature/test^{tree} 2> /dev/null || git --git-dir="$FAKE_REMOTE" rev-parse 'refs/heads/feature/test^{tree}')
[ "$EXPECT_TREE" = "$REMOTE_TREE" ] || fail "tree 불일치: $EXPECT_TREE != $REMOTE_TREE"
[ "$(git --git-dir="$FAKE_REMOTE" log -1 --format=%B refs/heads/feature/test)" = $'제목\n\n본문 줄' ] || fail "메시지 불일치"
[ "$(git rev-parse HEAD)" = "$(git --git-dir="$FAKE_REMOTE" rev-parse refs/heads/feature/test)" ] || fail "로컬 동기화 안 됨"
[ "$(git rev-parse --abbrev-ref HEAD)" = "feature/test" ] || fail "로컬 브랜치 아님"
[ -z "$(git status --porcelain)" ] || fail "동기화 후 작업트리 dirty"
echo "ok 1: 추가·수정·삭제·이름변경·모드·심볼릭 링크·바이너리·대용량 tree 동일"
grep -c 'git/blobs' "$T/log" | grep -qx '[1-9][0-9]*' || fail "blob 업로드 호출 없음(바이너리/대용량)"
[ "$(grep -c 'git/blobs' "$T/log")" -eq 5 ] || fail "blob 호출 수 불일치(바이너리·대용량·빈 파일·심볼릭 링크 5개만 기대): $(grep -c 'git/blobs' "$T/log")"

# 2) 변경 없음: 아무것도 만들지 않음
: > "$T/log"
GITHUB_OUTPUT="$T/gho" TEST_MESSAGE= run_script feature/test > "$T/out2"
grep -qx 'committed=false' "$T/gho" || fail "committed=false 아님"
[ ! -s "$T/log" ] || fail "변경 없는데 API 호출됨"
echo "ok 2: 변경 없음 -> 호출 0"

# 3) 기존 브랜치 갱신: 같은 부모에서 다른 커밋 -> force 없으면 거부, force면 성공
git checkout -q -b other "$PARENT"
printf 'other\n' > other.txt
git add -A
if PARENT_SHA="$PARENT" FORCE=false run_script feature/test > /dev/null 2>&1; then fail "non-FF 갱신이 force 없이 성공"; fi
git checkout -q other 2> /dev/null || true
printf 'other\n' > other.txt
git add -A
EXPECT3=$(git write-tree)
PARENT_SHA="$PARENT" FORCE=true run_script feature/test > /dev/null
[ "$(git --git-dir="$FAKE_REMOTE" rev-parse refs/heads/feature/test^{tree})" = "$EXPECT3" ] || fail "force 갱신 tree 불일치"
echo "ok 3: non-FF는 force 있을 때만"

# 4) 미서명 가드: ref 가 만들어지면 안 됨
printf 'u\n' > u.txt
git add -A
if FAKE_UNSIGNED=1 run_script feature/unsigned > /dev/null 2>&1; then fail "미서명인데 성공"; fi
git --git-dir="$FAKE_REMOTE" rev-parse -q --verify refs/heads/feature/unsigned > /dev/null && fail "미서명인데 ref 생성됨"
echo "ok 4: verified=false 면 실패하고 ref 미생성"

echo "ALL PASS"
