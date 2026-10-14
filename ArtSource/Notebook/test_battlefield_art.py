"""Fast integrity checks for the canonical tower-defense asset source."""
import json
import math
import unittest
from pathlib import Path

SOURCE = Path(__file__).with_name("notebook-theme.json")


class BattlefieldArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = json.loads(SOURCE.read_text(encoding="utf-8"))
        cls.shapes = {item["Id"]: item for item in cls.source["Shapes"]}
        cls.clips = {item["Id"]: item for item in cls.source["Clips"]}

    def test_six_families_have_complete_tier_and_animation_sets(self):
        for family in ("doodle", "runner", "gnawer", "spitter", "brute", "bulwark"):
            for tier in ("sketch", "outlined", "hatched"):
                for state in ("idle", "walk", "attack", "hit", "death"):
                    clip = self.clips[f"enemy.{family}.{tier}.{state}"]
                    self.assertGreater(clip["Duration"], 0)
                    self.assertGreaterEqual(len(clip["Frames"]), 4)
                    for frame in clip["Frames"]:
                        self.assertIn(frame["ShapeId"], self.shapes)

    def test_scenery_and_functional_buildings_have_symbols(self):
        required = ["terrain." + name for name in ("mountain", "ridge", "pine", "oak", "rock", "shrub")]
        required += ["object." + name for name in ("tar", "repair", "entrance", "barricade-site")]
        required += ["ui.tool." + name for name in ("wall", "spikes", "tar", "repair", "erase", "upgrade", "blotter", "paint")]
        for name in required:
            self.assertIn(name, self.shapes)
            self.assertTrue(self.shapes[name]["Marks"])

    def test_polygon_fills_are_convex_and_finite(self):
        for shape in self.shapes.values():
            for mark in shape["Marks"]:
                points = [(p["x"], p["y"]) for p in mark["Points"]]
                self.assertTrue(all(math.isfinite(v) for point in points for v in point), shape["Id"])
                if mark["Primitive"] != 1:
                    continue
                crosses = []
                for index, a in enumerate(points):
                    b, c = points[(index+1) % len(points)], points[(index+2) % len(points)]
                    cross = (b[0]-a[0])*(c[1]-b[1])-(b[1]-a[1])*(c[0]-b[0])
                    if abs(cross) > 1e-7:
                        crosses.append(cross)
                self.assertTrue(crosses and (min(crosses) > 0 or max(crosses) < 0), shape["Id"])

    def test_distinct_heavy_silhouettes(self):
        ogre = self.shapes["enemy.brute.outlined.idle"]["Marks"]
        escort = self.shapes["enemy.bulwark.outlined.idle"]["Marks"]
        self.assertNotEqual(ogre, escort)
        self.assertTrue(any(mark["Channel"] == 4 for mark in escort))

    def test_clip_durations_fit_importer_budget(self):
        for clip in self.clips.values():
            self.assertLessEqual(clip["Duration"], self.source["MaximumTerminalTailSeconds"], clip["Id"])


if __name__ == "__main__":
    unittest.main()
