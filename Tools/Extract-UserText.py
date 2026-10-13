"""Extract reviewed C# player copy without Unity or third party dependencies.

Examples:
  python Tools/Extract-UserText.py --write Assets/PaperDefense/Presentation/UI
  python Tools/Extract-UserText.py --audit Assets/PaperDefense/Core
  python Tools/Extract-UserText.py --merge path/to/reviewed-texts.json

The JSON manifest is canonical; keys are stable hashes of their original template.
Technical strings, attributes, comments and exception/log diagnostics are excluded.
Review English display copy separately; English object names are intentionally retained.
"""
import argparse
import hashlib
import json
import pathlib
import re
import uuid
import sys

if hasattr(sys.stdout, 'reconfigure'): sys.stdout.reconfigure(encoding='utf-8')

ROOT = pathlib.Path(__file__).resolve().parent.parent
MANIFEST = ROOT / 'Tools/user-texts.json'
ASSET = ROOT / 'Assets/PaperDefense/Resources/Text/ru.asset'
CONTRACTS = ROOT / 'Assets/PaperDefense/Core/UserTextContracts.Generated.cs'
SCRIPT_GUID = '825829aa318f42b19bca7f6b301be1f2'
CYRILLIC = re.compile('[А-Яа-яЁё]')
# Comments, ordinary/verbatim/interpolated strings, character literals.
LEXER = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|(?:\$@|@\$|\$|@)?"(?:\\.|""|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|[A-Za-z_][A-Za-z_0-9]*|=>|==|!=|&&|\|\||\+\+|--|\+=|\S')


class Span:
    def __init__(self, source, start, end): self.source, self.a, self.b = source, start, end
    def __getitem__(self, index): return self.source[self.a:self.b]
    def start(self): return self.a
    def end(self): return self.b


def string_end(source, start):
    quote = source.index('"', start)
    prefix = source[start:quote]
    interpolated, verbatim = '$' in prefix, '@' in prefix
    cursor, depth = quote + 1, 0
    while cursor < len(source):
        c = source[cursor]
        if depth:
            if c in ('"', "'"):
                nested_quote = c; cursor += 1
                while cursor < len(source):
                    if source[cursor] == '\\': cursor += 2; continue
                    if source[cursor] == nested_quote: break
                    cursor += 1
            elif c == '{': depth += 1
            elif c == '}': depth -= 1
        elif c == '\\' and not verbatim: cursor += 2; continue
        elif c == '"':
            if verbatim and source[cursor:cursor+2] == '""': cursor += 2; continue
            return cursor + 1
        elif interpolated and c == '{':
            if source[cursor:cursor+2] == '{{': cursor += 2; continue
            depth = 1
        cursor += 1
    raise ValueError('Unterminated C# string at ' + str(start))


def lex(source):
    cursor, result = 0, []
    while cursor < len(source):
        match = LEXER.search(source, cursor)
        if not match: break
        end = string_end(source, match.start()) if re.match(r'(?:\$@|@\$|\$|@)?"', match[0]) else match.end()
        result.append(Span(source, match.start(), end)); cursor = end
    return result


def decode_literal(raw):
    prefix = raw[:raw.index('"')]
    value = raw[raw.index('"') + 1:-1]
    if '$' in prefix: return value, True
    if '@' in prefix:
        value = value.replace('""', '"')
    else:
        value = re.sub(r'\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|.)',
            lambda m: chr(int(m[1][1:], 16)) if m[1][0] in 'uUx' else
            {'n':'\n','r':'\r','t':'\t','0':'\0','a':'\a','b':'\b','f':'\f','v':'\v'}.get(m[1], m[1]), value)
    return value, '$' in prefix


def interpolation(value, verbatim=False):
    """Preserve expressions, alignment and formats using .NET composite placeholders."""
    template, args, index = '', [], 0
    while index < len(value):
        if value[index:index+2] in ('{{', '}}'):
            template += value[index:index+2]; index += 2
        elif value[index] == '{':
            end, depth, quote = index + 1, 1, None
            while end < len(value) and depth:
                c = value[end]
                if quote:
                    if c == quote and value[end-1] != '\\': quote = None
                elif c in ('"', "'"): quote = c
                elif c == '{': depth += 1
                elif c == '}': depth -= 1
                end += 1
            if depth: raise ValueError('Unbalanced interpolation: ' + value)
            expression = value[index+1:end-1]
            # Only a top-level suffix is formatting; C# ternaries/method calls retain their syntax.
            fmt, nesting, quoted = '', 0, None
            for offset, character in enumerate(expression):
                if quoted:
                    if character == quoted and expression[offset-1] != '\\': quoted = None
                elif character in ('"', "'"): quoted = character
                elif character in '([{': nesting += 1
                elif character in ')]}': nesting -= 1
                elif nesting == 0 and character in ',:':
                    fmt, expression = expression[offset:], expression[:offset]; break
            expression = expression.strip()
            formatted = re.fullmatch(r'(.+)\.ToString\("([^"{}]*)"\)', expression)
            if formatted and ':' not in fmt:
                expression, fmt = formatted[1], fmt + ':' + formatted[2]
            template += '{' + str(len(args)) + fmt + '}'
            args.append(expression); index = end
        else:
            if not verbatim and value[index] == '\\':
                escaped = re.match(r'\\(?:u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|.)', value[index:])
                if escaped:
                    template += decode_literal('"' + escaped[0] + '"')[0]; index += len(escaped[0]); continue
            if verbatim and value[index:index+2] == '""': template += '"'; index += 2; continue
            template += value[index]; index += 1
    return template, args


def key_for(template):
    return 'copy.' + hashlib.sha256(template.encode('utf-8')).hexdigest()[:16]


def chain_operand(tokens, index, direction):
    """Find one C# operand without crossing expression/argument/statement boundaries."""
    depth, cursor = 0, index
    boundary = {';', ',', '=', '+=', '=>', '?', ':', '&&', '||', '==', '!=', '<', '>',
                'return', 'throw', 'case', '{', '}'}
    while 0 <= cursor < len(tokens):
        word = tokens[cursor][0]
        opening, closing = ('(', ')') if direction > 0 else (')', '(')
        opening2, closing2 = ('[', ']') if direction > 0 else (']', '[')
        if word in (opening, opening2): depth += 1
        elif word in (closing, closing2):
            if depth == 0: break
            depth -= 1
        elif depth == 0 and (word in boundary or word == '+'): break
        cursor += direction
    return cursor


def chain(source, tokens, literal):
    start, end = literal, literal + 1
    while start > 0 and tokens[start-1][0] == '+':
        before = chain_operand(tokens, start-2, -1)
        if before == start-2: break
        start = before + 1
    while end < len(tokens) and tokens[end][0] == '+':
        after = chain_operand(tokens, end+1, 1)
        if after == end+1: break
        end = after
    if end - start <= 1: return None
    depth, cuts = 0, [start]
    for cursor in range(start, end):
        word = tokens[cursor][0]
        if word in ('(', '['): depth += 1
        elif word in (')', ']'): depth -= 1
        elif depth == 0 and word == '+': cuts += [cursor, cursor+1]
    cuts.append(end)
    operands = []
    for i in range(0, len(cuts), 2):
        a, b = cuts[i], cuts[i+1]
        operands.append(source[tokens[a].start():tokens[b-1].end()].strip())
    template, args, first_text = '', [], False
    prefix_numbers = []
    for operand in operands:
        if re.fullmatch(r'(?:\$@|@\$|\$|@)?"(?:\\.|""|[^"\\])*"', operand):
            value, interpolated = decode_literal(operand)
            if interpolated: return None  # extracted independently; keep the interpolation parser authoritative
            if prefix_numbers:
                template += '{' + str(len(args)) + '}'
                args.append(' + '.join(prefix_numbers)); prefix_numbers.clear()
            template += value.replace('{', '{{').replace('}', '}}'); first_text = True
        elif not first_text:
            prefix_numbers.append(operand)
        else:
            fmt = ''
            formatted = re.fullmatch(r'(.+)\.ToString\("([^"{}]*)"\)', operand)
            if formatted:
                operand, fmt = formatted[1], ':' + formatted[2]
            template += '{' + str(len(args)) + fmt + '}'
            args.append(operand)
    if not first_text or not CYRILLIC.search(template): return None
    return tokens[start].start(), tokens[end-1].end(), template, args


def diagnostic_tokens(tokens):
    """Bound diagnostic expressions by syntax tokens, rather than physical lines."""
    code = [(index, token[0]) for index, token in enumerate(tokens)
            if not token[0].startswith(('//', '/*'))]
    diagnostic = set()
    opening = {'(': ')', '[': ']', '{': '}'}
    throw_boundaries = {';', ',', ':', ')', ']', '}'}
    log_methods = {'Log', 'LogError', 'LogWarning', 'LogException'}
    for position, (index, word) in enumerate(code):
        if word == 'throw':
            nesting = []
            for item_index, item in code[position + 1:]:
                if not nesting and item in throw_boundaries: break
                diagnostic.add(item_index)
                if item in opening: nesting.append(opening[item])
                elif nesting and item == nesting[-1]: nesting.pop()
        elif word in log_methods and position + 1 < len(code) and code[position + 1][1] == '(':
            # Preserve bare diagnostic helpers and UnityEngine.Debug.Log* calls;
            # unrelated UI/domain objects may also have a method named Log.
            if position and code[position - 1][1] == '.':
                if position < 2 or code[position - 2][1] != 'Debug': continue
            depth = 0
            for item_index, item in code[position + 1:]:
                diagnostic.add(item_index)
                if item == '(': depth += 1
                elif item == ')':
                    depth -= 1
                    if depth == 0: break
    return diagnostic


def extract(source, entries, include_diagnostics=False):
    tokens = lex(source)
    diagnostics = set() if include_diagnostics else diagnostic_tokens(tokens)
    replacements, skipped, consumed = [], [], -1
    for i, token in enumerate(tokens):
        raw = token[0]
        if token.start() < consumed: continue
        if not re.match(r'(?:\$@|@\$|\$|@)?"', raw): continue
        value, interpolated = decode_literal(raw)
        if not CYRILLIC.search(value): continue
        statement_start = max(source.rfind(';', 0, token.start()), source.rfind('\n', 0, token.start())) + 1
        line = source[statement_start:token.start()]
        if re.search(r'\[\s*(?:Header|Tooltip|CreateAssetMenu|MenuItem|ContextMenu)\b', line):
            skipped.append((token.start(), 'attribute', value)); continue
        if i in diagnostics:
            skipped.append((token.start(), 'diagnostic', value)); continue
        combined = None if interpolated else chain(source, tokens, i)
        # An earlier literal inside a nested operand may already own part of this span.
        # Leave this literal separate; another pass can safely join the resulting tokens.
        if combined and combined[0] < consumed: combined = None
        if combined:
            start, end, template, args = combined
        else:
            start, end = token.start(), token.end()
            template, args = interpolation(value, '@' in raw[:raw.index('"')]) if interpolated else (value.replace('{', '{{').replace('}', '}}'), [])
        key = key_for(template)
        if key in entries and entries[key] != template: raise ValueError('Resource key collision ' + key)
        entries[key] = template
        replacement = 'UserText.Message(' + json.dumps(key) + ''.join(', ' + arg for arg in args) + ')'
        replacements.append((start, end, replacement))
        consumed = end
    for start, end, replacement in reversed(replacements): source = source[:start] + replacement + source[end:]
    # const expressions cannot call Message. No selected attributes are transformed.
    source = re.sub(r'\bconst\s+string(\s+\w+\s*=\s*UserText\.Message\()', r'static readonly string\1', source)
    if replacements and 'using PaperDefense.Core;' not in source and 'namespace PaperDefense.Core' not in source:
        source = 'using PaperDefense.Core;\n' + source
    return source, replacements, skipped


def generate(entries):
    ASSET.parent.mkdir(parents=True, exist_ok=True)
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
        '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: ' + SCRIPT_GUID + ', type: 3}', '  m_Name: ru', '  m_EditorClassIdentifier: ',
        '  language: ru', '  culture: ru-RU', '  entries:']
    for key, value in sorted(entries.items()):
        lines += ['  - key: ' + json.dumps(key, ensure_ascii=False), '    value: ' + json.dumps(value, ensure_ascii=False)]
    ASSET.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    MANIFEST.write_text(json.dumps(dict(sorted(entries.items())), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    contracts = ['// Generated by Tools/Extract-UserText.py. Keys and argument contracts only; player copy belongs to Resources/Text/ru.asset.',
        'using System;', '', 'namespace PaperDefense.Core', '{', '    public static class UserTextContracts', '    {',
        '        public static void Validate(UserTextResolver resolver)', '        {',
        '            if (resolver == null) throw new ArgumentNullException(nameof(resolver));']
    for key, template in sorted(entries.items()):
        indices, formatted, formats, position = [], set(), {}, 0
        while position < len(template):
            if template[position:position+2] in ('{{', '}}'): position += 2; continue
            if template[position] == '{':
                match = re.match(r'\{(\d+)([^}]*)\}', template[position:])
                if not match: raise ValueError('Invalid composite template: ' + key)
                index = int(match[1]); indices.append(index)
                if ':' in match[2]:
                    formatted.add(index)
                    formats.setdefault(index, set()).add(match[2].split(':', 1)[1])
                position += len(match[0]); continue
            position += 1
        count = max(indices, default=-1) + 1
        values = 'new[] { ' + ', '.join(map(str, sorted(formatted))) + ' }' if formatted else 'Array.Empty<int>()'
        contracts.append('            resolver.RequireContract(' + json.dumps(key) + ', ' + str(count) + ', ' + values + ');')
        for index, accepted in sorted(formats.items()):
            contracts.append('            resolver.RequireFormatContract(' + json.dumps(key) + ', ' + str(index) + ', ' + ', '.join(json.dumps(value) for value in sorted(accepted)) + ');')
    contracts += ['        }', '    }', '}', '']
    CONTRACTS.write_text('\n'.join(contracts), encoding='utf-8')
    contracts_meta = pathlib.Path(str(CONTRACTS) + '.meta')
    if not contracts_meta.exists(): contracts_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8')
    for path in [ASSET.parent, ASSET]:
        meta = pathlib.Path(str(path) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n' + ('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if path.is_dir() else 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('paths', nargs='*')
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--audit', action='store_true')
    parser.add_argument('--merge', action='append', default=[])
    parser.add_argument('--include-diagnostics', action='store_true')
    args = parser.parse_args()
    entries = json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.exists() else {}
    for merge in args.merge: entries.update(json.loads((ROOT / merge).read_text(encoding='utf-8')))
    files = set()
    for path in args.paths:
        p = ROOT / path
        files.update(p.rglob('*.cs') if p.is_dir() else [p])
    total = 0
    for path in sorted(files):
        source = path.read_text(encoding='utf-8-sig')
        updated, replacements, skipped = extract(source, entries, args.include_diagnostics)
        total += len(replacements)
        if args.write and replacements: path.write_text(updated, encoding='utf-8')
        if replacements or args.audit:
            print(str(path.relative_to(ROOT)) + ': ' + str(len(replacements)) + ' player literals; ' + str(len(skipped)) + ' excluded attributes/diagnostics')
        if args.audit:
            for start, end, replacement in replacements:
                print('  ' + str(source.count('\n', 0, start)+1) + ': ' + source[start:end])
    if args.write or args.merge: generate(entries)
    print(str(total) + ' player literals; ' + str(len(entries)) + ' resource entries')
    return 1 if args.audit and total else 0


if __name__ == '__main__': raise SystemExit(main())
