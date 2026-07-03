from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


TYPE_DECL_RE = re.compile(
    r"(?P<prefix>(?:\[[^\]]+\]\s*)*(?:public|internal|private|protected|static|sealed|abstract|partial|\s)*)"
    r"\b(?P<kind>class|struct|interface|enum)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
    r"(?P<tail>[^{\n]*)"
)
TOKEN_RE = re.compile(r"\b[A-Za-z_][A-Za-z0-9_]*\b")
IF_DIRECTIVE_RE = re.compile(r"^\s*#(if|elif|else|endif)\b(.*)$")
BASE_TYPE_RE = re.compile(r":\s*([A-Za-z_][A-Za-z0-9_<>,\s.]*)")

UNITY_MESSAGE_NAMES = {
    "Awake",
    "Start",
    "Update",
    "FixedUpdate",
    "LateUpdate",
    "OnEnable",
    "OnDisable",
    "OnDestroy",
    "OnTriggerEnter",
    "OnTriggerExit",
    "OnCollisionEnter",
    "OnCollisionExit",
    "OnMouseDown",
    "OnMouseUp",
}

KEYWORDS = {
    "class",
    "struct",
    "interface",
    "enum",
    "public",
    "private",
    "protected",
    "internal",
    "static",
    "sealed",
    "abstract",
    "partial",
    "void",
    "return",
    "new",
    "null",
    "true",
    "false",
    "if",
    "else",
    "for",
    "while",
    "switch",
    "case",
    "typeof",
    "default",
    "base",
    "this",
}


@dataclass
class TypeInfo:
    name: str
    kind: str
    files: set[str] = field(default_factory=set)
    mono_behaviour: bool = False
    declaration_lines: list[tuple[str, int]] = field(default_factory=list)


@dataclass
class FileInfo:
    path: Path
    relative_path: str
    active_lines: list[str]
    skipped_disabled_lines: int
    type_decls: list[tuple[str, str, int, bool]]
    mono_behaviours: list[str]
    source_types: list[str]


def strip_disabled_preprocessor_lines(lines: list[str]) -> tuple[list[str], int]:
    active: list[str] = []
    stack: list[dict[str, bool]] = []
    skipped = 0

    def eval_expr(expr: str) -> bool | None:
        expr = expr.strip()
        if expr == "true":
            return True
        if expr == "false":
            return False
        return None

    for raw_line in lines:
        match = IF_DIRECTIVE_RE.match(raw_line)
        if match:
            directive, expr = match.group(1), match.group(2)
            if directive == "if":
                result = eval_expr(expr)
                active_now = True if result is None else result
                stack.append({"parent": all(frame["current"] for frame in stack), "seen_true": active_now, "current": active_now})
            elif directive == "elif" and stack:
                result = eval_expr(expr)
                frame = stack[-1]
                if frame["seen_true"]:
                    frame["current"] = False
                else:
                    active_now = True if result is None else result
                    frame["current"] = active_now
                    frame["seen_true"] = active_now
            elif directive == "else" and stack:
                frame = stack[-1]
                frame["current"] = not frame["seen_true"]
                frame["seen_true"] = True
            elif directive == "endif" and stack:
                stack.pop()
            active.append("")
            continue

        is_active = all(frame["current"] for frame in stack)
        if is_active:
            active.append(raw_line)
        else:
            active.append("")
            skipped += 1
    return active, skipped


def read_file_info(path: Path, root: Path) -> FileInfo:
    raw_lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
    active_lines, skipped_disabled_lines = strip_disabled_preprocessor_lines(raw_lines)
    type_decls: list[tuple[str, str, int, bool]] = []
    mono_behaviours: list[str] = []

    for lineno, line in enumerate(active_lines, start=1):
        match = TYPE_DECL_RE.search(line)
        if not match:
            continue
        kind = match.group("kind")
        name = match.group("name")
        tail = match.group("tail") or ""
        mono = "MonoBehaviour" in tail
        type_decls.append((name, kind, lineno, mono))
        if mono:
            mono_behaviours.append(name)

    source_types = [name for name, kind, _, _ in type_decls if kind == "class"] or [name for name, _, _, _ in type_decls]
    return FileInfo(
        path=path,
        relative_path=path.relative_to(root).as_posix(),
        active_lines=active_lines,
        skipped_disabled_lines=skipped_disabled_lines,
        type_decls=type_decls,
        mono_behaviours=mono_behaviours,
        source_types=source_types,
    )


def collect_types(files: Iterable[FileInfo]) -> dict[str, TypeInfo]:
    types: dict[str, TypeInfo] = {}
    for info in files:
        for name, kind, lineno, mono in info.type_decls:
            entry = types.setdefault(name, TypeInfo(name=name, kind=kind))
            entry.files.add(info.relative_path)
            entry.mono_behaviour = entry.mono_behaviour or mono
            entry.declaration_lines.append((info.relative_path, lineno))
    return types


def line_categories(line: str, target: str) -> list[str]:
    categories: list[str] = []
    snippets = {
        "GetComponent<": "GetComponent",
        "FindAnyObjectByType<": "FindObjectByType",
        "FindFirstObjectByType<": "FindObjectByType",
        "FindObjectsByType<": "FindObjectsByType",
        "AddComponent<": "AddComponent",
        "typeof(": "typeof",
    }
    for marker, label in snippets.items():
        if marker + target in line or marker + " " + target in line:
            categories.append(label)
    if f"{target}.Instance" in line:
        categories.append("Singleton.Instance")
    if f"{target}." in line:
        categories.append("Static/enum call")
    if re.search(rf"\b{re.escape(target)}\s+[A-Za-z_][A-Za-z0-9_]*\b", line):
        categories.append("typed field/param/local")
    if f"<{target}>" in line:
        categories.append("generic type")
    if not categories:
        categories.append("type reference")
    return categories


def build_references(files: list[FileInfo], types: dict[str, TypeInfo]) -> tuple[dict[str, dict[str, list[dict]]], set[str]]:
    known_types = set(types)
    lower_to_types: dict[str, list[str]] = defaultdict(list)
    for type_name in known_types:
        lower_to_types[type_name.lower()].append(type_name)

    edges: dict[str, dict[str, list[dict]]] = defaultdict(lambda: defaultdict(list))
    skipped_case_collisions: set[str] = set()

    for info in files:
        file_declared = {name for name, _, _, _ in info.type_decls}
        for lineno, line in enumerate(info.active_lines, start=1):
            if not line.strip():
                continue
            tokens = [tok for tok in TOKEN_RE.findall(line) if tok not in KEYWORDS]
            if not tokens:
                continue
            seen_targets: set[tuple[str, str]] = set()
            for token in set(tokens):
                candidates = lower_to_types.get(token.lower(), [])
                if not candidates:
                    continue
                if len(candidates) > 1 and token not in candidates:
                    skipped_case_collisions.update(candidates)
                    continue
                target = token if token in candidates else candidates[0]
                if target in file_declared and re.search(rf"\b(class|struct|interface|enum)\s+{re.escape(target)}\b", line):
                    continue
                categories = line_categories(line, target)
                for source in info.source_types:
                    if source == target:
                        continue
                    key = (source, target)
                    if key in seen_targets:
                        continue
                    seen_targets.add(key)
                    edges[source][target].append(
                        {
                            "file": info.relative_path,
                            "line": lineno,
                            "cat": categories[0],
                            "text": line.strip(),
                        }
                    )
    return edges, skipped_case_collisions


def compute_incoming(edges: dict[str, dict[str, list[dict]]]) -> dict[str, list[str]]:
    incoming: dict[str, set[str]] = defaultdict(set)
    for source, targets in edges.items():
        for target in targets:
            incoming[target].add(source)
    return {target: sorted(sources) for target, sources in incoming.items()}


def build_dependency_graph(edges: dict[str, dict[str, list[dict]]], types: dict[str, TypeInfo]) -> tuple[dict[str, list[str]], dict[str, int], dict[str, int]]:
    deps: dict[str, list[str]] = {}
    incoming_count = Counter()
    outgoing_count = Counter()

    for type_name in types:
        targets = sorted(edges.get(type_name, {}))
        deps[type_name] = targets
        outgoing_count[type_name] = len(targets)
        for target in targets:
            incoming_count[target] += 1

    for type_name in types:
        incoming_count.setdefault(type_name, 0)
        outgoing_count.setdefault(type_name, 0)

    return deps, dict(incoming_count), dict(outgoing_count)


def connected_components(deps: dict[str, list[str]]) -> list[list[str]]:
    undirected: dict[str, set[str]] = {node: set(targets) for node, targets in deps.items()}
    for node, targets in deps.items():
        for target in targets:
            undirected.setdefault(target, set()).add(node)
    seen: set[str] = set()
    components: list[list[str]] = []
    for node in sorted(undirected):
        if node in seen:
            continue
        stack = [node]
        seen.add(node)
        component = []
        while stack:
            current = stack.pop()
            component.append(current)
            for neighbor in undirected[current]:
                if neighbor not in seen:
                    seen.add(neighbor)
                    stack.append(neighbor)
        components.append(sorted(component))
    components.sort(key=lambda group: (-len(group), group[0]))
    return components


def strongly_connected_components(deps: dict[str, list[str]]) -> list[list[str]]:
    index = 0
    indices: dict[str, int] = {}
    lowlinks: dict[str, int] = {}
    stack: list[str] = []
    on_stack: set[str] = set()
    result: list[list[str]] = []

    def visit(node: str) -> None:
        nonlocal index
        indices[node] = index
        lowlinks[node] = index
        index += 1
        stack.append(node)
        on_stack.add(node)

        for neighbor in deps.get(node, []):
            if neighbor not in indices:
                visit(neighbor)
                lowlinks[node] = min(lowlinks[node], lowlinks[neighbor])
            elif neighbor in on_stack:
                lowlinks[node] = min(lowlinks[node], indices[neighbor])

        if lowlinks[node] == indices[node]:
            component: list[str] = []
            while True:
                popped = stack.pop()
                on_stack.remove(popped)
                component.append(popped)
                if popped == node:
                    break
            result.append(sorted(component))

    for node in sorted(deps):
        if node not in indices:
            visit(node)

    result.sort(key=lambda group: (-len(group), group[0]))
    return result


def classify_no_incoming(
    types: dict[str, TypeInfo],
    incoming_count: dict[str, int],
    files: list[FileInfo],
) -> tuple[list[str], list[str]]:
    file_map = {info.relative_path: info for info in files}
    data_like: list[str] = []
    runtime_roots: list[str] = []

    for type_name, type_info in sorted(types.items()):
        if incoming_count.get(type_name, 0) != 0:
            continue
        if type_info.kind in {"enum", "struct", "interface"}:
            data_like.append(type_name)
            continue
        if type_info.mono_behaviour:
            runtime_roots.append(type_name)
            continue
        file_info = file_map[next(iter(type_info.files))]
        joined = "\n".join(file_info.active_lines)
        if any(name in joined for name in UNITY_MESSAGE_NAMES) or "RuntimeInitializeOnLoadMethod" in joined:
            runtime_roots.append(type_name)
        else:
            data_like.append(type_name)
    return data_like, runtime_roots


def find_naming_risks(types: dict[str, TypeInfo], files: list[FileInfo]) -> dict[str, list[dict]]:
    lower_map: dict[str, list[str]] = defaultdict(list)
    mismatches: list[dict] = []
    priority_mismatches: list[dict] = []
    exact_case_mismatches: list[dict] = []
    multi_mono_files: list[dict] = []

    for type_name in types:
        lower_map[type_name.lower()].append(type_name)

    for info in files:
        stem = info.path.stem
        for type_name, kind, lineno, _ in info.type_decls:
            if stem.lower() != type_name.lower():
                item = {"file": info.relative_path, "type": type_name, "kind": kind, "line": lineno}
                mismatches.append(item)
                if kind == "class":
                    priority_mismatches.append(item)
            elif stem != type_name and kind == "class":
                item = {"file": info.relative_path, "type": type_name, "kind": kind, "line": lineno}
                priority_mismatches.append(item)
                exact_case_mismatches.append(item)
        if len(info.mono_behaviours) > 1:
            multi_mono_files.append(
                {
                    "file": info.relative_path,
                    "types": info.mono_behaviours,
                }
            )

    case_collisions = [
        {"lower": lower, "types": sorted(names)}
        for lower, names in lower_map.items()
        if len(names) > 1
    ]

    return {
        "case_collisions": sorted(case_collisions, key=lambda item: item["lower"]),
        "file_type_mismatches": sorted(mismatches, key=lambda item: (item["file"], item["line"], item["type"])),
        "priority_file_type_mismatches": sorted(
            {(
                item["file"],
                item["line"],
                item["type"],
                item["kind"],
            ): item for item in priority_mismatches}.values(),
            key=lambda item: (item["file"], item["line"], item["type"]),
        ),
        "exact_case_mismatches": sorted(
            exact_case_mismatches,
            key=lambda item: (item["file"], item["line"], item["type"]),
        ),
        "multi_monobehaviour_files": sorted(multi_mono_files, key=lambda item: item["file"]),
    }


def top_rows(counter_map: dict[str, int], deps_count: dict[str, int], limit: int = 15) -> list[dict]:
    rows = [
        {"type": type_name, "incoming": counter_map.get(type_name, 0), "outgoing": deps_count.get(type_name, 0)}
        for type_name in set(counter_map) | set(deps_count)
    ]
    rows.sort(key=lambda item: (-item["incoming"], -item["outgoing"], item["type"]))
    return rows[:limit]


def write_json(path: Path, data: object) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def write_report(
    output_path: Path,
    source_root: Path,
    files: list[FileInfo],
    types: dict[str, TypeInfo],
    edges: dict[str, dict[str, list[dict]]],
    incoming_sources: dict[str, list[str]],
    deps: dict[str, list[str]],
    incoming_count: dict[str, int],
    outgoing_count: dict[str, int],
    components: list[list[str]],
    sccs: list[list[str]],
    data_like: list[str],
    runtime_roots: list[str],
    naming_risks: dict[str, list[dict]],
    skipped_case_collisions: set[str],
) -> None:
    skipped_lines = sum(info.skipped_disabled_lines for info in files)
    top_incoming = top_rows(incoming_count, outgoing_count, limit=12)
    largest_scc = sccs[0] if sccs else []

    lines: list[str] = []
    lines.append("# Báo cáo dependency scripts")
    lines.append("")
    lines.append("## Phạm vi kiểm tra")
    lines.append("")
    lines.append(f"- Nguồn quét: `{source_root.as_posix()}`.")
    lines.append(f"- Tổng file C#: **{len(files)}**.")
    lines.append(f"- Tổng type phát hiện: **{len(types)}**.")
    lines.append(f"- Số dòng bị loại khỏi phân tích vì preprocessor tắt: **{skipped_lines}**.")
    lines.append("- Đã bỏ qua block `#if false` / `#else` không active trước khi dựng graph.")
    if skipped_case_collisions:
        lines.append(f"- Có xung đột tên khác hoa-thường cần đọc riêng: `{', '.join(sorted(skipped_case_collisions))}`.")
    lines.append("")
    lines.append("## 1. Trục lõi theo số liệu")
    lines.append("")
    lines.append("| Type | Incoming | Outgoing |")
    lines.append("|---|---:|---:|")
    for row in top_incoming:
        lines.append(f"| `{row['type']}` | {row['incoming']} | {row['outgoing']} |")
    lines.append("")
    lines.append("## 2. Mức độ dính chùm của hệ thống")
    lines.append("")
    lines.append(f"- Weak component: **{len(components)}**.")
    lines.append(f"- Component lớn nhất: **{len(components[0]) if components else 0} type**.")
    lines.append(f"- Strongly connected component (SCC) lớn nhất: **{len(largest_scc)} type**.")
    if largest_scc:
        lines.append(f"- Mẫu trong SCC lớn nhất: `{', '.join(largest_scc[:20])}`.")
    lines.append("")
    lines.append("## 3. No incoming đã tách nhóm")
    lines.append("")
    lines.append(f"- Data/enum/support: **{len(data_like)}** type.")
    if data_like:
        lines.append(f"- Mẫu: `{', '.join(data_like[:20])}`.")
    lines.append(f"- Runtime root / Inspector / Unity entrypoint: **{len(runtime_roots)}** type.")
    if runtime_roots:
        lines.append(f"- Mẫu: `{', '.join(runtime_roots[:20])}`.")
    lines.append("")
    lines.append("## 4. Naming và cấu trúc đáng chú ý")
    lines.append("")
    lines.append(f"- Case-collision: **{len(naming_risks['case_collisions'])}**.")
    for item in naming_risks["case_collisions"][:10]:
        lines.append(f"  - `{item['lower']}` => `{', '.join(item['types'])}`")
    lines.append(f"- File/type mismatch tổng: **{len(naming_risks['file_type_mismatches'])}**.")
    lines.append(f"- Lệch tên cùng chữ khác hoa-thường: **{len(naming_risks['exact_case_mismatches'])}**.")
    for item in naming_risks["exact_case_mismatches"][:10]:
        lines.append(f"  - `{item['file']}:{item['line']}` khai báo `{item['type']}`.")
    lines.append(f"- File/type mismatch ưu tiên xử lý: **{len(naming_risks['priority_file_type_mismatches'])}**.")
    for item in naming_risks["priority_file_type_mismatches"][:15]:
        lines.append(f"  - `{item['file']}:{item['line']}` khai báo `{item['type']}` ({item['kind']}).")
    lines.append(f"- File chứa nhiều MonoBehaviour: **{len(naming_risks['multi_monobehaviour_files'])}**.")
    for item in naming_risks["multi_monobehaviour_files"][:10]:
        lines.append(f"  - `{item['file']}` => `{', '.join(item['types'])}`")
    lines.append("")
    lines.append("## 5. Ghi chú sử dụng graph")
    lines.append("")
    lines.append("- Graph này chỉ phản ánh quan hệ nhìn thấy trong code active.")
    lines.append("- Quan hệ Scene/Prefab/Inspector/Button OnClick vẫn cần kiểm tra trong Unity.")
    lines.append("- Type trong file có nhiều class có thể làm tăng over-link cục bộ; vì vậy cần đọc kỹ các file lớn như `NpcSocialSystem.cs` hoặc `EntityCore.cs` trước khi refactor.")
    lines.append("")
    lines.append("## 6. File đầu ra")
    lines.append("")
    lines.append("- `direct_refs.json`: ref có line/text theo source type.")
    lines.append("- `code_graph.json`: graph đã tổng hợp và thống kê count.")
    lines.append("- `Scripts_dependency_cluster_report.md`: bản tóm tắt này.")
    lines.append("")
    output_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a dependency report for Unity C# scripts.")
    parser.add_argument("--source", required=True, help="Source folder to scan.")
    parser.add_argument("--output", required=True, help="Output folder for JSON and Markdown reports.")
    args = parser.parse_args()

    source_root = Path(args.source).resolve()
    output_root = Path(args.output).resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    files = [read_file_info(path, source_root) for path in sorted(source_root.rglob("*.cs"))]
    types = collect_types(files)
    edges, skipped_case_collisions = build_references(files, types)
    incoming_sources = compute_incoming(edges)
    deps, incoming_count, outgoing_count = build_dependency_graph(edges, types)
    components = connected_components(deps)
    sccs = strongly_connected_components(deps)
    data_like, runtime_roots = classify_no_incoming(types, incoming_count, files)
    naming_risks = find_naming_risks(types, files)

    direct_refs = {
        "types": {name: sorted(info.files) for name, info in sorted(types.items())},
        "edges": {source: dict(sorted(targets.items())) for source, targets in sorted(edges.items())},
        "incoming": incoming_sources,
        "no_incoming": sorted([name for name, count in incoming_count.items() if count == 0]),
        "no_incoming_groups": {
            "data_like": data_like,
            "runtime_roots": runtime_roots,
        },
        "naming_risks": naming_risks,
        "skipped_case_collisions": sorted(skipped_case_collisions),
    }
    code_graph = {
        "types": {name: sorted(info.files) for name, info in sorted(types.items())},
        "deps": deps,
        "incoming": incoming_count,
        "outgoing": outgoing_count,
        "components": components,
        "strongly_connected_components": sccs,
        "no_incoming_groups": {
            "data_like": data_like,
            "runtime_roots": runtime_roots,
        },
        "naming_risks": naming_risks,
        "skipped_case_collisions": sorted(skipped_case_collisions),
    }

    write_json(output_root / "direct_refs.json", direct_refs)
    write_json(output_root / "code_graph.json", code_graph)
    write_report(
        output_root / "Scripts_dependency_cluster_report.md",
        source_root,
        files,
        types,
        edges,
        incoming_sources,
        deps,
        incoming_count,
        outgoing_count,
        components,
        sccs,
        data_like,
        runtime_roots,
        naming_risks,
        skipped_case_collisions,
    )


if __name__ == "__main__":
    main()
