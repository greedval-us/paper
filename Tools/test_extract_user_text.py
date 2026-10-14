import importlib.util
import pathlib
import unittest
import sys

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location('extract_user_text', pathlib.Path(__file__).with_name('Extract-UserText.py'))
extractor = importlib.util.module_from_spec(spec)
spec.loader.exec_module(extractor)


class ExtractionTests(unittest.TestCase):
    def test_nested_ternary_and_outer_copy_do_not_overwrite_each_other(self):
        source = 'var label = bp.Name + " · " + (bp.Origin == DrawingOrigin.LegacyStandard ? "стандарт" : bp.Quality.ToString("0")) + " очков";'
        entries = {}
        output, _, _ = extractor.extract(source, entries)
        self.assertIn('bp.Origin == DrawingOrigin.LegacyStandard', output)
        self.assertNotIn('=UserText', output)
        self.assertEqual(output.count('UserText.Message('), 2)
        output, _, _ = extractor.extract(output, entries)
        self.assertEqual(output.count('UserText.Message('), 2)

    def test_numeric_formatting_is_retained_in_composite_template(self):
        output, _, _ = extractor.extract('label.text = "Цена " + cost.ToString("0.00") + " штр.";', {})
        self.assertRegex(output, r'UserText.Message\("copy\.[a-f0-9]+", cost\)')

    def test_attributes_comments_and_diagnostics_remain_valid_csharp(self):
        source = '[Tooltip("Подсказка")] public string id; // комментарий "слова"\nthrow new ArgumentException("Диагностика");'
        self.assertEqual(extractor.extract(source, {})[0], source)

    def test_interpolation_keeps_nested_csharp_strings_and_ternaries(self):
        source = 'reason = $"Цена {cost.ToString("0.0")} / {(ok ? "да" : "нет")}";'
        entries = {}
        output, _, _ = extractor.extract(source, entries)
        self.assertNotIn('cost.ToString("0.0")', output)
        self.assertIn(', cost,', output)
        self.assertIn('(ok ? "да" : "нет")', output)
        self.assertEqual(next(iter(entries.values())), 'Цена {0:0.0} / {1}')


if __name__ == '__main__': unittest.main()
