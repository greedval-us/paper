import importlib.util
import pathlib
import sys
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location(
    'extract_user_text_diagnostics',
    pathlib.Path(__file__).resolve().parents[1] / 'Extract-UserText.py')
extractor = importlib.util.module_from_spec(spec)
spec.loader.exec_module(extractor)


class DiagnosticExtractionTests(unittest.TestCase):
    def test_multiline_throw_keeps_every_diagnostic_argument(self):
        source = '''throw new ArgumentException(
    "Неизвестный ID: " + id,
    new InvalidOperationException(
        $"Каталог {catalogId} отсутствует"));'''
        output, replacements, skipped = extractor.extract(source, {})
        self.assertEqual(output, source)
        self.assertEqual(replacements, [])
        self.assertEqual(len(skipped), 2)

    def test_multiline_debug_calls_keep_nested_diagnostic_arguments(self):
        for method in ('Log', 'LogError', 'LogWarning', 'LogException'):
            with self.subTest(method=method):
                source = '''UnityEngine.Debug
    .''' + method + ''' /* diagnostic */ (
        string.Format(
            "Не удалось загрузить {0}", id));'''
                output, replacements, skipped = extractor.extract(source, {})
                self.assertEqual(output, source)
                self.assertEqual(replacements, [])
                self.assertEqual(len(skipped), 1)

    def test_ui_copy_after_a_throw_in_same_function_is_extracted(self):
        source = '''void UpdateLabel() {
    if (missing) throw new ArgumentException(
        "Нет каталога"); label.text = "Открыть стенд";
}'''
        entries = {}
        output, replacements, skipped = extractor.extract(source, entries)
        self.assertIn('"Нет каталога"', output)
        self.assertNotIn('"Открыть стенд"', output)
        self.assertEqual(list(entries.values()), ['Открыть стенд'])
        self.assertEqual(len(replacements), 1)
        self.assertEqual(len(skipped), 1)

    def test_ui_copy_in_other_branch_of_throw_expression_is_extracted(self):
        source = '''label.text = missing
    ? throw new ArgumentException(
        "Нет каталога")
    : "Открыть стенд";'''
        entries = {}
        output, replacements, skipped = extractor.extract(source, entries)
        self.assertIn('"Нет каталога"', output)
        self.assertNotIn('"Открыть стенд"', output)
        self.assertEqual(list(entries.values()), ['Открыть стенд'])
        self.assertEqual(len(replacements), 1)
        self.assertEqual(len(skipped), 1)

    def test_ui_copy_after_debug_call_on_same_line_is_extracted(self):
        source = '''Debug.Log(
    "Каталог загружен"); label.text = "Открыть стенд";'''
        entries = {}
        output, replacements, skipped = extractor.extract(source, entries)
        self.assertIn('"Каталог загружен"', output)
        self.assertNotIn('"Открыть стенд"', output)
        self.assertEqual(list(entries.values()), ['Открыть стенд'])
        self.assertEqual(len(replacements), 1)
        self.assertEqual(len(skipped), 1)

    def test_bare_log_calls_still_exclude_diagnostics(self):
        source = '''LogWarning(
    "Нет каталога");'''
        self.assertEqual(extractor.extract(source, {})[0], source)

    def test_other_objects_log_methods_can_receive_player_copy(self):
        source = 'journal.Log("Новая запись");'
        entries = {}
        output, replacements, skipped = extractor.extract(source, entries)
        self.assertNotIn('"Новая запись"', output)
        self.assertEqual(list(entries.values()), ['Новая запись'])
        self.assertEqual(len(replacements), 1)
        self.assertEqual(skipped, [])

    def test_include_diagnostics_explicitly_overrides_multiline_exclusion(self):
        source = '''throw new ArgumentException(
    "Нет каталога");'''
        entries = {}
        _, replacements, skipped = extractor.extract(source, entries, include_diagnostics=True)
        self.assertEqual(list(entries.values()), ['Нет каталога'])
        self.assertEqual(len(replacements), 1)
        self.assertEqual(skipped, [])


if __name__ == '__main__':
    unittest.main()
